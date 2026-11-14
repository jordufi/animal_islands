using UnityEngine;

// Co-op: a downed player can be revived by a teammate holding □ next to them (PlayerInteractor does the
// holding). Added to every player by the GameManager in co-op runs.
public class ReviveTarget : Interactable
{
    PlayerHealth health;

    public PlayerHealth Health => health;

    void Awake() => health = GetComponent<PlayerHealth>();

    public override Vector3 InteractPoint => transform.position;

    static string Name(PlayerHealth p)
    {
        var slot = PlayerSlot.Of(p);
        return slot != null ? slot.Tag + " (" + slot.Animal.displayName + ")" : "tu compañero";
    }

    public override string Prompt(PlayerInteractor player)
    {
        if (health == null || !health.InLastStand || player.Health == null || player.Health == health) return "";
        if (player.Health.IsDowned || player.Health.IsDead) return "";
        if (health.BeingRevivedBy != null && health.BeingRevivedBy != player.Health) return Name(health) + ": ya le están levantando";
        return "□  Mantén para levantar a " + Name(health);
    }

    public override bool CanInteract(PlayerInteractor player) =>
        health != null && health.InLastStand && player.Health != null && player.Health != health && !player.Health.IsDowned && !player.Health.IsDead &&
        (health.BeingRevivedBy == null || health.BeingRevivedBy == player.Health);

    // The press starts the hold; PlayerInteractor keeps it going while □ is held.
    public override void Interact(PlayerInteractor player) => player.BeginRevive(this);

    // Seconds the reviver needs (faster with the Tinto de Verano, like Quick Revive).
    public static float TimeFor(PlayerInteractor reviver)
    {
        var coop = GameManager.Instance != null ? GameManager.Instance.Coop : null;
        float normal = coop != null ? coop.reviveTime : 4f, fast = coop != null ? coop.reviveTimeFast : 2f;
        var drinks = reviver.GetComponent<PlayerDrinks>();
        return drinks != null && drinks.HasSpecialEffect(DrinkSpecial.SelfRevive) ? fast : normal;
    }
}
