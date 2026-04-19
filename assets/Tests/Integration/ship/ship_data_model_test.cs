#if false
using NUnit.Framework;
using UnityEngine;
using Game.Channels;
using Game.Data;

namespace Tests.Integration.Ship {
    /// <summary>
    /// Integration tests for ShipDataModel (Story 005).
    /// Verifies: blueprint defaults, IN_COCKPIT mutual exclusion,
    /// DESTROYED terminal state, IsPlayerControlled immutability,
    /// SetState atomicity + broadcast.
    /// </summary>
    [TestFixture]
    public class ShipDataModelTest {

        private ShipStateChannel _channel;
        private ShipBlueprint _genericBlueprint;
        private GameDataManager _gameDataManager;

        [SetUp]
        public void SetUp() {
            _channel = ScriptableObject.CreateInstance<ShipStateChannel>();
            _gameDataManager = new GameDataManager();

            // Create a basic blueprint for testing
            _genericBlueprint = ScriptableObject.CreateInstance<ShipBlueprint>();
            _genericBlueprint.BlueprintId = "generic_v1";
            _genericBlueprint.MaxHull = 100f;
            _genericBlueprint.ThrustPower = 8f;
            _genericBlueprint.TurnSpeed = 180f;
            _genericBlueprint.WeaponSlots = 1;
            _genericBlueprint.BuildCost = new ResourceBundle(30, 15);
            _genericBlueprint.RequiredShipyardTier = 0;
            _genericBlueprint.CarrierInstanceId = null;
            _genericBlueprint.HangarCapacity = 0;
        }

        [TearDown]
        public void TearDown() {
            Object.DestroyImmediate(_genericBlueprint);
            Object.DestroyImmediate(_channel);
        }

        // =====================================================================
        // AC-SHIP-04: BlueprintId reference gets default properties
        // =====================================================================

        [Test]
        public void ACSHIP04_BlueprintId_GetsDefaultHull_FromBlueprint() {
            // Given: ShipDataModel("ship_1", "generic_v1", isPlayerControlled=false)
            var ship = new ShipDataModel(
                "ship_1", "generic_v1", false, _genericBlueprint, _channel);

            // Then: CurrentHull = blueprint.MaxHull
            Assert.AreEqual(100f, ship.CurrentHull,
                "CurrentHull should be initialized to blueprint.MaxHull");
        }

        [Test]
        public void ACSHIP04_State_IsDOCKED_Initially() {
            var ship = new ShipDataModel(
                "ship_1", "generic_v1", false, _genericBlueprint, _channel);
            Assert.AreEqual(ShipState.DOCKED, ship.State,
                "New ships should start in DOCKED state");
        }

        [Test]
        public void ACSHIP04_IsPlayerControlled_IsFalse_ByDefault() {
            var ship = new ShipDataModel(
                "ship_1", "generic_v1", false, _genericBlueprint, _channel);
            Assert.IsFalse(ship.IsPlayerControlled,
                "Default IsPlayerControlled should be false");
        }

        [Test]
        public void ACSHIP04_IsPlayerControlled_CanBeTrue() {
            var ship = new ShipDataModel(
                "ship_1", "generic_v1", true, _genericBlueprint, _channel);
            Assert.IsTrue(ship.IsPlayerControlled);
        }

        // =====================================================================
        // AC-SHIP-07: IN_COCKPIT mutual exclusion
        // =====================================================================

        [Test]
        public void ACSHIP07_Cockpit_MutualExclusion_SecondShipRejected() {
            // Given: ship_1 is already IN_COCKPIT
            var ship1 = new ShipDataModel(
                "ship_1", "generic_v1", true, _genericBlueprint, _channel);
            var ship2 = new ShipDataModel(
                "ship_2", "generic_v1", false, _genericBlueprint, _channel);

            bool enterResult = ship1.SetState(ShipState.IN_COCKPIT);
            Assert.IsTrue(enterResult, "ship_1 should enter IN_COCKPIT");

            // When: ship_2 tries to enter IN_COCKPIT
            bool rejectResult = ship2.SetState(ShipState.IN_COCKPIT);

            // Then: rejected, ship_1 still in IN_COCKPIT
            Assert.IsFalse(rejectResult,
                "ship_2 should be rejected from IN_COCKPIT (mutual exclusion)");
            Assert.AreEqual(ShipState.IN_COCKPIT, ship1.State,
                "ship_1 should remain IN_COCKPIT");
            Assert.AreEqual(ShipState.DOCKED, ship2.State,
                "ship_2 should stay in DOCKED state");
        }

