using UnityEngine;

namespace Game.Scene {
    /// <summary>
    /// Accessibility settings for the game.
    /// TODO: Load from player preferences or config file.
    /// </summary>
    public static class AccessibilitySettings {
        /// <summary>
        /// When true, disables animations and transitions for players sensitive to motion.
        /// </summary>
        public static bool ReduceMotion => false;
    }
}