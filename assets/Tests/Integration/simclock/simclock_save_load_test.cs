using NUnit.Framework;
using UnityEngine;
using Game.Channels;
using Game.Data;
using Gameplay;

namespace Tests.Integration.Simclock {
    /// <summary>
    /// Integration tests for SimRate save/load round-trip (Story 009).
    /// Verifies: SaveData includes SimRate, Load restores SimRate,
    /// invalid values fall back to 1, new save defaults to 1.
    /// </summary>
    [TestFixture]
    public class SimClockSaveLoadTest {

        private SimClock _simClock;
        private SimRateChangedChannel _channel;
        private GameDataManager _gameDataManager;

        [SetUp]
        public void SetUp() {
            var go = new GameObject("SimClock");
            _simClock = go.AddComponent<SimClock>();
            _channel = ScriptableObject.CreateInstance<SimRateChangedChannel>();

            var field = typeof(SimClock).GetField("_simRateChangedChannel",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(_simClock, _channel);

            _gameDataManager = new GameDataManager();
        }

        [TearDown]
        public void TearDown() {
            Object.DestroyImmediate(_simClock.gameObject);
            Object.DestroyImmediate(_channel);
        }

        // =====================================================================
        // AC-1: Save writes SimRate = current SimRate value
        // =====================================================================

        [Test]
        public void AC1_Save_Writes_SimRate_5() {
            // Given: SimRate = 5
            _simClock.SetRate(5f);

            // When
            SaveData save = _gameDataManager.Save();

            // Then: saveData.simRate == 5f
            Assert.AreEqual(5f, save.SimRate,
                "Save() should write current SimRate to SaveData");
        }

        [Test]
        public void AC1_Save_Writes_SimRate_20() {
            _simClock.SetRate(20f);
            SaveData save = _gameDataManager.Save();
            Assert.AreEqual(20f, save.SimRate);
        }

        [Test]
        public void AC1_Save_Writes_SimRate_0() {
            _simClock.SetRate(0f);
            SaveData save = _gameDataManager.Save();
            Assert.AreEqual(0f, save.SimRate);
        }

        // =====================================================================
        // AC-2: Load restores SimRate from saveData
        // =====================================================================

        [Test]
        public void AC2_Load_Restores_SimRate_5() {
            // Given: SaveData with SimRate = 5, SimClock at default 1
            Assert.AreEqual(1f, _simClock.SimRate);

            SaveData save = new SaveData { SimRate = 5f };

            // When
            _gameDataManager.Load(save);

            // Then: SimClock.SimRate == 5f
            Assert.AreEqual(5f, _simClock.SimRate,
                "Load() should restore SimRate from SaveData");
        }

        [Test]
        public void AC2_Load_Restores_SimRate_20() {
            SaveData save = new SaveData { SimRate = 20f };
            _gameDataManager.Load(save);
            Assert.AreEqual(20f, _simClock.SimRate);
        }

        [Test]
        public void AC2_Load_Restores_SimRate_0() {
            SaveData save = new SaveData { SimRate = 0f };
            _gameDataManager.Load(save);
            Assert.AreEqual(0f, _simClock.SimRate);
        }

        [Test]
        public void AC2_Load_Broadcasts_SimRateChanged() {
            // Given: SaveData with SimRate = 5
            SaveData save = new SaveData { SimRate = 5f };
            float received = -1f;
            _channel.Subscribe(f => received = f);

            // When
            _gameDataManager.Load(save);

            // Then: channel broadcasts 5f
            Assert.AreEqual(5f, received,
                "Load() should broadcast SimRateChangedChannel on SetRate");
        }

        // =====================================================================
        // AC-3: Invalid save value defaults to 1
        // =====================================================================

        [Test]
        public void AC3_Load_InvalidRate_99_Defaults_To_1() {
            // Given: SaveData with invalid SimRate = 99
            SaveData save = new SaveData { SimRate = 99f };

            // When
            _gameDataManager.Load(save);

            // Then: SimRate = 1f (default)
            Assert.AreEqual(1f, _simClock.SimRate,
                "Load() should default invalid SimRate to 1");
        }

        [Test]
        public void AC3_Load_InvalidRate_Negative_Defaults_To_1() {
            SaveData save = new SaveData { SimRate = -1f };
            _gameDataManager.Load(save);
            Assert.AreEqual(1f, _simClock.SimRate);
        }

        [Test]
        public void AC3_Load_InvalidRate_3_No_Assert_Crash() {
            // Given: SaveData with SimRate = 3 (not in {0,1,5,20})
            SaveData save = new SaveData { SimRate = 3f };

            // When/Then: Load does not throw
            Assert.DoesNotThrow(() => _gameDataManager.Load(save),
                "Load with invalid SimRate should not throw");
        }

        [Test]
        public void AC3_Load_InvalidRate_NoBroadcast() {
            // Given: invalid save data
            SaveData save = new SaveData { SimRate = 99f };
            int broadcastCount = 0;
            _channel.Subscribe(f => broadcastCount++);

            // When
            _gameDataManager.Load(save);

            // Then: no broadcast (SetRate guards against invalid)
            Assert.AreEqual(0, broadcastCount,
                "Invalid rate should not trigger broadcast");
        }

        // =====================================================================
        // AC-4: New save defaults SimRate = 1
        // =====================================================================

        [Test]
        public void AC4_NewGame_Save_SimRate_Defaults_To_1() {
            // Given: fresh SimClock (SimRate = 1)
            Assert.AreEqual(1f, _simClock.SimRate);

            // When: NewGame() → Save()
            // (SimClock is already at default 1, no SetRate called)
            SaveData save = _gameDataManager.Save();

            // Then: saveData.simRate == 1f
            Assert.AreEqual(1f, save.SimRate,
                "New game save should have SimRate = 1f");
        }

        [Test]
        public void AC4_NewGame_Load_AfterNewGame_Restores_To_1() {
            // When: player starts new game and immediately saves
            SaveData newSave = _gameDataManager.Save();

            // Simulate load back
            _gameDataManager.Load(newSave);

            // Then: SimRate = 1
            Assert.AreEqual(1f, _simClock.SimRate);
        }

        // =====================================================================
        // Round-trip integrity
        // =====================================================================

        [Test]
        public void RoundTrip_SimRate_Preserved_At_All_Valid_Rates() {
            foreach (float rate in new float[] { 0f, 1f, 5f, 20f }) {
                _simClock.SetRate(rate);
                SaveData save = _gameDataManager.Save();
                _simClock.SetRate(1f); // reset
                _gameDataManager.Load(save);
                Assert.AreEqual(rate, _simClock.SimRate,
                    $"Round-trip should preserve SimRate={rate}");
            }
        }

        [Test]
        public void RoundTrip_Multiple_SaveLoad_Cycles() {
            // Given: save at rate 5
            _simClock.SetRate(5f);
            SaveData save1 = _gameDataManager.Save();

            // Load it
            _gameDataManager.Load(save1);
            Assert.AreEqual(5f, _simClock.SimRate);

            // Change to 20 and save
            _simClock.SetRate(20f);
            SaveData save2 = _gameDataManager.Save();

            // Load save1 again
            _gameDataManager.Load(save1);
            Assert.AreEqual(5f, _simClock.SimRate);

            // Load save2
            _gameDataManager.Load(save2);
            Assert.AreEqual(20f, _simClock.SimRate);
        }
    }
}
