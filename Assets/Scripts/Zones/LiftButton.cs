using UnityEngine;

// A button inside a lift cab: up (dir 1) or down (-1) to the next stop whose landing zone is open.
public class LiftButton : Interactable
{
    [SerializeField] Lift lift;
    [SerializeField] int stop;
    [SerializeField] int dir = 1;

    public void Configure(Lift l, int stopIndex, int direction)
    {
        lift = l;
        stop = stopIndex;
        dir = direction;
    }

    public int Target => lift != null ? lift.NextStop(stop, dir) : -1;

    public override bool CanInteract(PlayerInteractor player) => lift != null && !lift.Busy && Target >= 0;

    public override string Prompt(PlayerInteractor player)
    {
        if (lift == null) return "";
        string why = lift.Blocked(player);
        if (why != null) return lift.LiftName + ": " + why;
        return (dir > 0 ? "Subir a " : "Bajar a ") + lift.StopName(Target);
    }

    public override void Interact(PlayerInteractor player)
    {
        string why = lift.Blocked(player);
        if (why != null) { if (Game != null) Game.ShowMessageTo(player, why); return; }
        lift.Ride(stop, Target);
    }
}
