using System;
using System.Collections.Generic;
using UnityEngine;

// The cocktails a player has drunk, and their special effects.
public class PlayerDrinks : MonoBehaviour
{
    public const int MaxDrinks = 4;

    [SerializeField] float stealthSeconds = 4f;
    [SerializeField] float stealthCooldown = 25f;
    [SerializeField] Vector2 hiccupInterval = new Vector2(12f, 30f);

    readonly List<DrinkData> owned = new List<DrinkData>();
    PlayerStats stats;
    PlayerHealth health;
    PlayerController controller;
    float nextStealthTime;
    float nextHiccupTime = float.PositiveInfinity;

    public IReadOnlyList<DrinkData> Owned => owned;
    public event Action Changed;

    void Awake()
    {
        stats = GetComponent<PlayerStats>();
        health = GetComponent<PlayerHealth>();
        controller = GetComponent<PlayerController>();
    }

    public bool Has(DrinkData d) => owned.Contains(d);
    public bool IsFull => owned.Count >= MaxDrinks;

    public void Add(DrinkData d)
    {
        if (d == null || Has(d) || IsFull) return;
        owned.Add(d);
        stats.Apply(d.mods);
        if (HasSpecial(DrinkSpecial.Hiccups)) ScheduleHiccup();
        Changed?.Invoke();
        // online host: a remote player bought it, their own machine drinks it
        if (PlayerSlot.IsProxy(this) && NetGame.H != null) NetGame.H.Forward(this, NetGame.Fwd.AddDrink, d.name);
    }

    // Online proxy: the owner's drinks (no effects here, the owner has them).
    public void NetMirror(List<DrinkData> list)
    {
        owned.Clear();
        owned.AddRange(list);
        Changed?.Invoke();
    }

    public void ClearAll()
    {
        foreach (var d in owned) stats.Remove(d.mods);
        owned.Clear();
        nextHiccupTime = float.PositiveInfinity;
        Changed?.Invoke();
    }

    bool HasSpecial(DrinkSpecial s) => owned.Exists(d => d.special == s || d.sideEffect == s);
    public bool HasSpecialEffect(DrinkSpecial s) => HasSpecial(s);

    public bool TryConsumeRevive()
    {
        var revive = owned.Find(d => d.special == DrinkSpecial.SelfRevive);
        if (revive == null) return false;
        stats.Remove(revive.mods);
        owned.Remove(revive);
        Changed?.Invoke();
        return true;
    }

    public void OnDamaged()
    {
        if (!HasSpecial(DrinkSpecial.Stealth) || Time.time < nextStealthTime) return;
        nextStealthTime = Time.time + stealthCooldown;
        health.SetIgnored(stealthSeconds);
        GameManager.Instance?.ShowMessage("Carajillo del Abuelo: los abuelos te confunden con uno de los suyos");
    }

    void ScheduleHiccup() => nextHiccupTime = Time.time + UnityEngine.Random.Range(hiccupInterval.x, hiccupInterval.y);

    void Update()
    {
        if (Time.time < nextHiccupTime) return;
        ScheduleHiccup();
        if (controller != null && controller.IsGrounded && controller.ControlsEnabled)
        {
            controller.Launch(0.45f);
            GameManager.Instance?.ShowMessage("*hip*");
        }
    }
}
