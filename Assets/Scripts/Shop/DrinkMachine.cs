using UnityEngine;

// Cocktail stand: buy the drink once per player (max 4 drinks).
public class DrinkMachine : Interactable
{
    [SerializeField] DrinkData drink;
    [SerializeField] TextMesh worldLabel;

    public DrinkData Drink => drink;
    int Cost => Mathf.RoundToInt(drink.cost * RunModifiers.Get(Stat.DrinkCost) / 10f) * 10;

    void Update()
    {
        if (worldLabel != null) worldLabel.text = drink.displayName + "\n" + Cost + " conchas";
    }

    public override string Prompt(PlayerInteractor player)
    {
        var drinks = player.GetComponent<PlayerDrinks>();
        if (drinks == null) return "";
        if (drinks.Has(drink)) return drink.displayName + " (ya la tienes)";
        if (drinks.IsFull) return "Ya llevas " + PlayerDrinks.MaxDrinks + " bebidas";
        return "□  Beber " + drink.displayName + " (" + CostText(Cost) + "): " + drink.effectDescription;
    }

    public override bool CanInteract(PlayerInteractor player)
    {
        var drinks = player.GetComponent<PlayerDrinks>();
        return drinks != null && !drinks.Has(drink) && !drinks.IsFull;
    }

    public override void Interact(PlayerInteractor player)
    {
        if (!Pay(Cost, player)) return;
        player.GetComponent<PlayerDrinks>().Add(drink);
        Game.ShowMessage(drink.displayName + ": " + drink.effectDescription + (string.IsNullOrEmpty(drink.sideEffectDescription) ? "" : "  (Efecto secundario: " + drink.sideEffectDescription + ")"));
    }
}
