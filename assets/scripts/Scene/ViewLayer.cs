namespace Game.Scene {
    /// <summary>
    /// View layer states for the dual-perspective switching system.
    /// State transitions:
    ///   STARMAP → SWITCHING_IN → COCKPIT
    ///   COCKPIT → SWITCHING_OUT → STARMAP
    ///   COCKPIT → OPENING_OVERLAY → COCKPIT_WITH_OVERLAY
    ///   COCKPIT_WITH_OVERLAY → CLOSING_OVERLAY → COCKPIT
    ///   COCKPIT_WITH_OVERLAY → SWITCHING_SHIP → COCKPIT (overlay ship change, stays in COCKPIT)
    /// </summary>
    public enum ViewLayer {
        STARMAP,
        COCKPIT,
        COCKPIT_WITH_OVERLAY,
        SWITCHING_IN,
        SWITCHING_OUT,
        OPENING_OVERLAY,
        CLOSING_OVERLAY,
        SWITCHING_SHIP
    }
}
