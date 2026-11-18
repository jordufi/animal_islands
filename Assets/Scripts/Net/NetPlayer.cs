using System.Collections.Generic;
using UnityEngine;

// Online part of a player. The LOCAL player (the one this machine controls) sends its snapshot 20 times a
// second (position, facing, where it looks / aims, animation, health, who it is reviving), its status
// (downed / bled out / ignored by the old men) and loadout (weapons, drinks) when they change, and its shot /
// ukulele effects. A REMOTE player here is a proxy that just shows what its owner sends.
public class NetPlayer : MonoBehaviour
{
    const float SnapInterval = 0.05f;
    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int GroundedHash = Animator.StringToHash("Grounded");

    public int Index { get; private set; }
    public bool IsLocal { get; private set; }
    public float LastSnapTime { get; private set; } = -100f;   // tests

    PlayerSlot slot;
    PlayerHealth health;
    PlayerShooter shooter;
    PlayerDrinks drinks;
    PlayerMelee melee;
    PlayerInteractor interactor;
    Animator animator;
    readonly NetInterp interp = new NetInterp();
    float nextSnap, loadoutAt = -1f;
    bool statusSent, sDowned, sLast, sDead;
    float sIgnored;
    float targetSpeed;
    bool grounded = true;
    int revivingIndex = -1;

    public void Setup(int index, bool local)
    {
        Index = index;
        IsLocal = local;
    }

    void Awake()
    {
        slot = GetComponent<PlayerSlot>();
        health = GetComponent<PlayerHealth>();
        shooter = GetComponent<PlayerShooter>();
        drinks = GetComponent<PlayerDrinks>();
        melee = GetComponent<PlayerMelee>();
        interactor = GetComponent<PlayerInteractor>();
        animator = GetComponentInChildren<Animator>();
    }

    void Start()
    {
        if (!IsLocal) return;
        shooter.WeaponsChanged += () => loadoutAt = 0f;
        shooter.AmmoChanged += () => { if (loadoutAt < 0f) loadoutAt = Time.unscaledTime + 0.4f; };
        drinks.Changed += () => loadoutAt = 0f;
        shooter.ShotFx += SendShot;
        if (melee != null) melee.Swung += SendSwing;
        var grenade = GetComponent<PlayerGrenade>();
        if (grenade != null) grenade.Thrown += SendThrow;
        loadoutAt = 0f;
    }

    static NetSession S => NetSession.Instance;

    // local player's messages: the host sends them to everyone, a client to the host (who relays them)
    static void Send(NetBuf b, bool reliable)
    {
        if (S == null) return;
        if (S.IsHost) S.ToAll(b, reliable);
        else S.ToHost(b, reliable);
    }

    void Update()
    {
        if (!Net.Online) return;
        if (IsLocal)
        {
            if (Time.unscaledTime >= nextSnap) { nextSnap = Time.unscaledTime + SnapInterval; SendSnap(); }
            CheckStatus();
            if (loadoutAt >= 0f && Time.unscaledTime >= loadoutAt) { loadoutAt = -1f; SendLoadout(); }
        }
        else if (interp.Sample(Time.deltaTime, out var p, out var r))
        {
            transform.SetPositionAndRotation(p, r);
            if (animator != null)
            {
                animator.SetFloat(SpeedHash, targetSpeed, 0.08f, Time.deltaTime);
                animator.SetBool(GroundedHash, grounded);
            }
        }
    }

    // ---------------------------------------------------------------- local: send
    void SendSnap()
    {
        var cam = slot != null ? slot.Camera : null;
        Vector3 look = cam != null ? cam.transform.forward : transform.forward;
        var b = NetBuf.Make(NetMsg.PlayerSnap).Byte((byte)Index).Float(Time.unscaledTime)
            .Vec(transform.position).Angle(transform.eulerAngles.y)
            .Angle(Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg);
        float speed = animator != null ? animator.GetFloat(SpeedHash) : 0f;
        bool ground = animator == null || animator.GetBool(GroundedHash);
        b.Byte((byte)Mathf.Clamp(Mathf.RoundToInt(speed * 30f), 0, 255))
         .Byte((byte)((ground ? 1 : 0) | (shooter.CombatStance ? 2 : 0)))
         .Pos(shooter.AimPoint)
         .UShort(health.CurrentHealth).UShort(health.MaxHealth);
        var target = interactor != null && interactor.Reviving != null ? PlayerSlot.Of(interactor.Reviving) : null;
        b.SByte(target != null ? target.Index : -1).Unit(interactor != null ? interactor.ReviveProgress : 0f);
        Send(b, false);
    }

    void CheckStatus()
    {
        bool d = health.IsDowned, l = health.InLastStand, x = health.IsDead;
        float ign = health.IgnoredUntil;
        if (statusSent && d == sDowned && l == sLast && x == sDead && Mathf.Approximately(ign, sIgnored)) return;
        statusSent = true;
        sDowned = d; sLast = l; sDead = x; sIgnored = ign;
        Send(NetBuf.Make(NetMsg.PlayerStatus).Byte((byte)Index).Bool(d).Bool(l).Bool(x)
            .Float(Mathf.Max(0f, ign - Time.time)).Float(health.BleedOutRemaining), true);
    }

