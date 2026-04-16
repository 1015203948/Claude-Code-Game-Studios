using NUnit.Framework;
using UnityEngine;
using Game.Data;

namespace Tests.Unit.Starmap {
    [TestFixture]
    public class StarMapDataTest {
        private StarMapData _mapData;

        [SetUp]
        public void SetUp() {
            _mapData = StarMapData.CreateMvpDiamond();
        }

        [Test]
        public void AC1_FiveNodesInitialized_HomeBaseVisible_OthersUnexplored() {
            // AC-1: 5 nodes initialized, HOME_BASE fogState=VISIBLE, others=UNEXPLORED
            Assert.AreEqual(5, _mapData.Nodes.Count);

            foreach (var node in _mapData.Nodes) {
                if (node.NodeType == NodeType.HOME_BASE) {
                    Assert.AreEqual(FogState.VISIBLE, node.FogState);
                } else {
                    Assert.AreEqual(FogState.UNEXPLORED, node.FogState);
                }
            }
        }

        [Test]
        public void AC2_AreAdjacent_ReturnsCorrectResults() {
            // AC-2: AreAdjacent — home_base↔rich_a = true, home_base↔standard_c = true, rich_a↔standard_c = false
            Assert.IsTrue(_mapData.AreAdjacent("home_base", "rich_a"));
            Assert.IsTrue(_mapData.AreAdjacent("rich_a", "home_base"));
            Assert.IsTrue(_mapData.AreAdjacent("home_base", "standard_b"));
            Assert.IsTrue(_mapData.AreAdjacent("standard_b", "standard_c"));
            Assert.IsTrue(_mapData.AreAdjacent("standard_c", "rich_d"));

            Assert.IsFalse(_mapData.AreAdjacent("home_base", "standard_c"));
            Assert.IsFalse(_mapData.AreAdjacent("rich_a", "standard_c"));
            Assert.IsFalse(_mapData.AreAdjacent("home_base", "rich_d"));
        }

        [Test]
        public void AC3_GetNeighbors_HomeBaseReturnsTwoNeighbors() {
            // AC-3: GetNeighbors(home_base) returns [rich_a, standard_b] (2 neighbors)
            var neighbors = _mapData.GetNeighbors("home_base");
            Assert.AreEqual(2, neighbors.Count);

            var neighborIds = new System.Collections.Generic.List<string>();
            foreach (var n in neighbors) {
                neighborIds.Add(n.Id);
            }
            Assert.Contains("rich_a", neighborIds);
            Assert.Contains("standard_b", neighborIds);
        }

        [Test]
        public void AC4_GetNode_NonExistentReturnsNull() {
            // AC-4: GetNode("non_existent") returns null
            Assert.IsNull(_mapData.GetNode("non_existent"));
        }

        [Test]
        public void GetNode_ExistingNodes_ReturnsCorrectNode() {
            var node = _mapData.GetNode("home_base");
            Assert.IsNotNull(node);
            Assert.AreEqual("HOME_BASE", node.DisplayName);
            Assert.AreEqual(new Vector2(0, 0), node.Position);
            Assert.AreEqual(NodeType.HOME_BASE, node.NodeType);
        }

        [Test]
        public void AreAdjacent_NonExistentNode_ReturnsFalse() {
            Assert.IsFalse(_mapData.AreAdjacent("home_base", "non_existent"));
            Assert.IsFalse(_mapData.AreAdjacent("non_existent", "home_base"));
        }

        [Test]
        public void GetNeighbors_NonExistentNode_ReturnsEmptyList() {
            var neighbors = _mapData.GetNeighbors("non_existent");
            Assert.AreEqual(0, neighbors.Count);
        }

        [Test]
        public void CreateMvpDiamond_AllNodeTypesPresent() {
            Assert.IsNotNull(_mapData.GetNode("home_base"));
            Assert.IsNotNull(_mapData.GetNode("rich_a"));
            Assert.IsNotNull(_mapData.GetNode("standard_b"));
            Assert.IsNotNull(_mapData.GetNode("standard_c"));
            Assert.IsNotNull(_mapData.GetNode("rich_d"));

            Assert.AreEqual(NodeType.HOME_BASE, _mapData.GetNode("home_base").NodeType);
            Assert.AreEqual(NodeType.RICH, _mapData.GetNode("rich_a").NodeType);
            Assert.AreEqual(NodeType.STANDARD, _mapData.GetNode("standard_b").NodeType);
            Assert.AreEqual(NodeType.STANDARD, _mapData.GetNode("standard_c").NodeType);
            Assert.AreEqual(NodeType.RICH, _mapData.GetNode("rich_d").NodeType);
        }
    }
}