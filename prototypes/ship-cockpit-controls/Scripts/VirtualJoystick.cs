// PROTOTYPE - NOT FOR PRODUCTION
// Question: Which touch control scheme makes ship flight feel most natural on Android?
// Date: 2026-04-12

using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// A floating virtual joystick that repositions to the touch-down point.
/// Attach to a UI RectTransform that covers the joystick zone.
/// Requires: background (outer ring), handle (inner dot) as child RectTransforms.
/// </summary>
public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("References")]
    public RectTransform background;   // outer ring image
    public RectTransform handle;       // inner dot image

    [Header("Tuning")]
    [Tooltip("Max handle travel in pixels (screen space)")]
    public float handleRange = 60f;

    [Tooltip("Dead zone — inputs below this magnitude are treated as zero")]
    [Range(0f, 0.3f)]
    public float deadZone = 0.1f;

    public Vector2 Input { get; private set; }

    private Vector2 _center;
    private Canvas _canvas;

    void Awake()
    {
        _canvas = GetComponentInParent<Canvas>();
    }

    public void OnPointerDown(PointerEventData data)
    {
        // Reposition background to touch point (floating joystick feel)
        _center = ScreenToCanvasPoint(data.position);
        background.anchoredPosition = _center;
        background.gameObject.SetActive(true);
        OnDrag(data);
    }

    public void OnDrag(PointerEventData data)
    {
        Vector2 touchPos = ScreenToCanvasPoint(data.position);
        Vector2 delta = touchPos - _center;

        if (delta.magnitude > handleRange)
            delta = delta.normalized * handleRange;

        handle.anchoredPosition = _center + delta;

        Vector2 raw = delta / handleRange;
        Input = raw.magnitude < deadZone ? Vector2.zero : raw;
    }

    public void OnPointerUp(PointerEventData data)
    {
        Input = Vector2.zero;
        handle.anchoredPosition = _center;
        background.gameObject.SetActive(false);
    }

    Vector2 ScreenToCanvasPoint(Vector2 screenPos)
    {
        if (_canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return screenPos;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvas.transform as RectTransform,
            screenPos,
            _canvas.worldCamera,
            out Vector2 local);
        return local;
    }
}
