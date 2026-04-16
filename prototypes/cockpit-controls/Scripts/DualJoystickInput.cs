// PROTOTYPE - NOT FOR PRODUCTION
// Question: Does dual virtual joystick touch control feel responsive and fun
//           for spaceship piloting on Android?
// Date: 2026-04-14

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using Finger = UnityEngine.InputSystem.EnhancedTouch.Finger;

/// <summary>
/// Tracks two simultaneous touches as left/right virtual joysticks.
/// Left half of screen = thrust joystick.
/// Right half of screen = aim/rotation joystick.
/// Uses Finger object reference (not finger.index) per ADR-0003 pattern.
/// </summary>
public class DualJoystickInput : MonoBehaviour
{
    [Header("Tuning — tweak freely during prototype")]
    public float DeadZone = 0.08f;
    public float JoystickRadius = 80f; // pixels

    [Header("Optional visuals")]
    public JoystickVisual LeftVisual;
    public JoystickVisual RightVisual;

    // Outputs read by ShipController
    public Vector2 ThrustInput { get; private set; }
    public Vector2 AimInput { get; private set; }

    // Internal state — keyed by Finger object (not index!)
    private Finger _leftFinger;
    private Finger _rightFinger;
    private Vector2 _leftStartPos;
    private Vector2 _rightStartPos;

    private void OnEnable()
    {
        EnhancedTouchSupport.Enable();
        Touch.onFingerDown += OnFingerDown;
        Touch.onFingerMove += OnFingerMove;
        Touch.onFingerUp += OnFingerUp;
    }

    private void OnDisable()
    {
        Touch.onFingerDown -= OnFingerDown;
        Touch.onFingerMove -= OnFingerMove;
        Touch.onFingerUp -= OnFingerUp;
        EnhancedTouchSupport.Disable();
    }

    private void OnFingerDown(Finger finger)
    {
        float screenMidX = Screen.width * 0.5f;

        if (finger.screenPosition.x < screenMidX && _leftFinger == null)
        {
            _leftFinger = finger;
            _leftStartPos = finger.screenPosition;
            LeftVisual?.Show(_leftStartPos);
        }
        else if (finger.screenPosition.x >= screenMidX && _rightFinger == null)
        {
            _rightFinger = finger;
            _rightStartPos = finger.screenPosition;
            RightVisual?.Show(_rightStartPos);
        }
    }

    private void OnFingerMove(Finger finger)
    {
        if (finger == _leftFinger)
        {
            ThrustInput = ComputeJoystickValue(_leftStartPos, finger.screenPosition);
            LeftVisual?.MoveTo(finger.screenPosition);
        }
        else if (finger == _rightFinger)
        {
            AimInput = ComputeJoystickValue(_rightStartPos, finger.screenPosition);
            RightVisual?.MoveTo(finger.screenPosition);
        }
    }

    private void OnFingerUp(Finger finger)
    {
        if (finger == _leftFinger)
        {
            _leftFinger = null;
            ThrustInput = Vector2.zero;
            LeftVisual?.Hide();
        }
        else if (finger == _rightFinger)
        {
            _rightFinger = null;
            AimInput = Vector2.zero;
            RightVisual?.Hide();
        }
    }

    /// <summary>
    /// Dead zone formula from ADR-0003:
    /// normalizedInput = Clamp01((Abs(offset) - deadZone) / (1 - deadZone))
    /// Uses origin-offset, never touch.delta.
    /// </summary>
    private Vector2 ComputeJoystickValue(Vector2 startPos, Vector2 currentPos)
    {
        Vector2 rawOffset = currentPos - startPos;
        float magnitude = rawOffset.magnitude;

        if (magnitude < 0.001f) return Vector2.zero;

        // Clamp offset to joystick radius
        float clampedMag = Mathf.Min(magnitude, JoystickRadius);
        Vector2 direction = rawOffset / magnitude;

        // Normalize within radius
        float normalizedMag = clampedMag / JoystickRadius;

        // Apply dead zone
        float deadZonedMag = Mathf.Clamp01((normalizedMag - DeadZone) / (1f - DeadZone));

        return direction * deadZonedMag;
    }
}
