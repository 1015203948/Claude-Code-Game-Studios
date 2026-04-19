#if false
using NUnit.Framework;
using UnityEngine;
using Game.Channels;
using Game.Data;
using Gameplay;

namespace Tests.Integration.Colony {
    /// <summary>
    /// Integration tests for ColonyManager production tick (Story 010).
    /// Verifies: DeltaTime-driven tick, SimRate acceleration, ORE_CAP clamp,
    /// OnResourcesUpdated broadcast.
    /// </summary>
    [TestFixture]
    public class ColonyProductionTickTest {

        private ColonyManager _colonyManager;
        private OnResourcesUpdatedChannel _resourceChannel;
        private SimClock _simClock;
        private ResourceConfig _resourceConfig;

        [SetUp]
        public void SetUp() {
            // Create GameObject with ColonyManager
            var go = new GameObject("ColonyManager");
            _colonyManager = go.AddComponent<ColonyManager>();

            // Create and inject ResourceConfig
            _resourceConfig = ScriptableObject.CreateInstance<ResourceConfig>();
            // Use reflection to inject the SerializeField
            var field = typeof(ColonyManager).GetField("_resourceConfig",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(_colonyManager, _resourceConfig);

            // Create and inject OnResourcesUpdatedChannel
            _resourceChannel = ScriptableObject.CreateInstance<OnResourcesUpdatedChannel>();
            var channelField = typeof(ColonyManager).GetField("_onResourcesUpdatedChannel",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            channelField.SetValue(_colonyManager, _resourceChannel);

            // Create SimClock (SimRate = 1 by default)
            var simGo = new GameObject("SimClock");
            _simClock = simGo.AddComponent<SimClock>();
            var simField = typeof(SimClock).GetField("_simRateChangedChannel",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            simField.SetValue(_simClock, ScriptableObject.CreateInstance<SimRateChangedChannel>());

            // Inject ColonyShipChannel (no subscribers needed for these tests)
            var colonyShipChannel = ScriptableObject.CreateInstance<ColonyShipChannel>();
            var csField = typeof(ColonyManager).GetField("_colonyShipChannel",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            csField.SetValue(_colonyManager, colonyShipChannel);

            // Initialize with known state
            _colonyManager.Initialize(startingOre: 100, startingEnergy: 50);
            _colonyManager.ActiveMines = 0;
            _colonyManager.ActiveShipyards = 0;
        }

        [TearDown]
        public void TearDown() {
            Object.DestroyImmediate(_colonyManager.gameObject);
            Object.DestroyImmediate(_simClock.gameObject);
            Object.DestroyImmediate(_resourceConfig);
            Object.DestroyImmediate(_resourceChannel);
        }

        // =====================================================================
        // AC-1: DeltaTime drives tick — 1 real second = 1 tick at SimRate=1
        // =====================================================================

        [Test]
        public void AC1_DeltaTime_Drives_Tick_At_Rate1() {
            // Given: SimRate = 1, fresh accumulator
            _colonyManager.Initialize(startingOre: 0, startingEnergy: 0);
            var field = typeof(ColonyManager).GetField("_accumulator",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(_colonyManager, 0f);

            // Simulate: DeltaTime = 1.0f (1 second of sim time accumulated)
            // We mock this by setting accumulator directly and calling Tick via reflection
            field.SetValue(_colonyManager, 1.0f);

            // We can't call private Tick() directly, so we trigger via Update
            // by setting accumulator and calling Update
            float initialOre = _colonyManager.OreCurrent;

            // Manually set accumulator to trigger tick
            field.SetValue(_colonyManager, 1.0f);
            _colonyManager.Update(); // this subtracts 1.0 and calls Tick

            // Then: ore increased by production rate
            int expectedOreGain = _resourceConfig.EnergyPerColony; // base colony ore output
            Assert.Greater(_colonyManager.OreCurrent, initialOre,
                "Tick should increase ore by colony base production");
        }

        // =====================================================================
        // AC-2: SimRate = 5 → tick accelerates (5x production per real second)
        // =====================================================================

        [Test]
        public void AC2_SimRate_5_Accelerates_Tick() {
            // Given: SimRate = 5, accumulator = 0
            _simClock.SetRate(5f);
            var field = typeof(ColonyManager).GetField("_accumulator",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(_colonyManager, 0f);

            // Simulate 1 real second worth of DeltaTime
            // At SimRate=5, DeltaTime = unscaledDeltaTime × 5
            // After 1 real second, accumulator should have 5 "sim seconds"
            float unscaledDt = Time.unscaledDeltaTime;
            float simTimeAccumulated = unscaledDt * 5f;

            field.SetValue(_colonyManager, simTimeAccumulated);
            _colonyManager.Update();

            // Should have fired multiple ticks (accumulator >= 1.0)
            // At SimRate=5, each real frame accumulates 5x the usual sim time
            int oreAtRate5 = _colonyManager.OreCurrent;

            // Reset and do same at SimRate=1
            _colonyManager.Initialize(startingOre: 0, startingEnergy: 0);
            field.SetValue(_colonyManager, unscaledDt); // 1 frame at rate 1
            _simClock.SetRate(1f);
            _colonyManager.Update();
            int oreAtRate1 = _colonyManager.OreCurrent;

            // oreAtRate5 should be ~5x oreAtRate1
            Assert.Greater(oreAtRate5, oreAtRate1,
                "SimRate=5 should accumulate more ore per frame than SimRate=1");
        }

        // =====================================================================
        // AC-3: SimRate = 0 → tick pauses
        // =====================================================================

        [Test]
        public void AC3_SimRate_Zero_Pauses_Tick() {
            // Given: SimRate = 0, accumulator = 0
            _simClock.SetRate(0f);
            var field = typeof(ColonyManager).GetField("_accumulator",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(_colonyManager, 0f);

            int initialOre = _colonyManager.OreCurrent;

            // Call Update with DeltaTime = 0 (SimRate=0 means no time passes)
            _colonyManager.Update();

            // Then: accumulator unchanged (no tick fired)
            Assert.AreEqual(0f, field.GetValue(_colonyManager),
                "SimRate=0 should not advance accumulator");
        }

        // =====================================================================
        // AC-4: ORE_CAP clamp — ore capped at ORE_CAP
        // =====================================================================

        [Test]
        public void AC4_ORE_CAP_Clamp_PreventsOverflow() {
            // Given: oreCurrent = ORE_CAP - 5, tick would add +30
            int oreCap = _resourceConfig.ORE_CAP; // 1000
            _colonyManager.Initialize(startingOre: oreCap - 5, startingEnergy: 0);
            _colonyManager.ActiveMines = 3; // +30 ore per tick

            var field = typeof(ColonyManager).GetField("_accumulator",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(_colonyManager, 1.0f);
            _colonyManager.Update();

            // Then: oreCurrent = ORE_CAP (not ORE_CAP + 25)
            Assert.AreEqual(oreCap, _colonyManager.OreCurrent,
                "Ore should be clamped to ORE_CAP");
        }

        [Test]
        public void AC4_ORE_CAP_Clamp_ZeroFloor() {
            // Given: oreCurrent = 5, tick produces -10 (invalid — shouldn't happen with only positive production)
            // But clamp should also handle floor of 0
            int oreCap = _resourceConfig.ORE_CAP;
            _colonyManager.Initialize(startingOre: 5, startingEnergy: 0);
            _colonyManager.ActiveMines = -100; // negative mines (invalid) — simulates edge case

            var field = typeof(ColonyManager).GetField("_accumulator",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(_colonyManager, 1.0f);
            _colonyManager.Update();

            Assert.GreaterOrEqual(_colonyManager.OreCurrent, 0,
                "Ore should never go below 0");
        }

        // =====================================================================
        // AC-5: OnResourcesUpdated broadcast each tick
        // =====================================================================

        [Test]
        public void AC5_OnResourcesUpdated_Broadcasts_EachTick() {
            // Given: subscriber to OnResourcesUpdatedChannel
            int broadcastCount = 0;
            ResourceSnapshot? lastSnapshot = null;
            _resourceChannel.Subscribe(s => {
                broadcastCount++;
                lastSnapshot = s;
            });

            // Trigger one tick
            var field = typeof(ColonyManager).GetField("_accumulator",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(_colonyManager, 1.0f);
            _colonyManager.Update();

            // Then: subscriber was called once with correct values
            Assert.AreEqual(1, broadcastCount, "OnResourcesUpdated should broadcast once per tick");
            Assert.IsNotNull(lastSnapshot);
            Assert.AreEqual(_colonyManager.OreCurrent, lastSnapshot.Value.Ore);
            Assert.AreEqual(_colonyManager.EnergyCurrent, lastSnapshot.Value.Energy);
        }

        [Test]
        public void AC5_Broadcast_Contains_CurrentResourceValues() {
            // Given: specific resource values
            _colonyManager.Initialize(startingOre: 500, startingEnergy: 200);

            ResourceSnapshot? received = null;
            _resourceChannel.Subscribe(s => received = s);

            // Trigger tick to get initial broadcast
            var field = typeof(ColonyManager).GetField("_accumulator",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(_colonyManager, 1.0f);
            _colonyManager.Update();

            Assert.IsNotNull(received);
            Assert.AreEqual(500, received.Value.Ore);
            Assert.AreEqual(200, received.Value.Energy);
        }

        // =====================================================================
        // Initialize
        // =====================================================================

        [Test]
        public void Initialize_Sets_StartingResources() {
            _colonyManager.Initialize(startingOre: 250, startingEnergy: 100);
            Assert.AreEqual(250, _colonyManager.OreCurrent);
            Assert.AreEqual(100, _colonyManager.EnergyCurrent);
        }

        [Test]
        public void Initialize_Resets_Accumulator() {
            var field = typeof(ColonyManager).GetField("_accumulator",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(_colonyManager, 5f); // simulate some accumulated time

            _colonyManager.Initialize(startingOre: 0, startingEnergy: 0);

            Assert.AreEqual(0f, field.GetValue(_colonyManager),
                "Initialize should reset accumulator to 0");
        }
    }
}

#endif
