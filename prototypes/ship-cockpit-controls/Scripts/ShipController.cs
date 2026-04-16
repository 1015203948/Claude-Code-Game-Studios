// PROTOTYPE - NOT FOR PRODUCTION
// Question: Which touch control scheme makes ship flight feel most natural on Android?
// Date: 2026-04-12

using UnityEngine;

/// <summary>
/// Handles ship movement for all three control schemes.
/// Attach to the ship GameObject alongside a Rigidbody.
/// </summary>
public class ShipController : MonoBehaviour
{
    public enum ControlScheme { DualStick, SingleStick, TapToMove }

    [Header("Active Scheme")]
    public ControlScheme scheme = ControlScheme.DualStick;

    [Header("Movement — Dual & Single Stick")]
    public float thrustForce = 20f;
    public float rotationSpeed = 120f;   // deg/s
    public float maxSpeed = 15f;

    [Header("Movement — Tap To Move")]
    public float tapMoveSpeed = 8f;
    public float arrivalRadius = 1.5f;   // stop when within this distance

    // Inputs set by InputBridge
    [HideInInspector] public Vector2 leftInput;
    [HideInInspector] public Vector2 rightInput;
    [HideInInspector] public Vector3 tapTarget;
    [HideInInspector] public bool hasTapTarget;

    private Rigidbody _rb;

    void Start()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = false;
        _rb.linearDamping = 1.5f;   // Unity 6.x: was rb.drag
        _rb.angularDamping = 8f;    // Unity 6.x: was rb.angularDrag
    }

    void FixedUpdate()
    {
        switch (scheme)
        {
            case ControlScheme.DualStick:   UpdateDualStick();   break;
            case ControlScheme.SingleStick: UpdateSingleStick(); break;
            case ControlScheme.TapToMove:   UpdateTapToMove();   break;
        }
    }

    // ── Scheme A: Dual Stick ──────────────────────────────────────────────────
    // Left stick Y = forward/back thrust
    // Right stick X/Y = yaw/pitch rotation
    void UpdateDualStick()
    {
        // Thrust
        _rb.AddForce(transform.forward * (leftInput.y * thrustForce));

        // Rotation
        float yaw   =  rightInput.x * rotationSpeed * Time.fixedDeltaTime;
        float pitch = -rightInput.y * rotationSpeed * Time.fixedDeltaTime;
        transform.Rotate(pitch, yaw, 0f, Space.Self);

        ClampSpeed();
    }

    // ── Scheme B: Single Stick + Auto-Thrust ─────────────────────────────────
    // Stick direction = heading; stick magnitude = speed
    void UpdateSingleStick()
    {
        if (leftInput.magnitude < 0.1f) return;

        // Rotate toward stick direction (in XZ plane)
        Vector3 targetDir = new Vector3(leftInput.x, 0f, leftInput.y).normalized;
        Quaternion targetRot = Quaternion.LookRotation(targetDir, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, targetRot, rotationSpeed * Time.fixedDeltaTime);

        // Thrust proportional to stick magnitude
        _rb.AddForce(transform.forward * (leftInput.magnitude * thrustForce));
        ClampSpeed();
    }

    // ── Scheme C: Tap To Move ─────────────────────────────────────────────────
    // Player taps a point in world space; ship flies there automatically.
    void UpdateTapToMove()
    {
        if (!hasTapTarget) return;

        Vector3 toTarget = tapTarget - transform.position;
        float dist = toTarget.magnitude;

        if (dist < arrivalRadius)
        {
            hasTapTarget = false;
            _rb.linearVelocity = Vector3.zero;
            return;
        }

        // Rotate toward target
        Quaternion targetRot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, targetRot, rotationSpeed * Time.fixedDeltaTime);

        // Move at constant speed
        _rb.linearVelocity = transform.forward * tapMoveSpeed;
    }

    void ClampSpeed()
    {
        if (_rb.linearVelocity.magnitude > maxSpeed)
            _rb.linearVelocity = _rb.linearVelocity.normalized * maxSpeed;
    }
}
