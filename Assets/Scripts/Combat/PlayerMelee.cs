using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Ukulele melee (R3 / V / middle mouse). The ukulele hangs on the back; on a swing it jumps to the
// right hand, the arm swings it down in front of the body (procedural, after the Animator) and the
// closest old man in the arc takes the hit. If an old man is a few metres in front of the camera the
// player lunges at him first. One hit kills in the first rounds (MeleeData.damage).
[DefaultExecutionOrder(50)]   // after PlayerShooter, so the swing overrides the arm aiming
public class PlayerMelee : MonoBehaviour
{
    [SerializeField] InputActionAsset actions;
    [SerializeField] MeleeData data;
    [SerializeField] GameObject backUkulele;
    [SerializeField] GameObject handUkulele;
    [SerializeField] Transform upperArm;
    [SerializeField] Transform lowerArm;
    [SerializeField] Transform hand;

    InputAction meleeAction;
    PlayerController controller;
    PlayerHealth health;
    PlayerCarry carry;
    PlayerStats stats;
    float swingStart = -10f;
    float nextSwing;
    readonly Collider[] overlap = new Collider[32];
    readonly HashSet<EnemyHealth> seen = new HashSet<EnemyHealth>();

    public bool IsSwinging { get; private set; }
    public MeleeData Data => data;
    public event Action<bool> Hit;   // (killed)
    public event Action Swung;       // a swing started (online: the others see it)

    void Awake()
    {
        actions = PlayerSlot.ActionsFor(this, actions);   // co-op: this player's own devices
        meleeAction = actions.FindActionMap("Player", true).FindAction("Melee", true);
        controller = GetComponent<PlayerController>();
        health = GetComponent<PlayerHealth>();
        carry = GetComponent<PlayerCarry>();
        stats = GetComponent<PlayerStats>();
        ShowInHand(false);
    }

    void OnEnable() => meleeAction.Enable();

    void ShowInHand(bool inHand)
    {
        if (backUkulele != null) backUkulele.SetActive(!inHand);
        if (handUkulele != null) handUkulele.SetActive(inHand);
    }

    void Update()
    {
        var gm = GameManager.Instance;
        bool playing = gm == null || gm.IsPlaying;
        if (!playing || data == null || IsSwinging) return;
        if (!meleeAction.WasPressedThisFrame() || Time.time < nextSwing) return;
        if (health != null && (health.IsDead || health.IsDowned)) return;
        if (carry != null && carry.Carried != null) return;   // hands busy with the disc
        if (controller != null && !controller.ControlsEnabled) return;
        StartCoroutine(Swing());
    }

    // Starts a swing from code (tests); same rules as the button.
    public bool TrySwing()
    {
        if (IsSwinging || data == null || Time.time < nextSwing) return false;
        StartCoroutine(Swing());
        return true;
    }

    // Online: a remote player swung (only the animation, the hit is theirs).
    public void PlaySwingFx()
    {
        if (data == null || IsSwinging) return;
        StartCoroutine(SwingFx());
    }

    IEnumerator SwingFx()
    {
        IsSwinging = true;
        swingStart = Time.time;
        ShowInHand(true);
        yield return new WaitForSeconds(data.swingTime);
        ShowInHand(false);
        IsSwinging = false;
    }

    IEnumerator Swing()
    {
        IsSwinging = true;
        swingStart = Time.time;
        nextSwing = Time.time + data.cooldown;
        if (controller != null) controller.StopSprint();
        ShowInHand(true);
        Swung?.Invoke();

        // lunge at an old man a few metres in front of the camera
        var target = FindTarget(data.lungeRange, 40f, CameraForward());
        if (target != null && controller != null)
        {
            Vector3 to = target.transform.position - transform.position;
            to.y = 0f;
            float gap = to.magnitude - data.range * 0.6f;
            if (gap > 0.1f) controller.Dash(to.normalized * data.lungeSpeed, Mathf.Min(data.lungeMaxTime, gap / data.lungeSpeed));
        }

        yield return new WaitForSeconds(data.hitDelay);
        var victim = target != null && !target.IsDead && FlatDistance(target.transform.position) <= data.range + 0.6f
            ? target : FindTarget(data.range, data.arc, transform.forward);
        if (victim != null) Strike(victim);

        yield return new WaitForSeconds(Mathf.Max(0f, data.swingTime - data.hitDelay));
        ShowInHand(false);
        IsSwinging = false;
    }

    void Strike(EnemyHealth enemy)
    {
        float dmg = data.damage * (stats != null ? stats.Get(Stat.PlayerDamage) : 1f);
        if (enemy.GetComponent<BossAI>() != null) dmg *= data.bossDamageFactor;
        // stun + knockback if he survives, melee conchas (online client: the host applies it)
        var info = EnemyHits.Apply(enemy, dmg, false, HitKind.Melee, this, true, data.stun, data.knockback);
        var rig = PlayerSlot.RigOf(this);
        if (rig != null) rig.Shake(0.15f, info.killed ? 0.1f : 0.06f);
        Hit?.Invoke(info.killed);
    }

    Vector3 CameraForward()
    {
        var cam = PlayerSlot.CameraOf(this);
        Vector3 f = cam != null ? cam.transform.forward : transform.forward;
        f.y = 0f;
        return f.sqrMagnitude > 0.001f ? f.normalized : transform.forward;
    }

    float FlatDistance(Vector3 p)
    {
        Vector3 d = p - transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    // Closest living old man within range and within `halfAngle` of `forward`.
    EnemyHealth FindTarget(float range, float halfAngle, Vector3 forward)
    {
        int n = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up * 0.8f, range + 1f, overlap, 1 << 9, QueryTriggerInteraction.Ignore);
        seen.Clear();
        EnemyHealth best = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            var e = overlap[i].GetComponentInParent<EnemyHealth>();
            if (e == null || e.IsDead || !seen.Add(e)) continue;
            Vector3 to = e.transform.position - transform.position;
            to.y = 0f;
            float d = to.magnitude - 0.3f * e.transform.localScale.x;
            if (d > range) continue;
            if (to.sqrMagnitude > 0.04f && Vector3.Angle(forward, to) > halfAngle) continue;
            if (d < bestDist) { bestDist = d; best = e; }
        }
        return best;
    }

    // After the Animator (and the gun arm aiming): swing the right arm from raised behind the shoulder
    // down across the front of the body.
    void LateUpdate()
    {
        if (!IsSwinging || upperArm == null || lowerArm == null || hand == null) return;
        float t = (Time.time - swingStart) / Mathf.Max(0.05f, data.swingTime);
        float weight = Mathf.Clamp01(t / 0.12f) * Mathf.Clamp01((1f - t) / 0.25f);
        Vector3 f = transform.forward, r = transform.right, u = Vector3.up;
        Vector3 shoulder = upperArm.position;
        Vector3 raised = shoulder + u * 0.55f + r * 0.2f - f * 0.25f;
        Vector3 struck = shoulder + f * 0.75f - u * 0.45f - r * 0.3f;
        float s = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.55f, t));
        Vector3 target = shoulder + Vector3.Slerp(raised - shoulder, struck - shoulder, s);
        PlayerShooter.AimBone(upperArm, lowerArm.position, target, weight);
        PlayerShooter.AimBone(lowerArm, hand.position, target, weight);
    }
}
