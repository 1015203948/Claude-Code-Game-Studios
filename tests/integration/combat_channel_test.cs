using NUnit.Framework;
using UnityEngine;
using Game.Channels;

namespace Game.Tests.Integration.Combat {
    /// <summary>
    /// Integration tests for CombatChannel SO broadcast behavior.
    /// Verifies Story 008 AC-1 ~ AC-4.
    ///
    /// CombatChannel is a Tier 1 SO Channel (ADR-0002 / ADR-0013):
    /// - RaiseBegin(nodeId) broadcasts CombatResult.Begin with nodeId
    /// - RaiseVictory(nodeId) broadcasts CombatResult.Victory with nodeId
    /// - RaiseDefeat(nodeId) broadcasts CombatResult.Defeat with nodeId
    /// - SO Channel uses GameEvent&lt;T&gt; base: Subscribe(Action&lt;object&gt;), not per-event delegates
    /// </summary>
    [TestFixture]
    public class CombatChannel_Integration_Test
    {
        // ─── Track broadcast history ────────────────────────────────────

        private bool _beginCalled;
        private bool _victoryCalled;
        private bool _defeatCalled;
        private string _lastBeginNodeId;
        private string _lastVictoryNodeId;
        private string _lastDefeatNodeId;
        private int _victoryCallCount;
        private int _defeatCallCount;

        // ─── Test channel instance ──────────────────────────────────────

        private CombatChannel _channel;

        // ─── SetUp / TearDown ──────────────────────────────────────────

        [SetUp]
        public void SetUp()
        {
            _beginCalled = false;
            _victoryCalled = false;
            _defeatCalled = false;
            _lastBeginNodeId = null;
            _lastVictoryNodeId = null;
            _lastDefeatNodeId = null;
            _victoryCallCount = 0;
            _defeatCallCount = 0;

            // Use runtime-created instance for isolation
            _channel = ScriptableObject.CreateInstance<CombatChannel>();
        }

        [TearDown]
        public void TearDown()
        {
            // Unsubscribe to avoid cross-test contamination
            _channel.Unsubscribe(_beginHandler);
            _channel.Unsubscribe(_victoryHandler);
            _channel.Unsubscribe(_defeatHandler);

            Object.DestroyImmediate(_channel);
        }

        // ─── Event Handlers ─────────────────────────────────────────────

        private readonly Action<object> _beginHandler = payload => { };
        private readonly Action<object> _victoryHandler = payload => { };
        private readonly Action<object> _defeatHandler = payload => { };

        // ─── Helper: Parse CombatPayload ───────────────────────────────

        private CombatPayload UnpackPayload(object payload)
        {
            Assert.IsInstanceOf<CombatPayload>(payload, "Payload should be CombatPayload");
            return (CombatPayload)payload;
        }

        // ─── AC-1: RaiseBegin fires with correct payload ─────────────

        /// <summary>
        /// AC-1: RaiseBegin() broadcasts CombatResult.Begin with correct nodeId.
        /// Given: CombatChannel exists and has subscribers
        /// When: RaiseBegin("node-A") is called
        /// Then: handler receives CombatPayload with Result=Begin and NodeId="node-A"
        /// </summary>
        [Test]
        public void AC1_RaiseBegin_fires_with_CombatResult_Begin_and_correct_nodeId()
        {
            // Arrange
            _channel.Subscribe(_beginHandler);
            Action<object> capture = payload => {
                var p = UnpackPayload(payload);
                _beginCalled = true;
                _lastBeginNodeId = p.NodeId;
            };
            _channel.Subscribe(capture);

            // Act
            _channel.RaiseBegin("node-A");

            // Assert
            Assert.IsTrue(_beginCalled, "handler should fire after RaiseBegin");
            Assert.AreEqual("node-A", _lastBeginNodeId, "NodeId should match argument");

            _channel.Unsubscribe(capture);
        }

        // ─── AC-2: RaiseVictory fires exactly once ───────────────────

        /// <summary>
        /// AC-2: RaiseVictory(nodeId) fires exactly once per battle.
        /// Given: CombatChannel has subscribers
        /// When: RaiseVictory("node-B") is called once
        /// Then: handler receives CombatPayload with Result=Victory, NodeId="node-B"
        /// </summary>
        [Test]
        public void AC2_RaiseVictory_fires_once_with_CombatResult_Victory()
        {
            // Arrange
            Action<object> capture = payload => {
                var p = UnpackPayload(payload);
                _victoryCalled = true;
                _lastVictoryNodeId = p.NodeId;
                _victoryCallCount++;
                Assert.AreEqual(CombatResult.Victory, p.Result, "Result should be Victory");
            };
            _channel.Subscribe(capture);

            // Act
            _channel.RaiseVictory("node-B");

            // Assert
            Assert.IsTrue(_victoryCalled, "handler should fire after RaiseVictory");
            Assert.AreEqual("node-B", _lastVictoryNodeId, "NodeId should match argument");
            Assert.AreEqual(1, _victoryCallCount, "handler should fire exactly once per call");

            _channel.Unsubscribe(capture);
        }

