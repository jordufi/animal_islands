using System;
using UnityEngine;

public struct DamageInfo
{
    public float amount;
    public bool headshot;
    public bool killed;
}

// Enemy hit points. Max health is set by the RoundManager when it spawns the enemy
// (it grows every round). Hits on a HitZone marked as head count as headshots.
public class EnemyHealth : MonoBehaviour
{
    [SerializeField] float defaultHealth = 150f;

    public float MaxHealth { get; private set; }
    public float Current { get; private set; }
    public bool IsDead { get; private set; }
    public float PointsMultiplier { get; set; } = 1f;
    public bool PaysHits { get; set; } = true;   // false: only the kill pays (the boss's neighbours)
    public int NetId { get; set; }   // online: the same old man on every machine

    // Carnival mask: the top `maskPool` of the health bar. While it lasts, a hit takes exactly one of its `maskHits`
    // segments (whatever the weapon) and never counts as a headshot. Derived from Current, so it needs no network message.
    public bool Masked => maskPool > 0f && !IsDead && Current > MaxHealth - maskPool + 0.01f;
    public event Action<EnemyHealth> MaskBroken;
    float maskPool;
    int maskHits;

    public void SetMask(int hits, float share)
    {
        maskHits = Mathf.Max(0, hits);
        maskPool = maskHits > 0 ? MaxHealth * Mathf.Clamp(share, 0.05f, 0.9f) : 0f;
    }

    // What a hit really takes off: one mask segment while masked (pierce = insta-kill / nuke / host's kill order).
    float Shielded(ref float amount, ref bool headshot, bool pierce)
    {
        if (pierce || !Masked) return amount;
        headshot = false;
        float left = Current - (MaxHealth - maskPool);
        amount = Mathf.Min(maskPool / maskHits, left);
        if (left - amount < 0.02f) amount = left;   // the last segment takes the rounding with it
        return amount;
    }

    public event Action<EnemyHealth, DamageInfo> Damaged;
    public event Action<EnemyHealth, DamageInfo> Died;
    public static event Action<EnemyHealth, DamageInfo> AnyEnemyDied;

    void Awake()
    {
        MaxHealth = Current = defaultHealth;
    }

    public void Init(float maxHealth)
    {
        MaxHealth = Current = Mathf.Max(1f, maxHealth);
        IsDead = false;
        maskPool = 0f; maskHits = 0;
    }

    // Returns the resulting damage info (killed = this hit was the lethal one).
    public DamageInfo TakeDamage(float amount, bool headshot, bool pierce = false)
    {
        var info = new DamageInfo { amount = amount, headshot = headshot };
        if (IsDead || amount <= 0f) return info;
        bool wasMasked = Masked;
        info.amount = amount = Shielded(ref amount, ref headshot, pierce);
        info.headshot = headshot;
        Current -= amount;
        if (wasMasked && !Masked) MaskBroken?.Invoke(this);
        if (Current <= 0f)
        {
            Current = 0f;
            IsDead = true;
            info.killed = true;
        }
        Damaged?.Invoke(this, info);
        if (info.killed)
        {
            Died?.Invoke(this, info);
            AnyEnemyDied?.Invoke(this, info);
        }
        return info;
    }

    // ---------------------------------------------------------------- online client (puppet)
    // Our own hit, before the host confirms it: the bar drops and he flinches; only the host kills him.
    public DamageInfo Predict(float amount, bool headshot)
    {
        var info = new DamageInfo { amount = amount, headshot = headshot };
        if (IsDead || amount <= 0f) return info;
        bool wasMasked = Masked;
        info.amount = amount = Shielded(ref amount, ref headshot, false);
        info.headshot = headshot;
        info.killed = Current - amount <= 0f;
        Current = Mathf.Max(0f, Current - amount);
        predictedUntil = Time.time + 0.4f;
        if (wasMasked && !Masked) MaskBroken?.Invoke(this);
        Damaged?.Invoke(this, info);
        return info;
    }

    float predictedUntil;

    // Health from the host's snapshots (a drop = someone else hit him: flinch).
    public void NetSync(float current)
    {
        if (IsDead) return;
        if (Time.time < predictedUntil && current > Current) return;   // our hit is still on its way to the host
        bool hit = current < Current - 0.5f;
        bool wasMasked = Masked;
        Current = Mathf.Clamp(current, 0f, MaxHealth);
        if (wasMasked && !Masked) MaskBroken?.Invoke(this);
        if (hit) Damaged?.Invoke(this, new DamageInfo { amount = 0f });
    }

    public void NetKill(bool headshot)
    {
        if (IsDead) return;
        TakeDamage(Current + 1f, headshot, true);
    }
}
