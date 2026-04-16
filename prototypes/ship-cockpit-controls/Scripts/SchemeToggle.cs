// PROTOTYPE - NOT FOR PRODUCTION
// Question: Which touch control scheme makes ship flight feel most natural on Android?
// Date: 2026-04-12

using UnityEngine;
using TMPro;

/// <summary>
/// Cycles through the three control schemes at runtime.
/// Attach to any persistent GameObject. Wire up via Inspector.
/// </summary>
public class SchemeToggle : MonoBehaviour
{
    [Header("References")]
    public ShipController ship;
    public GameObject dualStickUI;      // Panel containing both joysticks
    public GameObject singleStickUI;    // Panel containing left joystick only
    public GameObject tapMoveUI;        // Panel with tap-to-move hint label
    public TMP_Text schemeLabel;        // Displays current scheme name

    private static readonly string[] SchemeNames =
    {
        "方案 A: 双摇杆\n左=推力 右=旋转",
        "方案 B: 单摇杆\n方向=航向 幅度=速度",
        "方案 C: 点击目标\n点击屏幕设定目标点"
    };

    void Start() => ApplyScheme();

    public void NextScheme()
    {
        int next = ((int)ship.scheme + 1) % 3;
        ship.scheme = (ShipController.ControlScheme)next;

        // Reset ship state on switch
        Rigidbody rb = ship.GetComponent<Rigidbody>();
        rb.linearVelocity        = Vector3.zero;
        rb.angularVelocity       = Vector3.zero;
        ship.hasTapTarget        = false;

        ApplyScheme();
    }

    void ApplyScheme()
    {
        dualStickUI.SetActive(ship.scheme == ShipController.ControlScheme.DualStick);
        singleStickUI.SetActive(ship.scheme == ShipController.ControlScheme.SingleStick);
        tapMoveUI.SetActive(ship.scheme == ShipController.ControlScheme.TapToMove);

        if (schemeLabel != null)
            schemeLabel.text = SchemeNames[(int)ship.scheme];
    }
}
