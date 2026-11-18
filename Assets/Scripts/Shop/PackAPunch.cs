using UnityEngine;

// "El Microondas del Chiringuito" (Pack-a-Punch): upgrades the weapon in hand once
// (more damage, bigger magazine, full ammo, new name and colour).
// A class of its own (user feedback 1): better than 3 stars in everything, whatever the stars were; no stars after it.
public class PackAPunch : Interactable
{
    int cost => Game != null && Game.Economy != null ? Game.Economy.packAPunchCost : 5000;   // Data/Economy.asset
    [SerializeField] TextMesh worldLabel;
    [SerializeField] Zone powerZone;   // Villajubilada: works with the light of this zone (null = the map's switch)

    void Update()
    {
        if (worldLabel != null) worldLabel.text = "EL MICROONDAS\nDEL CHIRINGUITO\n" + (BoatMission.PowerOnFor(powerZone) ? cost + " conchas" : "SIN LUZ");
    }

    public override string Prompt(PlayerInteractor player)
    {
        var w = player.Shooter != null ? player.Shooter.Current : null;
        if (w == null || w.data == null) return "";
        if (!BoatMission.PowerOnFor(powerZone)) return "El Microondas no tiene luz (hay que dar la luz)";
        if (w.upgraded) return w.Name + " ya está al punto";
        return "□  Microondas: mejorar " + w.Name + " → " + w.data.upgradedName + " (" + CostText(cost) + ")";
    }

    public override bool CanInteract(PlayerInteractor player)
    {
        var w = player.Shooter != null ? player.Shooter.Current : null;
        return BoatMission.PowerOnFor(powerZone) && w != null && w.data != null && !w.upgraded;
    }

    public override void Interact(PlayerInteractor player)
    {
        if (!BoatMission.PowerOnFor(powerZone) || !Pay(cost, player)) return;
        player.Shooter.UpgradeCurrent();
        Game.ShowMessage("¡DING! " + player.Shooter.Current.Name + " recién salida del microondas");
    }
}
