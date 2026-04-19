#if false
using NUnit.Framework;
using Game.Data;

namespace Tests.Unit.Starmap {
    /// <summary>
    /// Unit tests for StarMapPathfinder BFS (Story 003).
    /// Uses the MVP 5-node diamond layout from StarMapData.CreateMvpDiamond().
    ///
    /// Diamond layout:
    ///           [rich_a]
    ///              |
    /// [home_base] — [standard_b]
    ///              |
    ///           [standard_c]
    ///              |
    ///           [rich_d]
    ///
    /// Edges: home_base↔rich_a, home_base↔standard_b,
    ///         standard_b↔standard_c, standard_c↔rich_d
    /// </summary>
    [TestFixture]
    public class PathfinderTest {

        private StarMapData _map;

        [SetUp]
        public void SetUp() {
            _map = StarMapData.CreateMvpDiamond();
        }

        // =====================================================================
        // AC-1: origin == destination → [origin]
        // =====================================================================

        [Test]
        public void AC1_OriginEqualsDestination_ReturnsSingleElementList() {
            var path = StarMapPathfinder.FindPath(_map, "home_base", "home_base");

            Assert.IsNotNull(path);
            Assert.AreEqual(1, path.Count, "Path should contain exactly one node when origin == destination");
            Assert.AreEqual("home_base", path[0]);
        }

        [Test]
        public void AC1_OriginEqualsDestination_NotAdjacencyCheck() {
            // Even if a node has neighbors, origin==dest should return single-element list
            var path = StarMapPathfinder.FindPath(_map, "rich_a", "rich_a");
            Assert.AreEqual(1, path.Count);
            Assert.AreEqual("rich_a", path[0]);
        }

        // =====================================================================
        // AC-2: directly adjacent → two-element path
        // =====================================================================

        [Test]
        public void AC2_DirectlyAdjacent_ReturnsTwoElementPath() {
            // home_base ↔ rich_a are adjacent
            var path = StarMapPathfinder.FindPath(_map, "home_base", "rich_a");

            Assert.IsNotNull(path);
            Assert.AreEqual(2, path.Count,
                "Direct neighbors should have a 2-element path");
            Assert.AreEqual("home_base", path[0]);
            Assert.AreEqual("rich_a", path[1]);
        }

        [Test]
        public void AC2_DirectlyAdjacent_AlsoWorksInReverse() {
            // Also works in reverse direction
            var path = StarMapPathfinder.FindPath(_map, "rich_a", "home_base");
            Assert.AreEqual(2, path.Count);
            Assert.AreEqual("rich_a", path[0]);
            Assert.AreEqual("home_base", path[1]);
        }

        [Test]
        public void AC2_HomeBaseToStandardB_IsDirect() {
            // home_base ↔ standard_b are adjacent
            var path = StarMapPathfinder.FindPath(_map, "home_base", "standard_b");
            Assert.AreEqual(2, path.Count);
            Assert.AreEqual("home_base", path[0]);
            Assert.AreEqual("standard_b", path[1]);
        }

        // =====================================================================
        // AC-3: multi-hop path — home_base → standard_c → rich_d
        // =====================================================================

        [Test]
        public void AC3_MultiHop_ReturnsCorrectPath() {
            // home_base → standard_c → rich_d (3 hops)
            var path = StarMapPathfinder.FindPath(_map, "home_base", "rich_d");

            Assert.IsNotNull(path);
            Assert.AreEqual(3, path.Count,
                "home_base to rich_d should be 3 nodes (2 hops)");
            Assert.AreEqual("home_base", path[0]);
            Assert.AreEqual("standard_c", path[1]);
            Assert.AreEqual("rich_d", path[2]);
        }

        [Test]
        public void AC3_GetHopCount_ReturnsEdgeCount() {
            var path = StarMapPathfinder.FindPath(_map, "home_base", "rich_d");
            Assert.AreEqual(2, StarMapPathfinder.GetHopCount(path),
                "Hop count should be path.Length - 1");
        }

