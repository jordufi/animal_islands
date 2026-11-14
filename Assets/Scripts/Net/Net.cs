// Online play (2-4 players, each in their own home): the HOST runs the world (old men, rounds, doors, box,
// cards, the shared pot, boss) and every machine runs its OWN player (movement, aiming, shooting, health).
// Clients show the old men as puppets driven by the host's snapshots, send their hits to the host and ask
// the host to use doors / machines. Offline (solo, split-screen) nothing here is active: Authority is true.
public static class Net
{
    public static bool Online => NetSession.Instance != null && NetSession.Instance.Active;
    public static bool IsHost => Online && NetSession.Instance.IsHost;
    public static bool IsClient => Online && !NetSession.Instance.IsHost;   // a pure client (not the host)
    public static bool Authority => !IsClient;                              // runs the world: offline or host
    public static int LocalIndex => Online ? NetSession.Instance.LocalIndex : 0;

    // True while a client applies something the host already did (no duplicate toasts / side effects).
    public static bool Replaying { get; set; }
}

// Message types. Lobby / flow first, then the match.
public enum NetMsg : byte
{
    // session
    Lobby = 1, SetAnimal, StartGame, Loaded, Go, Restart, ToLobby, Closing,
    TestCmd = 15, TestReport,   // automated online play tests (NetTestBot, only with -netbot)

    // players
    PlayerSnap = 20, PlayerStatus, PlayerLoadout, PlayerFx, PlayerGone,
    HitEnemy, DamagePlayer, Forward, Interact, DropCarry, Revived, ReviveFor,

    // world
    Points = 40, Toast, GameEnd, RoundEvent, RoundTick,
    EnemySpawn, EnemySnap, EnemyEvent, ChanclaThrow, EntranceUse,
    DoorOpened, BoxState, KeyState, DiscPicked, KaraokeState, CarryState,
    PowerUpSpawn, PowerUpTaken, CardOffer, CardSelect, CardPick, CardChosen, BossDefeated, WorldInit,
}
