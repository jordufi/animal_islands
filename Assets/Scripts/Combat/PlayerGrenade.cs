using UnityEngine;
using UnityEngine.InputSystem;

// The player's false teeth (Dentadura, user feedback 1): R1 / G throws one where the camera looks. Each player carries
// EconomyData.dentaduraCount; the Nevera de munición (PlayerShooter.RefillAllAmmo) fills them up again. Added by
// PlayerShooter, reads the "Throw" action of the player's own devices.
public class PlayerGrenade : MonoBehaviour
{
    InputAction throwAction;
    PlayerHealth health;
    float nextThrow;

    public int Count { get; private set; }
    public int Max => Eco != null ? Eco.dentaduraCount : 2;
    public event System.Action Changed;
    public event System.Action<Vector3, Vector3> Thrown;   // (from, velocity): NetPlayer shows it on the other machines

    static EconomyData Eco => GameManager.Instance != null ? GameManager.Instance.Economy : null;

    public void Init(InputActionAsset actions)
    {
        health = GetComponent<PlayerHealth>();
        var map = actions != null ? actions.FindActionMap("Player", false) : null;
        throwAction = map != null ? map.FindAction("Throw", false) : null;
        if (throwAction != null) throwAction.Enable();
        Count = Max;
    }

    void Start()
    {
        Count = Max;   // the GameManager (and its economy) exists by now
        Changed?.Invoke();
    }

    public void Refill()
    {
        Count = Max;
        Changed?.Invoke();
    }

    void Update()
    {
        if (throwAction == null || !throwAction.WasPressedThisFrame()) return;
        TryThrow();
    }

    public bool TryThrow()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.State != GameState.Playing || PlayerSlot.IsProxy(this)) return false;
        if (health != null && (health.IsDead || health.IsDowned)) return false;
        if (Count <= 0 || Time.time < nextThrow) return false;
        Count--;
        nextThrow = Time.time + 0.8f;
        var cam = PlayerSlot.CameraOf(this);
        Vector3 fwd = cam != null ? cam.transform.forward : transform.forward;
        Vector3 from = transform.position + Vector3.up * 1.5f + Vector3.ProjectOnPlane(fwd, Vector3.up).normalized * 0.5f;
        float speed = Eco != null ? Eco.dentaduraThrowSpeed : 13f;
        Vector3 velocity = fwd * speed + Vector3.up * 4f;
        Dentadura.Throw(from, velocity, this, true);
        Thrown?.Invoke(from, velocity);
        Changed?.Invoke();
        return true;
    }
}
