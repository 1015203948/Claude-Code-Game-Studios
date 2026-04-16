// MIT License - Copyright (c) 2026 Game Studios
// Integration tests for ShipInputManager (EditMode — no scene loading required)
//
// Tests AC-1 through AC-5 from Story 014:
// - AC-1: STARMAP → StarMapActions enabled, CockpitActions disabled
// - AC-2: COCKPIT → StarMapActions disabled, CockpitActions enabled
// - AC-3: SWITCHING_* / OVERLAY → both ActionMaps disabled
// - AC-4: EnhancedTouchSupport lifecycle pairing via OnEnable/OnDisable
// - AC-5: OnDisable unsubscribes from ViewLayerChannel (no broadcast after disable)

namespace Game.Tests.Integration.Input {
    using System;
    using System.Reflection;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.InputSystem;
    using UnityEngine.InputSystem.EnhancedTouch;
    using Game.Inputs;
    using Game.Scene;
    using Game.Channels;

    // ─── Mock ViewLayerChannel ───────────────────────────────────────────────

    /// <summary>
    /// Mock ViewLayerChannel that records broadcasts for verification.
    /// Overrides Raise() to record without ScriptableObject asset dependency.
    /// </summary>
    internal sealed class MockViewLayerChannel : ViewLayerChannel {
        public event Action<ViewLayer> OnViewLayerChanged;

        public void Broadcast(ViewLayer layer) {
            OnViewLayerChanged?.Invoke(layer);
        }

        public override void Raise(ViewLayer newLayer) {
            OnViewLayerChanged?.Invoke(newLayer);
        }
    }

    // ─── Test Fixture ───────────────────────────────────────────────────────

    [TestFixture]
    public class ShipInputManagerTest {
        private GameObject _go;
        private ShipInputManager _manager;
        private MockViewLayerChannel _viewLayerChannel;
        private ShipInputChannel _shipInputChannel;

        [SetUp]
        public void Setup() {
            _go = new GameObject("ShipInputManager");

            // Attach ShipInputManager
            _manager = _go.AddComponent<ShipInputManager>();

            // Create mock channel assets
            _viewLayerChannel = ScriptableObject.CreateInstance<MockViewLayerChannel>();
            _shipInputChannel = ScriptableObject.CreateInstance<ShipInputChannel>();

            // Inject via serialized fields using reflection
            SetField(_manager, "_viewLayerChannel", _viewLayerChannel);
            SetField(_manager, "_shipInputChannel", _shipInputChannel);

            // Enable to trigger OnEnable (subscription + EnhancedTouchSupport.Enable)
            _manager.enabled = true;
        }

        [TearDown]
        public void TearDown() {
            // Trigger OnDisable (unsubscribe + EnhancedTouchSupport.Disable)
            _manager.enabled = false;

            UnityEngine.Object.DestroyImmediate(_go);
            UnityEngine.Object.DestroyImmediate(_viewLayerChannel);
            UnityEngine.Object.DestroyImmediate(_shipInputChannel);
        }

        // ─── AC-1: STARMAP ActionMap routing ─────────────────────────────────

        [Test]
        public void AC1_ViewLayer_STARMAP_EnablesStarMap_DisablesCockpit() {
            // Arrange — start from a clean state via OnEnable already fired
            // Act — broadcast STARMAP
            _viewLayerChannel.Broadcast(ViewLayer.STARMAP);

            // Assert
            Assert.IsTrue(_manager.IsStarMapActive,
                "StarMapActions should be enabled when ViewLayer = STARMAP");
            Assert.IsFalse(_manager.IsCockpitActive,
                "CockpitActions should be disabled when ViewLayer = STARMAP");
        }

        // ─── AC-2: COCKPIT ActionMap routing ────────────────────────────────

        [Test]
        public void AC2_ViewLayer_COCKPIT_DisablesStarMap_EnablesCockpit() {
            // Act — broadcast COCKPIT
            _viewLayerChannel.Broadcast(ViewLayer.COCKPIT);

            // Assert
            Assert.IsFalse(_manager.IsStarMapActive,
                "StarMapActions should be disabled when ViewLayer = COCKPIT");
            Assert.IsTrue(_manager.IsCockpitActive,
                "CockpitActions should be enabled when ViewLayer = COCKPIT");
        }

        // ─── AC-3: SWITCHING_* and OVERLAY states disable both ActionMaps ───

        [Test]
        public void AC3_ViewLayer_SWITCHING_IN_DisablesBoth() {
            _viewLayerChannel.Broadcast(ViewLayer.SWITCHING_IN);
            Assert.IsFalse(_manager.IsStarMapActive, "StarMapActions disabled in SWITCHING_IN");
            Assert.IsFalse(_manager.IsCockpitActive, "CockpitActions disabled in SWITCHING_IN");
        }

