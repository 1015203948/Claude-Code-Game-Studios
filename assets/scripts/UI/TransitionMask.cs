using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Game.UI {
    /// <summary>
    /// Full-screen transition mask for view layer switches.
    /// Attached to a UI Canvas covering the full screen (Sort Order = 100).
    /// Color = black (#000000).
    /// ADR-0001: Mask fades in/out during SWITCHING_IN, SWITCHING_OUT, SWITCHING_SHIP sequences.
    /// ReduceMotion: if AccessibilitySettings.ReduceMotion is true, transitions are instant.
    /// </summary>
    public abstract class TransitionMask : MonoBehaviour {
        /// <summary>Fades the mask in (transparent → black) over duration seconds.</summary>
        public abstract Task FadeInAsync(float duration, CancellationToken ct);

        /// <summary>Fades the mask out (black → transparent) over duration seconds.</summary>
        public abstract Task FadeOutAsync(float duration, CancellationToken ct);
    }
}
