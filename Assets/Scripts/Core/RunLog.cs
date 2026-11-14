using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

// Per-run log for balancing: conchas earned per round and per source, what was bought and when,
// round times. Written as text to <persistentDataPath>/runs/ when the run ends (death, victory,
// restart or leaving to the menu). Lives next to the GameManager.
public class RunLog : MonoBehaviour
{
    public static RunLog Instance { get; private set; }
    public static string LastPath { get; private set; } = "";

    class RoundEntry
    {
        public int round;
        public float start, end = -1f;
        public readonly Dictionary<PointSource, int> earned = new Dictionary<PointSource, int>();
        public readonly List<string> spent = new List<string>();
        public int kills;
        public float intermission;      // seconds between the previous round ending and this one starting
        public int damageTaken, falls;  // all players together
        public float enemyHealth = -1f; // old man health this round (-1 = round 0)
    }

    class WeaponStat { public int shots, hits, kills; }
    readonly Dictionary<string, WeaponStat> weaponStats = new Dictionary<string, WeaponStat>();
    readonly List<string> events = new List<string>();   // downs, deaths, falls: round, player, x/z (heat maps)
    readonly HashSet<PlayerHealth> hooked = new HashSet<PlayerHealth>();
    readonly Dictionary<PlayerHealth, float> groundY = new Dictionary<PlayerHealth, float>();
    readonly Dictionary<PlayerHealth, bool> wasGrounded = new Dictionary<PlayerHealth, bool>();
    float lastRoundEnd = -1f;
    const float FallLogHeight = 1.5f;   // metres dropped before landing that count as a fall

    readonly List<RoundEntry> rounds = new List<RoundEntry>();
    RoundEntry current;
    bool written;
    DateTime started;

    void Awake()
    {
        Instance = this;
        started = DateTime.Now;
        current = new RoundEntry { round = 0, start = 0f };
        rounds.Add(current);
    }

    void Start()
    {
        var rm = RoundManager.Instance;
        if (rm != null)
        {
            rm.RoundEnded += r => lastRoundEnd = Time.timeSinceLevelLoad;
            rm.RoundStarted += r =>
            {
                current.end = Time.timeSinceLevelLoad;
                current = new RoundEntry { round = r, start = Time.timeSinceLevelLoad, enemyHealth = rm.HealthForRound(r),
                    intermission = lastRoundEnd >= 0f ? Time.timeSinceLevelLoad - lastRoundEnd : 0f };
                rounds.Add(current);
            };
        }
        var gm = GameManager.Instance;
        if (gm != null) gm.StateChanged += s => { if (s == GameState.GameOver || s == GameState.Victory) Write(s.ToString()); };
        EnemyHealth.AnyEnemyDied += OnEnemyDied;
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        foreach (var p in gm.Players)
        {
            if (p == null) continue;
            if (hooked.Add(p)) Hook(p);
            TrackFall(p);
        }
    }

    void Hook(PlayerHealth p)
    {
        string who = Who(p);
        p.Damaged += d => current.damageTaken += d;
        p.Downed += () => Event("CAÍDO", who, p.transform.position);
        p.Died += () => Event("MUERTO", who, p.transform.position);
        var sh = p.GetComponent<PlayerShooter>();
        if (sh != null)
            sh.Fired += (hit, kill) =>
            {
                string n = sh.Current != null && sh.Current.data != null ? sh.Current.Name : "?";
                if (!weaponStats.TryGetValue(n, out var w)) weaponStats[n] = w = new WeaponStat();
                w.shots++; if (hit) w.hits++; if (kill) w.kills++;
            };
    }

    // a fall = landing after dropping more than FallLogHeight below the last grounded height
    void TrackFall(PlayerHealth p)
    {
        var pc = p.GetComponent<PlayerController>();
        if (pc == null || p.IsDead) return;
        bool g = pc.IsGrounded;
        float y = p.transform.position.y;
        wasGrounded.TryGetValue(p, out bool was);
        if (g)
        {
            if (!was && groundY.TryGetValue(p, out float from) && from - y > FallLogHeight)
            {
                current.falls++;
                Event($"CAÍDA {from - y:F1}m", Who(p), p.transform.position);
            }
            groundY[p] = y;
        }
        wasGrounded[p] = g;
    }

