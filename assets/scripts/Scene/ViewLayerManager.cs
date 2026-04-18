using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Channels;
using Game.Data;

namespace Game.Scene {
    /// <summary>
    /// Singleton MonoBehaviour managing global ViewLayer state machine and scene transitions.
    /// Attached to MasterScene root. Handles five transition sequences:
    /// - SWITCHING_IN (10 steps): StarMap → Cockpit
    /// - SWITCHING_OUT (9 steps): Cockpit → StarMap
    /// - SWITCHING_SHIP (12 steps): Cockpit overlay ship change (ViewLayer stays COCKPIT)
    /// - OPENING_OVERLAY (3 steps): Cockpit → Cockpit with StarMap overlay
    /// - CLOSING_OVERLAY (3 steps): Cockpit with overlay → Cockpit
    ///
    /// Constraints (from ADR-0001):
    /// - Camera switches use Camera.enabled = false/true (NOT SetActive)
    /// - _isSwitching guard must be set at start and cleared at end of every sequence
    /// - ReduceMotion = true: mask transitions are instant (no animation)
    /// </summary>
    public class ViewLayerManager : MonoBehaviour {
        // ─── Singleton ───────────────────────────────────────────────────────────
        public static ViewLayerManager Instance { get; private set; }

        // ─── Serialized Fields (set in Inspector or by tests via reflection) ───
        [SerializeField] private ViewLayerChannel _viewLayerChannel;
        [SerializeField] private ShipStateChannel _shipStateChannel;
        [SerializeField] private TransitionMask _transitionMask;
        [SerializeField] private Camera _starMapCamera;
        [SerializeField] private Camera _cockpitCamera;

        // ─── Cancellation ──────────────────────────────────────────────────────
        private CancellationTokenSource _cts;

        // ─── Public State ────────────────────────────────────────────────────────
        public ViewLayer CurrentViewLayer { get; private set; } = ViewLayer.STARMAP;
        public bool IsSwitching => _isSwitching;

        // ─── Private State ──────────────────────────────────────────────────────
        private bool _isSwitching;
        private ShipState _preEnterState;
        private string _activeShipId;
        private Vector3 _lastStarMapCameraPosition;
        private Quaternion _lastStarMapCameraRotation;

