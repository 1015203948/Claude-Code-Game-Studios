#if false
using NUnit.Framework;
using UnityEngine;
using Game.Channels;
using Gameplay;

namespace Tests.Unit.Simclock {
    /// <summary>
    /// Unit tests for SimClock core logic (Story 008).
    /// AC-1 through AC-5 from QA Test Cases.
    /// </summary>
    [TestFixture]
    public class SimClockDeltaTimeTest {

        private SimClock _simClock;
        private SimRateChangedChannel _channel;

        [SetUp]
        public void SetUp() {
            // Create SimClock on a GameObject
            var go = new GameObject("SimClock");
            _simClock = go.AddComponent<SimClock>();
            _channel = ScriptableObject.CreateInstance<SimRateChangedChannel>();

            // Inject channel via reflection (SerializeField not accessible in tests)
            var field = typeof(SimClock).GetField("_simRateChangedChannel",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(_simClock, _channel);
        }

        [TearDown]
        public void TearDown() {
            Object.DestroyImmediate(_simClock.gameObject);
            Object.DestroyImmediate(_channel);
        }

        // =====================================================================
        // AC-5: Initial SimRate = 1
        // =====================================================================

        [Test]
        public void AC5_Initial_SimRate_Is_One() {
            // Given: fresh SimClock instance (no SetRate called)
            // Then: SimRate == 1f
            Assert.AreEqual(1f, _simClock.SimRate,
                "Fresh SimClock should have SimRate = 1f");
        }

        [Test]
        public void AC5_Initial_DeltaTime_Equals_UnscaledDeltaTime() {
            // Given: fresh SimClock
            // When: DeltaTime is read
            float dt = _simClock.DeltaTime;

            // Then: DeltaTime ≈ Time.unscaledDeltaTime × 1
            Assert.AreApproximatelyEqual(
                Time.unscaledDeltaTime * 1f,
                dt,
                0.001f,
                "DeltaTime should equal Time.unscaledDeltaTime × 1 when SimRate=1");
        }

        // =====================================================================
        // AC-4: SetRate(0) → DeltaTime = 0
        // =====================================================================

        [Test]
        public void AC4_SetRate_Zero_DeltaTime_Is_Zero() {
            // Given: fresh SimClock (SimRate = 1)
            // When: SetRate(0) is called
            _simClock.SetRate(0f);

            // Then: SimRate == 0
            Assert.AreEqual(0f, _simClock.SimRate);
            // And: DeltaTime == 0
            Assert.AreEqual(0f, _simClock.DeltaTime);
        }

        [Test]
        public void AC4_SetRate_Zero_Broadcasts_SimRateChanged() {
            // Given: subscriber to SimRateChangedChannel
            float received = -1f;
            _channel.Subscribe(f => received = f);

            // When
            _simClock.SetRate(0f);

            // Then: subscriber receives 0f
            Assert.AreEqual(0f, received);
        }

        // =====================================================================
        // AC-1: DeltaTime formula correct at SimRate = 5
        // =====================================================================

        [Test]
        public void AC1_DeltaTime_Formula_Correct_At_Rate5() {
            // Given: SimRate = 5
            _simClock.SetRate(5f);
            float unscaledDt = Time.unscaledDeltaTime;

            // When: DeltaTime is read
            float dt = _simClock.DeltaTime;

            // Then: dt ≈ unscaledDeltaTime × 5 (within 0.001f)
            float expected = unscaledDt * 5f;
            Assert.AreApproximatelyEqual(expected, dt, 0.001f,
                $"DeltaTime should be {expected} (unscaled × 5), got {dt}");
        }

        // =====================================================================
        // AC-1: DeltaTime formula correct at SimRate = 20
        // =====================================================================

        [Test]
        public void AC1_DeltaTime_Formula_Correct_At_Rate20() {
            _simClock.SetRate(20f);
            float unscaledDt = Time.unscaledDeltaTime;
            float dt = _simClock.DeltaTime;
            float expected = unscaledDt * 20f;
            Assert.AreApproximatelyEqual(expected, dt, 0.001f);
        }

        // =====================================================================
        // AC-3: SetRate(2) → invalid, SimRate unchanged
        // =====================================================================

        [Test]
        public void AC3_SetRate_Invalid_2_SimRate_Unchanged() {
            // Given: SimRate = 1
            Assert.AreEqual(1f, _simClock.SimRate);

            // When: SetRate(2f) is called
            _simClock.SetRate(2f);

            // Then: SimRate stays 1 (invalid rate rejected)
            Assert.AreEqual(1f, _simClock.SimRate,
                "Invalid rate 2 should be rejected, SimRate stays 1");
        }

        [Test]
        public void AC3_SetRate_Invalid_Negative_SimRate_Unchanged() {
            // Given: SimRate = 1
            _simClock.SetRate(-1f);
            Assert.AreEqual(1f, _simClock.SimRate,
                "Negative rate should be rejected");
        }

        [Test]
        public void AC3_SetRate_Invalid_No_Broadcast() {
            // Given: subscriber
            int broadcastCount = 0;
            _channel.Subscribe(f => broadcastCount++);

            // When: SetRate(3f) called (invalid)
            _simClock.SetRate(3f);

            // Then: no broadcast
            Assert.AreEqual(0, broadcastCount,
                "Invalid rate should not trigger broadcast");
        }

        [Test]
        public void AC3_IsValidRate_Rejects_Invalid_Values() {
            Assert.IsFalse(SimClock.IsValidRate(2f), "2 is not valid");
            Assert.IsFalse(SimClock.IsValidRate(3f), "3 is not valid");
            Assert.IsFalse(SimClock.IsValidRate(-1f), "-1 is not valid");
            Assert.IsFalse(SimClock.IsValidRate(0.5f), "0.5 is not valid");
        }

        [Test]
        public void AC3_IsValidRate_Accepts_Valid_Values() {
            Assert.IsTrue(SimClock.IsValidRate(0f), "0 is valid");
            Assert.IsTrue(SimClock.IsValidRate(1f), "1 is valid");
            Assert.IsTrue(SimClock.IsValidRate(5f), "5 is valid");
            Assert.IsTrue(SimClock.IsValidRate(20f), "20 is valid");
        }

        // =====================================================================
        // AC-2: SetRate(5) broadcasts SimRateChanged
        // =====================================================================

        [Test]
        public void AC2_SetRate_5_Broadcasts_SimRateChanged() {
            // Given: subscriber
            float received = -1f;
            _channel.Subscribe(f => received = f);

            // When
            _simClock.SetRate(5f);

            // Then
            Assert.AreEqual(5f, received, "SetRate(5) should broadcast 5f");
            Assert.AreEqual(5f, _simClock.SimRate);
        }

        [Test]
        public void AC2_SetRate_20_Broadcasts_SimRateChanged() {
            float received = -1f;
            _channel.Subscribe(f => received = f);
            _simClock.SetRate(20f);
            Assert.AreEqual(20f, received);
        }

        [Test]
        public void AC2_SetRate_SameValue_NoDuplicateBroadcast() {
            // Given: SimRate already 1
            int count = 0;
            _channel.Subscribe(f => count++);
            _simClock.SetRate(1f); // no-op

            // When: SetRate(1) again
            _simClock.SetRate(1f);

            // Then: no extra broadcast (SetRate guards against same-value)
            Assert.AreEqual(0, count, "Setting same rate should not broadcast");
        }

        // =====================================================================
        // DeltaTime is a property (computed, not cached)
        // =====================================================================

        [Test]
        public void DeltaTime_Is_A_Property_Not_Cached() {
            // When: SimRate changes between two DeltaTime reads
            _simClock.SetRate(5f);
            float dt1 = _simClock.DeltaTime;

            // Simulate time passing (unscaledDeltaTime changes next frame)
            _simClock.SetRate(1f);
            float dt2 = _simClock.DeltaTime;

            // Then: values reflect the rate change (not cached)
            Assert.AreNotEqual(dt1, dt2,
                "DeltaTime should reflect current SimRate (not cached)");
        }
    }
}

#endif
