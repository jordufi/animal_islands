using System;
using System.Collections;
using UnityEngine;

// Player hit points with Zombies-style regeneration (heals after a few seconds without damage).
// Solo: at 0 HP the player goes down; with the self-revive drink (Tinto de Verano) they get back up losing
// their drinks, otherwise the run is over.
// Co-op (like Black Ops Zombies): at 0 HP the player is downed: lies on the ground with the pistol (last
// stand), loses every drink, old men ignore them; a teammate holding □ next to them revives them
// (PlayerReviver). After CoopData.bleedOutTime they bleed out (dead) and come back at the next round with
// the pistol only (GameManager). Everyone down or dead = game over.
public class PlayerHealth : MonoBehaviour
{
    [SerializeField] int baseMaxHealth = 100;
    [SerializeField] Transform visualRoot;   // fallback: rotated to lie down if the Animator has no Die state
    [SerializeField] float regenDelay = 4f;
    [SerializeField] float regenPerSecond = 30f;
    [SerializeField] float reviveTime = 2.5f;
    [SerializeField] float flinchAngle = 16f;
    [SerializeField] float flinchTime = 0.25f;

    PlayerStats stats;
    PlayerDrinks drinks;
    PlayerCarry carry;
    PlayerShooter shooter;
    PlayerController controller;
    Animator animator;
    bool animatedDeath;
    float current;
    float lastDamageTime = float.NegativeInfinity;
    float ignoredUntil;
    bool downed;
    bool lastStand;
    float bleedOutAt;
    Quaternion visualStartRotation;
    Transform spine;
    float flinchUntil;
    Coroutine downRoutine;

    public int MaxHealth => netMax > 0 ? netMax : Mathf.RoundToInt(baseMaxHealth * (stats != null ? stats.Get(Stat.MaxHealth) : 1f));
    public float IgnoredUntil => ignoredUntil;

    // Online: a remote player's proxy mirrors its owner (NetPlayer) and sends damage / revives to the owner.
    bool isProxy;
    int netMax;
    public int CurrentHealth => Mathf.CeilToInt(current);
    public int HitsTaken { get; private set; }
    public bool IsDead { get; private set; }
    public bool IsDowned => downed;
    public bool InLastStand => lastStand;   // co-op downed: waiting for a teammate
    public float BleedOutRemaining => lastStand ? Mathf.Max(0f, bleedOutAt - Time.time) : 0f;
    public float ReviveProgress { get; set; }   // 0..1 while a teammate holds □ (PlayerReviver)
    public PlayerHealth BeingRevivedBy { get; set; }
    public bool IgnoredByEnemies => Time.time < ignoredUntil;
    public bool CanBeTargeted => !IsDead && !downed && !IgnoredByEnemies;
    public bool GodMode { get; set; }   // debug menu

    public event Action<int, int> HealthChanged;   // (current, max)
    public event Action<int> Damaged;
    public event Action Downed;    // co-op: last stand starts
    public event Action Died;      // solo death, or co-op bleed-out
    public event Action Revived;   // back up (Tinto, teammate, or respawn next round)

    static GameManager Gm => GameManager.Instance;
    static CoopData Coop => Gm != null ? Gm.Coop : null;
    bool CoopRules => Gm != null && Gm.IsCoop;

    void Awake()
    {
        stats = GetComponent<PlayerStats>();
        drinks = GetComponent<PlayerDrinks>();
        carry = GetComponent<PlayerCarry>();
        shooter = GetComponent<PlayerShooter>();
        controller = GetComponent<PlayerController>();
        animator = GetComponentInChildren<Animator>();
        foreach (var t in GetComponentsInChildren<Transform>()) if (t.name == "Spine") { spine = t; break; }
        if (visualRoot != null) visualStartRotation = visualRoot.localRotation;
        isProxy = PlayerSlot.IsProxy(this);
    }

    void Start()
    {
        if (animator != null)
            foreach (var p in animator.parameters) if (p.name == "Die") animatedDeath = true;
        current = MaxHealth;
        if (stats != null) stats.Changed += OnStatsChanged;
        HealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }

    void OnDestroy()
    {
        if (stats != null) stats.Changed -= OnStatsChanged;
    }

    void OnStatsChanged()
    {
        current = Mathf.Min(current, MaxHealth);
        HealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }

    public void SetIgnored(float seconds) => ignoredUntil = Mathf.Max(ignoredUntil, Time.time + seconds);
    public void ClearIgnored() => ignoredUntil = 0f;

