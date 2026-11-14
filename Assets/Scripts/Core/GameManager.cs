using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;

public enum GameState { Playing, ChoosingCard, Paused, GameOver, Victory, Debug }

// Run controller for one match: spawns the players (1 solo, or 2-4 split-screen from the Party: each with
// its own devices, camera and HUD), owns the shared pot of conchas, run stats, game state (pause, card
// choice, game over, victory) and scene flow. Co-op like Black Ops Zombies: a player at 0 HP goes down,
// teammates revive them, everyone down = game over; a player who bled out is back at the next round.
public class GameManager : MonoBehaviour
{
    public const string MenuScene = "MainMenu";

    public static GameManager Instance { get; private set; }

    [SerializeField] AnimalData[] animals;
    [SerializeField] Transform playerSpawn;
    [SerializeField] ThirdPersonCamera cameraRig;
    [SerializeField] InputActionAsset actions;
    [SerializeField] EconomyData economy;
    [SerializeField] CoopData coop;

    public GameState State { get; private set; } = GameState.Playing;
    readonly List<PlayerHealth> players = new List<PlayerHealth>();
    public IReadOnlyList<PlayerHealth> Players => players;
    public int PlayerCount => players.Count;
    public int ExpectedPlayerCount { get; private set; } = 1;   // known before the players spawn (HUD layout)
    public int LocalViewCount { get; private set; } = 1;        // views on this screen (split-screen 2-4; online 1)
    public bool IsCoop => players.Count > 1;
    // J1 (solo: the player). Online: this machine's own player.
    public PlayerHealth Player
    {
        get
        {
            foreach (var p in players) if (p != null && !PlayerSlot.IsProxy(p)) return p;
            return players.Count > 0 ? players[0] : null;
        }
    }
    public AnimalData Animal => Player != null ? PlayerSlot.Of(Player).Animal : null;
    public InputActionAsset Actions => actions;
    public ThirdPersonCamera CameraRig => cameraRig;                             // J1's camera
    public AnimalData[] Animals => animals;
    public EconomyData Economy => economy;
    public CoopData Coop => coop;

    // economy + stats
    public int Points { get; private set; }
    public int TotalEarned { get; private set; }
    public int Kills { get; private set; }
    public int Headshots { get; private set; }
    public int DoorsOpened { get; set; }
    public int CardsTaken { get; set; }
    public bool BossDefeated { get; set; }
    public float RunTime => netRunTime >= 0f ? netRunTime : (endTime > 0f ? endTime : Time.time) - startTime;
    float netRunTime = -1f;   // online client: the host's time

    public event Action<int> PointsChanged;
    public event Action<int, PointSource> PointsDelta;   // +earned / -spent, for the floating numbers
    public event Action<GameState> StateChanged;
    public event Action<PlayerHealth> PlayerSpawned;
    public event Action<string> Message;   // short toast for the HUD

    float startTime, endTime = -1f;
    GameState stateBeforePause;
    readonly List<InputAction> pauseActions = new List<InputAction>();

    public bool IsPlaying => State == GameState.Playing;

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;

