using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

// The online session (lives across scenes): connection (Unity Relay with a join code, or a direct IP for
// LAN / tests), the room (who is in, their animal, difficulty / mode / map chosen by the host), the flow
// (start the match, everyone loaded -> go, restart, back to the room, leave) and the message plumbing:
// one unnamed Netcode message per NetMsg, payload written with NetBuf. The host relays what one client
// says to the others. No servers of our own: the host's PC is the server, Relay only forwards packets.
public class NetSession : MonoBehaviour
{
    public const int MaxPlayers = 4;
    public const ushort DefaultPort = 7777;
    const string Version = "animal-island-online-7";   // 7: weapon stars (loadout, box, Forward.Star). 6: the false teeth (PlayerFx kind 3). 5: lit zones in the quest state, traca message (Villajubilada)
    const float LoadTimeout = 45f;

    public static NetSession Instance { get; private set; }

    public class Member
    {
        public ulong clientId;
        public int index;
        public string animal = "RedPanda";
        public bool loaded;
        public bool gone;   // left during the match
        public string Tag => "J" + (index + 1);
    }

    public readonly List<Member> Roster = new List<Member>();
    public Difficulty Difficulty { get; private set; } = Difficulty.Normal;
    public bool AnselmoCagon { get; private set; }
    public string MapId { get; private set; } = "";
    public string SceneName { get; private set; } = "";
    public bool IsHost { get; private set; }
    public bool Active => nm != null && !nm.ShutdownInProgress && (nm.IsServer || nm.IsConnectedClient) && !leaving;
    public bool Busy { get; private set; }
    public string JoinCode { get; private set; } = "";
    public bool UsingRelay { get; private set; }
    public string Status { get; private set; } = "";
    public bool InGame { get; private set; }
    public bool AllLoaded => Roster.All(m => m.loaded || m.gone);
    public Member Me => nm == null ? null : Roster.Find(m => m.clientId == nm.LocalClientId);
    public int LocalIndex { get { var m = Me; return m != null ? m.index : 0; } }
    public int PlayerCount => Roster.Count(m => !m.gone);
    public static string LastEndReason { get; set; } = "";   // shown by the menu after the session ended

    public event Action Changed;   // roster / settings / status

    NetworkManager nm;
    UnityTransport transport;
    bool leaving;
    float loadStarted;
    readonly Dictionary<ulong, string> pendingAnimal = new Dictionary<ulong, string>();

