using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// Abstraction over Unity Physics.RaycastNonAlloc for testability.
    /// Combat code consumes this interface; production uses UnityPhysicsQuery.
    /// Tests inject a mock to avoid scene setup.
    /// </summary>
    public interface IPhysicsQuery
    {
        /// <summary>
        /// Cast a ray and fill the provided buffer with hits.
        /// Returns the number of hits (capped by buffer length).
        /// </summary>
        int Raycast(Vector3 origin, Vector3 direction, RaycastHit[] buffer, float maxDistance);
    }

    /// <summary>
    /// Production implementation wrapping Physics.RaycastNonAlloc.
    /// </summary>
    public class UnityPhysicsQuery : IPhysicsQuery
    {
        public int Raycast(Vector3 origin, Vector3 direction, RaycastHit[] buffer, float maxDistance)
        {
            return Physics.RaycastNonAlloc(origin, direction, buffer, maxDistance);
        }
    }
}
