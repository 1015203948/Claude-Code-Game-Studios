using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Channels;
using Game.Data;
using Game.Gameplay;
using Game.Inputs;

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

        /// <summary>Test hook: resets Instance to null for test isolation. Do NOT use in production.</summary>
        internal static void ResetInstanceForTest() => Instance = null;

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
            if (_cts == null) _cts = new CancellationTokenSource();
            _ = SWITCHING_IN_SequenceAsync(shipId, _cts.Token);
        }

        /// <summary>Request to return to star map from cockpit. Triggers SWITCHING_OUT.</summary>
        public void RequestReturnToStarMap() {
            if (_isSwitching) return;
            if (_cts == null) _cts = new CancellationTokenSource();
            _ = SWITCHING_OUT_SequenceAsync(_cts.Token);
        }

        /// <summary>
        /// Request to switch active ship while in cockpit overlay.
        /// Triggers SWITCHING_SHIP. ViewLayer stays at COCKPIT throughout.
        /// </summary>
        public void RequestSwitchShip(string targetShipId) {
            if (_isSwitching) return;
            if (string.IsNullOrEmpty(targetShipId)) return;
            if (_cts == null) _cts = new CancellationTokenSource();
            _ = SWITCHING_SHIP_SequenceAsync(targetShipId, _cts.Token);
        }

        /// <summary>Request to open star map overlay on top of cockpit. Triggers OPENING_OVERLAY.</summary>
        public void RequestOpenOverlay() {
            if (_isSwitching) return;
            if (_cts == null) _cts = new CancellationTokenSource();
            _ = OPENING_OVERLAY_SequenceAsync(_cts.Token);
        }

        /// <summary>Request to close star map overlay. Triggers CLOSING_OVERLAY.</summary>
        public void RequestCloseOverlay() {
            if (_isSwitching) return;
            if (_cts == null) _cts = new CancellationTokenSource();
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
                // 先检查场景是否已在 Editor 中预加载
                var existingCockpit = SceneManager.GetSceneByName("CockpitScene");
                AsyncOperation loadOp;
                bool needsLoad;

                if (existingCockpit.isLoaded) {
                    loadOp = null;
                    needsLoad = false;
                    Debug.Log("[ViewLayerManager] CockpitScene already loaded, reusing.");
                } else {
                    loadOp = SceneManager.LoadSceneAsync("CockpitScene", LoadSceneMode.Additive);
                    loadOp.allowSceneActivation = false;
                    needsLoad = true;
                }

                // Step 6: ShipState → IN_COCKPIT
                shipData.SetState(ShipState.IN_COCKPIT);

                // Step 8: allowSceneActivation = true; wait for scene fully activated
                if (needsLoad && loadOp != null) {
                    loadOp.allowSceneActivation = true;
                    await WaitUntil(() => loadOp.isDone, ct);
                }

                // Step 9: ViewLayer → COCKPIT; broadcast
                CurrentViewLayer = ViewLayer.COCKPIT;
                _viewLayerChannel?.Raise(ViewLayer.COCKPIT);

                // Step 10: Auto-find cockpit camera if not bound (CockpitScene loaded dynamically)
                if (_cockpitCamera == null) {
                    var mainCam = GameObject.FindGameObjectWithTag("MainCamera");
                    if (mainCam != null) _cockpitCamera = mainCam.GetComponent<Camera>();
                }

                // Camera switch
                if (_starMapCamera != null) _starMapCamera.enabled = false;
                if (_cockpitCamera != null) _cockpitCamera.enabled = true;

                // Wire CockpitScene components (loaded dynamically, not available at bootstrap)
                WireCockpitScene();

                // Trigger combat encounter
                CombatSystem.Instance?.BeginCombat(_activeShipId, "combat_node");

                // Mask FadeOut
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
                // 清理战斗状态和敌人（必须在卸载场景前执行）
                CombatSystem.Instance?.EndCombat();

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

                // Step 10: Wait for scene fully activated
                loadOp.allowSceneActivation = true;
                await WaitUntil(() => loadOp.isDone, ct);

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
        private async Task WaitUntil(Func<bool> condition, CancellationToken ct) {
            while (!condition()) {
                ct.ThrowIfCancellationRequested();
                await Task.Yield();
            }
        }

        /// <summary>
        /// Wires CockpitScene components after scene load.
        /// Injects channels and cross-references that can't be set at bootstrap time.
        /// </summary>
        private void WireCockpitScene() {
            var scs = ShipControlSystem.Instance;
            if (scs == null) {
                // Domain reload 后静态 Instance 被重置，但 Awake 不会重新调用 — 通过场景搜索恢复
                scs = UnityEngine.Object.FindAnyObjectByType<ShipControlSystem>();
                if (scs != null) {
                    var prop = typeof(ShipControlSystem).GetProperty("Instance",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    prop?.SetValue(null, scs);
                    Debug.Log("[ViewLayerManager] WireCockpitScene: Recovered ShipControlSystem.Instance after domain reload.");
                }
            }
            if (scs == null) {
                Debug.LogError("[ViewLayerManager] WireCockpitScene: ShipControlSystem not found in scene!");
                return;
            }
            Debug.Log($"[ViewLayerManager] WireCockpitScene: scs={scs.name}, _activeShipId={_activeShipId}");

            // Wire DualJoystickInput
            var joystick = scs.GetComponent<DualJoystickInput>();
            if (joystick == null) {
                joystick = scs.gameObject.AddComponent<DualJoystickInput>();
                Debug.Log($"[ViewLayerManager] Added DualJoystickInput to {scs.name}");
            } else {
                Debug.Log($"[ViewLayerManager] Found existing DualJoystickInput on {scs.name}");
            }
            SetField(scs, "_dualJoystick", joystick);

            // 注入 channel 到 DualJoystickInput（AddComponent 后立即注入，避免 OnEnable NRE）
            if (_viewLayerChannel != null) {
                SetField(joystick, "_viewLayerChannel", _viewLayerChannel);
            }
            // 手动设置 _isInCockpit = true（OnEnable 时 channel 为 null 导致订阅失败，需直接设置）
            SetField(joystick, "_isInCockpit", true);

            // 查找 ShipInputChannel 并注入
            var inputChannel = UnityEngine.Object.FindAnyObjectByType<ShipInputChannel>();
            if (inputChannel != null) {
                SetField(joystick, "_shipInputChannel", inputChannel);
            }

            // Wire CameraRig
            var rig = UnityEngine.Object.FindAnyObjectByType<CameraRig>();
            if (rig != null) {
                SetField(scs, "_cameraRig", rig);

                // 配置 CameraRig 引用（CockpitScene 中 Inspector 未设置）
                var mainCam = GameObject.FindGameObjectWithTag("MainCamera");
                if (mainCam != null) {
                    SetField(rig, "_camera", mainCam.GetComponent<Camera>());
                }
                SetField(rig, "_targetShip", scs.transform);

                // 动态创建 CockpitAnchor（CockpitScene 中不存在）
                var anchor = scs.transform.Find("CockpitAnchor");
                if (anchor == null) {
                    var anchorGO = new GameObject("CockpitAnchor");
                    anchorGO.transform.SetParent(scs.transform, false);
                    anchorGO.transform.localPosition = new Vector3(0f, 2f, 0f);
                    anchor = anchorGO.transform;
                }
                SetField(rig, "_cockpitAnchor", anchor);

                Debug.Log($"[ViewLayerManager] CameraRig wired: camera={(mainCam != null ? mainCam.name : "null")}, target={scs.name}, anchor={anchor.name}");
            }

            // Wire channels from GameBootstrap
            var bootstrap = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
            if (bootstrap != null) {
                bootstrap.WireCockpitComponents(scs);
            }

            // 手动启用 ShipControlSystem 输入（OnEnable 时 channel 为 null 导致订阅失败）
            SetField(scs, "_inputEnabled", true);
            SetField(scs, "_activeShipId", _activeShipId);

            // 缓存飞船参数
            var shipData = GameDataManager.Instance?.GetShip(_activeShipId);
            if (shipData != null) {
                SetField(scs, "_cachedThrustPower", shipData.GetThrustPower());
                SetField(scs, "_cachedTurnSpeed", shipData.GetTurnSpeed());
            }

            // CameraRig → THIRD_PERSON
            if (rig != null) {
                rig.SwitchMode(CameraRig.CameraMode.THIRD_PERSON);
            }

            // 强制重新激活 Rigidbody（防止之前 DESTROYED 状态遗留 kinematic）
            var rb = scs.GetComponent<Rigidbody>();
            if (rb != null) {
                rb.isKinematic = false;
                rb.constraints = RigidbodyConstraints.None;
                Debug.Log($"[ViewLayerManager] Rigidbody reset: isKinematic={rb.isKinematic}, constraints={rb.constraints}");
            }

            Debug.Log("[ViewLayerManager] CockpitScene components wired.");
        }

        private static void SetField(object target, string fieldName, object value) {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance);
            if (field != null) field.SetValue(target, value);
        }
    }
}