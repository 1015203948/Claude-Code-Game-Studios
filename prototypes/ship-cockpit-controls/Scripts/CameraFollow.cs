// PROTOTYPE - NOT FOR PRODUCTION
// Question: Which touch control scheme makes ship flight feel most natural on Android?
// Date: 2026-04-12

using UnityEngine;

/// <summary>
/// Smooth third-person follow camera.
/// Attach to the Main Camera.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    public Transform target;

    [Header("Offset (local space of target)")]
    public Vector3 offset = new Vector3(0f, 4f, -10f);

    [Header("Smoothing")]
    public float positionSmooth = 6f;
    public float rotationSmooth = 5f;

    void LateUpdate()
    {
        if (target == null) return;

        // Desired position: behind and above the ship
        Vector3 desired = target.TransformPoint(offset);
        transform.position = Vector3.Lerp(transform.position, desired,
            positionSmooth * Time.deltaTime);

        // Look slightly ahead of the ship
        Vector3 lookAt = target.position + target.forward * 4f;
        Quaternion desiredRot = Quaternion.LookRotation(lookAt - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot,
            rotationSmooth * Time.deltaTime);
    }
}
