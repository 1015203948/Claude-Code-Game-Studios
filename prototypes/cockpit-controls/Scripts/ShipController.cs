// PROTOTYPE - NOT FOR PRODUCTION
// Question: Does dual virtual joystick touch control feel responsive and fun
//           for spaceship piloting on Android?
// Date: 2026-04-14

using UnityEngine;

/// <summary>
/// Drives ship movement using Rigidbody2D.AddForce (never direct velocity writes).
/// Reads normalized joystick values from DualJoystickInput.
/// All values hardcoded for fast iteration — do not refactor into SO config here.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class ShipController : MonoBehaviour
{
    [Header("References")]
    public DualJoystickInput JoystickInput;
    public ParticleSystem ThrustParticles;

    [Header("Tuning — tweak freely")]
    public float ThrustPower = 8f;
    public float RotationSpeed = 180f;  // degrees per second
    public float LinearDamping = 1.5f;  // replaces old .drag — Unity 6 API

    private Rigidbody2D _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _rb.linearDamping = LinearDamping;  // Unity 6: linearDamping, not .drag
    }

    private void FixedUpdate()
    {
        if (JoystickInput == null) return;

        HandleThrust();
        HandleAim();
    }

    private void HandleThrust()
    {
        Vector2 thrust = JoystickInput.ThrustInput;

        if (thrust.sqrMagnitude > 0.001f)
        {
            // AddForce — never write _rb.linearVelocity directly
            _rb.AddForce(thrust * ThrustPower, ForceMode2D.Force);

            if (ThrustParticles != null && !ThrustParticles.isPlaying)
                ThrustParticles.Play();
        }
        else
        {
            if (ThrustParticles != null && ThrustParticles.isPlaying)
                ThrustParticles.Stop();
        }
    }

    private void HandleAim()
    {
        Vector2 aim = JoystickInput.AimInput;

        if (aim.sqrMagnitude > 0.001f)
        {
            // Rotate ship to face aim direction
            float targetAngle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg - 90f;
            float currentAngle = _rb.rotation;
            float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle,
                RotationSpeed * Time.fixedDeltaTime);
            _rb.MoveRotation(newAngle);
        }
    }

    // --- Editor helper: visualise thrust vector in Scene view ---
    private void OnDrawGizmos()
    {
        if (JoystickInput == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position,
            (Vector3)JoystickInput.ThrustInput * 2f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position,
            (Vector3)JoystickInput.AimInput * 2f);
    }
}
