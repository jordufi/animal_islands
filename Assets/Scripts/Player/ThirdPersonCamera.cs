using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Over-the-shoulder orbit camera (shooter style) that follows a target. Rotated with the "Look"
// action of the Player map (right stick / mouse). Moves closer and lowers sensitivity while
// Aiming, and pulls in when scenery is between the camera and the target. With the back against a wall
// it rises over the shoulder instead of ending inside the head, and the character is hidden (shadow
// only) when the camera gets too close to it. Thin colliders (poles, posts, trunks) don't push it in.
public class ThirdPersonCamera : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] InputActionAsset actions;

    [Header("Framing")]
    [SerializeField] Vector3 targetOffset = new Vector3(0f, 1.1f, 0f);
    [SerializeField] float distance = 4f;
    [SerializeField] Vector2 shoulderOffset = new Vector2(0.6f, 0.15f);
    [SerializeField] float minPitch = -30f;
    [SerializeField] float maxPitch = 70f;
    [SerializeField] float followSmoothing = 15f;

    [Header("Aiming")]
    [SerializeField] float aimDistance = 2f;
    [SerializeField] Vector2 aimShoulderOffset = new Vector2(0.65f, 0.1f);
    [SerializeField] float aimFieldOfView = 45f;
    [SerializeField] float aimSensitivityScale = 0.5f;
    [SerializeField] float aimBlendSpeed = 10f;

    [Header("Sensitivity")]
    [SerializeField] float stickDegreesPerSecond = 180f;
    [SerializeField] float mouseDegreesPerPixel = 0.15f;
    [SerializeField] bool invertY = false;

    [Header("Collision")]
    [SerializeField] float collisionRadius = 0.2f;
    [SerializeField] float minDistance = 0.5f;
    [SerializeField] LayerMask collisionLayers = ~0;
    [SerializeField] float thinColliderSize = 0.6f;    // colliders thinner than this (x and z) are ignored
    [SerializeField] float squeezeRise = 0.45f;        // metres the camera rises when fully pushed in
    [SerializeField] float squeezeShoulder = 0.35f;    // shoulder offset kept when fully pushed in
    [SerializeField] float hideTargetDistance = 0.95f; // closer than this to the head: hide the character

    public bool Aiming { get; set; }

    float shakeUntil, shakeDuration, shakeAmplitude;

    // Kicks the view up (and a bit sideways); the player has to pull it back down, like in Zombies.
    public void AddRecoil(float up, float side)
    {
        pitch = Mathf.Clamp(pitch - up, minPitch, maxPitch);
        yaw += side;
    }

    public void Shake(float duration, float amplitude)
    {
        shakeDuration = duration;
        shakeUntil = Time.time + duration;
        shakeAmplitude = Mathf.Max(amplitude, Time.time < shakeUntil ? shakeAmplitude : 0f);
    }
    public float AimFieldOfViewOverride { get; set; }   // set by the weapon in hand (sniper zooms more)
    public float Yaw => yaw;

    InputAction lookAction;
    Camera cam;
    float yaw, pitch = 15f;
    float aimBlend;
    float defaultFieldOfView;
    Vector3 smoothedFocus;
    float currentDistance;
    readonly RaycastHit[] hits = new RaycastHit[16];
    readonly List<Renderer> targetRenderers = new List<Renderer>();
    bool targetHidden;
    public float CurrentDistance => currentDistance;
    public bool TargetHidden => targetHidden;

    void Awake()
    {
        lookAction = actions.FindActionMap("Player", true).FindAction("Look", true);
        cam = GetComponent<Camera>();
        defaultFieldOfView = GameSettings.FieldOfView;
        currentDistance = distance;
    }

    void OnEnable() => lookAction.Enable();

    // Co-op: read Look from this player's own actions (bound to their devices).
    public void BindActions(InputActionAsset asset)
    {
        if (asset == null) return;
        actions = asset;
        lookAction = asset.FindActionMap("Player", true).FindAction("Look", true);
        lookAction.Enable();
    }

    void Start()
    {
        if (target != null)
        {
            yaw = target.eulerAngles.y;
            smoothedFocus = target.position + targetOffset;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        aimBlend = Mathf.MoveTowards(aimBlend, Aiming ? 1f : 0f, aimBlendSpeed * Time.deltaTime);
        float sensitivity = Mathf.Lerp(1f, aimSensitivityScale, aimBlend) * GameSettings.Sensitivity;
        bool invert = invertY ^ GameSettings.InvertY;
        defaultFieldOfView = GameSettings.FieldOfView;

        Vector2 look = lookAction.ReadValue<Vector2>();
        if (Net.Online && GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) look = Vector2.zero;   // online menus don't pause the game
        bool fromMouse = lookAction.activeControl != null && lookAction.activeControl.device is Pointer;
        Vector2 delta = (fromMouse ? look * mouseDegreesPerPixel : look * stickDegreesPerSecond * Time.deltaTime) * sensitivity;
        yaw += delta.x;
        pitch += invert ? delta.y : -delta.y;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        Vector3 focus = target.position + targetOffset;
        smoothedFocus = Vector3.Lerp(smoothedFocus, focus, 1f - Mathf.Exp(-followSmoothing * Time.deltaTime));

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector2 shoulder = Vector2.Lerp(shoulderOffset, aimShoulderOffset, aimBlend);
        float wantedDistance = Mathf.Lerp(distance, aimDistance, aimBlend);

        // keep the camera out of walls: cast from the focus point towards the desired position
        float length = CastLength(rotation, shoulder, wantedDistance, out var dir);
        // pushed in (back against a wall): rise and move towards the shoulder line, then cast again
        float squeeze = 1f - Mathf.Clamp01((length - minDistance) / Mathf.Max(0.01f, wantedDistance * 0.6f));
        if (squeeze > 0f)
        {
            var squeezed = new Vector2(Mathf.Lerp(shoulder.x, squeezeShoulder, squeeze), shoulder.y + squeezeRise * squeeze);
            length = CastLength(rotation, squeezed, wantedDistance, out dir);
        }
        // pull in instantly, ease back out
        currentDistance = length < currentDistance ? length : Mathf.Lerp(currentDistance, length, 1f - Mathf.Exp(-4f * Time.deltaTime));
        HideTarget(currentDistance < hideTargetDistance);

        Vector3 shake = Vector3.zero;
        if (Time.time < shakeUntil)
        {
            float k = (shakeUntil - Time.time) / Mathf.Max(0.01f, shakeDuration);
            shake = Random.insideUnitSphere * shakeAmplitude * k;
        }
        transform.SetPositionAndRotation(smoothedFocus + dir * currentDistance + shake, rotation);
        float aimFov = AimFieldOfViewOverride > 0f ? AimFieldOfViewOverride : aimFieldOfView;
        if (cam != null) cam.fieldOfView = Mathf.Lerp(defaultFieldOfView, aimFov, aimBlend);
    }

    // Free length from the focus towards the camera spot given by shoulder offset and distance.
    float CastLength(Quaternion rotation, Vector2 shoulder, float wantedDistance, out Vector3 dir)
    {
        Vector3 toDesired = rotation * new Vector3(shoulder.x, shoulder.y, -wantedDistance);
        float maxLength = toDesired.magnitude;
        dir = toDesired / Mathf.Max(maxLength, 0.0001f);
        float length = maxLength;
        int count = Physics.SphereCastNonAlloc(smoothedFocus, collisionRadius, dir, hits, maxLength, collisionLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            var col = hits[i].collider;
            if (col.transform.IsChildOf(target)) continue;
            var size = col.bounds.size;
            if (size.x < thinColliderSize && size.z < thinColliderSize) continue;   // poles, posts, thin trunks
            // never past the hit (a minimum distance here would push the camera into the wall)
            if (hits[i].distance > 0f) length = Mathf.Min(length, Mathf.Max(0.05f, hits[i].distance));
        }
        return length;
    }

    // Shadow-only rendering of the character (and what it holds) while the camera is inside / at its head.
    void HideTarget(bool hide)
    {
        if (hide == targetHidden && !hide) return;
        targetRenderers.Clear();
        target.GetComponentsInChildren(targetRenderers);
        var mode = hide ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
        foreach (var r in targetRenderers)
            if (r is MeshRenderer || r is SkinnedMeshRenderer) r.shadowCastingMode = mode;
        targetHidden = hide;
    }

    // Turns the camera so it looks at a point (tests / bots).
    public void LookAtPoint(Vector3 point)
    {
        if (target == null) return;
        Vector3 d = point - transform.position;   // from the camera itself (over the shoulder): the crosshair lands on it
        if (d.sqrMagnitude < 0.01f) return;
        yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        pitch = Mathf.Clamp(-Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg, minPitch, maxPitch);
    }

    public void SetTarget(Transform newTarget)
    {
        if (target != null && targetHidden) HideTarget(false);
        target = newTarget;
        if (target == null) return;
        smoothedFocus = target.position + targetOffset;
        yaw = target.eulerAngles.y;
    }
}
