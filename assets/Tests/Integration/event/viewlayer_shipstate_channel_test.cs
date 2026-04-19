using System;
using NUnit.Framework;
using UnityEngine;
using Game.Channels;
using Game.Scene;
using Game.Data;
using Object = UnityEngine.Object;

namespace Tests.Integration.Event {
    /// <summary>
    /// Integration tests for ViewLayerChannel and ShipStateChannel broadcast/consume.
    /// From story-007 QA Test Cases.
    /// </summary>
    [TestFixture]
    public class ViewLayerShipStateChannelTest {

        private ViewLayerChannel _viewLayerChannel;
        private ShipStateChannel _shipStateChannel;

        [SetUp]
        public void SetUp() {
            _viewLayerChannel = ScriptableObject.CreateInstance<ViewLayerChannel>();
            _shipStateChannel = ScriptableObject.CreateInstance<ShipStateChannel>();
        }

        [TearDown]
        public void TearDown() {
            Object.DestroyImmediate(_viewLayerChannel);
            Object.DestroyImmediate(_shipStateChannel);
        }

        // =====================================================================
        // AC-1: ViewLayerChannel broadcast and subscription
        // =====================================================================

        [Test]
        public void AC1_ViewLayerChannel_Broadcast_AllSubscribersCalled() {
            // Given: ViewLayerChannel with two subscribers
            int callCountA = 0, callCountB = 0;
            ViewLayer lastLayerA = ViewLayer.STARMAP, lastLayerB = ViewLayer.STARMAP;

            void HandlerA(ViewLayer l) { callCountA++; lastLayerA = l; }
            void HandlerB(ViewLayer l) { callCountB++; lastLayerB = l; }

            _viewLayerChannel.Subscribe(HandlerA);
            _viewLayerChannel.Subscribe(HandlerB);

            // When
            _viewLayerChannel.Raise(ViewLayer.COCKPIT);

            // Then: both subscribers called with correct value
            Assert.AreEqual(1, callCountA);
            Assert.AreEqual(ViewLayer.COCKPIT, lastLayerA);
            Assert.AreEqual(1, callCountB);
            Assert.AreEqual(ViewLayer.COCKPIT, lastLayerB);
        }

        [Test]
        public void AC1_ViewLayerChannel_COCKPIT_WITH_OVERLAY_Broadcast() {
            // Given: subscriber
            ViewLayer received = ViewLayer.STARMAP;
            void Handler(ViewLayer l) => received = l;
            _viewLayerChannel.Subscribe(Handler);

            // When
            _viewLayerChannel.Raise(ViewLayer.COCKPIT_WITH_OVERLAY);

            // Then
            Assert.AreEqual(ViewLayer.COCKPIT_WITH_OVERLAY, received);
        }

        // =====================================================================
        // AC-2: ShipStateChannel broadcast and subscription
        // =====================================================================

        [Test]
        public void AC2_ShipStateChannel_Broadcast_AllSubscribersCalled() {
            // Given: ShipStateChannel with subscriber
            (string, ShipState) received = ("", ShipState.DOCKED);
            void Handler((string, ShipState) p) => received = p;
            _shipStateChannel.Subscribe(Handler);

            // When: ShipDataModel calls SetState(IN_TRANSIT)
            _shipStateChannel.Raise(("ship_1", ShipState.IN_TRANSIT));

            // Then: subscriber receives correct tuple
            Assert.AreEqual("ship_1", received.Item1);
            Assert.AreEqual(ShipState.IN_TRANSIT, received.Item2);
        }

        [Test]
        public void AC2_ShipStateChannel_DESTROYED_Broadcast() {
            // Given: subscriber
            (string, ShipState) received = ("ship_x", ShipState.IN_TRANSIT);
            void Handler((string, ShipState) p) => received = p;
            _shipStateChannel.Subscribe(Handler);

            // When: ShipDataModel.SetState(DESTROYED) is called
            _shipStateChannel.Raise(("ship_1", ShipState.DESTROYED));

            // Then
            Assert.AreEqual("ship_1", received.Item1);
            Assert.AreEqual(ShipState.DESTROYED, received.Item2);
        }

        // =====================================================================
        // AC-3: OnDisable后不接收广播
        // =====================================================================

        [Test]
        public void AC3_Unsubscribe_OnDisable_PreventsFutureEvents() {
            // Given: subscriber that then unsubscribes (simulating OnDisable)
            int callCount = 0;
            void Handler(ViewLayer l) => callCount++;
            _viewLayerChannel.Subscribe(Handler);
            _viewLayerChannel.Raise(ViewLayer.COCKPIT);
            Assert.AreEqual(1, callCount);

            // Simulate OnDisable — unsubscribe
            _viewLayerChannel.Unsubscribe(Handler);

            // When: next Raise
            _viewLayerChannel.Raise(ViewLayer.STARMAP);

            // Then: handler NOT called
            Assert.AreEqual(1, callCount, "Handler should not be called after Unsubscribe");
        }

