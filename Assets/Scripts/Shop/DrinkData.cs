using UnityEngine;

public enum DrinkSpecial
{
    None,
    SelfRevive,   // get back up once when going down
    Stealth,      // when hit, enemies ignore you for a few seconds (with cooldown)
    Hiccups,      // side effect: random involuntary hops
}

// A cocktail (perk). Stat mods are applied to the player that drinks it.
[CreateAssetMenu(menuName = "Island/Drink")]
public class DrinkData : ScriptableObject
{
    public string id = "pina";
    public string displayName = "Piña Colapso";
    [TextArea] public string effectDescription;
    [TextArea] public string sideEffectDescription;
    public int cost = 2500;
    public Color color = Color.yellow;
    public StatMod[] mods;
    public DrinkSpecial special;
    public DrinkSpecial sideEffect;
}
