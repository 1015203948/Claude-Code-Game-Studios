using UnityEngine;
using Game.Channels;

namespace Gameplay {
    /// <summary>
    /// Strategy layer time singleton.
    ///
    /// DeltaTime formula: SimClock.Instance.DeltaTime = Time.unscaledDeltaTime × SimRate
    /// Time.timeScale is NEVER modified — cockpit Rigidbody physics use Time.deltaTime.
    ///
    /// SimRate domain: {0, 1, 5, 20} only. Invalid values are rejected.
    ///
    /// Script Execution Order must be -1000 (earliest) so strategy systems
    /// read correct DeltaTime from the first frame.
    /// Set via: Edit → Project Settings → Script Execution Order → SimClock = -1000
    /// </summary>
    public class SimClock : MonoBehaviour {
        public static SimClock Instance { get; private set; }

        /// <summary>
        /// Current simulation rate.
        /// Valid values: 0 (pause), 1 (1x), 5 (5x), 20 (20x).
        /// </summary>
        public float SimRate { get; private set; } = 1f;

        /// <summary>
        /// Strategy layer delta time = Time.unscaledDeltaTime × SimRate.
        /// This is a property (computed every access) — NOT cached.
        /// </summary>
        public float DeltaTime => Time.unscaledDeltaTime * SimRate;

        [Header("Channel References")]
        [SerializeField] private SimRateChangedChannel _simRateChangedChannel;

        private static readonly float[] ValidRates = { 0f, 1f, 5f, 20f };

        private void Awake() {
            if (Instance != null && Instance != this) {
                Debug.LogWarning("[SimClock] Duplicate SimClock detected. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>Test hook: resets Instance to null for test isolation. Do NOT use in production.</summary>
        internal static void ResetInstanceForTest() => Instance = null;

        /// <summary>
        /// Set the simulation rate. Only {0, 1, 5, 20} are valid.
        /// Invalid values are silently ignored (SimRate unchanged, no broadcast).
        /// Valid values broadcast SimRateChangedChannel.
        /// </summary>
        public void SetRate(float rate) {
            if (!IsValidRate(rate)) {
                Debug.LogWarning($"[SimClock] SetRate({rate}): invalid rate. Valid: {{0, 1, 5, 20}}.");
                return;
            }
            if (SimRate == rate) return; // no-op if already set

            SimRate = rate;
            _simRateChangedChannel.Raise(rate);
        }

        /// <summary>
        /// Returns true if rate is in the valid domain {0, 1, 5, 20}.
        /// </summary>
        public static bool IsValidRate(float rate) {
            foreach (var r in ValidRates) {
                if (Mathf.Approximately(rate, r)) return true;
            }
            return false;
        }
    }
}
