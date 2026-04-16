using NUnit.Framework;
using Game.Data;

namespace Game.Data.Tests {
    [TestFixture]
    public class ShipBlueprintRegistryTests {
        private ShipBlueprintRegistry _registry;

        [SetUp]
        public void SetUp() {
            // Create a fresh registry instance for each test
            _registry = new ShipBlueprintRegistry();
        }

        [TearDown]
        public void TearDown() {
            // Clear the singleton instance after each test
            typeof(ShipBlueprintRegistry)
                .GetProperty("Instance", System.Reflection.BindingFlags.Public |
                                          System.Reflection.BindingFlags.Static)
                ?.SetValue(null, null);
        }

        // AC-1: generic_v1 blueprint registration and query
        [Test]
        public void test_get_blueprint_generic_v1_returns_non_null_and_valid() {
            // Given: a ShipBlueprint with generic_v1 data
            var genericBp = ScriptableObject.CreateInstance<ShipBlueprint>();
            genericBp.BlueprintId = "generic_v1";
            genericBp.MaxHull = 100;
            genericBp.ThrustPower = 8.0f;
            genericBp.TurnSpeed = 180.0f;
            genericBp.WeaponSlots = 1;
            genericBp.BuildCost = new ResourceBundle(30, 15);
            genericBp.RequiredShipyardTier = 0;
            genericBp.HangarCapacity = 0;

            _registry.Register(genericBp);

            // When: querying for generic_v1
            var result = _registry.GetBlueprint("generic_v1");

            // Then: returns non-null, BlueprintId matches, MaxHull > 0
            Assert.IsNotNull(result);
            Assert.AreEqual("generic_v1", result.BlueprintId);
            Assert.Greater(result.MaxHull, 0);

            Object.DestroyImmediate(genericBp);
        }

        // AC-2: carrier_v1 blueprint registration and query
        [Test]
        public void test_get_blueprint_carrier_v1_returns_non_null_with_carrier_properties() {
            // Given: a ShipBlueprint with carrier_v1 data
            var carrierBp = ScriptableObject.CreateInstance<ShipBlueprint>();
            carrierBp.BlueprintId = "carrier_v1";
            carrierBp.MaxHull = 200;
            carrierBp.ThrustPower = 5.0f;
            carrierBp.TurnSpeed = 120.0f;
            carrierBp.WeaponSlots = 0;
            carrierBp.BuildCost = new ResourceBundle(80, 40);
            carrierBp.RequiredShipyardTier = 1;
            carrierBp.CarrierInstanceId = null;
            carrierBp.HangarCapacity = 3;

            _registry.Register(carrierBp);

            // When: querying for carrier_v1
            var result = _registry.GetBlueprint("carrier_v1");

            // Then: returns non-null, BlueprintId = carrier_v1, CarrierInstanceId is null, HangarCapacity = 3
            Assert.IsNotNull(result);
            Assert.AreEqual("carrier_v1", result.BlueprintId);
            Assert.IsNull(result.CarrierInstanceId);
            Assert.AreEqual(3, result.HangarCapacity);

            Object.DestroyImmediate(carrierBp);
        }

        // AC-3: non-existent blueprintId returns null
        [Test]
        public void test_get_blueprint_non_existent_returns_null() {
            // Given: an empty registry
            // When: querying for a blueprint that does not exist
            var result = _registry.GetBlueprint("non_existent_v1");

            // Then: returns null
            Assert.IsNull(result);
        }

        // AC-4: IsValid returns true for a legal blueprint
        [Test]
        public void test_is_valid_returns_true_for_legal_blueprint() {
            // Given: a ShipBlueprint with valid stats (MaxHull=100, ThrustPower=8, TurnSpeed=180, WeaponSlots=1)
            var bp = ScriptableObject.CreateInstance<ShipBlueprint>();
            bp.BlueprintId = "generic_v1";
            bp.MaxHull = 100;
            bp.ThrustPower = 8.0f;
            bp.TurnSpeed = 180.0f;
            bp.WeaponSlots = 1;

            // When: calling IsValid
            var isValid = ShipBlueprint.IsValid(bp);

            // Then: returns true
            Assert.IsTrue(isValid);

            Object.DestroyImmediate(bp);
        }

        // AC-5: IsValid returns false for a blueprint with MaxHull = 0
        [Test]
        public void test_is_valid_returns_false_when_max_hull_is_zero() {
            // Given: a ShipBlueprint with MaxHull = 0
            var bp = ScriptableObject.CreateInstance<ShipBlueprint>();
            bp.BlueprintId = "invalid_bp";
            bp.MaxHull = 0;
            bp.ThrustPower = 8.0f;
            bp.TurnSpeed = 180.0f;
            bp.WeaponSlots = 1;

            // When: calling IsValid
            var isValid = ShipBlueprint.IsValid(bp);

            // Then: returns false
            Assert.IsFalse(isValid);

            Object.DestroyImmediate(bp);
        }
    }
}