    static readonly Dictionary<NetMsg, Action<int, NetRead>> handlers = new Dictionary<NetMsg, Action<int, NetRead>>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        handlers.Clear();
        Instance = null;
        LastEndReason = "";
    }

    public static NetSession Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("NetSession");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<NetSession>();
        return Instance;
    }

    // In-game systems register what they handle (one handler per message type).
    public static void On(NetMsg msg, Action<int, NetRead> handler) => handlers[msg] = handler;
    public static void Off(NetMsg msg) => handlers.Remove(msg);

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (nm != null) { if (nm.IsListening) nm.Shutdown(); Destroy(nm.gameObject); }
    }

    void SetStatus(string s) { Status = s; Changed?.Invoke(); }

    // ---------------------------------------------------------------- connect
    void Build()
    {
        if (nm != null) return;
        var go = new GameObject("NetworkManager");
        go.SetActive(false);
        transport = go.AddComponent<UnityTransport>();
        nm = go.AddComponent<NetworkManager>();
        nm.NetworkConfig = new NetworkConfig
        {
            NetworkTransport = transport,
            ConnectionApproval = true,
            EnableSceneManagement = false,
            TickRate = 30,
            ClientConnectionBufferTimeout = 15,
        };
        nm.RunInBackground = true;
        go.SetActive(true);
        DontDestroyOnLoad(go);
        nm.ConnectionApprovalCallback = Approve;
        nm.OnClientConnectedCallback += OnConnected;
        nm.OnClientDisconnectCallback += OnDisconnected;
        nm.OnTransportFailure += OnTransportFailure;
    }

    byte[] Payload() => Encoding.UTF8.GetBytes(Version + "|" + GameSettings.Animal);

    void BeginHosting()
    {
        IsHost = true;
        Roster.Clear();
        Difficulty = GameSettings.Difficulty;
        AnselmoCagon = GameSettings.AnselmoCagon;
        MapId = GameSettings.Map;
        nm.NetworkConfig.ConnectionData = Payload();
        if (!nm.StartHost()) { Fail("No se pudo abrir la partida (¿el puerto " + DefaultPort + " está ocupado?)"); return; }
        nm.CustomMessagingManager.OnUnnamedMessage += OnMessage;
        Roster.Add(new Member { clientId = nm.LocalClientId, index = 0, animal = GameSettings.Animal });
        Busy = false;
        SetStatus("Sala abierta. Pasa el código a tus amigos.");
    }

    // Host with a direct IP (LAN, VPN, tests). Friends type this PC's IP.
    public void HostDirect(ushort port = DefaultPort, bool localOnly = false)
    {
        if (Busy || Active) return;
        Build();
        UsingRelay = false;
        string listen = localOnly ? "127.0.0.1" : "0.0.0.0";   // tests: this PC only (no firewall prompt)
        transport.SetConnectionData(listen, port, listen);
        JoinCode = (localOnly ? "127.0.0.1" : LocalAddress()) + ":" + port;
        BeginHosting();
    }

    // Host through Unity Relay: anyone on the internet joins with the 6-letter code.
    public async void HostRelay()
    {
        if (Busy || Active) return;
        Busy = true;
        UsingRelay = true;
        SetStatus("Conectando con Unity Relay...");
        try
        {
            await SignIn();
            Allocation alloc = await RelayService.Instance.CreateAllocationAsync(MaxPlayers - 1);
            string code = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);
            if (this == null) return;
            Build();
            transport.SetRelayServerData(alloc.ToRelayServerData("dtls"));
            JoinCode = code;
            BeginHosting();
        }
        catch (Exception e)
        {
            Fail("No se pudo crear la partida con Relay.\n" + Short(e) + "\n(¿Relay activado en el Unity Dashboard?)");
        }
    }

    // `code`: a Relay join code, or an IP ("192.168.1.20" / "1.2.3.4:7777") for a direct connection.
    public async void Join(string code)
    {
        if (Busy || Active) return;
        code = (code ?? "").Trim();
        if (code.Length == 0) { Fail("Escribe el código de la partida"); return; }
        Busy = true;
        IsHost = false;
        Roster.Clear();
        try
        {
            if (code.Contains(".") || code.Contains(":") || code.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            {
                string ip = code;
                ushort port = DefaultPort;
                int colon = code.LastIndexOf(':');
                if (colon > 0) { ip = code.Substring(0, colon); ushort.TryParse(code.Substring(colon + 1), out port); }
                if (ip.Equals("localhost", StringComparison.OrdinalIgnoreCase)) ip = "127.0.0.1";
                SetStatus("Conectando con " + ip + ":" + port + "...");
                Build();
                UsingRelay = false;
                transport.SetConnectionData(ip, port);
            }
            else
            {
                SetStatus("Buscando la partida " + code.ToUpperInvariant() + "...");
                await SignIn();
                JoinAllocation join = await RelayService.Instance.JoinAllocationAsync(code.ToUpperInvariant());
                if (this == null) return;
                Build();
                UsingRelay = true;
                transport.SetRelayServerData(join.ToRelayServerData("dtls"));
            }
            JoinCode = code.ToUpperInvariant();
            nm.NetworkConfig.ConnectionData = Payload();
            if (!nm.StartClient()) { Fail("No se pudo conectar"); return; }
            nm.CustomMessagingManager.OnUnnamedMessage += OnMessage;
            SetStatus("Conectando...");
        }
        catch (Exception e)
        {
            Fail("No se encuentra la partida \"" + code + "\".\n" + Short(e));
        }
    }

    static string Short(Exception e)
    {
        var m = e.InnerException != null ? e.InnerException.Message : e.Message;
        return m.Length > 160 ? m.Substring(0, 160) + "..." : m;
    }

    static async Task SignIn()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            var options = new InitializationOptions();
            string profile = Arg("-netprofile");
            if (!string.IsNullOrEmpty(profile)) options.SetProfile(profile);
            await UnityServices.InitializeAsync(options);
        }
        if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    public static string Arg(string name)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
        return null;
    }

    public static bool HasArg(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;

    static string LocalAddress()
    {
        try
        {
            foreach (var a in System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList)
                if (a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(a)) return a.ToString();
        }
        catch { }
        return "127.0.0.1";
    }

    void Fail(string reason)
    {
        Busy = false;
        Debug.LogWarning("[Net] " + reason);
        LastEndReason = reason;
        ShutdownNetwork();
        SetStatus(reason);
    }

    void Approve(NetworkManager.ConnectionApprovalRequest req, NetworkManager.ConnectionApprovalResponse res)
    {
        res.CreatePlayerObject = false;
        if (req.ClientNetworkId == NetworkManager.ServerClientId) { res.Approved = true; return; }
        var parts = Encoding.UTF8.GetString(req.Payload ?? new byte[0]).Split('|');
        if (parts.Length < 2 || parts[0] != Version) { res.Approved = false; res.Reason = "Tenéis versiones distintas del juego: usad todos el mismo .zip"; return; }
        if (InGame) { res.Approved = false; res.Reason = "La partida ya ha empezado: espera a que vuelvan a la sala"; return; }
        if (Roster.Count >= MaxPlayers) { res.Approved = false; res.Reason = "La sala está llena (4 jugadores)"; return; }
        pendingAnimal[req.ClientNetworkId] = parts[1];
        res.Approved = true;
    }

    void OnConnected(ulong clientId)
    {
        if (IsHost)
        {
            if (clientId == nm.LocalClientId || Roster.Exists(m => m.clientId == clientId)) return;
            int index = 0;
            while (Roster.Exists(m => m.index == index)) index++;
            pendingAnimal.TryGetValue(clientId, out var animal);
            Roster.Add(new Member { clientId = clientId, index = index, animal = string.IsNullOrEmpty(animal) ? "RedPanda" : animal });
            Roster.Sort((a, b) => a.index.CompareTo(b.index));
            BroadcastLobby();
            SetStatus("J" + (index + 1) + " se ha unido");
        }
        else if (clientId == nm.LocalClientId)
        {
            Busy = false;
            SetStatus("Conectado. Esperando al anfitrión...");
        }
    }

    void OnDisconnected(ulong clientId)
    {
        if (nm == null) return;
        if (IsHost)
        {
            var m = Roster.Find(x => x.clientId == clientId);
            if (m == null) return;
            if (InGame)
            {
                m.gone = true;
                if (NetGame.Instance != null) NetGame.Instance.PlayerLeft(m.index);
                CheckAllLoaded();
            }
            else Roster.Remove(m);
            BroadcastLobby();
            SetStatus(m.Tag + " se ha ido");
        }
        else if (clientId == nm.LocalClientId || clientId == NetworkManager.ServerClientId)
        {
            string reason = nm.DisconnectReason;
            EndSession(string.IsNullOrEmpty(reason) ? (Busy ? "No se pudo conectar con la partida" : "Se ha perdido la conexión con el anfitrión") : reason);
        }
    }

    void OnTransportFailure() => EndSession("Fallo de conexión");

    // Back to the main menu with a message (the session is over for this machine).
    void EndSession(string reason)
    {
        if (leaving) return;
        LastEndReason = reason;
        Debug.LogWarning("[Net] " + reason);
        Busy = false;
        bool wasInGame = InGame;
        ShutdownNetwork();
        SetStatus(reason);
        if (wasInGame || SceneManager.GetActiveScene().name != GameManager.MenuScene) SceneManager.LoadScene(GameManager.MenuScene);
    }

    void ShutdownNetwork()
    {
        InGame = false;
        Roster.Clear();
        if (nm != null)
        {
            nm.OnClientConnectedCallback -= OnConnected;
            nm.OnClientDisconnectCallback -= OnDisconnected;
            nm.OnTransportFailure -= OnTransportFailure;
            if (nm.CustomMessagingManager != null) nm.CustomMessagingManager.OnUnnamedMessage -= OnMessage;
            if (nm.IsListening) nm.Shutdown();
            Destroy(nm.gameObject);
            nm = null;
        }
        JoinCode = "";
    }

    // Leave the room / match (the host closes it for everyone).
    public void Leave()
    {
        if (leaving) return;
        if (IsHost && nm != null && nm.IsServer)
        {
            foreach (var m in Roster) if (m.clientId != nm.LocalClientId) nm.DisconnectClient(m.clientId, "El anfitrión ha cerrado la partida");
            StartCoroutine(LeaveSoon());
            return;
        }
        Finish();
    }

    IEnumerator LeaveSoon()
    {
        leaving = true;
        yield return new WaitForSecondsRealtime(0.3f);
        Finish();
    }

    void Finish()
    {
        leaving = true;
        ShutdownNetwork();
        Instance = null;
        Destroy(gameObject);
        if (SceneManager.GetActiveScene().name != GameManager.MenuScene) SceneManager.LoadScene(GameManager.MenuScene);
    }

    // ---------------------------------------------------------------- messages
    void OnMessage(ulong clientId, FastBufferReader reader)
    {
        int len = reader.Length - reader.Position;
        if (len <= 0) return;
        var data = new byte[len];
        reader.ReadBytesSafe(ref data, len);
        var op = (NetMsg)data[0];
        int sender = IsHost ? IndexOf(clientId) : 0;
        if (IsHost && sender < 0) return;
        var r = new NetRead(data, 1);
        try
        {
            if (!HandleSession(op, sender, r) && handlers.TryGetValue(op, out var h)) h(sender, r);
        }
        catch (Exception e) { Debug.LogException(e); }
    }

    int IndexOf(ulong clientId)
    {
        var m = Roster.Find(x => x.clientId == clientId);
        return m != null ? m.index : -1;
    }

    void Transmit(ulong clientId, NetBuf b, bool reliable)
    {
        if (nm == null || !nm.IsListening) return;
        using (var w = new FastBufferWriter(b.Length, Allocator.Temp))
        {
            w.WriteBytesSafe(b.Data, b.Length);
            nm.CustomMessagingManager.SendUnnamedMessage(clientId, w, reliable ? NetworkDelivery.ReliableFragmentedSequenced : NetworkDelivery.UnreliableSequenced);
        }
    }

    public void ToHost(NetBuf b, bool reliable = true)
    {
        if (IsHost || nm == null) return;
        Transmit(NetworkManager.ServerClientId, b, reliable);
    }

    // Host: to every other player (by default only those already in the match scene).
    public void ToAll(NetBuf b, bool reliable = true, int except = -1, bool loadedOnly = true)
    {
        if (!IsHost || nm == null) return;
        foreach (var m in Roster)
        {
            if (m.clientId == nm.LocalClientId || m.index == except || m.gone || (loadedOnly && !m.loaded)) continue;
            Transmit(m.clientId, b, reliable);
        }
    }

    // Host: to one player (nothing if that is the host itself).
    public void ToPlayer(int index, NetBuf b, bool reliable = true)
    {
        if (!IsHost || nm == null) return;
        var m = Roster.Find(x => x.index == index);
        if (m == null || m.gone || m.clientId == nm.LocalClientId) return;
        Transmit(m.clientId, b, reliable);
    }

    public bool IsLocalIndex(int index) => index == LocalIndex;

    // ---------------------------------------------------------------- room (lobby)
    bool HandleSession(NetMsg op, int sender, NetRead r)
    {
        switch (op)
        {
            case NetMsg.Lobby: if (!IsHost) ReadLobby(r); return true;
            case NetMsg.SetAnimal:
                if (IsHost) { var m = Roster.Find(x => x.index == sender); if (m != null) { m.animal = r.Str(); BroadcastLobby(); } }
                return true;
            case NetMsg.StartGame:
            case NetMsg.Restart:
                if (!IsHost) { string scene = r.Str(); ReadLobby(r); BeginLoad(scene); }
                return true;
            case NetMsg.Loaded:
                if (IsHost)
                {
                    var m = Roster.Find(x => x.index == sender);
                    if (m != null) m.loaded = true;
                    if (NetGame.Instance != null && NetGame.Instance.Ready) NetGame.Instance.LateLoaded(sender);   // arrived after the start
                    else CheckAllLoaded();
                }
                return true;
            case NetMsg.Go:
                if (!IsHost && NetGame.Instance != null) NetGame.Instance.OnGo();
                return true;
            case NetMsg.ToLobby:
                if (!IsHost) { InGame = false; ReadLobby(r); SceneManager.LoadScene(GameManager.MenuScene); }
                return true;
        }
        return false;
    }

    void WriteLobby(NetBuf b)
    {
        b.Byte((byte)Difficulty).Bool(AnselmoCagon).Str(MapId).Byte((byte)Roster.Count);
        foreach (var m in Roster) b.Int((int)(m.clientId & 0xffffffff)).Int((int)(m.clientId >> 32)).Byte((byte)m.index).Str(m.animal).Bool(m.gone);
    }

    void ReadLobby(NetRead r)
    {
        Difficulty = (Difficulty)r.Byte();
        AnselmoCagon = r.Bool();
        MapId = r.Str();
        int n = r.Byte();
        Roster.Clear();
        for (int i = 0; i < n; i++)
        {
            ulong lo = (uint)r.Int(), hi = (uint)r.Int();
            Roster.Add(new Member { clientId = lo | (hi << 32), index = r.Byte(), animal = r.Str(), gone = r.Bool() });
        }
        Changed?.Invoke();
    }

    void BroadcastLobby()
    {
        if (!IsHost) return;
        var b = NetBuf.Make(NetMsg.Lobby);
        WriteLobby(b);
        ToAll(b, true, -1, false);
        Changed?.Invoke();
    }

    public void SetMyAnimal(string animal)
    {
        var me = Me;
        if (me == null) return;
        me.animal = animal;
        GameSettings.Animal = animal;
        if (IsHost) BroadcastLobby();
        else { ToHost(NetBuf.Make(NetMsg.SetAnimal).Str(animal)); Changed?.Invoke(); }
    }

    public void SetSettings(Difficulty difficulty, bool cagon, string mapId)
    {
        if (!IsHost) return;
        Difficulty = difficulty;
        AnselmoCagon = cagon;
        MapId = mapId ?? "";
        BroadcastLobby();
    }

    // ---------------------------------------------------------------- match flow
    public void StartMatch(string scene)
    {
        if (!IsHost || InGame) return;
        SendLoad(NetMsg.StartGame, scene);
    }

    public void RestartMatch()
    {
        if (!IsHost) return;
        SendLoad(NetMsg.Restart, SceneName.Length > 0 ? SceneName : SceneManager.GetActiveScene().name);
    }

    void SendLoad(NetMsg op, string scene)
    {
        Roster.RemoveAll(m => m.gone);
        foreach (var m in Roster) m.loaded = false;
        var b = NetBuf.Make(op).Str(scene);
        WriteLobby(b);
        ToAll(b, true, -1, false);
        BeginLoad(scene);
    }

    void BeginLoad(string scene)
    {
        InGame = true;
        SceneName = scene;
        loadStarted = Time.unscaledTime;
        Time.timeScale = 1f;
        SceneManager.LoadScene(scene);
    }

    public void BackToLobby()
    {
        if (!IsHost) return;
        InGame = false;
        Roster.RemoveAll(m => m.gone);
        foreach (var m in Roster) m.loaded = false;
        var b = NetBuf.Make(NetMsg.ToLobby);
        WriteLobby(b);
        ToAll(b, true, -1, false);
        SceneManager.LoadScene(GameManager.MenuScene);
    }

    // The match scene is ready on this machine (GameManager).
    public void NotifyLoaded()
    {
        var me = Me;
        if (me != null) me.loaded = true;
        if (IsHost) CheckAllLoaded();
        else ToHost(NetBuf.Make(NetMsg.Loaded));
    }

    void CheckAllLoaded(bool force = false)
    {
        if (!IsHost || !InGame || NetGame.Instance == null || NetGame.Instance.Ready) return;
        var me = Me;
        if (me == null || !me.loaded) return;
        if (!AllLoaded && !force) return;
        ToAll(NetBuf.Make(NetMsg.Go));
        NetGame.Instance.OnGo();
    }

    void Update()
    {
        // someone takes forever to load: start without them (they join the action when they arrive)
        if (IsHost && InGame && NetGame.Instance != null && !NetGame.Instance.Ready && Time.unscaledTime - loadStarted > LoadTimeout)
            CheckAllLoaded(true);
    }
}
