// PROTOTYPE - NOT FOR PRODUCTION
// Question: Which touch control scheme makes ship flight feel most natural on Android?
// Date: 2026-04-12

using UnityEngine;

/// <summary>
/// Spawns a simple test environment: 5 obstacle spheres arranged in a ring.
/// Attach to any GameObject in the scene and call from Start.
/// No prefabs needed — everything is procedural.
/// </summary>
public class EnvironmentSetup : MonoBehaviour
{
    [Header("Obstacles")]
    public int obstacleCount = 5;
    public float ringRadius = 20f;
    public float obstacleScale = 2f;
    public Material obstacleMaterial;   // assign any material; null = default

    [Header("Target Markers (Tap-To-Move visual aid)")]
    public bool showTargetRing = true;

    void Start()
    {
        SpawnObstacles();
        if (showTargetRing) SpawnTargetRing();
    }

    void SpawnObstacles()
    {
        for (int i = 0; i < obstacleCount; i++)
        {
            float angle = i * (360f / obstacleCount) * Mathf.Deg2Rad;
            Vector3 pos = new Vector3(
                Mathf.Sin(angle) * ringRadius,
                Random.Range(-3f, 3f),
                Mathf.Cos(angle) * ringRadius);

            GameObject obs = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            obs.name = $"Obstacle_{i}";
            obs.transform.position = pos;
            obs.transform.localScale = Vector3.one * obstacleScale;

            if (obstacleMaterial != null)
                obs.GetComponent<Renderer>().material = obstacleMaterial;

            // Tag as obstacle so InputBridge raycast can hit it
            obs.layer = LayerMask.NameToLayer("Default");
        }
    }

    void SpawnTargetRing()
    {
        // A flat disc on the XZ plane as a visual floor reference
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        floor.name = "FloorDisc";
        floor.transform.position = Vector3.zero;
        floor.transform.localScale = new Vector3(ringRadius * 2.5f, 0.05f, ringRadius * 2.5f);
        floor.GetComponent<Collider>().enabled = true;  // needed for tap raycast
        floor.layer = LayerMask.NameToLayer("Default");

        // Make it semi-transparent if possible
        Renderer r = floor.GetComponent<Renderer>();
        r.material.color = new Color(0.1f, 0.1f, 0.3f, 0.5f);
    }
}
