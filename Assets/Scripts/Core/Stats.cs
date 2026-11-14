using System;
using UnityEngine;

// Every tunable gameplay number that cards, drinks, animals or difficulty can change.
// All are multipliers with a neutral value of 1.
public enum Stat
{
    PlayerDamage,
    HeadshotDamage,
    FireRate,
    ReloadSpeed,
    MagazineSize,
    MoveSpeed,
    MaxHealth,
    DamageTaken,
    RegenDelay,
    EnemyHealth,
    EnemySpeed,
    EnemyCount,
    EnemyDamage,
    PointsGain,
    DoorCost,
    BoxCost,
    DrinkCost,
    AmmoDropChance,
}

[Serializable]
public struct StatMod
{
    public Stat stat;
    public float multiplier;

    public StatMod(Stat stat, float multiplier)
    {
        this.stat = stat;
        this.multiplier = multiplier;
    }
}

public class StatBlock
{
    readonly float[] values = new float[Enum.GetValues(typeof(Stat)).Length];

    public StatBlock() => Reset();

    public float Get(Stat s) => values[(int)s];
    public void Multiply(Stat s, float m) => values[(int)s] *= m;

    public void Apply(StatMod[] mods)
    {
        if (mods == null) return;
        foreach (var m in mods) Multiply(m.stat, m.multiplier);
    }

    public void Remove(StatMod[] mods)
    {
        if (mods == null) return;
        foreach (var m in mods) if (m.multiplier != 0f) values[(int)m.stat] /= m.multiplier;
    }

    public void Reset()
    {
        for (int i = 0; i < values.Length; i++) values[i] = 1f;
    }
}

// Modifiers that affect the whole run (difficulty + blessing/curse cards).
// Per-player modifiers (drinks, animal passives) live in PlayerStats.
public static class RunModifiers
{
    static readonly StatBlock block = new StatBlock();
    public static event Action Changed;

    public static float Get(Stat s) => block.Get(s);

    public static void Apply(StatMod[] mods)
    {
        block.Apply(mods);
        Changed?.Invoke();
    }

    public static void Multiply(Stat s, float m)
    {
        block.Multiply(s, m);
        Changed?.Invoke();
    }

    public static void Reset()
    {
        block.Reset();
        Changed?.Invoke();
    }
}