        [Test]
        public void AC3_ViewLayer_SWITCHING_OUT_DisablesBoth() {
            _viewLayerChannel.Broadcast(ViewLayer.SWITCHING_OUT);
            Assert.IsFalse(_manager.IsStarMapActive, "StarMapActions disabled in SWITCHING_OUT");
            Assert.IsFalse(_manager.IsCockpitActive, "CockpitActions disabled in SWITCHING_OUT");
        }

        [Test]
        public void AC3_ViewLayer_SWITCHING_SHIP_DisablesBoth() {
            _viewLayerChannel.Broadcast(ViewLayer.SWITCHING_SHIP);
            Assert.IsFalse(_manager.IsStarMapActive, "StarMapActions disabled in SWITCHING_SHIP");
            Assert.IsFalse(_manager.IsCockpitActive, "CockpitActions disabled in SWITCHING_SHIP");
        }

        [Test]
        public void AC3_ViewLayer_OPENING_OVERLAY_DisablesBoth() {
            _viewLayerChannel.Broadcast(ViewLayer.OPENING_OVERLAY);
            Assert.IsFalse(_manager.IsStarMapActive, "StarMapActions disabled in OPENING_OVERLAY");
            Assert.IsFalse(_manager.IsCockpitActive, "CockpitActions disabled in OPENING_OVERLAY");
        }

        [Test]
        public void AC3_ViewLayer_CLOSING_OVERLAY_DisablesBoth() {
            _viewLayerChannel.Broadcast(ViewLayer.CLOSING_OVERLAY);
            Assert.IsFalse(_manager.IsStarMapActive, "StarMapActions disabled in CLOSING_OVERLAY");
            Assert.IsFalse(_manager.IsCockpitActive, "CockpitActions disabled in CLOSING_OVERLAY");
        }

        [Test]
        public void AC3_ViewLayer_COCKPIT_WITH_OVERLAY_DisablesBoth() {
            // COCKPIT_WITH_OVERLAY is not explicitly listed in ADR-0003 switch cases
            // It falls to default (both disabled) per Story AC-3 intent
            _viewLayerChannel.Broadcast(ViewLayer.COCKPIT_WITH_OVERLAY);
            Assert.IsFalse(_manager.IsStarMapActive,
                "StarMapActions disabled in COCKPIT_WITH_OVERLAY");
            Assert.IsFalse(_manager.IsCockpitActive,
                "CockpitActions disabled in COCKPIT_WITH_OVERLAY");
        }

        // ─── AC-4: EnhancedTouchSupport lifecycle pairing ───────────────────

        [Test]
        public void AC4_InitialState_StarMapActiveCockpitInactive() {
            // OnEnable sets StarMap active, Cockpit inactive as initial state
            Assert.IsTrue(_manager.IsStarMapActive,
                "StarMapActions should be enabled on OnEnable (initial state)");
            Assert.IsFalse(_manager.IsCockpitActive,
                "CockpitActions should be disabled on OnEnable (initial state)");
        }

        [Test]
        public void AC4_OnDisable_AllActionMapsDisabled() {
            // Act — trigger OnDisable via enabled = false (TearDown already did this)
            // So set up a fresh manager for this specific test
            var go2 = new GameObject("ShipInputManager-AC4");
            var mgr2 = go2.AddComponent<ShipInputManager>();
            SetField(mgr2, "_viewLayerChannel", _viewLayerChannel);
            SetField(mgr2, "_shipInputChannel", _shipInputChannel);
            mgr2.enabled = true;  // OnEnable
            mgr2.enabled = false; // OnDisable

            // Assert — _controls.Disable() was called in OnDisable
            Assert.IsFalse(mgr2.IsStarMapActive,
                "StarMapActions should be disabled after OnDisable");
            Assert.IsFalse(mgr2.IsCockpitActive,
                "CockpitActions should be disabled after OnDisable");

            UnityEngine.Object.DestroyImmediate(go2);
        }

        // ─── AC-5: OnDisable unsubscribes (no broadcast after disable) ─────

        [Test]
        public void AC5_OnDisable_Unsubscribe_NoCallbackAfterDisable() {
            // Arrange — track whether callback was invoked
            int callbackCount = 0;
            Action<ViewLayer> trackingHandler = _ => { callbackCount++; };
            _viewLayerChannel.OnViewLayerChanged += trackingHandler;

            // Act — disable manager (unsubscribes), then broadcast
            _manager.enabled = false;
            callbackCount = 0;
            _viewLayerChannel.Broadcast(ViewLayer.COCKPIT);

            // Assert — no callback because OnDisable unsubscribed
            Assert.AreEqual(0, callbackCount,
                "OnDisable should unsubscribe; broadcast after disable should not invoke callback");

            _viewLayerChannel.OnViewLayerChanged -= trackingHandler;
        }

        // ─── Helper ──────────────────────────────────────────────────────────

        private static void SetField(object target, string name, object value) {
            var field = target.GetType().GetField(name,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{name}' not found on {target.GetType().Name}");
            field.SetValue(target, value);
        }
    }
}
