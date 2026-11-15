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
    }

    // Returns the resulting damage info (killed = this hit was the lethal one).
    public DamageInfo TakeDamage(float amount, bool headshot)
    {
        var info = new DamageInfo { amount = amount, headshot = headshot };
        if (IsDead || amount <= 0f) return info;
        Current -= amount;
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
        info.killed = Current - amount <= 0f;
        Current = Mathf.Max(0f, Current - amount);
        predictedUntil = Time.time + 0.4f;
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
        Current = Mathf.Clamp(current, 0f, MaxHealth);
        if (hit) Damaged?.Invoke(this, new DamageInfo { amount = 0f });
    }

    public void NetKill(bool headshot)
    {
        if (IsDead) return;
        TakeDamage(Current + 1f, headshot);
    }
}