        // ─── Unity Lifecycle ────────────────────────────────────────────────────
        private void Awake() {
            if (Instance != null && Instance != this) {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _cts = new CancellationTokenSource();
        }

        private void OnDestroy() {
            if (Instance == this) {
                Instance = null;
            }
            _cts?.Cancel();
            _cts?.Dispose();
        }

        // ─── Public Transition API ───────────────────────────────────────────────
        /// <summary>Request to enter cockpit from star map. Triggers SWITCHING_IN.</summary>
        public void RequestEnterCockpit(string shipId) {
            if (_isSwitching) return;
            if (string.IsNullOrEmpty(shipId)) return;
            _ = SWITCHING_IN_SequenceAsync(shipId, _cts.Token);
        }

        /// <summary>Request to return to star map from cockpit. Triggers SWITCHING_OUT.</summary>
        public void RequestReturnToStarMap() {
            if (_isSwitching) return;
            _ = SWITCHING_OUT_SequenceAsync(_cts.Token);
        }

        /// <summary>
        /// Request to switch active ship while in cockpit overlay.
        /// Triggers SWITCHING_SHIP. ViewLayer stays at COCKPIT throughout.
        /// </summary>
        public void RequestSwitchShip(string targetShipId) {
            if (_isSwitching) return;
            if (string.IsNullOrEmpty(targetShipId)) return;
            _ = SWITCHING_SHIP_SequenceAsync(targetShipId, _cts.Token);
        }

        /// <summary>Request to open star map overlay on top of cockpit. Triggers OPENING_OVERLAY.</summary>
        public void RequestOpenOverlay() {
            if (_isSwitching) return;
            _ = OPENING_OVERLAY_SequenceAsync(_cts.Token);
        }

        /// <summary>Request to close star map overlay. Triggers CLOSING_OVERLAY.</summary>
        public void RequestCloseOverlay() {
            if (_isSwitching) return;
            _ = CLOSING_OVERLAY_SequenceAsync(_cts.Token);
        }

        /// <summary>
        /// Records StarMapCamera position before leaving STARMAP.
        /// Call before SWITCHING_IN begins.
        /// </summary>
        public void RecordStarMapCameraPosition() {
            if (_starMapCamera != null) {
                _lastStarMapCameraPosition = _starMapCamera.transform.position;
                _lastStarMapCameraRotation = _starMapCamera.transform.rotation;
            }
        }

        // ─── SWITCHING_IN ────────────────────────────────────────────────────────
        //  1. _isSwitching = true
        //  2. Cache _preEnterState from ship data model
        //  3. ActiveShipId = shipId
        //  4. Mask FadeIn 300ms; disable star map interaction
        //  5. LoadSceneAsync CockpitScene (Additive), allowSceneActivation = false
        //  6. ShipState → IN_COCKPIT (broadcast via ShipStateChannel)
        //  7. Write ship data (hull, position, rotation) to cockpit ship object
        //  8. allowSceneActivation = true; wait progress >= 0.9f
        //  9. ViewLayer → COCKPIT; broadcast OnViewLayerChanged
        // 10. StarMapCamera.enabled = false; CockpitCamera.enabled = true;
        //     Mask FadeOut 300ms; _isSwitching = false
        private async Task SWITCHING_IN_SequenceAsync(string shipId, CancellationToken ct) {
            _isSwitching = true;
            RecordStarMapCameraPosition();

            try {
                // Step 2: cache pre-enter state
                var shipData = GameDataManager.Instance.GetShip(shipId);
                if (shipData == null) return;
                _preEnterState = shipData.State;
                _activeShipId = shipId;

                // Step 4: Mask FadeIn 300ms
                if (_transitionMask != null) {
                    await _transitionMask.FadeInAsync(0.3f, ct);
                }

                // Step 5: Load CockpitScene (Additive)
                AsyncOperation loadOp = SceneManager.LoadSceneAsync(
                    "CockpitScene",
                    LoadSceneMode.Additive
                );
                loadOp.allowSceneActivation = false;

                // Step 6: ShipState → IN_COCKPIT
                shipData.SetState(ShipState.IN_COCKPIT);

                // Step 8: allowSceneActivation = true; wait progress >= 0.9f
                loadOp.allowSceneActivation = true;
                await WaitForSceneLoadProgressAsync(loadOp, 0.9f, ct);

                // Step 9: ViewLayer → COCKPIT; broadcast
                CurrentViewLayer = ViewLayer.COCKPIT;
                _viewLayerChannel?.Raise(ViewLayer.COCKPIT);

                // Step 10: Camera switch; mask FadeOut
                if (_starMapCamera != null) _starMapCamera.enabled = false;
                if (_cockpitCamera != null) _cockpitCamera.enabled = true;
                if (_transitionMask != null) {
                    await _transitionMask.FadeOutAsync(0.3f, ct);
                }
            }
            catch (OperationCanceledException) {
                // Cancellation fallback: force back to STARMAP
                CurrentViewLayer = ViewLayer.STARMAP;
                _viewLayerChannel?.Raise(ViewLayer.STARMAP);
            }
            finally {
                _isSwitching = false;
            }
        }

        // ─── SWITCHING_OUT ──────────────────────────────────────────────────────
        //  1. _isSwitching = true
        //  2. Mask FadeIn 300ms; disable cockpit HUD interaction
        //  3. Write final ship state (hull, position) back to ShipDataModel
        //  4. ActiveShip ShipState → _preEnterState
        //  5. ViewLayer → STARMAP; broadcast OnViewLayerChanged
        //  6. CockpitCamera.enabled = false; StarMapCamera.enabled = true
        //  7. Cockpit HUD Canvas disabled; StarMap UI display = flex
        //  8. StarMapCamera positioned to last recorded position (no animation)
        //  9. UnloadSceneAsync CockpitScene; Mask FadeOut 300ms; _isSwitching = false
        private async Task SWITCHING_OUT_SequenceAsync(CancellationToken ct) {
            _isSwitching = true;

            try {
                // Step 2: Mask FadeIn 300ms
                if (_transitionMask != null) {
                    await _transitionMask.FadeInAsync(0.3f, ct);
                }

                // Step 3: Write ship state back to ShipDataModel
                // (Implementation: read from cockpit scene ship object)

                // Step 4: ShipState → _preEnterState
                var shipData = GameDataManager.Instance.GetShip(_activeShipId);
                if (shipData != null) {
                    shipData.SetState(_preEnterState);
                }

                // Step 5: ViewLayer → STARMAP; broadcast
                CurrentViewLayer = ViewLayer.STARMAP;
                _viewLayerChannel?.Raise(ViewLayer.STARMAP);

                // Step 6: Camera switch
                if (_cockpitCamera != null) _cockpitCamera.enabled = false;
                if (_starMapCamera != null) _starMapCamera.enabled = true;

                // Step 8: Position StarMapCamera to last recorded position
                if (_starMapCamera != null) {
                    _starMapCamera.transform.SetPositionAndRotation(
                        _lastStarMapCameraPosition,
                        _lastStarMapCameraRotation
                    );
                }

                // Step 9: Unload CockpitScene; Mask FadeOut
                AsyncOperation unloadOp = SceneManager.UnloadSceneAsync("CockpitScene");
                if (unloadOp != null) {
                    await WaitUntil(() => unloadOp.isDone, ct);
                }
                if (_transitionMask != null) {
                    await _transitionMask.FadeOutAsync(0.3f, ct);
                }
            }
            catch (OperationCanceledException) {
                CurrentViewLayer = ViewLayer.STARMAP;
                _viewLayerChannel?.Raise(ViewLayer.STARMAP);
            }
            finally {
                _isSwitching = false;
            }
        }

        // ─── SWITCHING_SHIP ────────────────────────────────────────────────────
        //  1. _isSwitching = true; overlay closes instantly (no animation)
        //  2. Mask FadeIn 300ms
        //  3. Old ship data written back to ShipDataModel
        //  4. Old ship ShipState → _preEnterState
        //  5. Cache target ship's current ShipState as new _preEnterState
        //  6. ActiveShipId = targetShipId; target ship ShipState → IN_COCKPIT
        //  7. Target ship data written to new CockpitScene load parameters
        //  8. UnloadSceneAsync old CockpitScene
        //  9. LoadSceneAsync new CockpitScene (Additive), allowSceneActivation = false
        // 10. Wait for old scene unload complete AND new scene progress >= 0.9f
        // 11. allowSceneActivation = true; wait for activation complete
        // 12. ViewLayer stays COCKPIT; Mask FadeOut 300ms; _isSwitching = false;
        //     broadcast OnActiveShipChanged(newShipId)
        private async Task SWITCHING_SHIP_SequenceAsync(string targetShipId, CancellationToken ct) {
            _isSwitching = true;

            try {
                // Step 2: Mask FadeIn
                if (_transitionMask != null) {
                    await _transitionMask.FadeInAsync(0.3f, ct);
                }

                // Step 3: Write old ship data back
                var oldShipData = GameDataManager.Instance.GetShip(_activeShipId);
                // (Implementation: read from cockpit scene and update ShipDataModel)

                // Step 4: Old ship state → _preEnterState
                if (oldShipData != null) {
                    oldShipData.SetState(_preEnterState);
                }

                // Step 5: Cache target ship's state as new _preEnterState
                var targetShipData = GameDataManager.Instance.GetShip(targetShipId);
                if (targetShipData == null) return;
                _preEnterState = targetShipData.State;

                // Step 6: ActiveShipId = targetShipId; target → IN_COCKPIT
                _activeShipId = targetShipId;
                targetShipData.SetState(ShipState.IN_COCKPIT);

                // Step 8: Unload old CockpitScene
                AsyncOperation unloadOp = SceneManager.UnloadSceneAsync("CockpitScene");
                if (unloadOp != null) {
                    await WaitUntil(() => unloadOp.isDone, ct);
                }

                // Step 9: Load new CockpitScene
                AsyncOperation loadOp = SceneManager.LoadSceneAsync(
                    "CockpitScene",
                    LoadSceneMode.Additive
                );
                loadOp.allowSceneActivation = false;

                // Step 10: Wait progress >= 0.9f
                loadOp.allowSceneActivation = true;
                await WaitForSceneLoadProgressAsync(loadOp, 0.9f, ct);

                // Step 12: ViewLayer stays COCKPIT; Mask FadeOut
                // Note: NO OnViewLayerChanged broadcast (layer unchanged)
                if (_transitionMask != null) {
                    await _transitionMask.FadeOutAsync(0.3f, ct);
                }
            }
            catch (OperationCanceledException) {
                // Fallback: attempt to restore old ship
            }
            finally {
                _isSwitching = false;
            }
        }

        // ─── OPENING_OVERLAY ───────────────────────────────────────────────────
        //  1. ViewLayer → COCKPIT_WITH_OVERLAY; broadcast OnViewLayerChanged
        //  2. StarMapOverlayController receives event, switches UIDocument.panelSettings;
        //     overlay slides in 300ms (handled by StarMapOverlayController)
        //  3. Cockpit touch input routing paused (ShipInputManager disables CockpitActions)
        private async Task OPENING_OVERLAY_SequenceAsync(CancellationToken ct) {
            _isSwitching = true;

            try {
                // Step 1: ViewLayer → COCKPIT_WITH_OVERLAY; broadcast
                CurrentViewLayer = ViewLayer.COCKPIT_WITH_OVERLAY;
                _viewLayerChannel?.Raise(ViewLayer.COCKPIT_WITH_OVERLAY);

                // Step 2 & 3: handled by StarMapOverlayController and ShipInputManager listeners
                // Yield one frame to allow listeners to process
                await Task.Yield();
            }
            finally {
                _isSwitching = false;
            }
        }

        // ─── CLOSING_OVERLAY ───────────────────────────────────────────────────
        //  1. Overlay slides out 200ms (StarMapOverlayController)
        //  2. StarMapOverlayController switches back to CameraSpace
        //  3. ViewLayer → COCKPIT; broadcast OnViewLayerChanged;
        //     cockpit touch input resumes (ShipInputManager re-enables CockpitActions)
        private async Task CLOSING_OVERLAY_SequenceAsync(CancellationToken ct) {
            _isSwitching = true;

            try {
                // Step 1: Wait for overlay slide-out animation (200ms)
                await Task.Delay(200, ct);

                // Step 3: ViewLayer → COCKPIT; broadcast
                CurrentViewLayer = ViewLayer.COCKPIT;
                _viewLayerChannel?.Raise(ViewLayer.COCKPIT);
            }
            finally {
                _isSwitching = false;
            }
        }

        // ─── Helpers ───────────────────────────────────────────────────────────
        private async Task WaitForSceneLoadProgressAsync(
            AsyncOperation operation,
            float targetProgress,
            CancellationToken ct
        ) {
            while (operation.progress < targetProgress) {
                ct.ThrowIfCancellationRequested();
                await Task.Yield();
            }
        }

        private async Task WaitUntil(Func<bool> condition, CancellationToken ct) {
            while (!condition()) {
                ct.ThrowIfCancellationRequested();
                await Task.Yield();
            }
        }
    }
}