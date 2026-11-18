using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// The online match on this machine (added to the GameManager when the session is online).
// HOST: broadcasts what happens in the world (old men spawning / moving / attacking / dying, rounds, doors,
// box, key, records, karaoke, disc, coolers, cards, boss, conchas, toasts, end of the run), applies the hits
// the clients send, runs their door / machine requests with their proxy player, and forwards to each owner
// what happens to their player (damage, a new weapon, a drink, a revive...).
// CLIENT: applies all that (old men are puppets, world scripts don't decide anything) and sends its hits
// and requests. Players' own snapshots: NetPlayer.
// Scene objects are matched between machines by their place in the hierarchy (same scene file).
public class NetGame : MonoBehaviour
{
    public const int DiscId = 60000;
    const float EnemySnapInterval = 0.05f;
    const float TickInterval = 0.25f;

    public enum Fwd : byte { GiveWeapon = 1, Upgrade, RefillAmmo, RefillAll, AddDrink, CardBusy, CardFree, Revive, Gondola, Star }

    public static NetGame Instance { get; private set; }
    public static NetGame H => Net.IsHost ? Instance : null;     // host-side hooks: NetGame.H?.Something()
    public static NetGame C => Net.IsClient ? Instance : null;   // client-side hooks

    public bool Ready { get; private set; }
    public int EnemyCount => enemies.Count;              // tests
    public int MessagesIn { get; private set; }          // tests

    GameManager gm;
    readonly List<Interactable> things = new List<Interactable>();
    readonly Dictionary<Interactable, int> thingIndex = new Dictionary<Interactable, int>();
    readonly List<SpawnEntrance> entrances = new List<SpawnEntrance>();
    readonly List<Transform> boxSpots = new List<Transform>();
    readonly Dictionary<int, EnemyHealth> enemies = new Dictionary<int, EnemyHealth>();
    readonly Dictionary<int, AmmoPickup> pickups = new Dictionary<int, AmmoPickup>();
    readonly List<int> openedDoors = new List<int>();
    readonly List<int> pickedDiscs = new List<int>();
    int nextEnemyId = 1, nextPickupId = 1;
    Carryable disc;
    float nextEnemySnap, nextTick;

    static NetSession S => NetSession.Instance;

    // ---------------------------------------------------------------- setup
    // Called by the GameManager before the players spawn (so player-made interactables aren't listed).
    public void Init(GameManager manager)
    {
        Instance = this;
        gm = manager;
        foreach (var i in FindObjectsByType<Interactable>().OrderBy(i => PathKey(i.transform), StringComparer.Ordinal))
        {
            thingIndex[i] = things.Count;
            things.Add(i);
        }
        entrances.AddRange(SpawnEntrance.All.Where(e => e != null).OrderBy(e => PathKey(e.transform), StringComparer.Ordinal));
        if (ZoneManager.Instance != null)
            foreach (var z in ZoneManager.Instance.Zones.OrderBy(z => PathKey(z.transform), StringComparer.Ordinal)) boxSpots.AddRange(z.BoxSpots);

        On(NetMsg.PlayerSnap, (s, r) => OnPlayerMsg(s, r, false, (p, rd) => p.ApplySnap(rd)));
        On(NetMsg.PlayerStatus, (s, r) => OnPlayerMsg(s, r, true, (p, rd) => p.ApplyStatus(rd)));
        On(NetMsg.PlayerLoadout, (s, r) => OnPlayerMsg(s, r, true, (p, rd) => p.ApplyLoadout(rd)));
        On(NetMsg.PlayerFx, (s, r) => OnPlayerMsg(s, r, false, (p, rd) => p.ApplyFx(rd)));
        On(NetMsg.PlayerGone, (s, r) => { if (Net.IsClient) PlayerLeft(r.Byte()); });
        On(NetMsg.HitEnemy, OnHitEnemy);
        On(NetMsg.DamagePlayer, (s, r) => { if (Net.IsClient) { var p = LocalPlayer; if (p != null) p.TakeDamage(r.UShort()); } });
        On(NetMsg.Forward, OnForward);
        On(NetMsg.Interact, OnInteract);
        On(NetMsg.DropCarry, OnDropCarry);
        On(NetMsg.Revived, OnRevived);
        On(NetMsg.Points, (s, r) => { if (Net.IsClient) gm.NetSetPoints(r.Int(), r.Int(), (PointSource)r.Byte(), r.Int()); });
        On(NetMsg.Toast, (s, r) => { if (Net.IsClient) gm.NetToast(r.Str()); });
        On(NetMsg.GameEnd, OnGameEnd);
        On(NetMsg.RoundEvent, OnRoundEvent);
        On(NetMsg.RoundTick, OnRoundTick);
        On(NetMsg.EnemySpawn, OnEnemySpawn);
        On(NetMsg.EnemySnap, OnEnemySnap);
        On(NetMsg.EnemyEvent, OnEnemyEvent);
        On(NetMsg.ChanclaThrow, (s, r) => { if (Net.IsClient) ChanclaProjectile.ThrowVisual(r.Vec(), r.Vec(), r.Float()); });
        On(NetMsg.EntranceUse, OnEntranceUse);
        On(NetMsg.DoorOpened, (s, r) => { if (Net.IsClient) ApplyDoor(r.UShort()); });
        On(NetMsg.BoxState, (s, r) => { if (Net.IsClient) ApplyBox(r); });
        On(NetMsg.KeyState, (s, r) => { if (Net.IsClient) ApplyKey(r); });
        On(NetMsg.DiscPicked, (s, r) => { if (Net.IsClient) ApplyDiscPicked(r.UShort(), r.Byte()); });
        On(NetMsg.KaraokeState, (s, r) => { if (Net.IsClient) ApplyKaraoke(r); });
        On(NetMsg.CarryState, (s, r) => { if (Net.IsClient) ApplyCarry(r); });
        On(NetMsg.PowerUpSpawn, OnPowerUpSpawn);
        On(NetMsg.PowerUpTaken, (s, r) => { if (Net.IsClient && pickups.TryGetValue(r.UShort(), out var p)) { if (p != null) Destroy(p.gameObject); } });
        On(NetMsg.CardOffer, OnCardOffer);
        On(NetMsg.CardSelect, OnCardSelect);
        On(NetMsg.CardPick, OnCardPick);
        On(NetMsg.CardChosen, (s, r) => { if (Net.IsClient) Replay(() => { var cm = CardManager.Instance; if (cm != null) cm.NetChosen(FindAsset<CardData>(r.Str())); }); });
        On(NetMsg.BossDefeated, (s, r) => { if (Net.IsClient && BossEncounter.Instance != null) BossEncounter.Instance.NetBossDefeated(); });
        On(NetMsg.WorldInit, (s, r) => { if (Net.IsClient) ApplyWorld(r); });
        On(NetMsg.Encierro, (s, r) => { if (Net.IsClient && Encierro.Instance != null) Encierro.Instance.NetBegin(); });
        On(NetMsg.Trap, (s, r) => { int i = r.UShort(); if (Net.IsClient && i >= 0 && i < things.Count && things[i] is TracaTrap t) t.NetFired(); });
        On(NetMsg.Quest, (s, r) => { if (Net.IsClient && BoatMission.Instance != null) BoatMission.Instance.ReadNet(r); });
        On(NetMsg.Buffs, (s, r) => { if (Net.IsClient) ApplyBuffs(r); });
    }

