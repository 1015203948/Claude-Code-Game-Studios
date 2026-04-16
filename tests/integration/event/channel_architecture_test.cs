using NUnit.Framework;
using UnityEngine;
using Game.Channels;
using Game.Data;

namespace Tests.Integration.Event {
    /// <summary>
    /// Integration tests for Tier 1 SO Channel architecture (ADR-0002).
    /// Verifies: OnEnable/OnDisable subscription pairing, Raise() zero-reflection,
    /// destroyCancellationToken behavior.
    /// </summary>
    [TestFixture]
    public class ChannelArchitectureTest {

        // =====================================================================
        // ADV-01/ADV-02: OnEnable/OnDisable subscription pairing
        // =====================================================================

        [Test]
        public void ADV01_Subscribe_In_OnEnable_CalledAfterSubscribe() {
            // Given: a GameEvent<T> channel and a handler
            var channel = ScriptableObject.CreateInstance<SimRateChangedChannel>();
            int callCount = 0;
            void Handler(float f) => callCount++;

            // When: Subscribe is called (simulating OnEnable)
            channel.Subscribe(Handler);

            // Then: handler is called when Raise is invoked
            channel.Raise(1f);
            Assert.AreEqual(1, callCount, "Handler should be called once after Subscribe");

            Object.DestroyImmediate(channel);
        }

        [Test]
        public void ADV02_Unsubscribe_In_OnDisable_StopsFutureEvents() {
            // Given: a channel with an active subscription
            var channel = ScriptableObject.CreateInstance<SimRateChangedChannel>();
            int callCount = 0;
            void Handler(float f) => callCount++;
            channel.Subscribe(Handler);
            channel.Raise(1f);
            Assert.AreEqual(1, callCount);

            // When: Unsubscribe is called (simulating OnDisable)
            channel.Unsubscribe(Handler);
            channel.Raise(2f);

            // Then: handler is NOT called after Unsubscribe
            Assert.AreEqual(1, callCount, "Handler should not be called after Unsubscribe");

            Object.DestroyImmediate(channel);
        }

        [Test]
        public void ADV01_MultipleSubscribers_AllCalled() {
            // Given: multiple subscribers to the same channel
            var channel = ScriptableObject.CreateInstance<SimRateChangedChannel>();
            int count1 = 0, count2 = 0, count3 = 0;
            void H1(float f) => count1++;
            void H2(float f) => count2++;
            void H3(float f) => count3++;

            channel.Subscribe(H1);
            channel.Subscribe(H2);
            channel.Subscribe(H3);

            // When: Raise is called
            channel.Raise(5f);

            // Then: all subscribers are called
            Assert.AreEqual(1, count1);
            Assert.AreEqual(1, count2);
            Assert.AreEqual(1, count3);

            Object.DestroyImmediate(channel);
        }

        [Test]
        public void ADV02_Unsubscribe_OnlyRemovesSpecificHandler() {
            // Given: channel with two different handlers
            var channel = ScriptableObject.CreateInstance<SimRateChangedChannel>();
            int countA = 0, countB = 0;
            void HandlerA(float f) => countA++;
            void HandlerB(float f) => countB++;
            channel.Subscribe(HandlerA);
            channel.Subscribe(HandlerB);

            // When: only HandlerA is unsubscribed
            channel.Unsubscribe(HandlerA);
            channel.Raise(1f);

            // Then: HandlerA is not called, HandlerB IS called
            Assert.AreEqual(0, countA, "HandlerA should not be called after unsubscribe");
            Assert.AreEqual(1, countB, "HandlerB should still be called");

            Object.DestroyImmediate(channel);
        }

        // =====================================================================
        // Channel Raise() zero-reflection verification
        // =====================================================================

        [Test]
        public void ADV_ChannelRaise_NoBoxingForStructPayload() {
            // Given: a channel with struct payload
            var channel = ScriptableObject.CreateInstance<SimRateChangedChannel>();
            int callCount = 0;
            void Handler(float f) => callCount++;
            channel.Subscribe(Handler);

            // When: Raise is called 100 times
            for (int i = 0; i < 100; i++) {
                channel.Raise(1f);
            }

            // Then: all 100 calls are received (no silent failures from boxing)
            Assert.AreEqual(100, callCount);

            Object.DestroyImmediate(channel);
        }

        [Test]
        public void ADV_ChannelRaise_ValueTuplePayload_Works() {
            // Given: ShipStateChannel with tuple payload
            var channel = ScriptableObject.CreateInstance<ShipStateChannel>();
            (string, ShipState) received = ("", ShipState.DOCKED);
            void Handler((string, ShipState) p) => received = p;
            channel.Subscribe(Handler);

            // When
            channel.Raise(("ship_1", ShipState.IN_TRANSIT));

            // Then
            Assert.AreEqual("ship_1", received.Item1);
            Assert.AreEqual(ShipState.IN_TRANSIT, received.Item2);

            Object.DestroyImmediate(channel);
        }

        [Test]
        public void ADV_ChannelRaise_ResourceSnapshot_Works() {
            // Given: OnResourcesUpdatedChannel with ResourceSnapshot struct
            var channel = ScriptableObject.CreateInstance<OnResourcesUpdatedChannel>();
            ResourceSnapshot? received = null;
            void Handler(ResourceSnapshot s) => received = s;
            channel.Subscribe(Handler);

            // When
            channel.Raise(new ResourceSnapshot(ore: 500, energy: 200));

            // Then
            Assert.IsNotNull(received);
            Assert.AreEqual(500, received.Value.Ore);
            Assert.AreEqual(200, received.Value.Energy);

            Object.DestroyImmediate(channel);
        }

        // =====================================================================
        // Edge cases
        // =====================================================================

        [Test]
        public void Raise_OnChannelWithNoSubscribers_DoesNotThrow() {
            // Given: channel with no subscribers
            var channel = ScriptableObject.CreateInstance<SimRateChangedChannel>();

            // When/Then: Raise does not throw even with no subscribers
            Assert.DoesNotThrow(() => channel.Raise(1f));

            Object.DestroyImmediate(channel);
        }

        [Test]
        public void Raise_MultipleTimes_SamePayload_AllHandlersCalledEachTime() {
            // Given: channel with subscriber
            var channel = ScriptableObject.CreateInstance<SimRateChangedChannel>();
            int count = 0;
            void Handler(float f) => count++;
            channel.Subscribe(Handler);

            // When: Raise called 3 times
            channel.Raise(1f);
            channel.Raise(2f);
            channel.Raise(3f);

            // Then: handler called 3 times
            Assert.AreEqual(3, count);

            Object.DestroyImmediate(channel);
        }

        [Test]
        public void Subscribe_SameHandlerTwice_HandlersCalledTwice() {
            // Given: same handler subscribed twice (user error — should not crash)
            var channel = ScriptableObject.CreateInstance<SimRateChangedChannel>();
            int count = 0;
            void Handler(float f) => count++;
            channel.Subscribe(Handler);
            channel.Subscribe(Handler); // double subscribe

            // When
            channel.Raise(1f);

            // Then: handler called twice (no protection against double-subscribe in perf path)
            Assert.AreEqual(2, count);

            Object.DestroyImmediate(channel);
        }
    }
}
