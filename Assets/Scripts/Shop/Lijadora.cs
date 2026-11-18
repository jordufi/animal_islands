using UnityEngine;

// "La Lijadora del Taller" (user feedback 1, phase 5): +1 star to the weapon in hand for EconomyData.lijadoraCost.
// Up to 3 stars; wall weapons (0 stars) too; not a Microondas weapon (that is a class of its own). One per map, far
// from the Microondas. Works without the light (it is a hand tool). Online: the host runs it with the client's proxy
// and forwards the star to the owner (PlayerShooter.AddStarCurrent -> NetGame.Fwd.Star).
public class Lijadora : Interactable
{
    int Cost => Game != null && Game.Economy != null ? Game.Economy.lijadoraCost : 1000;
    [SerializeField] TextMesh worldLabel;

    void Update()
    {
        if (worldLabel != null) worldLabel.text = "LA LIJADORA\nDEL TALLER\n+1 ★  " + Cost + " conchas";
    }

    public override string Prompt(PlayerInteractor player)
    {
        var shooter = player.Shooter;
        var w = shooter != null ? shooter.Current : null;
        if (w == null || w.data == null) return "";
        if (w.upgraded) return w.Name + " ya ha pasado por el Microondas: la lijadora no le hace nada";
        if (w.stars >= EconomyData.MaxStars) return w.Name + " ya tiene las 3 estrellas";
        return "□  Lijadora: " + w.Name + " → " + w.data.displayName + PlayerShooter.StarText(w.stars + 1) + " (" + CostText(Cost) + ")";
    }

    public override bool CanInteract(PlayerInteractor player) => player.Shooter != null && player.Shooter.CanAddStar(player.Shooter.Current);

    public override void Interact(PlayerInteractor player)
    {
        if (!CanInteract(player) || !Pay(Cost, player)) return;
        player.Shooter.AddStarCurrent();
        Game.ShowMessage("¡Rrrriiii! " + player.Shooter.Current.Name + ": bien lijada");
    }
}
