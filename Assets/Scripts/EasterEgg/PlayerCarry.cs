using System;
using UnityEngine;

// Carrying a quest item (e.g. the Golden Disc): hands are busy (no shooting) and you move slower.
// Taking a hit drops it.
public class PlayerCarry : MonoBehaviour
{
    [SerializeField] Vector3 carryOffset = new Vector3(0f, 1.55f, 0.25f);
    static readonly StatMod[] Slow = { new StatMod(Stat.MoveSpeed, 0.8f) };

    PlayerStats stats;
    public Carryable Carried { get; private set; }
    public event Action Changed;

    void Awake() => stats = GetComponent<PlayerStats>();

    public void Pick(Carryable item)
    {
        if (Carried != null || item == null) return;
        Carried = item;
        item.OnPicked(transform, carryOffset);
        stats.Apply(Slow);
        Changed?.Invoke();
        if (NetGame.H != null) NetGame.H.CarryChanged(item);
    }

    public void Drop() => DropAt(transform.position + transform.forward * 0.8f);

    // Online: a client drops it where it is and tells the host; the host tells everyone.
    public void DropAt(Vector3 at, bool announce = true)
    {
        if (Carried == null) return;
        var item = Carried;
        Carried = null;
        stats.Remove(Slow);
        item.OnDropped(at);
        if (announce && GameManager.Instance != null) GameManager.Instance.ShowMessage("¡Se te ha caído " + item.DisplayName + "!");
        Changed?.Invoke();
        if (NetGame.H != null) NetGame.H.CarryChanged(item);
        else if (NetGame.C != null && !Net.Replaying && !PlayerSlot.IsProxy(this)) NetGame.C.SendDropCarry(at);
    }
}