        [Test]
        public void ACSHIP07_Cockpit_ExitAllowsOther() {
            // Given: ship_1 in IN_COCKPIT
            var ship1 = new ShipDataModel(
                "ship_1", "generic_v1", true, _genericBlueprint, _channel);
            var ship2 = new ShipDataModel(
                "ship_2", "generic_v1", false, _genericBlueprint, _channel);

            ship1.SetState(ShipState.IN_COCKPIT);

            // When: ship_1 exits cockpit
            bool exitResult = ship1.SetState(ShipState.DOCKED);

            // Then: ship_2 can now enter
            Assert.IsTrue(exitResult);
            Assert.IsTrue(ship2.SetState(ShipState.IN_COCKPIT),
                "ship_2 should be able to enter IN_COCKPIT after ship_1 exits");
        }

        // =====================================================================
        // AC-SHIP-08: DESTROYED rejects all instructions
        // =====================================================================

        [Test]
        public void ACSHIP08_DESTROYED_RejectsAllTransitions() {
            // Given: ship is DESTROYED
            var ship = new ShipDataModel(
                "ship_1", "generic_v1", true, _genericBlueprint, _channel);
            ship.Destroy();
            Assert.AreEqual(ShipState.DESTROYED, ship.State);

            // When: try to transition to any other state
            bool toTransit = ship.SetState(ShipState.IN_TRANSIT);
            bool toDocked = ship.SetState(ShipState.DOCKED);
            bool toCockpit = ship.SetState(ShipState.IN_COCKPIT);

            // Then: all rejected, state unchanged
            Assert.IsFalse(toTransit, "DESTROYED → IN_TRANSIT should be rejected");
            Assert.IsFalse(toDocked, "DESTROYED → DOCKED should be rejected");
            Assert.IsFalse(toCockpit, "DESTROYED → IN_COCKPIT should be rejected");
            Assert.AreEqual(ShipState.DESTROYED, ship.State,
                "State should remain DESTROYED");
        }

        [Test]
        public void ACSHIP08_DESTROYED_HullStaysZero() {
            var ship = new ShipDataModel(
                "ship_1", "generic_v1", true, _genericBlueprint, _channel);
            ship.Destroy();
            Assert.AreEqual(0f, ship.CurrentHull,
                "DESTROYED ship should have 0 hull");
            Assert.AreEqual(ShipState.DESTROYED, ship.State);
        }

        // =====================================================================
        // AC-SHIP-12: IsPlayerControlled immutable after construction
        // =====================================================================

        [Test]
        public void ACSHIP12_IsPlayerControlled_IsReadonly() {
            var ship = new ShipDataModel(
                "ship_1", "generic_v1", true, _genericBlueprint, _channel);

            // IsPlayerControlled has no public setter — compilation fails if attempted
            // We verify immutability by checking the value cannot be changed via any public API
            Assert.IsTrue(ship.IsPlayerControlled);

            // Attempt various transitions — IsPlayerControlled value unchanged
            ship.SetState(ShipState.IN_TRANSIT);
            ship.SetState(ShipState.DOCKED);
            Assert.IsTrue(ship.IsPlayerControlled,
                "IsPlayerControlled should remain true after state transitions");
        }

        // =====================================================================
        // ShipStateChannel broadcast on SetState
        // =====================================================================

