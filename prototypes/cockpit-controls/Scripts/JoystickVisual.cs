// PROTOTYPE - NOT FOR PRODUCTION
// Question: Does dual virtual joystick touch control feel responsive and fun
//           for spaceship piloting on Android?
// Date: 2026-04-14

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple UI representation of a virtual joystick.
/// Shows a base circle where touch began and a knob that follows the finger.
/// Wire up via DualJoystickInput.LeftVisual / RightVisual.
/// </summary>
public class JoystickVisual : MonoBehaviour
{
    [Header("UI References — assign in Inspector")]
    public RectTransform Base;   // outer circle (stays at touch origin)
    public RectTransform Knob;   // inner circle (follows finger, clamped)

    [Header("Tuning")]
    public float KnobMaxOffset = 60f; // pixels — should match DualJoystickInput.JoystickRadius

    private Vector2 _baseScreenPos;

    private void Awake()
    {
        Hide();
    }

    public void Show(Vector2 screenPos)
    {
        gameObject.SetActive(true);
        _baseScreenPos = screenPos;

        // Position base at touch origin
        if (Base != null)
            Base.position = screenPos;

        // Reset knob to center
        if (Knob != null)
            Knob.position = screenPos;
    }

    public void MoveTo(Vector2 currentScreenPos)
    {
        if (Knob == null) return;

        Vector2 offset = currentScreenPos - _baseScreenPos;
        offset = Vector2.ClampMagnitude(offset, KnobMaxOffset);
        Knob.position = _baseScreenPos + offset;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
