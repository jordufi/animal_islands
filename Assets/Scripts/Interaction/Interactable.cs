using System.Collections.Generic;
using UnityEngine;

// Something the player can use with the Use button (Square / F): doors, box, drinks, Pack-a-Punch...
public abstract class Interactable : MonoBehaviour
{
    public static readonly List<Interactable> All = new List<Interactable>();

    [SerializeField] float interactRadius = 2.2f;

    public float Radius => interactRadius;
    public virtual Vector3 InteractPoint => transform.position;

    protected virtual void OnEnable() => All.Add(this);
    protected virtual void OnDisable() => All.Remove(this);

    // Text shown in the HUD while the player is in range ("" hides the prompt).
    public abstract string Prompt(PlayerInteractor player);
    public virtual bool CanInteract(PlayerInteractor player) => true;
    public abstract void Interact(PlayerInteractor player);

    protected static GameManager Game => GameManager.Instance;

    protected static string CostText(int cost) => cost + " conchas";

    // Spend points or show "not enough" message (only to the buyer, online). Returns true if paid (the run
    // log records what was bought).
    protected bool Pay(int cost, Component buyer = null)
    {
        if (Game == null) return false;
        if (Game.TrySpend(cost, name)) return true;
        string text = "No tienes suficientes conchas (" + cost + ")";
        if (buyer != null) Game.ShowMessageTo(buyer, text);
        else Game.ShowMessage(text);
        return false;
    }
}