    void Update()
    {
        if (isProxy) return;   // the owner runs regen and bleed-out
        if (lastStand)
        {
            if (GodMode) bleedOutAt = Time.time + 5f;
            if (BeingRevivedBy == null && Time.time >= bleedOutAt) BleedOut();
            return;
        }
        if (IsDead || downed) return;
        float delay = regenDelay * (stats != null ? stats.Get(Stat.RegenDelay) : 1f);
        if (current < MaxHealth && Time.time - lastDamageTime >= delay)
        {
            int before = CurrentHealth;
            current = Mathf.Min(MaxHealth, current + regenPerSecond * Time.deltaTime);
            if (CurrentHealth != before) HealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }
    }

    void LateUpdate()
    {
        // after the Animator: knock the upper body back for a moment when hit
        if (spine == null || Time.time >= flinchUntil || downed || IsDead) return;
        float k = (flinchUntil - Time.time) / flinchTime;
        spine.rotation = Quaternion.AngleAxis(-flinchAngle * k, transform.right) * spine.rotation;
    }

    public void TakeDamage(int amount)
    {
        if (isProxy)
        {
            // online host: an old man hit a remote player: their own machine takes the hit
            if (!IsDead && !downed && amount > 0 && NetGame.H != null) NetGame.H.SendDamage(this, amount);
            return;
        }
        if (IsDead || downed || amount <= 0 || GodMode) return;
        float taken = amount * (stats != null ? stats.Get(Stat.DamageTaken) : 1f);
        current = Mathf.Max(0f, current - taken);
        lastDamageTime = Time.time;
        HitsTaken++;
        if (carry != null) carry.Drop();
        if (drinks != null) drinks.OnDamaged();
        Damaged?.Invoke(Mathf.RoundToInt(taken));
        flinchUntil = Time.time + flinchTime;
        var cam = PlayerSlot.RigOf(this);
        if (cam != null) cam.Shake(0.25f, 0.12f);
        HealthChanged?.Invoke(CurrentHealth, MaxHealth);
        if (current <= 0f) downRoutine = StartCoroutine(CoopRules ? GoDownCoop() : GoDown());
    }

    // ---------------------------------------------------------------- solo
    IEnumerator GoDown()
    {
        downed = true;
        SetControls(false);
        yield return LieDown();

        if (drinks != null && drinks.TryConsumeRevive())
        {
            if (Gm != null) Gm.ShowMessage("¡Tinto de Verano Eterno! Te levantas (y pierdes tus bebidas)");
            yield return new WaitForSeconds(reviveTime);
            drinks.ClearAll();
            current = MaxHealth * 0.5f;
            yield return StandUp();
            downed = false;
            SetIgnored(2.5f);
            SetControls(true);
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
            Revived?.Invoke();
            yield break;
        }

        IsDead = true;
        downed = false;
        Died?.Invoke();   // the GameManager ends the run
    }

    // ---------------------------------------------------------------- co-op: last stand
    IEnumerator GoDownCoop()
    {
        downed = true;
        lastStand = true;
        ReviveProgress = 0f;
        BeingRevivedBy = null;
        bleedOutAt = Time.time + (Coop != null ? Coop.bleedOutTime : 30f);
        if (drinks != null) drinks.ClearAll();   // like Zombies: going down costs every perk
        if (controller != null) { controller.ControlsEnabled = false; controller.StopSprint(); }
        if (shooter != null) { shooter.enabled = true; shooter.BeginLastStand(Coop != null ? Coop.lastStandMagazines : 2); }
        Downed?.Invoke();
        yield return LieDown();
    }

    // A teammate finished holding □ next to us.
    public void Revive()
    {
        if (!lastStand) return;
        if (isProxy) { if (NetGame.Instance != null) NetGame.Instance.ProxyRevived(this); return; }   // their owner gets up
        if (downRoutine != null) StopCoroutine(downRoutine);
        lastStand = false;
        BeingRevivedBy = null;
        ReviveProgress = 0f;
        current = MaxHealth * (Coop != null ? Coop.reviveHealth : 0.5f);
        lastDamageTime = Time.time;
        if (shooter != null) shooter.EndLastStand(true);
        StartCoroutine(GetUp());
    }

    IEnumerator GetUp()
    {
        yield return StandUp();
        downed = false;
        SetIgnored(2f);
        SetControls(true);
        HealthChanged?.Invoke(CurrentHealth, MaxHealth);
        Revived?.Invoke();
    }

    void BleedOut()
    {
        lastStand = false;
        downed = false;
        IsDead = true;
        BeingRevivedBy = null;
        ReviveProgress = 0f;
        if (shooter != null) { shooter.EndLastStand(false); shooter.enabled = false; }
        SetVisible(false);
        // spectate a teammate who is still standing
        var rig = PlayerSlot.RigOf(this);
        var mate = Gm != null ? Gm.NearestStanding(transform.position) : null;
        if (rig != null && mate != null) rig.SetTarget(mate.transform);
        if (Gm != null) Gm.ShowMessage((PlayerSlot.Of(this) != null ? PlayerSlot.Of(this).Tag : "Un jugador") + " se ha desangrado. Volverá la próxima ronda.");
        Died?.Invoke();
    }

