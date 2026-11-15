using UnityEngine;

// "El Microondas del Chiringuito" (Pack-a-Punch): upgrades the weapon in hand once
// (more damage, bigger magazine, full ammo, new name and colour).
public class PackAPunch : Interactable
{
    [SerializeField] int cost = 5000;
    [SerializeField] TextMesh worldLabel;

    void Update()
    {
        if (worldLabel != null) worldLabel.text = "EL MICROONDAS\nDEL CHIRINGUITO\n" + (BoatMission.PowerOn ? cost + " conchas" : "SIN LUZ");
    }

    public override string Prompt(PlayerInteractor player)
    {
        var w = player.Shooter != null ? player.Shooter.Current : null;
        if (w == null || w.data == null) return "";
        if (!BoatMission.PowerOn) return "El Microondas no tiene luz (hay que dar la luz)";
        if (w.upgraded) return w.Name + " ya está al punto";
        return "□  Microondas: mejorar " + w.Name + " → " + w.data.upgradedName + " (" + CostText(cost) + ")";
    }

    public override bool CanInteract(PlayerInteractor player)
    {
        var w = player.Shooter != null ? player.Shooter.Current : null;
        return BoatMission.PowerOn && w != null && w.data != null && !w.upgraded;
    }

    public override void Interact(PlayerInteractor player)
    {
        if (!BoatMission.PowerOn || !Pay(cost, player)) return;
        player.Shooter.UpgradeCurrent();
        Game.ShowMessage("¡DING! " + player.Shooter.Current.Name + " recién salida del microondas");
    }
}