        [Test]
        public void SetState_Broadcasts_ShipStateChannel() {
            // Given: subscriber to ShipStateChannel
            (string, ShipState) received = ("", ShipState.DOCKED);
            _channel.Subscribe(p => received = p);

            var ship = new ShipDataModel(
                "ship_1", "generic_v1", true, _genericBlueprint, _channel);

            // When
            bool result = ship.SetState(ShipState.IN_TRANSIT);

            // Then: broadcast called
            Assert.IsTrue(result);
            Assert.AreEqual("ship_1", received.Item1);
            Assert.AreEqual(ShipState.IN_TRANSIT, received.Item2);
        }

        [Test]
        public void SetState_SameState_ReturnsFalse_NoBroadcast() {
            var ship = new ShipDataModel(
                "ship_1", "generic_v1", true, _genericBlueprint, _channel);

            int count = 0;
            _channel.Subscribe(p => count++);

            bool result = ship.SetState(ShipState.DOCKED); // no-op: already DOCKED

            Assert.IsFalse(result, "Setting same state should return false");
            Assert.AreEqual(0, count, "No broadcast for no-op state change");
        }

        [Test]
        public void Destroy_Broadcasts_ShipStateChannel() {
            (string, ShipState) received = ("", ShipState.DOCKED);
            _channel.Subscribe(p => received = p);

            var ship = new ShipDataModel(
                "ship_1", "generic_v1", true, _genericBlueprint, _channel);
            ship.Destroy();

            Assert.AreEqual("ship_1", received.Item1);
            Assert.AreEqual(ShipState.DESTROYED, received.Item2);
        }

        // =====================================================================
        // Legal state transitions
        // =====================================================================

        [Test]
        public void LegalTransition_DOCKED_To_IN_TRANSIT() {
            var ship = new ShipDataModel(
                "ship_1", "generic_v1", true, _genericBlueprint, _channel);
            Assert.IsTrue(ship.SetState(ShipState.IN_TRANSIT));
            Assert.AreEqual(ShipState.IN_TRANSIT, ship.State);
        }

        [Test]
        public void LegalTransition_DOCKED_To_IN_COCKPIT() {
            var ship = new ShipDataModel(
                "ship_1", "generic_v1", true, _genericBlueprint, _channel);
            Assert.IsTrue(ship.SetState(ShipState.IN_COCKPIT));
            Assert.AreEqual(ShipState.IN_COCKPIT, ship.State);
        }

        [Test]
        public void LegalTransition_IN_TRANSIT_To_DOCKED() {
            var ship = new ShipDataModel(
                "ship_1", "generic_v1", true, _genericBlueprint, _channel);
            ship.SetState(ShipState.IN_TRANSIT);
            Assert.IsTrue(ship.SetState(ShipState.DOCKED));
        }

        [Test]
        public void LegalTransition_IN_COMBAT_To_DESTROYED() {
            var ship = new ShipDataModel(
                "ship_1", "generic_v1", true, _genericBlueprint, _channel);
            ship.SetState(ShipState.IN_TRANSIT);
            ship.SetState(ShipState.IN_COMBAT);
            Assert.IsTrue(ship.Destroy());
            Assert.AreEqual(ShipState.DESTROYED, ship.State);
        }

        [Test]
        public void IllegalTransition_IN_TRANSIT_To_IN_COCKPIT() {
            // Can't go directly from IN_TRANSIT to IN_COCKPIT
            var ship = new ShipDataModel(
                "ship_1", "generic_v1", true, _genericBlueprint, _channel);
            ship.SetState(ShipState.IN_TRANSIT);
            bool result = ship.SetState(ShipState.IN_COCKPIT);
            Assert.IsFalse(result, "IN_TRANSIT → IN_COCKPIT is not a legal transition");
        }

        [Test]
        public void IllegalTransition_DOCKED_To_IN_COMBAT() {
            // Can't go directly from DOCKED to IN_COMBAT (must be in transit)
            var ship = new ShipDataModel(
                "ship_1", "generic_v1", true, _genericBlueprint, _channel);
            bool result = ship.SetState(ShipState.IN_COMBAT);
            Assert.IsFalse(result);
        }
    }
}

#endif