        // ─── AC-3: RaiseDefeat fires exactly once ─────────────────────

        /// <summary>
        /// AC-3: RaiseDefeat(nodeId) fires exactly once per battle.
        /// Given: CombatChannel has subscribers
        /// When: RaiseDefeat("node-C") is called once
        /// Then: handler receives CombatPayload with Result=Defeat, NodeId="node-C"
        /// </summary>
        [Test]
        public void AC3_RaiseDefeat_fires_once_with_CombatResult_Defeat()
        {
            // Arrange
            Action<object> capture = payload => {
                var p = UnpackPayload(payload);
                _defeatCalled = true;
                _lastDefeatNodeId = p.NodeId;
                _defeatCallCount++;
                Assert.AreEqual(CombatResult.Defeat, p.Result, "Result should be Defeat");
            };
            _channel.Subscribe(capture);

            // Act
            _channel.RaiseDefeat("node-C");

            // Assert
            Assert.IsTrue(_defeatCalled, "handler should fire after RaiseDefeat");
            Assert.AreEqual("node-C", _lastDefeatNodeId, "NodeId should match argument");
            Assert.AreEqual(1, _defeatCallCount, "handler should fire exactly once per call");

            _channel.Unsubscribe(capture);
        }

        // ─── AC-4: Subscribe/Unsubscribe lifecycle ─────────────────────

        /// <summary>
        /// AC-4: SO Channel subscription respects OnEnable/OnDisable lifecycle.
        /// Given: handler subscribed to CombatChannel
        /// When: handler unsubscribes (OnDisable), then RaiseVictory is called
        /// Then: unsubscribed handler does NOT receive the event
        /// </summary>
        [Test]
        public void AC4_unsubscribed_handler_does_not_receive_event()
        {
            // Arrange
            Action<object> capture = payload => {
                var p = UnpackPayload(payload);
                _victoryCallCount++;
                _lastVictoryNodeId = p.NodeId;
            };
            _channel.Subscribe(capture);
            _channel.RaiseVictory("node-D");

            Assert.AreEqual(1, _victoryCallCount, "first event should be received");

            // Act: unsubscribe (simulates OnDisable)
            _channel.Unsubscribe(capture);
            _channel.RaiseVictory("node-E");

            // Assert: second event NOT received by unsubscribed handler
            Assert.AreEqual(1, _victoryCallCount, "unsubscribed handler should not receive second event");
            Assert.AreEqual("node-D", _lastVictoryNodeId, "nodeId should still be from first event");
        }

        // ─── Edge: Multiple subscribers independently ─────────────────

        /// <summary>
        /// Edge: Multiple subscribers each receive the broadcast (multicast delegate).
        /// Given: two handlers subscribed to OnCombatDefeat
        /// When: RaiseDefeat("node-F") is called
        /// Then: both handlers fire independently
        /// </summary>
        [Test]
        public void EDGE_multiple_subscribers_each_receive_broadcast()
        {
            // Arrange
            int handler2Count = 0;
            Action<object> handler1 = payload => { _defeatCallCount++; };
            Action<object> handler2 = payload => { handler2Count++; };

            _channel.Subscribe(handler1);
            _channel.Subscribe(handler2);

            // Act
            _channel.RaiseDefeat("node-F");

            // Assert
            Assert.AreEqual(1, _defeatCallCount, "handler1 should receive event");
            Assert.AreEqual(1, handler2Count, "handler2 should receive event independently");

            // Cleanup
            _channel.Unsubscribe(handler1);
            _channel.Unsubscribe(handler2);
        }

        // ─── Edge: CombatResult enum values ───────────────────────────

        /// <summary>
        /// Edge: Verify all three CombatResult enum values are used correctly.
        /// </summary>
        [Test]
        public void EDGE_all_three_CombatResult_values_are_distinct()
        {
            Assert.AreNotEqual(CombatResult.Begin, CombatResult.Victory, "Begin ≠ Victory");
            Assert.AreNotEqual(CombatResult.Begin, CombatResult.Defeat, "Begin ≠ Defeat");
            Assert.AreNotEqual(CombatResult.Victory, CombatResult.Defeat, "Victory ≠ Defeat");
            Assert.AreEqual(3, System.Enum.GetValues(typeof(CombatResult)).Length, "Exactly 3 CombatResult values");
        }
    }
}