        [Test]
        public void AC3_ShipStateChannel_Unsubscribe_PreventsFutureEvents() {
            // Given: subscriber that unsubscribes
            int callCount = 0;
            void Handler((string, ShipState) p) => callCount++;
            _shipStateChannel.Subscribe(Handler);
            _shipStateChannel.Raise(("ship_1", ShipState.IN_TRANSIT));
            Assert.AreEqual(1, callCount);

            // Simulate OnDisable
            _shipStateChannel.Unsubscribe(Handler);
            _shipStateChannel.Raise(("ship_1", ShipState.DOCKED));

            // Then
            Assert.AreEqual(1, callCount);
        }

        // =====================================================================
        // AC-4: ViewLayer enum values exist
        // =====================================================================

        [Test]
        public void AC4_ViewLayer_Enum_HasRequiredValues() {
            // Verify all required ViewLayer values exist
            Assert.IsTrue(Enum.IsDefined(typeof(ViewLayer), ViewLayer.STARMAP));
            Assert.IsTrue(Enum.IsDefined(typeof(ViewLayer), ViewLayer.COCKPIT));
            Assert.IsTrue(Enum.IsDefined(typeof(ViewLayer), ViewLayer.COCKPIT_WITH_OVERLAY));
            Assert.IsTrue(Enum.IsDefined(typeof(ViewLayer), ViewLayer.SWITCHING_IN));
            Assert.IsTrue(Enum.IsDefined(typeof(ViewLayer), ViewLayer.SWITCHING_OUT));
            Assert.IsTrue(Enum.IsDefined(typeof(ViewLayer), ViewLayer.SWITCHING_SHIP));
            Assert.IsTrue(Enum.IsDefined(typeof(ViewLayer), ViewLayer.OPENING_OVERLAY));
            Assert.IsTrue(Enum.IsDefined(typeof(ViewLayer), ViewLayer.CLOSING_OVERLAY));
        }

        [Test]
        public void AC4_ShipState_Enum_HasRequiredValues() {
            // Verify all required ShipState values exist
            Assert.IsTrue(Enum.IsDefined(typeof(ShipState), ShipState.DOCKED));
            Assert.IsTrue(Enum.IsDefined(typeof(ShipState), ShipState.IN_COCKPIT));
            Assert.IsTrue(Enum.IsDefined(typeof(ShipState), ShipState.IN_TRANSIT));
            Assert.IsTrue(Enum.IsDefined(typeof(ShipState), ShipState.IN_COMBAT));
            Assert.IsTrue(Enum.IsDefined(typeof(ShipState), ShipState.DESTROYED));
        }

        // =====================================================================
        // Multi-subscriber scenarios
        // =====================================================================

        [Test]
        public void AC_MultipleSubscribers_ViewLayer_StarMapUI_ShipHUD_Simultaneous() {
            // Simulate: StarMapUI and ShipHUD both subscribed
            int starMapUICalls = 0, shipHUDCalls = 0;
            void StarMapHandler(ViewLayer l) => starMapUICalls++;
            void ShipHUDHandler(ViewLayer l) => shipHUDCalls++;

            _viewLayerChannel.Subscribe(StarMapHandler);
            _viewLayerChannel.Subscribe(ShipHUDHandler);

            // When: ViewLayerManager raises COCKPIT
            _viewLayerChannel.Raise(ViewLayer.COCKPIT);

            // Then: both UI layers receive
            Assert.AreEqual(1, starMapUICalls);
            Assert.AreEqual(1, shipHUDCalls);
        }

        [Test]
        public void AC_MultipleShips_ShipStateChannel_RoutedToCorrectSubscriber() {
            // Given: two ship state change handlers
            ShipState lastStateShip1 = ShipState.DOCKED;
            ShipState lastStateShip2 = ShipState.DOCKED;
            void HandlerShip1((string, ShipState) p) {
                if (p.Item1 == "ship_1") lastStateShip1 = p.Item2;
            }
            void HandlerShip2((string, ShipState) p) {
                if (p.Item1 == "ship_2") lastStateShip2 = p.Item2;
            }
            _shipStateChannel.Subscribe(HandlerShip1);
            _shipStateChannel.Subscribe(HandlerShip2);

            // When: ship_1 transitions to IN_TRANSIT
            _shipStateChannel.Raise(("ship_1", ShipState.IN_TRANSIT));

            // Then: ship_1 handler updated, ship_2 unchanged
            Assert.AreEqual(ShipState.IN_TRANSIT, lastStateShip1);
            Assert.AreEqual(ShipState.DOCKED, lastStateShip2);
        }
    }
}
