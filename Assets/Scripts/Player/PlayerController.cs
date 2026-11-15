using UnityEngine;
using UnityEngine.InputSystem;

// Third-person character movement driven by the "Player" action map of InputSystem_Actions:
//   Move   - left stick / WASD          (relative to the camera)
//   Jump   - Cross (buttonSouth) / Space
//   Sprint - L3 (left stick press): press once and keep sprinting while the stick is pushed (stops when
//            the stick is released, when aiming / shooting / swinging, or on a second press);
//            "hold" mode in the settings. Left Shift is always held.
// Animator parameters: Speed (m/s at base speed: idle / walk / run blend), Grounded (bool).
// Combat hooks: StrafeActive makes the body face StrafeYaw (camera direction) instead of the
// movement direction, and ControlsEnabled = false ignores input (used when the player dies).
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] InputActionAsset actions;

    [Header("Movement")]
    [SerializeField] float walkSpeed = 2.5f;
    [SerializeField] float sprintSpeed = 5f;
    [SerializeField] float acceleration = 12f;
    [SerializeField] float turnSpeed = 720f;        // degrees per second
    [SerializeField] float airControl = 0.5f;

    [Header("Jump")]
    [SerializeField] float jumpHeight = 1.2f;
    [SerializeField] float gravity = -20f;
    [SerializeField] float coyoteTime = 0.12f;      // jump still allowed shortly after leaving a ledge
    [SerializeField] float jumpBufferTime = 0.15f;  // jump pressed shortly before landing still counts

    [Header("Sprint toggle")]
    [SerializeField] float sprintStickThreshold = 0.35f;   // stick below this ends a toggled sprint
    [SerializeField] float sprintReleaseGrace = 0.15f;     // ...after this long (stick flicks / direction changes)

    [Header("Animation")]
    [SerializeField] float airborneDelay = 0.12f;   // ignore tiny ground losses on slopes and steps

    [Header("Safety")]
    [SerializeField] float respawnBelowY = -10f;

    CharacterController controller;
    PlayerStats stats;
    Animator animator;
    Transform cameraTransform;
    InputAction moveAction, jumpAction, sprintAction;

    Vector3 horizontalVelocity;
    float verticalVelocity;
    float lastGroundedTime = float.NegativeInfinity;
    float lastJumpPressedTime = float.NegativeInfinity;
    float airTime;
    bool sprintLatched;
    float stickLowSince = -1f;
    Vector3 dashVelocity;
    float dashUntil;
    Vector3 spawnPosition;
    Quaternion spawnRotation;

    public bool IsGrounded => controller.isGrounded;
    public float HorizontalSpeed => horizontalVelocity.magnitude;
    public bool IsSprinting { get; private set; }
    public void StopSprint() { sprintLatched = false; IsSprinting = false; }

    // Short burst of movement that overrides the input (melee lunge).
    public void Dash(Vector3 velocity, float duration)
    {
        dashVelocity = new Vector3(velocity.x, 0f, velocity.z);
        dashUntil = Time.time + duration;
    }

    public bool ControlsEnabled { get; set; } = true;
    public bool StrafeActive { get; set; }
    public float StrafeYaw { get; set; }

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        stats = GetComponent<PlayerStats>();
        animator = GetComponentInChildren<Animator>();
        if (animator != null) animator.applyRootMotion = false;
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;

        actions = PlayerSlot.ActionsFor(this, actions);   // co-op: this player's own devices
        var map = actions.FindActionMap("Player", true);
        moveAction = map.FindAction("Move", true);
        jumpAction = map.FindAction("Jump", true);
        sprintAction = map.FindAction("Sprint", true);
    }

    void OnEnable() => actions.FindActionMap("Player", true).Enable();
    void OnDisable() => actions.FindActionMap("Player", true).Disable();

    void Start()
    {
        var cam = PlayerSlot.CameraOf(this);   // co-op: this player's split-screen camera
        if (cam != null) cameraTransform = cam.transform;
    }

    public float ExternalSpeedFactor { get; set; } = 1f;   // wading in the acqua alta (Rounds/AcquaAlta)
    float SpeedMultiplier => (stats != null ? stats.Get(Stat.MoveSpeed) : 1f) * ExternalSpeedFactor;

    // Instant upward jump of the given height (abilities, hiccups).
    public void Launch(float height)
    {
        verticalVelocity = Mathf.Sqrt(height * -2f * gravity);
        lastGroundedTime = float.NegativeInfinity;
    }

    // Online the pause / debug menus don't stop the world: the player just stands still meanwhile.
    bool InputAllowed => ControlsEnabled && !(Net.Online && GameManager.Instance != null && GameManager.Instance.State != GameState.Playing
                                              && GameManager.Instance.State != GameState.GameOver && GameManager.Instance.State != GameState.Victory);

    void Update()
    {
        if (InputAllowed && jumpAction.WasPressedThisFrame()) lastJumpPressedTime = Time.time;
        if (controller.isGrounded) lastGroundedTime = Time.time;

        UpdateHorizontal();
        UpdateVertical();

        Vector3 motion = (horizontalVelocity + Vector3.up * verticalVelocity) * Time.deltaTime;
        controller.Move(motion);

        if (transform.position.y < respawnBelowY) Respawn();
        UpdateAnimation();
    }

    void UpdateHorizontal()
    {
        Vector2 input = InputAllowed ? Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f) : Vector2.zero;

        // camera-relative direction on the ground plane
        Vector3 forward = cameraTransform ? cameraTransform.forward : Vector3.forward;
        Vector3 right = cameraTransform ? cameraTransform.right : Vector3.right;
        forward.y = 0f; right.y = 0f;
        forward.Normalize(); right.Normalize();
        Vector3 desiredDirection = forward * input.y + right * input.x;

        IsSprinting = UpdateSprint(input) && !StrafeActive;
        float maxSpeed = (IsSprinting ? sprintSpeed : walkSpeed) * SpeedMultiplier;
        Vector3 targetVelocity = desiredDirection * maxSpeed;   // stick tilt gives analog speed
        if (IsSprinting && input.sqrMagnitude > 0.01f) targetVelocity = desiredDirection.normalized * maxSpeed;   // full speed even at partial tilt

        if (Time.time < dashUntil)
        {
            horizontalVelocity = dashVelocity;
            if (dashVelocity.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dashVelocity), turnSpeed * 2f * Time.deltaTime);
            return;
        }

        float accel = acceleration * (controller.isGrounded ? 1f : airControl);
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, accel * maxSpeed * Time.deltaTime);

        if (StrafeActive)
        {
            Quaternion targetRotation = Quaternion.Euler(0f, StrafeYaw, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * 2f * Time.deltaTime);
        }
        else if (desiredDirection.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(desiredDirection, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }
    }

    // Hold on the keyboard (or with the "hold" setting); on the gamepad a press latches the sprint until the
    // stick goes back to the centre, the player aims / shoots / swings, or L3 is pressed again.
    bool UpdateSprint(Vector2 input)
    {
        if (!InputAllowed) { sprintLatched = false; return false; }
        var control = sprintAction.activeControl;
        bool fromKeyboard = control != null && control.device is Keyboard;
        if (fromKeyboard || !GameSettings.SprintToggle) { sprintLatched = false; return sprintAction.IsPressed() && input.sqrMagnitude > 0.01f; }

        if (sprintAction.WasPressedThisFrame()) sprintLatched = !sprintLatched && input.magnitude >= sprintStickThreshold * 0.5f;
        if (StrafeActive) sprintLatched = false;
        if (sprintLatched)
        {
            if (input.magnitude < sprintStickThreshold)
            {
                if (stickLowSince < 0f) stickLowSince = Time.time;
                else if (Time.time - stickLowSince > sprintReleaseGrace) sprintLatched = false;
            }
            else stickLowSince = -1f;
        }
        return sprintLatched;
    }

    void UpdateVertical()
    {
        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f; // keep the controller pressed onto slopes and steps

        bool canJump = Time.time - lastGroundedTime <= coyoteTime;
        bool wantsJump = Time.time - lastJumpPressedTime <= jumpBufferTime;
        if (canJump && wantsJump)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            lastGroundedTime = float.NegativeInfinity;
            lastJumpPressedTime = float.NegativeInfinity;
        }

        verticalVelocity += gravity * Time.deltaTime;
    }

    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int GroundedHash = Animator.StringToHash("Grounded");

    void UpdateAnimation()
    {
        if (animator == null) return;
        airTime = controller.isGrounded ? 0f : airTime + Time.deltaTime;
        // speed relative to the base walk/run speeds, so buffs don't push walking into the run clip
        animator.SetFloat(SpeedHash, horizontalVelocity.magnitude / SpeedMultiplier, 0.08f, Time.deltaTime);
        animator.SetBool(GroundedHash, airTime < airborneDelay);
    }

    public void Respawn()
    {
        controller.enabled = false;
        transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        controller.enabled = true;
        horizontalVelocity = Vector3.zero;
        verticalVelocity = 0f;
    }
}