    void On(NetMsg m, Action<int, NetRead> h) => NetSession.On(m, (s, r) => { MessagesIn++; h(s, r); });

    void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        foreach (NetMsg m in Enum.GetValues(typeof(NetMsg))) if ((byte)m >= 20) NetSession.Off(m);
    }

    // Hierarchy path with sibling indices: the same object has the same key on every machine.
    static string PathKey(Transform t)
    {
        var parts = new List<string>();
        for (var x = t; x != null; x = x.parent) parts.Add(x.GetSiblingIndex().ToString("D4") + x.name);
        parts.Reverse();
        return string.Join("/", parts);
    }

    static readonly Dictionary<string, UnityEngine.Object> assets = new Dictionary<string, UnityEngine.Object>();

    // ScriptableObjects (weapons, drinks, cards, variants) travel by name.
    public static T FindAsset<T>(string name) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(name)) return null;
        string key = typeof(T).Name + "/" + name;
        if (assets.TryGetValue(key, out var o) && o != null) return (T)o;
        foreach (var a in Resources.FindObjectsOfTypeAll<T>())
            if (a.name == name) { assets[key] = a; return a; }
        return null;
    }

    // Applying something the host already did: no duplicate toasts on this machine.
    static void Replay(Action a)
    {
        bool was = Net.Replaying;
        Net.Replaying = true;
        try { a(); }
        finally { Net.Replaying = was; }
    }

    // ---------------------------------------------------------------- players
    public PlayerHealth PlayerAt(int index)
    {
        if (gm == null) return null;
        foreach (var p in gm.Players)
        {
            if (p == null) continue;
            var slot = PlayerSlot.Of(p);
            if (slot != null && slot.Index == index) return p;
        }
        return null;
    }

    public PlayerHealth LocalPlayer => PlayerAt(Net.LocalIndex);
    public static int IndexOf(Component c) { var s = PlayerSlot.Of(c); return s != null ? s.Index : -1; }

    void OnPlayerMsg(int sender, NetRead r, bool reliable, Action<NetPlayer, NetRead> apply)
    {
        int index = r.Byte();
        if (Net.IsHost)
        {
            if (index != sender) return;   // a client only speaks for its own player
            S.ToAll(NetBuf.Wrap(r.Raw), reliable, sender);
        }
        var p = PlayerAt(index);
        var np = p != null ? p.GetComponent<NetPlayer>() : null;
        if (np != null && !np.IsLocal) apply(np, r);
    }

    // A client left the match: their player is out for good.
    public void PlayerLeft(int index)
    {
        var p = PlayerAt(index);
        if (p == null) return;
        var slot = PlayerSlot.Of(p);
        if (slot != null) slot.Gone = true;
        if (Net.IsHost)
        {
            var carry = p.GetComponent<PlayerCarry>();
            if (carry != null && carry.Carried != null) carry.DropAt(p.transform.position);
            S.ToAll(NetBuf.Make(NetMsg.PlayerGone).Byte((byte)index));
            gm.ShowMessage((slot != null ? slot.Tag : "Un jugador") + " se ha desconectado");
        }
        p.NetGone();
    }

    // ---------------------------------------------------------------- hits, damage, forwards
    public void SendHit(EnemyHealth enemy, float amount, bool head, HitKind kind, bool payHit, float stun, float knockback)
    {
        if (enemy.NetId <= 0) return;
        S.ToHost(NetBuf.Make(NetMsg.HitEnemy).UShort(enemy.NetId).Float(amount).Bool(head).Byte((byte)kind).Bool(payHit).Float(stun).Float(knockback));
    }

    void OnHitEnemy(int sender, NetRead r)
    {
        if (!Net.IsHost) return;
        int id = r.UShort();
        float amount = r.Float();
        bool head = r.Bool();
        var kind = (HitKind)r.Byte();
        bool pay = r.Bool();
        float stun = r.Float(), knock = r.Float();
        var by = PlayerAt(sender);
        if (by == null || !enemies.TryGetValue(id, out var e) || e == null) return;
        EnemyHits.ApplyLocal(e, Mathf.Clamp(amount, 0f, 100000f), head, kind, by, pay, stun, knock, by.transform.position);
    }

    // Host: an old man / chancla / slam hurt a remote player: their owner applies it.
    public void SendDamage(PlayerHealth proxy, int amount)
    {
        int index = IndexOf(proxy);
        if (index >= 0) S.ToPlayer(index, NetBuf.Make(NetMsg.DamagePlayer).UShort(Mathf.Clamp(amount, 0, 65535)));
    }

    // Host: something happened to a remote player's own stuff (a weapon from the box, a drink...).
    public void Forward(Component player, Fwd op, string arg = "", float value = 0f)
    {
        int index = IndexOf(player);
        if (index >= 0) S.ToPlayer(index, NetBuf.Make(NetMsg.Forward).Byte((byte)op).Str(arg).Float(value));
    }

    void OnForward(int sender, NetRead r)
    {
        if (!Net.IsClient) return;
        var op = (Fwd)r.Byte();
        string arg = r.Str();
        float value = r.Float();
        var p = LocalPlayer;
        if (p == null) return;
        var shooter = p.GetComponent<PlayerShooter>();
        switch (op)
        {
            case Fwd.GiveWeapon: shooter.GiveWeapon(FindAsset<WeaponData>(arg), Mathf.RoundToInt(value)); break;
            case Fwd.Star: shooter.AddStarCurrent(); break;
            case Fwd.Upgrade: shooter.UpgradeCurrent(); break;
            case Fwd.RefillAmmo: shooter.RefillAmmo(FindAsset<WeaponData>(arg)); break;
            case Fwd.RefillAll: shooter.RefillAllAmmo(); break;
            case Fwd.AddDrink: p.GetComponent<PlayerDrinks>().Add(FindAsset<DrinkData>(arg)); break;
            case Fwd.CardBusy: if (CardManager.Instance != null) CardManager.Instance.NetBusy(p.GetComponent<PlayerInteractor>(), true, value); break;
            case Fwd.CardFree: if (CardManager.Instance != null) CardManager.Instance.NetBusy(p.GetComponent<PlayerInteractor>(), false, 0f); break;
            case Fwd.Revive: p.Revive(); break;
            case Fwd.Gondola:   // the host charged for it: this player's own machine does the ride
                foreach (var g in FindObjectsByType<GondolaStop>()) if (g.name == arg) g.StartRide(p, Mathf.RoundToInt(value));
                break;
        }
    }

    // Client: use a door / machine / pickup (the host runs it with our proxy player).
    public void RequestInteract(Interactable i)
    {
        int id = i == disc && disc != null ? DiscId : thingIndex.TryGetValue(i, out var x) ? x : -1;
        if (id >= 0) S.ToHost(NetBuf.Make(NetMsg.Interact).UShort(id));
    }

    void OnInteract(int sender, NetRead r)
    {
        if (!Net.IsHost) return;
        int id = r.UShort();
        Interactable thing = id == DiscId ? disc : id < things.Count ? things[id] : null;
        var p = PlayerAt(sender);
        var interactor = p != null ? p.GetComponent<PlayerInteractor>() : null;
        if (thing == null || interactor == null || !thing.isActiveAndEnabled || p.IsDead || p.IsDowned) return;
        if (thing.CanInteract(interactor)) thing.Interact(interactor);
    }

    public void SendDropCarry(Vector3 at) => S.ToHost(NetBuf.Make(NetMsg.DropCarry).Vec(at));

    void OnDropCarry(int sender, NetRead r)
    {
        if (!Net.IsHost) return;
        Vector3 at = r.Vec();
        var p = PlayerAt(sender);
        var carry = p != null ? p.GetComponent<PlayerCarry>() : null;
        if (carry != null && carry.Carried != null) Replay(() => carry.DropAt(at));
    }

    // A downed REMOTE player was revived by someone on this machine.
    public void ProxyRevived(PlayerHealth target)
    {
        if (Net.IsHost) Forward(target, Fwd.Revive);
        else S.ToHost(NetBuf.Make(NetMsg.Revived).Byte((byte)IndexOf(target)));
    }

    void OnRevived(int sender, NetRead r)
    {
        if (!Net.IsHost) return;
        var target = PlayerAt(r.Byte());
        if (target == null) return;
        if (PlayerSlot.IsProxy(target)) Forward(target, Fwd.Revive);
        else target.Revive();
        var by = PlayerAt(sender);
        var bySlot = PlayerSlot.Of(by);
        if (bySlot != null) bySlot.Revives++;
        var t = PlayerSlot.Of(target);
        gm.ShowMessage((bySlot != null ? bySlot.Tag : "Alguien") + " ha levantado a " + (t != null ? t.Tag : "su compañero"));
    }

    // ---------------------------------------------------------------- conchas, toasts, end
    public void PointsChanged(int total, int delta, PointSource source, int earned) =>
        S.ToAll(NetBuf.Make(NetMsg.Points).Int(total).Int(delta).Byte((byte)source).Int(earned));

    public void Toast(string text) => S.ToAll(NetBuf.Make(NetMsg.Toast).Str(text));

    public void ToastTo(Component player, string text)
    {
        int index = IndexOf(player);
        if (index >= 0) S.ToPlayer(index, NetBuf.Make(NetMsg.Toast).Str(text));
    }

    public void GameEnded(GameState state)
    {
        var b = NetBuf.Make(NetMsg.GameEnd).Byte((byte)state).Int(gm.Kills).Int(gm.Headshots).Int(gm.TotalEarned)
            .Int(gm.DoorsOpened).Int(gm.CardsTaken).Bool(gm.BossDefeated).Float(gm.RunTime).Byte((byte)gm.PlayerCount);
        foreach (var p in gm.Players)
        {
            var s = PlayerSlot.Of(p);
            b.Byte((byte)(s != null ? s.Index : 0)).UShort(s != null ? s.Kills : 0).UShort(s != null ? s.Headshots : 0)
             .UShort(s != null ? s.Downs : 0).UShort(s != null ? s.Revives : 0);
        }
        S.ToAll(b);
    }

    void OnGameEnd(int sender, NetRead r)
    {
        if (!Net.IsClient) return;
        var state = (GameState)r.Byte();
        int kills = r.Int(), heads = r.Int(), earned = r.Int(), doors = r.Int(), cards = r.Int();
        bool boss = r.Bool();
        float time = r.Float();
        int n = r.Byte();
        for (int i = 0; i < n; i++)
        {
            int index = r.Byte();
            var s = PlayerSlot.Of(PlayerAt(index));
            int k = r.UShort(), h = r.UShort(), d = r.UShort(), rv = r.UShort();
            if (s != null) { s.Kills = k; s.Headshots = h; s.Downs = d; s.Revives = rv; }
        }
        gm.NetEnd(state, kills, heads, earned, doors, cards, boss, time);
    }

    // ---------------------------------------------------------------- rounds
    public void RoundEvent(bool started, int round, bool night) =>
        S.ToAll(NetBuf.Make(NetMsg.RoundEvent).Bool(started).UShort(round).Bool(night));

    void OnRoundEvent(int sender, NetRead r)
    {
        if (!Net.IsClient) return;
        bool started = r.Bool();
        int round = r.UShort();
        bool night = r.Bool();
        var rm = RoundManager.Instance;
        if (rm == null) return;
        Replay(() => { if (started) rm.NetRoundStarted(round, night); else rm.NetRoundEnded(round); });
    }

    void SendTick()
    {
        var rm = RoundManager.Instance;
        if (rm == null) return;
        S.ToAll(NetBuf.Make(NetMsg.RoundTick).UShort(rm.Round).UShort(rm.RemainingInRound).Bool(rm.InIntermission)
            .Bool(rm.SpawningPaused).Bool(rm.NextRoundNight).Bool(rm.IsNight), false);
    }

    void OnRoundTick(int sender, NetRead r)
    {
        if (!Net.IsClient) return;
        int round = r.UShort(), remaining = r.UShort();
        bool inter = r.Bool(), paused = r.Bool(), nextNight = r.Bool(), night = r.Bool();
        if (RoundManager.Instance != null) RoundManager.Instance.NetTick(round, remaining, inter, paused, nextNight, night);
    }

    // ---------------------------------------------------------------- old men
    public void EnemySpawned(EnemyHealth h, int kind, string variant, float maxHealth)
    {
        if (h == null) return;
        int id = nextEnemyId++;
        if (nextEnemyId > 65000) nextEnemyId = 1;
        h.NetId = id;
        enemies[id] = h;
        h.Died += (e, info) =>
        {
            enemies.Remove(e.NetId);
            S.ToAll(NetBuf.Make(NetMsg.EnemyEvent).UShort(e.NetId).Byte(2).Bool(info.headshot));
        };
        S.ToAll(SpawnMsg(h, kind, variant, maxHealth));
    }

    static NetBuf SpawnMsg(EnemyHealth h, int kind, string variant, float maxHealth) =>
        NetBuf.Make(NetMsg.EnemySpawn).UShort(h.NetId).Byte((byte)kind).Str(variant ?? "")
            .Vec(h.transform.position).Angle(h.transform.eulerAngles.y).Float(maxHealth).Float(h.Current);

    public void EnemyAttack(EnemyHealth h)
    {
        if (h != null && h.NetId > 0) S.ToAll(NetBuf.Make(NetMsg.EnemyEvent).UShort(h.NetId).Byte(1).Bool(false));
    }

    void SendEnemySnap()
    {
        var b = NetBuf.Make(NetMsg.EnemySnap).Float(Time.unscaledTime);
        var list = new List<EnemyHealth>();
        foreach (var kv in enemies) if (kv.Value != null && !kv.Value.IsDead) list.Add(kv.Value);
        b.Byte((byte)Mathf.Min(list.Count, 255));
        int n = 0;
        foreach (var e in list)
        {
            if (n++ >= 255) break;
            var t = e.transform;
            float pitch = t.eulerAngles.x;
            if (pitch > 180f) pitch -= 360f;
            var anim = e.GetComponentInChildren<Animator>();
            float speed = anim != null ? anim.GetFloat("Speed") : 0f;
            var ai = e.GetComponent<EnemyAI>();
            bool hidden = ai != null && ai.InTunnel;
            b.UShort(e.NetId).Pos(t.position).Angle(t.eulerAngles.y).SByte(Mathf.RoundToInt(pitch))
             .Byte((byte)Mathf.Clamp(Mathf.RoundToInt(speed * 40f), 0, 255)).Byte((byte)(hidden ? 1 : 0))
             .Unit(e.MaxHealth > 0f ? e.Current / e.MaxHealth : 1f);
        }
        S.ToAll(b, false);
    }

    void OnEnemySpawn(int sender, NetRead r)
    {
        if (!Net.IsClient) return;
        int id = r.UShort();
        int kind = r.Byte();
        string variantName = r.Str();
        Vector3 pos = r.Vec();
        float yaw = r.Angle();
        float maxHealth = r.Float(), current = r.Float();
        if (enemies.ContainsKey(id)) return;
        GameObject prefab = kind == 1
            ? (BossEncounter.Instance != null && BossEncounter.Instance.BossPrefab != null ? BossEncounter.Instance.BossPrefab.gameObject : null)
            : (RoundManager.Instance != null ? RoundManager.Instance.EnemyPrefab : null);
        if (prefab == null) return;
        // built under an inactive holder so the NavMeshAgent never wakes up (puppets don't use the NavMesh)
        var holder = new GameObject("PuppetHolder");
        holder.SetActive(false);
        var go = Instantiate(prefab, pos, Quaternion.Euler(0f, yaw, 0f), holder.transform);
        var agent = go.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = false;
        go.name = prefab.name + (kind == 1 ? "" : "_Net" + id);
        var h = go.GetComponent<EnemyHealth>();
        h.NetId = id;
        var ne = go.AddComponent<NetEnemy>();
        ne.Id = id;
        go.transform.SetParent(null, true);
        Destroy(holder);
        if (kind == 1)
        {
            var boss = go.GetComponent<BossAI>();
            boss.MakePuppet();
            boss.Configure(maxHealth);
            if (BossEncounter.Instance != null) BossEncounter.Instance.NetBossSpawned(boss);
        }
        else
        {
            var ai = go.GetComponent<EnemyAI>();
            ai.MakePuppet();
            ai.Configure(FindAsset<EnemyVariant>(variantName), maxHealth);
        }
        h.NetSync(current);
        enemies[id] = h;
    }

    void OnEnemySnap(int sender, NetRead r)
    {
        if (!Net.IsClient) return;
        float t = r.Float();
        int n = r.Byte();
        for (int i = 0; i < n; i++)
        {
            int id = r.UShort();
            Vector3 p = r.Pos();
            float yaw = r.Angle();
            float pitch = r.SByte();
            float speed = r.Byte() / 40f;
            bool hidden = (r.Byte() & 1) != 0;
            float hp = r.Unit();
            if (enemies.TryGetValue(id, out var e) && e != null)
            {
                var ne = e.GetComponent<NetEnemy>();
                if (ne != null) ne.Push(t, p, yaw, pitch, speed, hidden, hp);
            }
        }
    }

    void OnEnemyEvent(int sender, NetRead r)
    {
        if (!Net.IsClient) return;
        int id = r.UShort();
        int ev = r.Byte();
        bool head = r.Bool();
        if (!enemies.TryGetValue(id, out var e) || e == null) return;
        if (ev == 1) { var ne = e.GetComponent<NetEnemy>(); if (ne != null) ne.Attack(); }
        else if (ev == 2) { enemies.Remove(id); Replay(() => e.NetKill(head)); }
    }

    public void ChanclaThrown(Vector3 from, Vector3 target, float speed) =>
        S.ToAll(NetBuf.Make(NetMsg.ChanclaThrow).Vec(from).Vec(target).Float(speed));

    public void EntranceUsed(SpawnEntrance e, bool enter, float seconds)
    {
        int i = entrances.IndexOf(e);
        if (i >= 0) S.ToAll(NetBuf.Make(NetMsg.EntranceUse).UShort(i).Bool(enter).Float(seconds));
    }

    void OnEntranceUse(int sender, NetRead r)
    {
        if (!Net.IsClient) return;
        int i = r.UShort();
        bool enter = r.Bool();
        float seconds = r.Float();
        if (i < 0 || i >= entrances.Count || entrances[i] == null) return;
        if (enter) entrances[i].Enter(seconds);
        else entrances[i].Use();
    }

    // ---------------------------------------------------------------- doors, box, key, records, karaoke, disc
    public void DoorOpened(ZoneDoor d)
    {
        if (!thingIndex.TryGetValue(d, out var i)) return;
        openedDoors.Add(i);
        S.ToAll(NetBuf.Make(NetMsg.DoorOpened).UShort(i));
    }

    void ApplyDoor(int i)
    {
        if (i < 0 || i >= things.Count) return;
        if (!openedDoors.Contains(i)) openedDoors.Add(i);
        var d = things[i] as ZoneDoor;
        if (d != null) Replay(d.NetOpened);
    }

    public int BoxSpotIndex(Transform spot) => spot != null ? boxSpots.IndexOf(spot) : -1;
    public Transform BoxSpot(int i) => i >= 0 && i < boxSpots.Count ? boxSpots[i] : null;

    public void BoxChanged(MysteryBox box, string text)
    {
        if (!thingIndex.TryGetValue(box, out var i)) return;
        var b = NetBuf.Make(NetMsg.BoxState).UShort(i);
        box.WriteNet(b, text);
        S.ToAll(b);
    }

    void ApplyBox(NetRead r)
    {
        int i = r.UShort();
        var box = i < things.Count ? things[i] as MysteryBox : null;
        if (box != null) Replay(() => box.ReadNet(r));
    }

    public void KeyChanged(MasterKey key)
    {
        int holder = key.Holder != null ? IndexOf(key.Holder) : -1;
        S.ToAll(NetBuf.Make(NetMsg.KeyState).SByte(holder).Vec(key.transform.position).Bool(key.Used));
    }

    void ApplyKey(NetRead r)
    {
        int holder = r.SByte();
        Vector3 pos = r.Vec();
        bool used = r.Bool();
        var key = MasterKey.Instance;
        if (key == null) return;
        var p = holder >= 0 ? PlayerAt(holder) : null;
        key.NetApply(p != null ? p.GetComponent<PlayerInteractor>() : null, pos, used);
    }

    public void DiscPicked(DiscPickup d)
    {
        if (!thingIndex.TryGetValue(d, out var i)) return;
        pickedDiscs.Add(i);
        S.ToAll(NetBuf.Make(NetMsg.DiscPicked).UShort(i).Byte((byte)(EasterEgg.Instance != null ? EasterEgg.Instance.DiscsFound : 0)));
    }

    void ApplyDiscPicked(int i, int count)
    {
        if (!pickedDiscs.Contains(i)) pickedDiscs.Add(i);
        if (i >= 0 && i < things.Count && things[i] != null) Destroy(things[i].gameObject);
        if (EasterEgg.Instance != null) EasterEgg.Instance.NetSetDiscs(count);
    }

    public void KaraokeChanged(Karaoke k, bool singing, float duration) =>
        S.ToAll(NetBuf.Make(NetMsg.KaraokeState).UShort(thingIndex.TryGetValue(k, out var i) ? i : 0).Byte((byte)(singing ? 1 : 0)).Float(duration).Vec(Vector3.zero));

    // The Golden Disc appeared (karaoke done).
    public void DiscSpawned(Karaoke k, Carryable item)
    {
        disc = item;
        S.ToAll(NetBuf.Make(NetMsg.KaraokeState).UShort(thingIndex.TryGetValue(k, out var i) ? i : 0).Byte(2).Float(0f).Vec(item.transform.position));
    }

    void ApplyKaraoke(NetRead r)
    {
        int i = r.UShort();
        int state = r.Byte();
        float duration = r.Float();
        Vector3 pos = r.Vec();
        var k = i < things.Count ? things[i] as Karaoke : null;
        if (k == null) k = FindAnyObjectByType<Karaoke>();
        if (k == null) return;
        if (state == 2) { if (disc == null) disc = k.NetDone(pos); }
        else k.NetSinging(state == 1, duration);
    }

    // Host: the Golden Disc was picked up / dropped.
    public void CarryChanged(Carryable item)
    {
        if (item == null || item != disc) return;
        S.ToAll(CarryMsg());
    }

    NetBuf CarryMsg()
    {
        int holder = -1;
        foreach (var p in gm.Players)
        {
            var c = p != null ? p.GetComponent<PlayerCarry>() : null;
            if (c != null && c.Carried == disc && disc != null) holder = IndexOf(p);
        }
        return NetBuf.Make(NetMsg.CarryState).Bool(disc != null).SByte(holder).Vec(disc != null ? disc.transform.position : Vector3.zero);
    }

    void ApplyCarry(NetRead r)
    {
        bool exists = r.Bool();
        int holder = r.SByte();
        Vector3 pos = r.Vec();
        if (!exists || disc == null) return;
        Replay(() =>
        {
            PlayerCarry current = null;
            foreach (var p in gm.Players)
            {
                var c = p != null ? p.GetComponent<PlayerCarry>() : null;
                if (c != null && c.Carried == disc) current = c;
            }
            var target = holder >= 0 ? PlayerAt(holder) : null;
            var targetCarry = target != null ? target.GetComponent<PlayerCarry>() : null;
            if (current != null && current != targetCarry) current.DropAt(pos, false);
            if (targetCarry != null && targetCarry.Carried != disc) targetCarry.Pick(disc);
            if (targetCarry == null && current == null && !disc.IsCarried) disc.OnDropped(pos);
        });
    }

    // ---------------------------------------------------------------- coolers (max ammo)
    public void PowerUpSpawned(AmmoPickup p)
    {
        int id = nextPickupId++;
        p.NetId = id;
        pickups[id] = p;
        S.ToAll(NetBuf.Make(NetMsg.PowerUpSpawn).UShort(id).Vec(p.transform.position).Byte((byte)p.Kind));
    }

    // ---------------------------------------------------------------- boat mission, timed power-ups
    public void EncierroStarted() => S.ToAll(NetBuf.Make(NetMsg.Encierro));

    public void TrapFired(TracaTrap t)
    {
        if (thingIndex.TryGetValue(t, out var i)) S.ToAll(NetBuf.Make(NetMsg.Trap).UShort(i));
    }

    public void QuestChanged()
    {
        var m = BoatMission.Instance;
        if (m == null) return;
        var b = NetBuf.Make(NetMsg.Quest);
        m.WriteNet(b);
        S.ToAll(b);
    }

    public void BuffsChanged()
    {
        var pm = PowerUpManager.Instance;
        if (pm != null) S.ToAll(NetBuf.Make(NetMsg.Buffs).Float(pm.InstaKillLeft).Float(pm.DoublePointsLeft));
    }

    void ApplyBuffs(NetRead r)
    {
        float insta = r.Float(), dbl = r.Float();
        if (PowerUpManager.Instance != null) PowerUpManager.Instance.NetSetBuffs(insta, dbl);
    }

    public void PowerUpTaken(AmmoPickup p)
    {
        pickups.Remove(p.NetId);
        S.ToAll(NetBuf.Make(NetMsg.PowerUpTaken).UShort(p.NetId));
    }

    void OnPowerUpSpawn(int sender, NetRead r)
    {
        if (!Net.IsClient) return;
        int id = r.UShort();
        Vector3 pos = r.Vec();
        var kind = (PowerUpKind)r.Byte();
        var pm = FindAnyObjectByType<PowerUpManager>();
        if (pm == null || pickups.ContainsKey(id)) return;
        var p = pm.NetSpawn(pos, kind);
        if (p != null) { p.NetId = id; pickups[id] = p; }
    }

    // ---------------------------------------------------------------- cards
    public void CardOffered(CardManager cm)
    {
        if (cm.CurrentOffer == null) return;
        var b = NetBuf.Make(NetMsg.CardOffer).SByte(IndexOf(cm.Chooser)).Float(cm.TimeLeft).Byte((byte)cm.CurrentOffer.Length);
        foreach (var c in cm.CurrentOffer) b.Str(c.name);
        S.ToAll(b);
    }

    public void CardSelected(int index) => S.ToAll(NetBuf.Make(NetMsg.CardSelect).Byte((byte)index));
    public void CardChosen(CardData card) => S.ToAll(NetBuf.Make(NetMsg.CardChosen).Str(card != null ? card.name : ""));
    public void SendCardSelect(int index) => S.ToHost(NetBuf.Make(NetMsg.CardSelect).Byte((byte)index));
    public void SendCardPick(int index) => S.ToHost(NetBuf.Make(NetMsg.CardPick).Byte((byte)index));

    void OnCardOffer(int sender, NetRead r)
    {
        if (!Net.IsClient) return;
        int chooser = r.SByte();
        float time = r.Float();
        int n = r.Byte();
        var cards = new List<CardData>();
        for (int i = 0; i < n; i++) { var c = FindAsset<CardData>(r.Str()); if (c != null) cards.Add(c); }
        var p = PlayerAt(chooser);
        if (CardManager.Instance != null && cards.Count > 0)
            CardManager.Instance.NetOffer(cards.ToArray(), p != null ? p.GetComponent<PlayerInteractor>() : null, time);
    }

    void OnCardSelect(int sender, NetRead r)
    {
        int index = r.Byte();
        var cm = CardManager.Instance;
        if (cm == null || cm.CurrentOffer == null) return;
        if (Net.IsHost) { if (IndexOf(cm.Chooser) == sender) cm.Select(index); }
        else cm.NetSelect(index);
    }

    void OnCardPick(int sender, NetRead r)
    {
        if (!Net.IsHost) return;
        int index = r.Byte();
        var cm = CardManager.Instance;
        if (cm == null || cm.CurrentOffer == null || IndexOf(cm.Chooser) != sender) return;
        if (index >= 0 && index < cm.CurrentOffer.Length) cm.Choose(cm.CurrentOffer[index]);
    }

    public void BossDefeated() => S.ToAll(NetBuf.Make(NetMsg.BossDefeated));

    // ---------------------------------------------------------------- start of the match / late arrivals
    // Everyone has loaded the map: the rounds can start (host) and the world is sent to the clients.
    public void OnGo()
    {
        if (Ready) return;
        Ready = true;
        if (Net.IsHost) S.ToAll(WorldMsg());
    }

    // Host: someone finished loading after the start: everything they missed.
    public void LateLoaded(int index)
    {
        S.ToPlayer(index, NetBuf.Make(NetMsg.Go));
        S.ToPlayer(index, WorldMsg());
        foreach (var kv in enemies)
        {
            var e = kv.Value;
            if (e == null || e.IsDead) continue;
            bool boss = e.GetComponent<BossAI>() != null;
            var ai = e.GetComponent<EnemyAI>();
            S.ToPlayer(index, SpawnMsg(e, boss ? 1 : 0, ai != null && ai.Variant != null ? ai.Variant.name : "", e.MaxHealth));
        }
        foreach (var p in gm.Players) { var np = p != null ? p.GetComponent<NetPlayer>() : null; if (np != null && np.IsLocal) np.SendAll(); }
    }

    NetBuf WorldMsg()
    {
        var b = NetBuf.Make(NetMsg.WorldInit).Int(gm.Points).Int(gm.TotalEarned);
        b.Byte((byte)openedDoors.Count);
        foreach (var d in openedDoors) b.UShort(d);
        b.Byte((byte)pickedDiscs.Count);
        foreach (var d in pickedDiscs) b.UShort(d);
        var egg = EasterEgg.Instance;
        b.Byte((byte)(egg != null ? egg.DiscsFound : 0)).Bool(egg != null && egg.Completed);
        var box = things.OfType<MysteryBox>().FirstOrDefault();
        b.Bool(box != null);
        if (box != null) { b.UShort(thingIndex[box]); box.WriteNet(b); }
        var key = MasterKey.Instance;
        b.Bool(key != null);
        if (key != null) b.SByte(key.Holder != null ? IndexOf(key.Holder) : -1).Vec(key.transform.position).Bool(key.Used);
        b.Bool(disc != null);
        if (disc != null) { var c = CarryMsg(); b.Vec(disc.transform.position); b.Int(c.Length); for (int i = 1; i < c.Length; i++) b.Byte(c.Data[i]); }
        var mission = BoatMission.Instance;
        b.Bool(mission != null);
        if (mission != null) mission.WriteNet(b);
        var pum = PowerUpManager.Instance;
        b.Float(pum != null ? pum.InstaKillLeft : 0f).Float(pum != null ? pum.DoublePointsLeft : 0f);
        return b;
    }

    void ApplyWorld(NetRead r)
    {
        int points = r.Int(), earned = r.Int();
        gm.NetSetPoints(points, 0, PointSource.Bonus, earned);
        int n = r.Byte();
        for (int i = 0; i < n; i++) ApplyDoor(r.UShort());
        n = r.Byte();
        var discs = new List<int>();
        for (int i = 0; i < n; i++) discs.Add(r.UShort());
        int found = r.Byte();
        bool completed = r.Bool();
        foreach (var d in discs) ApplyDiscPicked(d, found);
        if (EasterEgg.Instance != null) { EasterEgg.Instance.NetSetDiscs(found); EasterEgg.Instance.Completed = completed; }
        if (r.Bool()) { int i = r.UShort(); var box = i < things.Count ? things[i] as MysteryBox : null; if (box != null) Replay(() => box.ReadNet(r)); }
        if (r.Bool()) ApplyKey(r);
        if (r.Bool())
        {
            Vector3 pos = r.Vec();
            r.Int();
            var k = FindAnyObjectByType<Karaoke>();
            if (disc == null && k != null) disc = k.NetDone(pos);
            ApplyCarry(r);
        }
        if (r.Bool() && BoatMission.Instance != null) BoatMission.Instance.ReadNet(r);
        ApplyBuffs(r);
    }

    // ---------------------------------------------------------------- loop
    void Update()
    {
        if (!Net.IsHost || !Ready) return;
        float now = Time.unscaledTime;
        if (now >= nextEnemySnap) { nextEnemySnap = now + EnemySnapInterval; SendEnemySnap(); }
        if (now >= nextTick) { nextTick = now + TickInterval; SendTick(); }
    }
}
