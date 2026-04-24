using UnityEngine;

namespace Game.Channels {
    /// <summary>
    /// Payload for weapon fired events.
    /// </summary>
    public readonly struct WeaponFiredPayload {
        public Vector3 Origin { get; }
        public Vector3 Direction { get; }
        public Vector3? HitPoint { get; }
        public bool Hit { get; }
        public string TargetId { get; }
        public float Damage { get; }

        public WeaponFiredPayload(Vector3 origin, Vector3 direction, Vector3? hitPoint, bool hit, string targetId = null, float damage = 0f) {
            Origin = origin;
            Direction = direction;
            HitPoint = hitPoint;
            Hit = hit;
            TargetId = targetId;
            Damage = damage;
        }
    }

    /// <summary>
    /// Channel for weapon fired events — drives muzzle flash, hit sparks, audio.
    /// </summary>
    [CreateAssetMenu(menuName = "Channels/WeaponFiredChannel")]
    public class WeaponFiredChannel : GameEvent<WeaponFiredPayload> {
        public static WeaponFiredChannel Instance { get; private set; }

        private void OnEnable() {
            if (Instance != null && Instance != this) {
                Debug.LogWarning("[WeaponFiredChannel] Duplicate instance detected.");
            }
            Instance = this;
        }

        internal static void ResetInstanceForTest() => Instance = null;
    }
}