    // Back next round (co-op), full health, pistol only, no drinks.
    public void RespawnAt(Vector3 position)
    {
        if (!IsDead) return;
        StopAllCoroutines();
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        transform.position = position + Vector3.up * 0.1f;
        if (cc != null) cc.enabled = true;
        IsDead = false;
        downed = false;
        lastStand = false;
        current = MaxHealth;
        lastDamageTime = float.NegativeInfinity;
        SetVisible(true);
        if (animatedDeath) { animator.Rebind(); animator.Update(0f); }
        else if (visualRoot != null) visualRoot.localRotation = visualStartRotation;
        if (drinks != null) drinks.ClearAll();
        if (shooter != null) { shooter.enabled = true; shooter.ResetToStartingWeapon(); }
        var rig = PlayerSlot.RigOf(this);
        if (rig != null) rig.SetTarget(transform);
        SetIgnored(2f);
        SetControls(true);
        HealthChanged?.Invoke(CurrentHealth, MaxHealth);
        Revived?.Invoke();
    }

    // ---------------------------------------------------------------- online proxy (what the owner sends)
    public void NetSetHealth(int hp, int max)
    {
        if (!isProxy) return;
        netMax = Mathf.Max(1, max);
        if (hp < CurrentHealth && !downed && !IsDead) flinchUntil = Time.time + flinchTime;
        current = hp;
        HealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }

    public void NetApplyStatus(bool isDowned, bool isLastStand, bool dead, float ignoredFor, float bleedRemaining)
    {
        if (!isProxy) return;
        ignoredUntil = ignoredFor > 0f ? Time.time + ignoredFor : 0f;
        if (dead && !IsDead)
        {
            StopAllCoroutines();
            IsDead = true;
            downed = false;
            lastStand = false;
            BeingRevivedBy = null;
            ReviveProgress = 0f;
            SetVisible(false);
            Died?.Invoke();
            return;
        }
        if (!dead && IsDead)   // back next round
        {
            StopAllCoroutines();
            IsDead = false;
            downed = false;
            lastStand = false;
            SetVisible(true);
            if (animatedDeath) { animator.Rebind(); animator.Update(0f); }
            else if (visualRoot != null) visualRoot.localRotation = visualStartRotation;
            Revived?.Invoke();
        }
        if (dead) return;
        if (isDowned && !downed)
        {
            downed = true;
            lastStand = isLastStand;
            bleedOutAt = Time.time + bleedRemaining;
            ReviveProgress = 0f;
            BeingRevivedBy = null;
            StopAllCoroutines();
            StartCoroutine(LieDown());
            Downed?.Invoke();
        }
        else if (!isDowned && downed)
        {
            downed = false;
            lastStand = false;
            BeingRevivedBy = null;
            ReviveProgress = 0f;
            StopAllCoroutines();
            StartCoroutine(StandUp());
            Revived?.Invoke();
        }
        else if (isDowned)
        {
            lastStand = isLastStand;
            bleedOutAt = Time.time + bleedRemaining;
        }
    }

    // Online: this player left the match.
    public void NetGone()
    {
        StopAllCoroutines();
        bool was = IsDead;
        IsDead = true;
        downed = false;
        lastStand = false;
        BeingRevivedBy = null;
        SetVisible(false);
        if (!was) Died?.Invoke();
    }

    // ---------------------------------------------------------------- helpers
    IEnumerator LieDown()
    {
        if (animatedDeath) { animator.SetTrigger("Die"); yield return new WaitForSeconds(0.8f); }
        else if (visualRoot != null) yield return Rotate(visualStartRotation, Quaternion.AngleAxis(-85f, Vector3.right) * visualStartRotation, 0.5f);
    }

    IEnumerator StandUp()
    {
        if (animatedDeath) { animator.SetTrigger("Revive"); yield return new WaitForSeconds(0.35f); }
        else if (visualRoot != null) yield return Rotate(visualRoot.localRotation, visualStartRotation, 0.35f);
    }

    void SetVisible(bool on)
    {
        foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = on;
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.detectCollisions = on;
    }

    void SetControls(bool on)
    {
        if (controller != null) controller.ControlsEnabled = on;
        if (shooter != null) shooter.enabled = on;
    }

    IEnumerator Rotate(Quaternion from, Quaternion to, float duration)
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
        {
            visualRoot.localRotation = Quaternion.Slerp(from, to, t * t);
            yield return null;
        }
        visualRoot.localRotation = to;
    }
}
