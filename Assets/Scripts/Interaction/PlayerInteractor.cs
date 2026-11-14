using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Finds the closest interactable in range and uses it with the "Use" action (Square / F).
// With nothing in range the same button reloads (or drops a carried item).
// Co-op: next to a downed teammate, holding the button revives them (ReviveTarget).
public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] InputActionAsset actions;

    InputAction useAction;
    PlayerShooter shooter;
    PlayerCarry carry;

    public Interactable Focus { get; private set; }
    public PlayerShooter Shooter => shooter;
    public PlayerCarry Carry => carry;
    public PlayerHealth Health { get; private set; }
    public event Action<Interactable> FocusChanged;

    ReviveTarget reviving;
    float reviveProgress;
    public ReviveTarget Reviving => reviving;
    public float ReviveProgress => reviveProgress;
    public event Action<PlayerHealth> RevivedTeammate;

    void Awake()
    {
        actions = PlayerSlot.ActionsFor(this, actions);   // co-op: this player's own devices
        useAction = actions.FindActionMap("Player", true).FindAction("Use", true);
        shooter = GetComponent<PlayerShooter>();
        carry = GetComponent<PlayerCarry>();
        Health = GetComponent<PlayerHealth>();
    }

    void OnEnable() => useAction.Enable();

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm != null && !gm.IsPlaying) return;
        if (Health != null && (Health.IsDead || Health.IsDowned)) { CancelRevive(); SetFocus(null); return; }
        if (reviving != null) { UpdateRevive(); return; }

        Interactable best = null;
        float bestDist = float.MaxValue;
        Vector3 p = transform.position;
        foreach (var i in Interactable.All)
        {
            if (i == null || !i.isActiveAndEnabled) continue;
            Vector3 d = i.InteractPoint - p;
            if (Mathf.Abs(d.y) > 2.5f) continue;
            d.y = 0f;
            float dist = d.magnitude;
            if (dist > i.Radius || dist >= bestDist) continue;
            if (string.IsNullOrEmpty(i.Prompt(this))) continue;
            best = i;
            bestDist = dist;
        }
        SetFocus(best);

        if (useAction.WasPressedThisFrame()) Press();
    }

    // The Use button (also tests / bots): use what is in focus, else drop the carried item, else reload.
    public void Press()
    {
        if (Focus != null)
        {
            if (!Focus.CanInteract(this)) { }
            else if (NetGame.C != null && !(Focus is ReviveTarget)) NetGame.C.RequestInteract(Focus);   // online client: the host runs it
            else Focus.Interact(this);
        }
        else if (carry != null && carry.Carried != null) carry.Drop();
        else if (shooter != null) shooter.Reload();
    }

    // ---------------------------------------------------------------- reviving a teammate (hold □)
    public void BeginRevive(ReviveTarget target)
    {
        if (reviving != null || target == null || !target.CanInteract(this)) return;
        reviving = target;
        reviveProgress = 0f;
        target.Health.BeingRevivedBy = Health;
        SetReviverControls(false);
    }

    void UpdateRevive()
    {
        var target = reviving;
        Vector3 d = target.transform.position - transform.position;
        d.y = 0f;
        bool holding = useAction.IsPressed() || HoldOverride;
        if (!holding || !target.Health.InLastStand || d.magnitude > target.Radius + 0.5f) { CancelRevive(); return; }
        reviveProgress += Time.deltaTime / Mathf.Max(0.1f, ReviveTarget.TimeFor(this));
        target.Health.ReviveProgress = Mathf.Clamp01(reviveProgress);
        if (reviveProgress < 1f) return;
        var health = target.Health;
        reviving = null;
        reviveProgress = 0f;
        SetReviverControls(true);
        health.Revive();
        var slot = PlayerSlot.Of(this);
        if (slot != null) slot.Revives++;
        RevivedTeammate?.Invoke(health);
        var gm = GameManager.Instance;
        var other = PlayerSlot.Of(health);
        // (online client: the host announces it)
        if (gm != null && !Net.IsClient) gm.ShowMessage((slot != null ? slot.Tag : "Alguien") + " ha levantado a " + (other != null ? other.Tag : "su compañero"));
    }

    // Tests: keep the revive going without a real button.
    public bool HoldOverride { get; set; }

    void CancelRevive()
    {
        if (reviving == null) return;
        if (reviving.Health != null && reviving.Health.BeingRevivedBy == Health)
        {
            reviving.Health.BeingRevivedBy = null;
            reviving.Health.ReviveProgress = 0f;
        }
        reviving = null;
        reviveProgress = 0f;
        SetReviverControls(true);
    }

    void SetReviverControls(bool on)
    {
        var controller = GetComponent<PlayerController>();
        if (controller != null) controller.ControlsEnabled = on;
        if (shooter != null) shooter.enabled = on;
    }

    void SetFocus(Interactable i)
    {
        if (Focus == i) return;
        Focus = i;
        FocusChanged?.Invoke(i);
    }
}
