using UnityEngine;
using UnityEngine.AI;

// A quest item you can pick up and carry to the extraction for a score multiplier.
public class Carryable : Interactable
{
    [SerializeField] string displayName = "Disco de Oro";
    [SerializeField] float scoreMultiplier = 2f;

    public string DisplayName => displayName;
    public float ScoreMultiplier => scoreMultiplier;
    public bool IsCarried { get; private set; }

    public override string Prompt(PlayerInteractor player)
    {
        if (IsCarried || player.Carry == null || player.Carry.Carried != null) return "";
        return "□  Coger " + displayName + " (×" + scoreMultiplier + " puntos si lo sacas de la isla; no podrás disparar)";
    }

    public override void Interact(PlayerInteractor player)
    {
        if (Net.Authority) player.Carry.Pick(this);
    }

    public void OnPicked(Transform carrier, Vector3 offset)
    {
        IsCarried = true;
        transform.SetParent(carrier, false);
        transform.localPosition = offset;
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
    }

    public void OnDropped(Vector3 position)
    {
        IsCarried = false;
        transform.SetParent(null, true);
        if (NavAgents.Sample(position, 3f, out var hit)) position = hit;
        transform.position = position + Vector3.up * 0.4f;
        transform.rotation = Quaternion.identity;
    }

    void Update()
    {
        if (!IsCarried) transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
    }
}
