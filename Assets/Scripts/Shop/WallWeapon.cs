using UnityEngine;

// Weapon hanging on a wall (Black Ops "wall buy"): always there, fixed price. Buy it once, then the same
// spot sells its ammo. Isla del Faro: the Lentos-peta in the Camping, the first zone you open.
public class WallWeapon : Interactable
{
    [SerializeField] WeaponData weapon;
    [SerializeField] TextMesh worldLabel;

    public WeaponData Weapon => weapon;
    public int Cost => weapon.wallCost;

    int AmmoCost(PlayerShooter shooter)
    {
        var slot = shooter.SlotOf(weapon);
        return slot != null && slot.upgraded ? weapon.wallAmmoCost * 2 : weapon.wallAmmoCost;
    }

    void Update()
    {
        if (worldLabel != null) worldLabel.text = weapon.displayName.ToUpperInvariant() + "\n" + weapon.wallCost + " conchas";
    }

    public override string Prompt(PlayerInteractor player)
    {
        var shooter = player.Shooter;
        if (shooter == null) return "";
        var slot = shooter.SlotOf(weapon);
        if (slot == null) return "□  Comprar " + weapon.displayName + " (" + CostText(Cost) + ")";
        if (shooter.IsFull(slot)) return weapon.displayName + ": munición llena";
        return "□  Munición de " + weapon.displayName + " (" + CostText(AmmoCost(shooter)) + ")";
    }

    public override bool CanInteract(PlayerInteractor player)
    {
        var shooter = player.Shooter;
        if (shooter == null) return false;
        var slot = shooter.SlotOf(weapon);
        return slot == null || !shooter.IsFull(slot);
    }

    public override void Interact(PlayerInteractor player)
    {
        var shooter = player.Shooter;
        if (shooter.SlotOf(weapon) == null)
        {
            if (!Pay(Cost, player)) return;
            shooter.GiveWeapon(weapon, 0);   // wall weapons: 0 stars (La Lijadora del Taller sands them up)
            Game.ShowMessage(weapon.reloadOneByOne ? "¡" + weapon.displayName + "! Lenta pero contundente. Mete los cartuchos de uno en uno."
                                                   : "¡" + weapon.displayName + "! Recién descolgada de la pared.");
        }
        else
        {
            if (!Pay(AmmoCost(shooter), player)) return;
            shooter.RefillAmmo(weapon);
            Game.ShowMessage("Munición de " + weapon.displayName + " repuesta.");
        }
    }
}
