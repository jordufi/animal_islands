using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Weapons for the third-person player: two slots, ammo, reload, pellets, penetration, headshots
// and Pack-a-Punch upgrades.
//   Fire   - R2 / left mouse        Aim    - L2 / right mouse
//   Switch - Triangle / Q           Reload - Square / F / R (handled by PlayerInteractor when nothing is in range)
// The shot is a raycast from the centre of the camera (the crosshair). The ray starts at the
// player's depth so nothing between the camera and the player can be hit. While aiming or right
// after a shot the body faces the camera direction and the right arm points the gun at the target.
public class PlayerShooter : MonoBehaviour
{
    public class WeaponSlot
    {
        public WeaponData data;
        public bool upgraded;
        public int stars = 1;      // 0 wall weapon, 1-3 box / Lijadora; ignored once upgraded (the Microondas is its own class)
        public int magazine;
        public int reserve;
        public GameObject model;
        public Transform muzzle;

        public string Name => data == null ? "" : upgraded ? data.upgradedName : data.displayName + StarText(stars);
        public string BaseName => data == null ? "" : upgraded ? data.upgradedName : data.displayName;
    }

    // " ★★" (nothing for 0 stars)
    public static string StarText(int stars) => stars <= 0 ? "" : " " + new string('★', Mathf.Min(stars, EconomyData.MaxStars));

