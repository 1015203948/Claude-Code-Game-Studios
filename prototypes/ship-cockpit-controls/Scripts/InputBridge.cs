// PROTOTYPE - NOT FOR PRODUCTION
// Question: Which touch control scheme makes ship flight feel most natural on Android?
// Date: 2026-04-12

using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Bridges touch input to ShipController.
/// For Scheme C (Tap To Move): raycasts from tap position to find world target.
/// Attach to any persistent GameObject in the scene.
/// </summary>
public class InputBridge : MonoBehaviour
{
    [Header("References")]
    public VirtualJoystick leftJoystick;
    public VirtualJoystick rightJoystick;
    public ShipController ship;
    public Camera mainCamera;

    [Header("Tap To Move")]
    [Tooltip("Layer mask for tap raycast — set to your obstacle/floor layer")]
    public LayerMask tapRaycastLayers = ~0;
    [Tooltip("Fallback plane Y if raycast misses")]
    public float tapPlaneY = 0f;

    void Update()
    {
        ship.leftInput  = leftJoystick  != null ? leftJoystick.Input  : Vector2.zero;
        ship.rightInput = rightJoystick != null ? rightJoystick.Input : Vector2.zero;

        if (ship.scheme == ShipController.ControlScheme.TapToMove)
            HandleTapInput();
    }

    void HandleTapInput()
    {
        // Ignore taps that hit UI elements
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            return;

#if UNITY_EDITOR
        if (UnityEngine.Input.GetMouseButtonDown(0))
            ProcessTapAt(UnityEngine.Input.mousePosition);
#else
        foreach (Touch t in UnityEngine.Input.touches)
        {
            if (t.phase == TouchPhase.Began)
                ProcessTapAt(t.position);
        }
#endif
    }

    void ProcessTapAt(Vector2 screenPos)
    {
        Ray ray = mainCamera.ScreenPointToRay(screenPos);

        if (Physics.Raycast(ray, out RaycastHit hit, 200f, tapRaycastLayers))
        {
            ship.tapTarget    = hit.point;
            ship.hasTapTarget = true;
        }
        else
        {
            // Fallback: intersect with horizontal plane at tapPlaneY
            Plane plane = new Plane(Vector3.up, new Vector3(0f, tapPlaneY, 0f));
            if (plane.Raycast(ray, out float enter))
            {
                ship.tapTarget    = ray.GetPoint(enter);
                ship.hasTapTarget = true;
            }
        }
    }
}
