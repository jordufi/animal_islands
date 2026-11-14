using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

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
    }

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
            rm.RoundStarted += r =>
            {
                current.end = Time.timeSinceLevelLoad;
                current = new RoundEntry { round = r, start = Time.timeSinceLevelLoad };
                rounds.Add(current);
            };
        }
        var gm = GameManager.Instance;
        if (gm != null) gm.StateChanged += s => { if (s == GameState.GameOver || s == GameState.Victory) Write(s.ToString()); };
        EnemyHealth.AnyEnemyDied += OnEnemyDied;
    }

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
        sb.AppendLine($"Partida {started:yyyy-MM-dd HH:mm} · fin: {reason} · animal {GameSettings.Animal} · dificultad {GameSettings.Difficulty} · modo {(RoundManager.Instance != null && RoundManager.Instance.TunnelMode ? "Anselmo cagón" : "clásico")}");
        if (gm != null) sb.AppendLine($"Tiempo {gm.RunTime:F0}s · conchas ganadas {gm.TotalEarned} · bajas {gm.Kills} (cabeza {gm.Headshots}) · puertas {gm.DoorsOpened}");
        sb.AppendLine("ronda | duración | bajas | ganado (por fuente) | gastos");
        int total = 0;
        foreach (var r in rounds)
        {
            int sum = 0;
            var parts = new List<string>();
            foreach (var kv in r.earned) { sum += kv.Value; parts.Add(kv.Key + " " + kv.Value); }
            total += sum;
            float dur = (r.end < 0f ? Time.timeSinceLevelLoad : r.end) - r.start;
            sb.AppendLine($"{r.round,5} | {dur,6:F0}s | {r.kills,5} | {sum,5} ({string.Join(", ", parts)}) acumulado {total} | {string.Join("; ", r.spent)}");
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