    static EconomyData fallbackEconomy;
    public static EconomyData Eco
    {
        get
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.Economy != null) return gm.Economy;
            if (fallbackEconomy == null) fallbackEconomy = ScriptableObject.CreateInstance<EconomyData>();
            return fallbackEconomy;
        }
    }

    // the numbers a slot shoots with: stars, or the Microondas class
    public static float DamageFactor(WeaponSlot s) => s.upgraded ? s.data.upgradeDamageMultiplier : Eco.StarDamage(s.stars);
    public static float FireRateFactor(WeaponSlot s) => s.upgraded ? Eco.microwaveFireRate : Eco.StarFireRate(s.stars);
    public static float ReloadFactor(WeaponSlot s) => s.upgraded ? Eco.microwaveReload : Eco.StarReload(s.stars);
    public static float PierceKeep(WeaponSlot s) => s.upgraded ? Eco.microwavePierceKeep : Eco.StarPierceKeep(s.stars);
    float FireInterval(WeaponSlot s) => s.data.fireInterval / Mathf.Max(0.1f, GetStat(Stat.FireRate) * FireRateFactor(s));

    [SerializeField] InputActionAsset actions;
    [SerializeField] ThirdPersonCamera cameraRig;
    [SerializeField] Transform weaponMount;
    [SerializeField] WeaponData startingWeapon;

    [Header("Shooting")]
    [SerializeField] LayerMask hitLayers = ~0;     // the setup excludes the Player layer
    [SerializeField] float strafeHoldTime = 0.6f;  // keep facing the aim direction this long after a shot

    [Header("Arm aiming (bones of the right arm)")]
    [SerializeField] Transform upperArm;
    [SerializeField] Transform lowerArm;
    [SerializeField] Transform hand;
    [SerializeField] float armBlendSpeed = 10f;

    [Header("Tracer / feel")]
    [SerializeField] float tracerDuration = 0.05f;
    [SerializeField] float kickAngle = 12f;          // weapon kicks up this much per shot (scaled by recoil)
    [SerializeField] float kickRecovery = 12f;
    [SerializeField] float flashDuration = 0.045f;

    public event Action WeaponsChanged;
    public event Action AmmoChanged;
    public event Action<bool, bool> Fired;   // (hit an enemy, killed it)
    public event Action ShellLoaded;         // one-by-one reload: a shell went in (tests, future audio)

    readonly WeaponSlot[] slots = { new WeaponSlot(), new WeaponSlot() };
    int current;
    InputAction fireAction, aimAction, switchAction;
    PlayerController controller;
    PlayerStats stats;
    PlayerCarry carry;
    Camera cam;
    readonly List<LineRenderer> tracers = new List<LineRenderer>();
    float nextFireTime;
    float lastFireTime = float.NegativeInfinity;
    float tracerHideTime;
    float armWeight;
    float reloadEnd = -1f;
    float kick;
    GameObject muzzleFlash;
    float flashHideTime;
    readonly RaycastHit[] hitBuffer = new RaycastHit[32];
    readonly HashSet<EnemyHealth> hitThisPellet = new HashSet<EnemyHealth>();
    readonly HashSet<EnemyHealth> paidThisShot = new HashSet<EnemyHealth>();   // hit points once per enemy per shot (not per pellet)
    PlayerMelee melee;

    // Online. Local player: where it aims (snapshot) and its shots (effects for the others).
    // Remote player's proxy: aims where its owner aims and shows its shots; weapons mirror the owner's.
    public event Action<Vector3, IReadOnlyList<Vector3>> ShotFx;   // (muzzle, end of each pellet)
    public Vector3 AimPoint { get; private set; }
    public bool CombatStance { get; private set; }
    public bool RemoteCombat { get; set; }
    public Vector3 RemoteAim { get; set; }
    bool isProxy;
    readonly List<Vector3> shotEnds = new List<Vector3>();

    public bool IsAiming { get; private set; }
    public bool IsReloading => reloadEnd > 0f;
    public float ReloadProgress { get; private set; }
    public WeaponSlot Current => slots[current];
    public WeaponSlot Other => slots[1 - current];
    public IReadOnlyList<WeaponSlot> Slots => slots;
    public int CurrentIndex => current;
    public bool IsFull(WeaponSlot s) => s.data != null && s.magazine >= MagazineSize(s) && s.reserve >= MaxReserve(s);

    void Awake()
    {
        actions = PlayerSlot.ActionsFor(this, actions);   // co-op: this player's own devices
        var grenade = GetComponent<PlayerGrenade>();     // the false teeth (R1 / G) read the same devices
        if (grenade == null) grenade = gameObject.AddComponent<PlayerGrenade>();
        grenade.Init(actions);
        var map = actions.FindActionMap("Player", true);
        fireAction = map.FindAction("Fire", true);
        aimAction = map.FindAction("Aim", true);
        switchAction = map.FindAction("SwitchWeapon", true);
        controller = GetComponent<PlayerController>();
        stats = GetComponent<PlayerStats>();
        carry = GetComponent<PlayerCarry>();
        melee = GetComponent<PlayerMelee>();
        isProxy = PlayerSlot.IsProxy(this);

        for (int i = 0; i < 8; i++)
        {
            var go = new GameObject("Tracer" + i);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.widthMultiplier = 0.02f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.useWorldSpace = true;
            lr.enabled = false;
            tracers.Add(lr);
        }

        muzzleFlash = new GameObject("MuzzleFlash");
        muzzleFlash.transform.SetParent(transform, false);
        var light = muzzleFlash.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.75f, 0.35f);
        light.range = 4f;
        light.intensity = 3f;
        var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(core.GetComponent<Collider>());
        core.transform.SetParent(muzzleFlash.transform, false);
        core.transform.localScale = Vector3.one * 0.12f;
        var mat = new Material(Shader.Find("Sprites/Default")) { color = new Color(1f, 0.85f, 0.4f, 0.9f) };
        core.GetComponent<Renderer>().sharedMaterial = mat;
        muzzleFlash.SetActive(false);
    }

    void Start()
    {
        if (cameraRig == null) cameraRig = PlayerSlot.RigOf(this);
        cam = cameraRig != null ? cameraRig.GetComponent<Camera>() : isProxy ? null : Camera.main;
        if (startingWeapon != null && Current.data == null) Give(startingWeapon, Eco.startingWeaponStars);
    }

    void OnEnable()
    {
        fireAction.Enable();
        aimAction.Enable();
        switchAction.Enable();
    }

    void OnDisable()
    {
        IsAiming = false;
        if (cameraRig != null) cameraRig.Aiming = false;
        if (controller != null) controller.StrafeActive = false;
        foreach (var t in tracers) if (t != null) t.enabled = false;
    }

    float GetStat(Stat s) => stats != null ? stats.Get(s) : RunModifiers.Get(s);

    // ---------------------------------------------------------------- weapons
    public bool HasWeapon(WeaponData data) => slots[0].data == data || slots[1].data == data;

    // stars: 0 wall weapon, 1-3 from the box; -1 = the default (the starting pistol's stars, 1 for anything else)
    public void GiveWeapon(WeaponData data, int stars = -1)
    {
        if (data == null) return;
        if (stars < 0) stars = data == startingWeapon ? Eco.startingWeaponStars : 1;
        Give(data, stars);
        if (isProxy && NetGame.H != null) NetGame.H.Forward(this, NetGame.Fwd.GiveWeapon, data.name, stars);   // the owner gets it too
    }

    // La Lijadora del Taller: +1 star to the weapon in hand (not a Microondas weapon, max 3)
    public bool CanAddStar(WeaponSlot s) => s != null && s.data != null && !s.upgraded && s.stars < EconomyData.MaxStars;

    public bool AddStarCurrent()
    {
        var slot = Current;
        if (!CanAddStar(slot)) return false;
        slot.stars++;
        WeaponsChanged?.Invoke();
        AmmoChanged?.Invoke();
        if (isProxy && NetGame.H != null) NetGame.H.Forward(this, NetGame.Fwd.Star);
        return true;
    }

    void Give(WeaponData data, int stars)
    {
        int target;
        if (slots[0].data == data) target = 0;
        else if (slots[1].data == data) target = 1;
        else if (slots[0].data == null) target = 0;
        else if (slots[1].data == null) target = 1;
        else target = current;   // both full: replace the weapon in hand

        var slot = slots[target];
        if (slot.data != data) { slot.upgraded = false; slot.stars = stars; }
        else slot.stars = Mathf.Max(slot.stars, stars);   // the same weapon again: keep the better one
        slot.stars = Mathf.Clamp(slot.stars, 0, EconomyData.MaxStars);
        slot.data = data;
        slot.magazine = MagazineSize(slot);
        slot.reserve = MaxReserve(slot);
        SetCurrent(target);
        RebuildModel();
        WeaponsChanged?.Invoke();
        AmmoChanged?.Invoke();
    }

    public bool UpgradeCurrent()
    {
        var slot = Current;
        if (slot.data == null || slot.upgraded) return false;
        slot.upgraded = true;
        slot.magazine = MagazineSize(slot);
        slot.reserve = MaxReserve(slot);
        RebuildModel();
        WeaponsChanged?.Invoke();
        AmmoChanged?.Invoke();
        if (isProxy && NetGame.H != null) NetGame.H.Forward(this, NetGame.Fwd.Upgrade);
        return true;
    }

    // Online proxy: the owner's weapons (models follow the weapon in hand).
    public void NetMirror(WeaponData[] weapons, bool[] upgraded, int[] stars, int[] magazines, int[] reserves, int currentIndex)
    {
        if (!isProxy) return;
        bool rebuild = currentIndex != current;
        for (int i = 0; i < 2; i++)
        {
            var s = slots[i];
            if (s.data != weapons[i] || s.upgraded != upgraded[i] || s.stars != stars[i]) rebuild = true;
            if (s.data != weapons[i] && s.model != null) { Destroy(s.model); s.model = null; s.muzzle = null; }
            s.data = weapons[i];
            s.upgraded = upgraded[i];
            s.stars = stars[i];
            s.magazine = magazines[i];
            s.reserve = reserves[i];
        }
        current = Mathf.Clamp(currentIndex, 0, 1);
        if (rebuild) { RebuildModel(); WeaponsChanged?.Invoke(); }
        AmmoChanged?.Invoke();
    }

    // Online proxy: its owner fired.
    public void PlayShotFx(Vector3 from, IReadOnlyList<Vector3> ends)
    {
        lastFireTime = Time.time;
        var slot = Current;
        Color color = slot.data == null ? Color.yellow : slot.upgraded ? new Color(1f, 0.45f, 0.1f) : slot.data.tracerColor;
        if (slot.muzzle != null) from = slot.muzzle.position;
        for (int i = 0; i < tracers.Count; i++)
        {
            var lr = tracers[i];
            if (i >= ends.Count) { lr.enabled = false; continue; }
            lr.startColor = lr.endColor = color;
            lr.SetPosition(0, from);
            lr.SetPosition(1, ends[i]);
            lr.enabled = true;
        }
        tracerHideTime = Time.time + tracerDuration;
        kick = Mathf.Clamp01(kick + 0.5f);
        muzzleFlash.transform.position = from;
        muzzleFlash.SetActive(true);
        flashHideTime = Time.time + flashDuration;
    }

    // ---------------------------------------------------------------- co-op last stand
    // Downed: the pistol only (the weapons are kept aside); revived: the weapons come back. Bled out:
    // everything is lost (back next round with the pistol, ResetToStartingWeapon).
    WeaponSlot[] saved;
    int savedCurrent;
    public bool InLastStand => saved != null;
    public WeaponData StartingWeapon => startingWeapon;

    public void BeginLastStand(int spareMagazines)
    {
        if (saved != null || startingWeapon == null) return;
        ClearModels();
        saved = new[] { slots[0], slots[1] };
        savedCurrent = current;
        slots[0] = new WeaponSlot { data = startingWeapon };
        slots[0].magazine = MagazineSize(slots[0]);
        slots[0].reserve = slots[0].magazine * Mathf.Max(0, spareMagazines);
        slots[1] = new WeaponSlot();
        SetCurrent(0);
        RebuildModel();
        WeaponsChanged?.Invoke();
        AmmoChanged?.Invoke();
    }

    public void EndLastStand(bool restore)
    {
        if (saved == null) return;
        ClearModels();
        if (restore)
        {
            slots[0] = saved[0];
            slots[1] = saved[1];
            SetCurrent(savedCurrent);
        }
        saved = null;
        if (!restore) { ResetToStartingWeapon(); return; }
        RebuildModel();
        WeaponsChanged?.Invoke();
        AmmoChanged?.Invoke();
    }

    // Back from bleeding out: only the starting pistol, full ammo.
    public void ResetToStartingWeapon()
    {
        ClearModels();
        saved = null;
        slots[0] = new WeaponSlot { data = startingWeapon };
        slots[0].magazine = MagazineSize(slots[0]);
        slots[0].reserve = MaxReserve(slots[0]);
        slots[1] = new WeaponSlot();
        SetCurrent(0);
        RebuildModel();
        WeaponsChanged?.Invoke();
        AmmoChanged?.Invoke();
    }

    void ClearModels()
    {
        foreach (var s in slots)
            if (s.model != null) { Destroy(s.model); s.model = null; s.muzzle = null; }
    }

    public void RefillAllAmmo()
    {
        foreach (var s in slots)
            if (s.data != null) s.reserve = MaxReserve(s);
        var grenade = GetComponent<PlayerGrenade>();
        if (grenade != null) grenade.Refill();   // the Nevera also brings the false teeth back
        AmmoChanged?.Invoke();
        if (isProxy && NetGame.H != null) NetGame.H.Forward(this, NetGame.Fwd.RefillAll);
    }

    // Wall buy ammo: refills the slot holding `data` (magazine + reserve). False if the player lacks it.
    public bool RefillAmmo(WeaponData data)
    {
        foreach (var s in slots)
        {
            if (s.data != data) continue;
            s.magazine = MagazineSize(s);
            s.reserve = MaxReserve(s);
            AmmoChanged?.Invoke();
            if (isProxy && NetGame.H != null) NetGame.H.Forward(this, NetGame.Fwd.RefillAmmo, data.name);
            return true;
        }
        return false;
    }

    public WeaponSlot SlotOf(WeaponData data) => slots[0].data == data ? slots[0] : slots[1].data == data ? slots[1] : null;

    public int MagazineSize(WeaponSlot s) => s.data == null ? 0 :
        Mathf.Max(1, Mathf.CeilToInt(s.data.magazineSize * (s.upgraded ? s.data.upgradeMagazineMultiplier : 1f) * GetStat(Stat.MagazineSize)));

    int MaxReserve(WeaponSlot s) => s.data == null ? 0 : Mathf.CeilToInt(s.data.maxReserve * (s.upgraded ? s.data.upgradeMagazineMultiplier : 1f));

    void SetCurrent(int index)
    {
        current = index;
        CancelReload();
    }

    void RebuildModel()
    {
        foreach (var s in slots)
        {
            if (s.model != null && (s != Current || s.data == null)) { Destroy(s.model); s.model = null; s.muzzle = null; }
        }
        var slot = Current;
        if (slot.data == null || slot.data.model == null || weaponMount == null) return;
        if (slot.model != null) Destroy(slot.model);
        slot.model = Instantiate(slot.data.model, weaponMount, false);
        slot.model.transform.localPosition = Vector3.zero;
        slot.model.transform.localRotation = Quaternion.identity;
        foreach (var t in slot.model.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = gameObject.layer;
            if (t.name == "Muzzle") slot.muzzle = t;
        }
        foreach (var c in slot.model.GetComponentsInChildren<Collider>()) Destroy(c);
        if (slot.upgraded)
        {
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", new Color(1f, 0.55f, 0.15f));
            block.SetColor("_Color", new Color(1f, 0.55f, 0.15f));
            foreach (var r in slot.model.GetComponentsInChildren<Renderer>()) r.SetPropertyBlock(block);
        }
    }

    void SwitchWeapon()
    {
        if (Other.data == null) return;
        SetCurrent(1 - current);
        RebuildModel();
        WeaponsChanged?.Invoke();
        AmmoChanged?.Invoke();
    }

    // One shot right now if the weapon is ready (tests / bots).
    public bool TestFire()
    {
        var s = Current;
        if (s.data == null || s.magazine <= 0 || IsReloading || Time.time < nextFireTime) return false;
        nextFireTime = Time.time + FireInterval(s);
        Shoot();
        return true;
    }

    public void Reload()
    {
        var s = Current;
        if (s.data == null || IsReloading || s.reserve <= 0 || s.magazine >= MagazineSize(s)) return;
        reloadEnd = Time.time + ReloadStepTime(s, true);
    }

    // seconds until the reload (or, one-by-one, the next shell) is done
    float ReloadStepTime(WeaponSlot s, bool first)
    {
        float speed = Mathf.Max(0.1f, GetStat(Stat.ReloadSpeed) * ReloadFactor(s));
        if (!s.data.reloadOneByOne) return s.data.reloadTime / speed;
        return ((first ? s.data.reloadStartTime : 0f) + s.data.shellLoadTime) / speed;
    }

    void CancelReload()
    {
        reloadEnd = -1f;
        ReloadProgress = 0f;
    }

    // ---------------------------------------------------------------- update
    void Update()
    {
        var gm = GameManager.Instance;
        bool playing = gm == null || gm.IsPlaying;
        bool carrying = carry != null && carry.Carried != null;
        bool swinging = melee != null && melee.IsSwinging;
        if (Current.model != null) Current.model.SetActive(!carrying && !swinging);
        if (isProxy)
        {
            // online: its owner shoots; here only the effects fade
            if (Time.time >= tracerHideTime) foreach (var t in tracers) t.enabled = false;
            if (muzzleFlash.activeSelf && Time.time >= flashHideTime) muzzleFlash.SetActive(false);
            kick = Mathf.MoveTowards(kick, 0f, kickRecovery * Time.deltaTime);
            return;
        }

        IsAiming = playing && !carrying && !swinging && aimAction.IsPressed();
        if (cameraRig != null)
        {
            cameraRig.Aiming = IsAiming;
            cameraRig.AimFieldOfViewOverride = Current.data != null ? Current.data.aimFieldOfView : 0f;
        }
        if (!playing) return;

        if (IsReloading)
        {
            var s = Current;
            if (s.data.reloadOneByOne)
            {
                // one shell per step; the bar shows the whole reload (shells loaded / shells needed)
                float step = ReloadStepTime(s, false);
                int size = MagazineSize(s);
                int missing = Mathf.Max(1, Mathf.Min(size - s.magazine, s.reserve));
                ReloadProgress = Mathf.Clamp01((1f - (reloadEnd - Time.time) / step) / missing);
                if (Time.time >= reloadEnd)
                {
                    s.magazine++;
                    s.reserve--;
                    ShellLoaded?.Invoke();
                    if (s.magazine < size && s.reserve > 0) reloadEnd = Time.time + step;
                    else CancelReload();
                    AmmoChanged?.Invoke();
                }
            }
            else
            {
                float total = ReloadStepTime(s, true);
                ReloadProgress = 1f - (reloadEnd - Time.time) / total;
                if (Time.time >= reloadEnd)
                {
                    int need = MagazineSize(s) - s.magazine;
                    int take = Mathf.Min(need, s.reserve);
                    s.magazine += take;
                    s.reserve -= take;
                    CancelReload();
                    AmmoChanged?.Invoke();
                }
            }
        }

        if (!carrying && !swinging)
        {
            if (switchAction.WasPressedThisFrame()) SwitchWeapon();

            var data = Current.data;
            if (data != null)
            {
                bool wantsFire = data.automatic ? fireAction.IsPressed() : fireAction.WasPressedThisFrame();
                // shotgun style: shooting with shells in the tube stops the reload
                if (wantsFire && IsReloading && data.reloadOneByOne && Current.magazine > 0) CancelReload();
                if (wantsFire && Time.time >= nextFireTime && !IsReloading)
                {
                    if (Current.magazine > 0)
                    {
                        nextFireTime = Time.time + FireInterval(Current);
                        Shoot();
                    }
                    else Reload();
                }
            }
        }

        bool combatStance = IsAiming || swinging || Time.time - lastFireTime < strafeHoldTime;
        if (controller != null)
        {
            controller.StrafeActive = combatStance;
            if (cameraRig != null) controller.StrafeYaw = cameraRig.Yaw;
        }

        if (Time.time >= tracerHideTime) foreach (var t in tracers) t.enabled = false;
        if (muzzleFlash.activeSelf && Time.time >= flashHideTime) muzzleFlash.SetActive(false);
        kick = Mathf.MoveTowards(kick, 0f, kickRecovery * Time.deltaTime);
    }

    void LateUpdate()
    {
        // runs after the Animator: bend the right arm so the gun points at the crosshair
        bool carrying = carry != null && carry.Carried != null;
        bool swinging = melee != null && melee.IsSwinging;
        bool combatStance = !carrying && !swinging && (isProxy ? RemoteCombat || Time.time - lastFireTime < strafeHoldTime : IsAiming || Time.time - lastFireTime < strafeHoldTime);
        CombatStance = combatStance;
        if (!isProxy && cam != null && Net.Online) AimPoint = GetAimPoint(CrosshairRay(), out _);   // sent to the others
        armWeight = Mathf.MoveTowards(armWeight, combatStance ? 1f : 0f, armBlendSpeed * Time.deltaTime);
        if (armWeight <= 0f || upperArm == null || lowerArm == null || hand == null || (cam == null && !isProxy))
        {
            ApplyKick();
            return;
        }

        Vector3 target = isProxy ? RemoteAim : Net.Online ? AimPoint : GetAimPoint(CrosshairRay(), out _);
        AimBone(upperArm, lowerArm.position, target, armWeight);
        AimBone(lowerArm, hand.position, target, armWeight);
        if (weaponMount != null)
        {
            Quaternion fix = Quaternion.FromToRotation(weaponMount.forward, target - weaponMount.position);
            hand.rotation = Quaternion.Slerp(hand.rotation, fix * hand.rotation, armWeight);
        }
        ApplyKick();
    }

    // weapon kicks up after each shot (around the hand), then settles
    void ApplyKick()
    {
        if (kick <= 0f || hand == null || weaponMount == null) return;
        hand.rotation = Quaternion.AngleAxis(-kickAngle * kick, weaponMount.right) * hand.rotation;
    }

    internal static void AimBone(Transform bone, Vector3 childPosition, Vector3 target, float weight)
    {
        Vector3 current = childPosition - bone.position;
        Vector3 wanted = target - bone.position;
        if (current.sqrMagnitude < 1e-6f || wanted.sqrMagnitude < 1e-6f) return;
        Quaternion aimed = Quaternion.FromToRotation(current, wanted) * bone.rotation;
        bone.rotation = Quaternion.Slerp(bone.rotation, aimed, weight);
    }

    Ray CrosshairRay()
    {
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        // start the ray level with the player so walls or props behind the character are ignored
        float skip = Vector3.Dot(transform.position + Vector3.up * 0.8f - ray.origin, ray.direction);
        if (skip > 0f) ray.origin += ray.direction * skip;
        return ray;
    }

    Vector3 GetAimPoint(Ray ray, out RaycastHit hit)
    {
        float range = Current.data != null ? Current.data.range : 100f;
        if (Physics.Raycast(ray, out hit, range, hitLayers, QueryTriggerInteraction.Ignore)) return hit.point;
        return ray.origin + ray.direction * range;
    }

    // ---------------------------------------------------------------- shooting
    void Shoot()
    {
        var slot = Current;
        var data = slot.data;
        slot.magazine--;
        lastFireTime = Time.time;

        Ray center = CrosshairRay();
        float spread = IsAiming ? data.aimSpread : data.hipSpread;
        float damage = data.damage * DamageFactor(slot) * GetStat(Stat.PlayerDamage);
        float keep = PierceKeep(slot);   // each old man the bullet goes through leaves it weaker (never stronger)
        float headMult = data.headshotMultiplier * GetStat(Stat.HeadshotDamage);
        Vector3 from = slot.muzzle != null ? slot.muzzle.position : transform.position + Vector3.up;

        bool anyHit = false, anyKill = false;
        paidThisShot.Clear();
        shotEnds.Clear();
        for (int p = 0; p < Mathf.Max(1, data.pellets); p++)
        {
            Vector3 dir = Quaternion.AngleAxis(UnityEngine.Random.Range(0f, 360f), center.direction) *
                          Quaternion.AngleAxis(UnityEngine.Random.Range(0f, spread), Vector3.Cross(center.direction, Vector3.up).normalized) * center.direction;
            if (float.IsNaN(dir.x)) dir = center.direction;
            Vector3 end = center.origin + dir * data.range;

            int count = Physics.RaycastNonAlloc(center.origin, dir, hitBuffer, data.range, hitLayers, QueryTriggerInteraction.Ignore);
            Array.Sort(hitBuffer, 0, count, HitDistanceComparer.Instance);
            hitThisPellet.Clear();
            int pierced = 0;
            for (int i = 0; i < count; i++)
            {
                var h = hitBuffer[i];
                var enemy = h.collider.GetComponentInParent<EnemyHealth>();
                if (enemy == null) { end = h.point; break; }   // world geometry stops the bullet
                if (enemy.IsDead || !hitThisPellet.Add(enemy)) continue;

                bool head = h.collider.GetComponent<HitZone>() is HitZone z && z.isHead;
                float falloff = h.distance <= data.falloffStart ? 1f :
                    Mathf.Lerp(1f, data.minDamageFactor, Mathf.InverseLerp(data.falloffStart, data.range, h.distance));
                // points: kill / headshot, or the hit once per enemy per shot (online client: the host pays)
                bool pay = !paidThisShot.Contains(enemy);
                float pierce = pierced == 0 ? 1f : Mathf.Pow(Mathf.Clamp01(keep), pierced);
                var info = EnemyHits.Apply(enemy, damage * falloff * pierce * (head ? headMult : 1f), head, HitKind.Gun, this, pay);
                if (!info.killed && pay) paidThisShot.Add(enemy);
                anyHit = true;
                if (info.killed) anyKill = true;
                end = h.point;
                if (++pierced >= data.penetration) break;
            }

            if (p < tracers.Count)
            {
                var lr = tracers[p];
                lr.startColor = lr.endColor = slot.upgraded ? new Color(1f, 0.45f, 0.1f) : data.tracerColor;
                lr.SetPosition(0, from);
                lr.SetPosition(1, end);
                lr.enabled = true;
            }
            shotEnds.Add(end);
        }
        tracerHideTime = Time.time + tracerDuration;
        ShotFx?.Invoke(from, shotEnds);

        float recoil = Mathf.Max(0f, data.recoil) * (IsAiming ? 0.7f : 1f);
        if (cameraRig != null) cameraRig.AddRecoil(recoil, UnityEngine.Random.Range(-0.35f, 0.35f) * recoil);
        kick = Mathf.Clamp01(kick + Mathf.Clamp(recoil / 3f, 0.25f, 1f));
        muzzleFlash.transform.position = from;
        muzzleFlash.SetActive(true);
        flashHideTime = Time.time + flashDuration;
        AmmoChanged?.Invoke();
        Fired?.Invoke(anyHit, anyKill);
        if (slot.magazine <= 0) Reload();
    }

    class HitDistanceComparer : IComparer<RaycastHit>
    {
        public static readonly HitDistanceComparer Instance = new HitDistanceComparer();
        public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
    }
}