    static string Who(PlayerHealth p)
    {
        var slot = p.GetComponent<PlayerSlot>();
        return slot != null ? "J" + (slot.Index + 1) : "J1";
    }

    void Event(string what, string who, Vector3 pos) =>
        events.Add($"r{current.round} {Time.timeSinceLevelLoad:F0}s {what} {who} ({pos.x:F0},{pos.z:F0})");

    void OnDestroy()
    {
        EnemyHealth.AnyEnemyDied -= OnEnemyDied;
        Write("salida");
        if (Instance == this) Instance = null;
    }

    void OnEnemyDied(EnemyHealth e, DamageInfo info) => current.kills++;

    public void Earned(int amount, PointSource source)
    {
        current.earned.TryGetValue(source, out var n);
        current.earned[source] = n + amount;
    }

    public void Spent(int amount, string what) => current.spent.Add($"{Time.timeSinceLevelLoad:F0}s {what} -{amount}");

    public void Write(string reason)
    {
        if (written) return;
        written = true;
        current.end = Time.timeSinceLevelLoad;
        var sb = new StringBuilder();
        var gm = GameManager.Instance;
        sb.AppendLine($"Partida {started:yyyy-MM-dd HH:mm} · fin: {reason} · mapa {SceneManager.GetActiveScene().name} · jugadores {(gm != null ? gm.Players.Count : 1)} · animal {GameSettings.Animal} · dificultad {GameSettings.Difficulty} · modo {(RoundManager.Instance != null && RoundManager.Instance.TunnelMode ? "Anselmo cagón" : "clásico")}");
        if (gm != null) sb.AppendLine($"Tiempo {gm.RunTime:F0}s · conchas ganadas {gm.TotalEarned} · bajas {gm.Kills} (cabeza {gm.Headshots}) · puertas {gm.DoorsOpened}");
        sb.AppendLine("ronda | duración | pausa | vida abuelo | bajas | daño recibido | caídas | ganado (por fuente) | gastos");
        int total = 0;
        foreach (var r in rounds)
        {
            int sum = 0;
            var parts = new List<string>();
            foreach (var kv in r.earned) { sum += kv.Value; parts.Add(kv.Key + " " + kv.Value); }
            total += sum;
            float dur = Mathf.Max(0f, (r.end < 0f ? Time.timeSinceLevelLoad : r.end) - r.start);
            string hp = r.enemyHealth >= 0f ? r.enemyHealth.ToString("F0") : "-";
            sb.AppendLine($"{r.round,5} | {dur,6:F0}s | {r.intermission,4:F0}s | {hp,6} | {r.kills,5} | {r.damageTaken,5} | {r.falls,3} | {sum,5} ({string.Join(", ", parts)}) acumulado {total} | {string.Join("; ", r.spent)}");
        }
        if (weaponStats.Count > 0)
        {
            sb.AppendLine("arma | disparos | aciertos | bajas con disparo");
            foreach (var kv in weaponStats) sb.AppendLine($"{kv.Key} | {kv.Value.shots} | {kv.Value.hits} | {kv.Value.kills}");
        }
        if (events.Count > 0)
        {
            sb.AppendLine("eventos (ronda, tiempo, qué, jugador, x/z):");
            foreach (var e in events) sb.AppendLine("  " + e);
        }
        try
        {
            string dir = Path.Combine(Application.persistentDataPath, "runs");
            Directory.CreateDirectory(dir);
            LastPath = Path.Combine(dir, $"run_{started:yyyyMMdd_HHmmss}.txt");
            File.WriteAllText(LastPath, sb.ToString());
        }
        catch (Exception ex) { Debug.LogWarning("[RunLog] " + ex.Message); }
    }
}