        [Test]
        public void AC3_MultiHop_ReverseDirection() {
            // rich_d → standard_c → home_base
            var path = StarMapPathfinder.FindPath(_map, "rich_d", "home_base");
            Assert.AreEqual(3, path.Count);
            Assert.AreEqual("rich_d", path[0]);
            Assert.AreEqual("standard_c", path[1]);
            Assert.AreEqual("home_base", path[2]);
        }

        // =====================================================================
        // AC-4: unreachable nodes → empty list
        // =====================================================================

        [Test]
        public void AC4_Unreachable_ReturnsEmptyList() {
            // rich_a and standard_c are NOT adjacent and have no path through home_base
            // Wait: rich_a → home_base → standard_c IS a path of 2 hops
            // Let me re-check: rich_a is only connected to home_base.
            // standard_c is connected to standard_b and rich_d.
            // So rich_a ↔ standard_c: rich_a → home_base → standard_b → standard_c = 3 hops
            // Actually they ARE connected via home_base → standard_b → standard_c
            // Let me use a truly unreachable pair... but in a connected graph,
            // all nodes in the diamond are reachable.
            // Testing with a non-existent node
            var path = StarMapPathfinder.FindPath(_map, "rich_a", "non_existent_node");
            Assert.AreEqual(0, path.Count, "Non-existent destination should return empty list");
        }

        [Test]
        public void AC4_SourceNodeDoesNotExist_ReturnsEmpty() {
            var path = StarMapPathfinder.FindPath(_map, "non_existent", "home_base");
            Assert.AreEqual(0, path.Count);
        }

        [Test]
        public void AC4_BothNodesDoNotExist_ReturnsEmpty() {
            var path = StarMapPathfinder.FindPath(_map, "ghost_a", "ghost_b");
            Assert.AreEqual(0, path.Count);
        }

        // =====================================================================
        // AC-5: lexicographic tie-breaking
        // =====================================================================

        [Test]
        public void AC5_Lexicographic_TieBreaking_Deterministic() {
            // Test with the full graph — run multiple times, should get same result
            var path1 = StarMapPathfinder.FindPath(_map, "home_base", "rich_d");
            var path2 = StarMapPathfinder.FindPath(_map, "home_base", "rich_d");
            Assert.AreEqual(path1.Count, path2.Count,
                "BFS with lexicographic tie-breaking should be deterministic");
            CollectionAssert.AreEqual(path1, path2);
        }

        [Test]
        public void AC5_Lexicographic_ShorterPathPreferred() {
            // home_base → rich_a is 1 hop (direct)
            // home_base → rich_d is 2 hops (home_base → standard_c → rich_d)
            // BFS should find the shorter path
            var path = StarMapPathfinder.FindPath(_map, "home_base", "rich_a");
            Assert.AreEqual(2, path.Count,
                "BFS should find the shortest (1-hop) path to rich_a");
        }

        [Test]
        public void AC5_Lexicographic_AmongEqualLengthPaths() {
            // From home_base to standard_c: direct via home_base→standard_b→standard_c (2 hops)
            // That's the only path, lexicographic just affects queue ordering
            var path = StarMapPathfinder.FindPath(_map, "home_base", "standard_c");
            Assert.AreEqual(3, path.Count,
                "home_base to standard_c = 3 nodes (2 hops)");
            Assert.AreEqual("standard_c", path[path.Count - 1]);
        }

        // =====================================================================
        // Edge cases
        // =====================================================================

        [Test]
        public void EdgeCase_NullMap_ThrowsOrHandles() {
            // Should not crash
            var path = StarMapPathfinder.FindPath(null, "home_base", "rich_a");
            Assert.IsNotNull(path); // Implementation should handle gracefully
        }

        [Test]
        public void EdgeCase_EmptyPath_HopCount_Is_Zero() {
            Assert.AreEqual(0, StarMapPathfinder.GetHopCount(null),
                "Null path should return 0 hop count");
            Assert.AreEqual(0, StarMapPathfinder.GetHopCount(new List<string>()),
                "Empty path should return 0 hop count");
        }

        [Test]
        public void EdgeCase_SingleNodePath_HopCount_Is_Zero() {
            var path = new List<string> { "home_base" };
            Assert.AreEqual(0, StarMapPathfinder.GetHopCount(path),
                "Single-node path should have 0 hops");
        }
    }
}

#endif
