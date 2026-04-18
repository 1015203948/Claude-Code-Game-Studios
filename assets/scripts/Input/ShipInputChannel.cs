using System;
using UnityEngine;

namespace Game.Inputs {
    /// <summary>
    /// Tier 1 SO Channel — broadcasts cockpit input events to other scenes.
    /// Events: OnThrustChanged (float normalized magnitude [0,1]),
    ///         OnAimChanged (Vector2 normalized direction [-1,1]).
    /// Created per ADR-0003; consumed by ShipInputManager (Story 014) and
    /// ShipControlSystem (Story 016).
    /// </summary>
    [CreateAssetMenu(menuName = "Channels/ShipInputChannel")]
    public class ShipInputChannel : ScriptableObject {
        public event Action<float> OnThrustChanged;
        public event Action<Vector2> OnAimChanged;

        public void RaiseThrust(float normalizedThrust) {
            OnThrustChanged?.Invoke(normalizedThrust);
        }

        public void RaiseAim(Vector2 normalizedAim) {
            OnAimChanged?.Invoke(normalizedAim);
        }
    }
}
