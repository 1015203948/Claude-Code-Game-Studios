using NUnit.Framework;
using Game.Data;

namespace Tests.Unit.Data {
    [TestFixture]
    public class ResourceConfigTest {

        // AC-1: ORE_CAP clamp
        [Test]
        public void test_ore_accumulation_clamps_to_ore_cap() {
            int oreCurrent = 80;
            int oreCap = 100;
            int delta = 30;
            int result = ResourceConfig.ClampOre(delta, oreCurrent, oreCap);
            Assert.AreEqual(100, result);
        }

        [Test]
        public void test_ore_accumulation_clamps_to_zero() {
            int oreCurrent = 5;
            int oreCap = 100;
            int delta = -10;
            int result = ResourceConfig.ClampOre(delta, oreCurrent, oreCap);
            Assert.AreEqual(0, result);
        }

        [Test]
        public void test_ore_accumulation_no_clamp_when_within_bounds() {
            int oreCurrent = 50;
            int oreCap = 100;
            int delta = 30;
            int result = ResourceConfig.ClampOre(delta, oreCurrent, oreCap);
            Assert.AreEqual(80, result);
        }

        // AC-2: CanAfford returns true
        [Test]
        public void test_can_afford_returns_true_when_sufficient() {
            int currentOre = 60;
            int currentEnergy = 20;
            var cost = new ResourceBundle(50, 15);
            bool result = ResourceConfig.CanAfford(currentOre, currentEnergy, cost);
            Assert.IsTrue(result);
        }

        // AC-3: CanAfford returns false (ore insufficient)
        [Test]
        public void test_can_afford_returns_false_when_ore_insufficient() {
            int currentOre = 40;
            int currentEnergy = 20;
            var cost = new ResourceBundle(50, 15);
            bool result = ResourceConfig.CanAfford(currentOre, currentEnergy, cost);
            Assert.IsFalse(result);
        }

        // AC-4: CanAfford returns false (energy insufficient)
        [Test]
        public void test_can_afford_returns_false_when_energy_insufficient() {
            int currentOre = 60;
            int currentEnergy = 10;
            var cost = new ResourceBundle(50, 15);
            bool result = ResourceConfig.CanAfford(currentOre, currentEnergy, cost);
            Assert.IsFalse(result);
        }

        [Test]
        public void test_can_afford_returns_false_when_both_insufficient() {
            int currentOre = 30;
            int currentEnergy = 5;
            var cost = new ResourceBundle(50, 15);
            bool result = ResourceConfig.CanAfford(currentOre, currentEnergy, cost);
            Assert.IsFalse(result);
        }

        [Test]
        public void test_can_afford_exact_match() {
            int currentOre = 50;
            int currentEnergy = 15;
            var cost = new ResourceBundle(50, 15);
            bool result = ResourceConfig.CanAfford(currentOre, currentEnergy, cost);
            Assert.IsTrue(result);
        }

        [Test]
        public void test_can_afford_zero_cost() {
            int currentOre = 0;
            int currentEnergy = 0;
            var cost = new ResourceBundle(0, 0);
            bool result = ResourceConfig.CanAfford(currentOre, currentEnergy, cost);
            Assert.IsTrue(result);
        }
    }
}
