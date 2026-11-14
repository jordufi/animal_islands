using UnityEngine;

// One of the fake Julio Iglesias records hidden around the island.
public class DiscPickup : Interactable
{
    public override string Prompt(PlayerInteractor player) => "□  Recoger disco de Julio Iglesias (¿original?)";

    public override void Interact(PlayerInteractor player)
    {
        if (!Net.Authority) return;
        EasterEgg.Instance?.AddDisc();
        if (NetGame.H != null) NetGame.H.DiscPicked(this);
        Destroy(gameObject);
    }

    void Update() => transform.Rotate(0f, 120f * Time.deltaTime, 0f, Space.World);
}