    void SendLoadout()
    {
        var b = NetBuf.Make(NetMsg.PlayerLoadout).Byte((byte)Index).Byte((byte)shooter.CurrentIndex);
        for (int i = 0; i < 2; i++)
        {
            var s = shooter.Slots[i];
            b.Str(s.data != null ? s.data.name : "").Bool(s.upgraded).Byte((byte)s.stars).UShort(s.magazine).UShort(s.reserve);
        }
        b.Byte((byte)drinks.Owned.Count);
        foreach (var d in drinks.Owned) b.Str(d.name);
        Send(b, true);
    }

    void SendShot(Vector3 from, IReadOnlyList<Vector3> ends)
    {
        var b = NetBuf.Make(NetMsg.PlayerFx).Byte((byte)Index).Byte(1).Vec(from).Byte((byte)Mathf.Min(ends.Count, 12));
        for (int i = 0; i < ends.Count && i < 12; i++) b.Vec(ends[i]);
        Send(b, false);
    }

    void SendSwing() => Send(NetBuf.Make(NetMsg.PlayerFx).Byte((byte)Index).Byte(2), true);

    // The false teeth (Dentadura): the others see them fly and bite; the damage is the thrower's hits.
    void SendThrow(Vector3 from, Vector3 velocity) => Send(NetBuf.Make(NetMsg.PlayerFx).Byte((byte)Index).Byte(3).Vec(from).Vec(velocity), true);

    // Tests / late joiners: everything about this player right now.
    public void SendAll()
    {
        statusSent = false;
        loadoutAt = 0f;
    }

    // ---------------------------------------------------------------- remote: apply (index already read)
    public void ApplySnap(NetRead r)
    {
        float t = r.Float();
        Vector3 p = r.Vec();
        float yaw = r.Angle();
        float lookYaw = r.Angle();
        targetSpeed = r.Byte() / 30f;
        int flags = r.Byte();
        Vector3 aim = r.Pos();
        int hp = r.UShort(), max = r.UShort();
        int reviving = r.SByte();
        float progress = r.Unit();

        LastSnapTime = Time.unscaledTime;
        interp.Add(t, p, Quaternion.Euler(0f, yaw, 0f));
        grounded = (flags & 1) != 0;
        shooter.RemoteCombat = (flags & 2) != 0;
        shooter.RemoteAim = aim;
        if (slot != null) slot.RemoteLook = Quaternion.Euler(0f, lookYaw, 0f) * Vector3.forward;
        health.NetSetHealth(hp, max);
        ApplyReviving(reviving, progress);
    }

    // This player holds □ next to a downed teammate: the teammate's owner pauses the bleed-out.
    void ApplyReviving(int index, float progress)
    {
        if (index != revivingIndex && revivingIndex >= 0)
        {
            var old = NetGame.Instance != null ? NetGame.Instance.PlayerAt(revivingIndex) : null;
            if (old != null && old.BeingRevivedBy == health) { old.BeingRevivedBy = null; old.ReviveProgress = 0f; }
        }
        revivingIndex = index;
        if (index < 0) return;
        var target = NetGame.Instance != null ? NetGame.Instance.PlayerAt(index) : null;
        if (target == null || !target.InLastStand) return;
        target.BeingRevivedBy = health;
        target.ReviveProgress = progress;
    }

    public void ApplyStatus(NetRead r)
    {
        bool downed = r.Bool(), lastStand = r.Bool(), dead = r.Bool();
        float ignored = r.Float(), bleed = r.Float();
        health.NetApplyStatus(downed, lastStand, dead, ignored, bleed);
        if (dead) interp.Reset();   // comes back somewhere else next round
    }

    public void ApplyLoadout(NetRead r)
    {
        int current = r.Byte();
        var names = new string[2];
        var up = new bool[2];
        var stars = new int[2];
        var mag = new int[2];
        var res = new int[2];
        for (int i = 0; i < 2; i++) { names[i] = r.Str(); up[i] = r.Bool(); stars[i] = r.Byte(); mag[i] = r.UShort(); res[i] = r.UShort(); }
        var weapons = new WeaponData[2];
        for (int i = 0; i < 2; i++) weapons[i] = NetGame.FindAsset<WeaponData>(names[i]);
        shooter.NetMirror(weapons, up, stars, mag, res, current);
        int n = r.Byte();
        var list = new List<DrinkData>();
        for (int i = 0; i < n; i++) { var d = NetGame.FindAsset<DrinkData>(r.Str()); if (d != null) list.Add(d); }
        drinks.NetMirror(list);
    }

    public void ApplyFx(NetRead r)
    {
        int kind = r.Byte();
        if (kind == 1)
        {
            Vector3 from = r.Vec();
            int n = r.Byte();
            var ends = new List<Vector3>(n);
            for (int i = 0; i < n; i++) ends.Add(r.Vec());
            shooter.PlayShotFx(from, ends);
        }
        else if (kind == 2 && melee != null) melee.PlaySwingFx();
        else if (kind == 3) { Vector3 from = r.Vec(), velocity = r.Vec(); Dentadura.Throw(from, velocity, this, false); }
    }
}
