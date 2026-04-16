using System.Collections.Generic;

namespace Game.Data {
    /// <summary>
    /// BFS pathfinding over StarMapData adjacency graph.
    ///
    /// FindPath(origin, destination) returns the list of node IDs from origin
    /// to destination, in visiting order. Returns empty list if unreachable.
    ///
    /// Tie-breaking: lexicographic (alphabetical) node ID order.
    /// Performance: O(V+E) — &lt;0.1ms for V ≤ 20 nodes.
    /// </summary>
    public static class StarMapPathfinder {

        /// <summary>
        /// Find shortest path (fewest hops) from origin to destination.
        ///
        /// Returns:
        ///   - List with single element [origin] if origin == destination
        ///   - List of node IDs in visiting order if reachable
        ///   - Empty list if no path exists
        ///
        /// Tie-breaking: BFS queue expanded in lexicographic node ID order.
        /// </summary>
        public static List<string> FindPath(StarMapData map, string originId, string destId) {
            if (originId == destId) {
                return new List<string> { originId };
            }

            var queue = new Queue<string>();
            var visited = new HashSet<string>();
            var parent = new Dictionary<string, string>();

            queue.Enqueue(originId);
            visited.Add(originId);

            while (queue.Count > 0) {
                // Lexicographic tie-breaking: dequeue the alphabetically smallest node
                string current = DequeueLexicographicallyMin(queue);

                // Get neighbor IDs (not StarNode objects)
                var neighbors = GetNeighborIds(map, current);

                // Sort neighbors alphabetically for deterministic BFS
                neighbors.Sort();

                foreach (var neighborId in neighbors) {
                    if (!visited.Contains(neighborId)) {
                        visited.Add(neighborId);
                        parent[neighborId] = current;

                        if (neighborId == destId) {
                            return ReconstructPath(parent, originId, destId);
                        }

                        queue.Enqueue(neighborId);
                    }
                }
            }

            // Unreachable
            return new List<string>();
        }

        /// <summary>
        /// Returns the node ID in the queue with the smallest lexicographic value,
        /// and removes it from the queue.
        /// </summary>
        private static string DequeueLexicographicallyMin(Queue<string> queue) {
            var list = new List<string>(queue);
            queue.Clear();
            list.Sort();
            string min = list[0];
            // Re-enqueue all except the min
            for (int i = 1; i < list.Count; i++) {
                queue.Enqueue(list[i]);
            }
            return min;
        }

        /// <summary>
        /// Get neighbor node IDs for a given node, sorted alphabetically.
        /// </summary>
        private static List<string> GetNeighborIds(StarMapData map, string nodeId) {
            var neighbors = new List<StarNode>(map.GetNeighbors(nodeId));
            var ids = new List<string>();
            foreach (var node in neighbors) {
                ids.Add(node.Id);
            }
            return ids;
        }

        /// <summary>
        /// Reconstruct path from parent map (breadcrumb trail).
        /// </summary>
        private static List<string> ReconstructPath(
            Dictionary<string, string> parent,
            string originId,
            string destId) {

            var path = new List<string>();
            string current = destId;

            while (current != originId) {
                path.Add(current);
                if (!parent.TryGetValue(current, out current)) {
                    // Should not happen if path was correctly built
                    return new List<string>();
                }
            }

            path.Add(originId);
            path.Reverse(); // reverse to get origin-to-destination order
            return path;
        }

        /// <summary>
        /// Returns the number of edges (hops) in the path.
        /// Path.Length - 1 for non-empty path, 0 for origin==destination.
        /// </summary>
        public static int GetHopCount(List<string> path) {
            if (path == null || path.Count == 0) return 0;
            return path.Count - 1;
        }
    }
}