        // run-wide modifiers start from the chosen difficulty
        RunModifiers.Reset();
        var p = GameSettings.Preset(Net.Online ? NetSession.Instance.Difficulty : GameSettings.Difficulty);   // online: the host's choice
        RunModifiers.Multiply(Stat.EnemyHealth, p.enemyHealth);
        RunModifiers.Multiply(Stat.EnemyDamage, p.enemyDamage);
        RunModifiers.Multiply(Stat.EnemySpeed, p.enemySpeed);
        RunModifiers.Multiply(Stat.EnemyCount, p.enemyCount);
        RunModifiers.Multiply(Stat.PointsGain, p.pointsGain);
        RunModifiers.Multiply(Stat.RegenDelay, p.regenDelay);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Time.timeScale = 1f;
        foreach (var a in pauseActions) a.Disable();
    }

    void Start()
    {
        startTime = Time.time;
        Points = economy != null ? economy.startingPoints : 500;
        if (Net.Online) gameObject.AddComponent<NetGame>().Init(this);   // before the players (scene objects get their ids)
        SpawnPlayers();
        if (RoundManager.Instance != null) RoundManager.Instance.RoundStarted += _ => RespawnFallen();
        PointsChanged?.Invoke(Points);
        StateChanged?.Invoke(State);
        if (Net.Online) NetSession.Instance.NotifyLoaded();
    }

    void Update()
    {
        foreach (var a in pauseActions)
        {
            if (!a.WasPressedThisFrame()) continue;
            if (State == GameState.Playing) SetPaused(true);
            else if (State == GameState.Paused) SetPaused(false);
            break;
        }
    }

    // ---------------------------------------------------------------- players
    void SpawnPlayers()
    {
        if (Net.Online) { SpawnOnline(); return; }
        Party.Prune();
        var members = Party.Members.ToList();
        if (members.Count == 0) members.Add(new Party.Member { animal = GameSettings.Animal, devices = null });
        int n = members.Count;
        ExpectedPlayerCount = n;
        LocalViewCount = n;
        for (int i = 0; i < n; i++)
        {
            var m = members[i];
            // own copy of the actions bound to this player's devices (solo keeps the shared asset: every device)
            InputActionAsset own = actions;
            if (n > 1 || m.devices != null)
            {
                own = Instantiate(actions);
                own.name = actions.name + "_J" + (i + 1);
                if (m.devices != null) own.devices = new ReadOnlyArray<InputDevice>(m.devices);
            }
            var rig = i == 0 ? cameraRig : CloneCamera(i);
            rig.GetComponent<Camera>().rect = ViewRect(i, n);
            SpawnOne(i, n, m.animal, own, rig, false);
        }
    }

    // Online: everyone in the room; this machine's player gets the camera, the others are proxies.
    void SpawnOnline()
    {
        var roster = NetSession.Instance.Roster.Where(m => !m.gone).OrderBy(m => m.index).ToList();
        int n = roster.Count;
        ExpectedPlayerCount = n;
        LocalViewCount = 1;
        cameraRig.GetComponent<Camera>().rect = ViewRect(0, 1);
        int me = Net.LocalIndex;
        foreach (var m in roster)
        {
            bool local = m.index == me;
            InputActionAsset own = actions;
            if (!local)
            {
                own = Instantiate(actions);   // bound to no device: a proxy never reads input
                own.name = actions.name + "_Net" + (m.index + 1);
                own.devices = new ReadOnlyArray<InputDevice>(new InputDevice[0]);
            }
            SpawnOne(m.index, n, m.animal, own, local ? cameraRig : null, !local);
        }
    }

    void SpawnOne(int index, int n, string animalId, InputActionAsset own, ThirdPersonCamera rig, bool remote)
    {
        var animal = Array.Find(animals, a => a != null && a.id == animalId);
        if (animal == null) animal = animals[0];
        Vector3 pos = playerSpawn.position + playerSpawn.right * ((index - (n - 1) / 2f) * 1.3f);
        if (n > 1 && NavAgents.Sample(pos, 2f, out var s)) pos = s;
        // instantiated under an inactive holder so the slot is set before the components wake up
        var holder = new GameObject("PlayerHolder");
        holder.SetActive(false);
        var go = Instantiate(animal.playerPrefab, pos, playerSpawn.rotation, holder.transform);
        go.name = "Player_" + animal.id + (n > 1 ? "_J" + (index + 1) : "");
        var slot = go.AddComponent<PlayerSlot>();
        slot.Index = index;
        slot.Actions = own;
        slot.CameraRig = rig;
        slot.Animal = animal;
        slot.IsRemote = remote;
        if (remote)
        {
            // a remote player only shows what its owner sends (NetPlayer)
            go.GetComponent<PlayerController>().enabled = false;
            go.GetComponent<PlayerInteractor>().enabled = false;
            var ab = go.GetComponent<PlayerAbility>();
            if (ab != null) ab.enabled = false;
            go.GetComponent<PlayerDrinks>().enabled = false;
        }
        if (Net.Online) go.AddComponent<NetPlayer>().Setup(index, !remote);
        go.transform.SetParent(null, true);
        Destroy(holder);

        var player = go.GetComponent<PlayerHealth>();
        players.Add(player);
        go.GetComponent<PlayerStats>().Apply(animal.passiveMods);
        var ability = go.GetComponent<PlayerAbility>();
        if (ability != null) ability.Setup(animal);
        if (rig != null)
        {
            rig.BindActions(own);
            rig.SetTarget(go.transform);
        }
        if (n > 1) go.AddComponent<ReviveTarget>();
        player.Downed += () => OnPlayerDowned(player);
        player.Died += CheckTeamDown;

        if (!remote)
        {
            var pause = own.FindActionMap("Player", true).FindAction("Pause", true);
            pause.Enable();
            pauseActions.Add(pause);
        }
        PlayerSpawned?.Invoke(player);
    }

    ThirdPersonCamera CloneCamera(int index)
    {
        var go = Instantiate(cameraRig.gameObject);
        go.name = "Camera_J" + (index + 1);
        go.tag = "Untagged";   // Camera.main stays J1's
        var listener = go.GetComponent<AudioListener>();
        if (listener != null) Destroy(listener);
        foreach (Transform child in go.transform) Destroy(child.gameObject);   // torch etc. are added per camera later
        return go.GetComponent<ThirdPersonCamera>();
    }

    // Split-screen layout: 1 full screen, 2 top / bottom, 3-4 quadrants (the 4th quadrant shows the map).
    public static Rect ViewRect(int i, int n)
    {
        if (n <= 1) return new Rect(0f, 0f, 1f, 1f);
        if (n == 2) return i == 0 ? new Rect(0f, 0.5f, 1f, 0.5f) : new Rect(0f, 0f, 1f, 0.5f);
        switch (i)
        {
            case 0: return new Rect(0f, 0.5f, 0.5f, 0.5f);
            case 1: return new Rect(0.5f, 0.5f, 0.5f, 0.5f);
            case 2: return new Rect(0f, 0f, 0.5f, 0.5f);
            default: return new Rect(0.5f, 0f, 0.5f, 0.5f);
        }
    }

    // Standing players the old men can go for (not downed, not dead, not hidden by a drink / menu).
    public IEnumerable<PlayerHealth> Targetable => players.Where(p => p != null && p.CanBeTargeted);
    public IEnumerable<PlayerHealth> Standing => players.Where(p => p != null && !p.IsDead && !p.IsDowned);

    public PlayerHealth NearestTarget(Vector3 from)
    {
        PlayerHealth best = null;
        float bd = float.MaxValue;
        foreach (var p in Targetable)
        {
            float d = (p.transform.position - from).sqrMagnitude;
            if (d < bd) { bd = d; best = p; }
        }
        return best;
    }

    public PlayerHealth NearestStanding(Vector3 from)
    {
        PlayerHealth best = null;
        float bd = float.MaxValue;
        foreach (var p in Standing)
        {
            float d = (p.transform.position - from).sqrMagnitude;
            if (d < bd) { bd = d; best = p; }
        }
        return best;
    }

    void OnPlayerDowned(PlayerHealth p)
    {
        if (!Net.Authority) return;   // online: the host counts it and charges the pot
        var slot = PlayerSlot.Of(p);
        if (slot != null) slot.Downs++;
        if (IsCoop && coop != null && coop.downPenalty > 0f && Points > 0)
        {
            int lost = Mathf.RoundToInt(Points * coop.downPenalty);
            if (lost > 0) TrySpend(lost, "Caído " + (slot != null ? slot.Tag : ""));
        }
        if (IsCoop) ShowMessage("¡" + (slot != null ? slot.Tag + " (" + slot.Animal.displayName + ")" : "Un jugador") + " ha caído! Mantén □ a su lado para levantarle");
        CheckTeamDown();
    }

    // Everyone down or dead: game over (like Zombies). Solo: the player's own death ends it.
    void CheckTeamDown()
    {
        if (!Net.Authority) return;   // online: the host decides
        if (State == GameState.GameOver || State == GameState.Victory || players.Count == 0) return;
        if (players.All(p => p == null || p.IsDead || p.IsDowned))
        {
            if (IsCoop) ShowMessage("Todo el equipo ha caído...");
            Invoke(nameof(GameOver), IsCoop ? 1.5f : 1.2f);
        }
    }

    // Start of each round: players who bled out come back next to a teammate with the pistol only.
    void RespawnFallen()
    {
        foreach (var p in players)
        {
            if (p == null || !p.IsDead || !IsCoop) continue;
            var pslot = PlayerSlot.Of(p);
            if (pslot != null && (pslot.IsRemote || pslot.Gone)) continue;   // online: each machine brings back its own player
            var mate = NearestStanding(p.transform.position);
            Vector3 at = mate != null ? mate.transform.position - mate.transform.forward * 1.5f : playerSpawn.position;
            if (NavAgents.Sample(at, 3f, out var s)) at = s;
            p.RespawnAt(at);
            var slot = PlayerSlot.Of(p);
            ShowMessage((slot != null ? slot.Tag : "Un jugador") + " vuelve a la isla (solo con la pistola)");
        }
    }

    // ---------------------------------------------------------------- points
    public int AddPoints(int raw, PointSource source = PointSource.Bonus)
    {
        if (Net.IsClient) return 0;   // online: the pot lives on the host
        int amount = source == PointSource.Debug ? raw : Mathf.RoundToInt(raw * RunModifiers.Get(Stat.PointsGain));
        Points += amount;
        if (source != PointSource.Debug)
        {
            TotalEarned += amount;
            if (RunLog.Instance != null) RunLog.Instance.Earned(amount, source);
        }
        PointsChanged?.Invoke(Points);
        if (amount != 0) PointsDelta?.Invoke(amount, source);
        if (NetGame.H != null) NetGame.H.PointsChanged(Points, amount, source, TotalEarned);
        return amount;
    }

    public bool TrySpend(int cost, string what = "")
    {
        if (Net.IsClient) return false;   // online: only the host spends (clients ask it)
        if (cost > Points) return false;
        Points -= cost;
        PointsChanged?.Invoke(Points);
        if (cost > 0)
        {
            PointsDelta?.Invoke(-cost, PointSource.Spend);
            if (RunLog.Instance != null) RunLog.Instance.Spent(cost, what);
            if (NetGame.H != null) NetGame.H.PointsChanged(Points, -cost, PointSource.Spend, TotalEarned);
        }
        return true;
    }

    // Online client: the host's pot.
    public void NetSetPoints(int total, int delta, PointSource source, int earned)
    {
        Points = total;
        TotalEarned = earned;
        PointsChanged?.Invoke(Points);
        if (delta != 0) PointsDelta?.Invoke(delta, source);
    }

    public void RegisterKill(bool headshot, Component by = null)
    {
        Kills++;
        if (headshot) Headshots++;
        var slot = PlayerSlot.Of(by);
        if (slot != null)
        {
            slot.Kills++;
            if (headshot) slot.Headshots++;
        }
    }

    // Toast for everyone (online: the host's toasts reach every player).
    public void ShowMessage(string text)
    {
        if (Net.Replaying) return;   // replaying the host's action: its own toast is on its way
        if (NetGame.H != null) NetGame.H.Toast(text);
        Message?.Invoke(text);
    }

    // Toast only for one player ("not enough conchas").
    public void ShowMessageTo(Component player, string text)
    {
        if (Net.Replaying) return;
        if (NetGame.H != null && PlayerSlot.IsProxy(player)) { NetGame.H.ToastTo(player, text); return; }
        Message?.Invoke(text);
    }

    public void NetToast(string text) => Message?.Invoke(text);

    // ---------------------------------------------------------------- state
    public void SetState(GameState s)
    {
        State = s;
        // online nothing pauses the world (menus only cover this screen)
        Time.timeScale = Net.Online ? 1f : s == GameState.Playing || s == GameState.GameOver || s == GameState.Victory ? 1f : 0f;
        StateChanged?.Invoke(s);
    }

    public void SetPaused(bool paused)
    {
        if (paused && State == GameState.Playing) { stateBeforePause = State; SetState(GameState.Paused); }
        else if (!paused && State == GameState.Paused) SetState(stateBeforePause);
    }

    public void GameOver()
    {
        if (State == GameState.GameOver || State == GameState.Victory) return;
        endTime = Time.time;
        SetState(GameState.GameOver);
        if (NetGame.H != null) NetGame.H.GameEnded(GameState.GameOver);
    }

    public void Victory()
    {
        if (State == GameState.GameOver || State == GameState.Victory) return;
        endTime = Time.time;
        foreach (var p in players)
        {
            if (p == null) continue;
            p.GetComponent<PlayerController>().ControlsEnabled = false;
            p.GetComponent<PlayerShooter>().enabled = false;
        }
        SetState(GameState.Victory);
        if (NetGame.H != null) NetGame.H.GameEnded(GameState.Victory);
    }

    // Online client: the host ended the run.
    public void NetEnd(GameState state, int kills, int headshots, int earned, int doors, int cards, bool boss, float runTime)
    {
        Kills = kills;
        Headshots = headshots;
        TotalEarned = earned;
        DoorsOpened = doors;
        CardsTaken = cards;
        BossDefeated = boss;
        netRunTime = runTime;
        if (state == GameState.Victory) Victory();
        else GameOver();
    }

    // Final score: points earned + survival + boss + extracted items, scaled by the curses taken.
    public ScoreBreakdown ComputeScore()
    {
        var s = new ScoreBreakdown();
        s.points = TotalEarned;
        s.rounds = (RoundManager.Instance != null ? RoundManager.Instance.Round : 0) * 500;
        s.boss = BossDefeated ? 5000 : 0;
        s.extracted = State == GameState.Victory ? 2500 : 0;
        s.itemMultiplier = 1f;
        if (State == GameState.Victory)
            foreach (var p in players)
            {
                var carry = p != null ? p.GetComponent<PlayerCarry>() : null;
                if (carry != null && carry.Carried != null) s.itemMultiplier = Mathf.Max(s.itemMultiplier, carry.Carried.ScoreMultiplier);
            }
        s.curseMultiplier = 1f + 0.1f * CardsTaken;
        s.total = Mathf.RoundToInt((s.points + s.rounds + s.boss + s.extracted) * s.itemMultiplier * s.curseMultiplier);
        return s;
    }

    public void Restart()
    {
        if (Net.Online) { if (Net.IsHost) NetSession.Instance.RestartMatch(); return; }   // online: the host restarts for everyone
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Online: the host takes everyone back to the room; a client leaves the match.
    public void ReturnToMenu()
    {
        if (Net.Online) { if (Net.IsHost) NetSession.Instance.BackToLobby(); else NetSession.Instance.Leave(); return; }
        Time.timeScale = 1f;
        SceneManager.LoadScene(MenuScene);
    }
}

public struct ScoreBreakdown
{
    public int points, rounds, boss, extracted, total;
    public float itemMultiplier, curseMultiplier;
}
