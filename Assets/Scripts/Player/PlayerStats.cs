using System;
using UnityEngine;

// Per-player stat multipliers (drinks, animal passive, active ability).
// Final value of a stat = run-wide modifier (cards, difficulty) x this player's modifier.
public class PlayerStats : MonoBehaviour
{
    readonly StatBlock local = new StatBlock();
    public event Action Changed;

    public float Get(Stat s) => RunModifiers.Get(s) * local.Get(s);

    public void Apply(StatMod[] mods)
    {
        local.Apply(mods);
        Changed?.Invoke();
    }

    public void Remove(StatMod[] mods)
    {
        local.Remove(mods);
        Changed?.Invoke();
    }

    void OnEnable() => RunModifiers.Changed += RaiseChanged;
    void OnDisable() => RunModifiers.Changed -= RaiseChanged;
    void RaiseChanged() => Changed?.Invoke();
}
