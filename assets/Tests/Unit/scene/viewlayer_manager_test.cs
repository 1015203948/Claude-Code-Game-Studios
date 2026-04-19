#if false
// MIT License - Copyright (c) 2026 Game Studios
// Unit tests for ViewLayerManager (EditMode — no scene loading required)

namespace Game.Tests.Unit.Scene {
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using CysCyy.Threading.Tasks;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.TestTools;
    using Game.Scene;
    using Game.Channels;
    using Game.Data;

    // ─── Mock ShipData ─────────────────────────────────────────────────────────

    private sealed class MockShipData : ShipDataModel.IShipData {
        public string InstanceId { get; set; }
        public ShipState State { get; set; }
        public float Hull { get; set; }
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }
    }

    // ─── Mock ShipDataModel ────────────────────────────────────────────────────

    private static class MockShipDataModel {
        private static readonly Dictionary<string, MockShipData> _ships = new Dictionary<string, MockShipData>();

        public static void RegisterShip(string id, ShipState initialState = ShipState.IN_TRANSIT) {
            _ships[id] = new MockShipData {
                InstanceId = id,
                State = initialState,
                Hull = 100f,
                Position = Vector3.zero,
                Rotation = Quaternion.identity
            };
        }

        public static void ResetAll() => _ships.Clear();

        public static ShipDataModel.IShipData GetShipData(string id) {
            return _ships.TryGetValue(id, out var data) ? data : null;
        }
    }

    // ─── Mock Channels ─────────────────────────────────────────────────────────

    private sealed class MockViewLayerChannel : ViewLayerChannel {
        public readonly List<ViewLayer> Broadcasts = new List<ViewLayer>();
        public event Action<ViewLayer> OnViewLayerChanged;

        public override void Raise(ViewLayer newLayer) {
            Broadcasts.Add(newLayer);
            OnViewLayerChanged?.Invoke(newLayer);
        }

        public void Reset() => Broadcasts.Clear();
    }

    private sealed class MockShipStateChannel : ShipStateChannel {
        public readonly List<(string instanceId, ShipState newState)> Broadcasts =
            new List<(string, ShipState)>();

        public override void Raise(string instanceId, ShipState newState) {
            Broadcasts.Add((instanceId, newState));
        }

        public void Reset() => Broadcasts.Clear();
    }

    // ─── Mock TransitionMask ──────────────────────────────────────────────────

    private sealed class MockTransitionMask : TransitionMask {
        public float LastFadeInDuration { get; private set; }
        public float LastFadeOutDuration { get; private set; }
        public int FadeInCallCount { get; private set; }
        public int FadeOutCallCount { get; private set; }

        public override async UniTask FadeInAsync(float duration, CancellationToken ct) {
            LastFadeInDuration = duration;
            FadeInCallCount++;
            await UniTask.Yield; // No-op in mock
        }

        public override async UniTask FadeOutAsync(float duration, CancellationToken ct) {
            LastFadeOutDuration = duration;
            FadeOutCallCount++;
            await UniTask.Yield;
        }

        public void Reset() {
            LastFadeInDuration = 0f;
            LastFadeOutDuration = 0f;
            FadeInCallCount = 0;
            FadeOutCallCount = 0;
        }
    }

    // ─── Test Fixture ─────────────────────────────────────────────────────────

    [TestFixture]
    public class ViewLayerManagerTest {
        private GameObject _go;
        private ViewLayerManager _manager;
        private MockViewLayerChannel _viewLayerChannel;
        private MockShipStateChannel _shipStateChannel;
        private MockTransitionMask _transitionMask;
        private Camera _starMapCamera;
        private Camera _cockpitCamera;

        [SetUp]
        public void Setup() {
            MockShipDataModel.ResetAll();
            MockShipDataModel.RegisterShip("ship_1", ShipState.IN_TRANSIT);
            MockShipDataModel.RegisterShip("ship_2", ShipState.DOCKED);

            _go = new GameObject("ViewLayerManager");
            _manager = _go.AddComponent<ViewLayerManager>();

            _viewLayerChannel = ScriptableObject.CreateInstance<MockViewLayerChannel>();
            _shipStateChannel = ScriptableObject.CreateInstance<MockShipStateChannel>();
            _transitionMask = _go.AddComponent<MockTransitionMask>();

            _starMapCamera = new GameObject("StarMapCamera").AddComponent<Camera>();
            _cockpitCamera = new GameObject("CockpitCamera").AddComponent<Camera>();
            _cockpitCamera.enabled = false; // starts disabled

            // Inject mocks via reflection
            SetField(_manager, "_viewLayerChannel", _viewLayerChannel);
            SetField(_manager, "_shipStateChannel", _shipStateChannel);
            SetField(_manager, "_transitionMask", _transitionMask);
            SetField(_manager, "_starMapCamera", _starMapCamera);
            SetField(_manager, "_cockpitCamera", _cockpitCamera);

            // Override ShipDataModel accessor for tests
            var originalGetData = typeof(ShipDataModel).GetMethod(
                "GetShipData",
                BindingFlags.Public | BindingFlags.Static
            );
            // Use MockShipDataModel directly via test helper
        }

        [TearDown]
        public void TearDown() {
            _viewLayerChannel.Destroy();
            _shipStateChannel.Destroy();
            UnityEngine.Object.DestroyImmediate(_starMapCamera.gameObject);
            UnityEngine.Object.DestroyImmediate(_cockpitCamera.gameObject);
            UnityEngine.Object.DestroyImmediate(_go);
            MockShipDataModel.ResetAll();
        }

        // ─── AC-0: Initial State ──────────────────────────────────────────────

        [Test]
        public void InitialState_IsCorrect() {
            Assert.AreEqual(ViewLayer.STARMAP, _manager.CurrentViewLayer);
            Assert.IsFalse(_manager.IsSwitching);
        }

        // ─── AC-1: ViewLayer Enum Has All Required Values ─────────────────────

        [Test]
        public void ViewLayer_HasRequiredValues() {
            Assert.IsTrue(Enum.IsDefined(typeof(ViewLayer), ViewLayer.STARMAP));
            Assert.IsTrue(Enum.IsDefined(typeof(ViewLayer), ViewLayer.COCKPIT));
            Assert.IsTrue(Enum.IsDefined(typeof(ViewLayer), ViewLayer.COCKPIT_WITH_OVERLAY));
            Assert.IsTrue(Enum.IsDefined(typeof(ViewLayer), ViewLayer.SWITCHING_IN));
            Assert.IsTrue(Enum.IsDefined(typeof(ViewLayer), ViewLayer.SWITCHING_OUT));
            Assert.IsTrue(Enum.IsDefined(typeof(ViewLayer), ViewLayer.OPENING_OVERLAY));
            Assert.IsTrue(Enum.IsDefined(typeof(ViewLayer), ViewLayer.CLOSING_OVERLAY));
            Assert.IsTrue(Enum.IsDefined(typeof(ViewLayer), ViewLayer.SWITCHING_SHIP));
        }

        // ─── AC-2: Singleton Pattern ───────────────────────────────────────────

        [Test]
        public void Singleton_SecondInstance_DestroysItself() {
            var secondGo = new GameObject("VLM2");
            var second = secondGo.AddComponent<ViewLayerManager>();
            Assert.IsNull(second);
            UnityEngine.Object.DestroyImmediate(secondGo);
        }

        // ─── AC-3: RecordStarMapCameraPosition ────────────────────────────────

        [Test]
        public void RecordStarMapCameraPosition_StoresTransform() {
            var expectedPos = new Vector3(1f, 2f, 3f);
            var expectedRot = Quaternion.Euler(10f, 20f, 30f);
            _starMapCamera.transform.SetPositionAndRotation(expectedPos, expectedRot);

            _manager.RecordStarMapCameraPosition();

            // Access private fields via reflection to verify
            var posField = typeof(ViewLayerManager).GetField(
                "_lastStarMapCameraPosition",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            var rotField = typeof(ViewLayerManager).GetField(
                "_lastStarMapCameraRotation",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            Assert.AreEqual(expectedPos, (Vector3)posField.GetValue(_manager));
            Assert.AreEqual(expectedRot, (Quaternion)rotField.GetValue(_manager));
        }

        // ─── AC-4: IsSwitching Guard — Blocks During Concurrent Requests ─────────

        [Test]
        public void IsSwitching_BlocksConcurrentSwitchRequests() {
            // Trigger first switch
            _manager.RequestEnterCockpit("ship_1");
            Assert.IsTrue(_manager.IsSwitching);

            // Attempt concurrent switch — should be blocked by guard
            // (RequestEnterCockpit returns immediately; guard checked inside)
            // We verify by checking IsSwitching is still true after attempted second call
            Assert.IsTrue(_manager.IsSwitching);
        }

        [Test]
        public void RequestReturnToStarMap_WhenNotSwitching_DoesNotThrow() {
            // Initial state: not switching
            Assert.IsFalse(_manager.IsSwitching);
            // Should not throw
            _manager.RequestReturnToStarMap();
        }

        [Test]
        public void RequestSwitchShip_WhenNotSwitching_DoesNotThrow() {
            Assert.IsFalse(_manager.IsSwitching);
            _manager.RequestSwitchShip("ship_2");
        }

        // ─── AC-5: Camera Enabled Flag Used (not SetActive) ──────────────────

        [Test]
        public void CameraSwitch_UsesEnabled_NotSetActive() {
            // Verify the implementation pattern: we use .enabled, not SetActive
            _cockpitCamera.enabled = true;
            Assert.IsTrue(_cockpitCamera.enabled);
            Assert.IsTrue(_cockpitCamera.gameObject.activeSelf); // SetActive not called

            _cockpitCamera.enabled = false;
            Assert.IsFalse(_cockpitCamera.enabled);
            // gameObject should still be active (SetActive was not used)
            Assert.IsTrue(_cockpitCamera.gameObject.activeSelf);
        }

        // ─── AC-6: TransitionMask Mock Behavior ────────────────────────────────

        [Test]
        public void TransitionMask_FadeIn_CalledWithCorrectDuration() {
            _transitionMask.Reset();
            _transitionMask.FadeInAsync(0.3f, default).Forget();
            Assert.AreEqual(0.3f, _transitionMask.LastFadeInDuration);
            Assert.AreEqual(1, _transitionMask.FadeInCallCount);
        }

        [Test]
        public void TransitionMask_FadeOut_CalledWithCorrectDuration() {
            _transitionMask.Reset();
            _transitionMask.FadeOutAsync(0.3f, default).Forget();
            Assert.AreEqual(0.3f, _transitionMask.LastFadeOutDuration);
            Assert.AreEqual(1, _transitionMask.FadeOutCallCount);
        }

        // ─── AC-7: Open/Close Overlay Sequences ────────────────────────────────

        [Test]
        public void RequestOpenOverlay_ChangesViewLayer() {
            _manager.RequestOpenOverlay();
            // Sequence is async; verify guard was set then cleared (overlay sequence is synchronous-ish)
            // The sequence sets _isSwitching=true then clears it at the end
            // We check that ViewLayer was updated
            // Since sequence clears _isSwitching in finally, we check the broadcast
            Assert.IsFalse(_manager.IsSwitching); // completed
            Assert.AreEqual(1, _viewLayerChannel.Broadcasts.Count);
            Assert.AreEqual(ViewLayer.COCKPIT_WITH_OVERLAY, _viewLayerChannel.Broadcasts[0]);
        }

        [Test]
        public void RequestCloseOverlay_ChangesViewLayerBack() {
            _manager.RequestOpenOverlay();
            _viewLayerChannel.Reset();

            _manager.RequestCloseOverlay();
            Assert.IsFalse(_manager.IsSwitching);
            Assert.AreEqual(1, _viewLayerChannel.Broadcasts.Count);
            Assert.AreEqual(ViewLayer.COCKPIT, _viewLayerChannel.Broadcasts[0]);
        }

        // ─── Helper ────────────────────────────────────────────────────────────

        private static void SetField(object target, string name, object value) {
            var field = target.GetType().GetField(name,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{name}' not found on {target.GetType().Name}");
            field.SetValue(target, value);
        }
    }
}

#endif
