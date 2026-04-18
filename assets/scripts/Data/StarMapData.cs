using System.Collections.Generic;
using UnityEngine;

namespace Game.Data {
    /// <summary>
    /// Layer 2 Runtime data — held by GameDataManager in MasterScene.
    /// All query methods are read-only.
    /// </summary>
    public class StarMapData {
        public IReadOnlyList<StarNode> Nodes { get; }
        public IReadOnlyList<StarEdge> Edges { get; }

        private readonly Dictionary<string, List<string>> _adjacencyIndex;

        public StarMapData(List<StarNode> nodes, List<StarEdge> edges) {
            Nodes = nodes;
            Edges = edges;
            _adjacencyIndex = new Dictionary<string, List<string>>();
            BuildAdjacencyIndex();
            InitFogState();
        }

        /// <summary>
        /// O(V+E) adjacency index construction.
        /// </summary>
        private void BuildAdjacencyIndex() {
            _adjacencyIndex.Clear();
            foreach (var node in Nodes) {
                _adjacencyIndex[node.Id] = new List<string>();
            }
            foreach (var edge in Edges) {
                _adjacencyIndex[edge.FromNodeId].Add(edge.ToNodeId);
                _adjacencyIndex[edge.ToNodeId].Add(edge.FromNodeId);
            }
        }

        /// <summary>
        /// Initialize fogState: HOME_BASE is VISIBLE, all others UNEXPLORED.
        /// </summary>
        private void InitFogState() {
            foreach (var node in Nodes) {
                if (node.NodeType == NodeType.HOME_BASE) {
                    node.FogState = FogState.VISIBLE;
                }
            }
        }

        public IReadOnlyList<StarNode> GetNeighbors(string nodeId) {
            if (!_adjacencyIndex.TryGetValue(nodeId, out var neighbors)) {
                return new List<StarNode>();
            }
            var result = new List<StarNode>();
            foreach (var nId in neighbors) {
                foreach (var node in Nodes) {
                    if (node.Id == nId) { result.Add(node); break; }
                }
            }
            return result;
        }

        public bool AreAdjacent(string nodeA, string nodeB) {
            if (!_adjacencyIndex.TryGetValue(nodeA, out var neighbors)) return false;
            return neighbors.Contains(nodeB);
        }

        public StarNode GetNode(string nodeId) {
            foreach (var node in Nodes) {
                if (node.Id == nodeId) return node;
            }
            return null;
        }

        /// <summary>
        /// Creates MVP 5-node diamond layout.
        /// </summary>
        public static StarMapData CreateMvpDiamond() {
            var nodes = new List<StarNode> {
                new StarNode("home_base", "HOME_BASE", new Vector2(0, 0), NodeType.HOME_BASE),
                new StarNode("rich_a", "RICH-A", new Vector2(0, 1), NodeType.RICH),
                new StarNode("standard_b", "STANDARD-B", new Vector2(1, 0), NodeType.STANDARD),
                new StarNode("standard_c", "STANDARD-C", new Vector2(0, -1), NodeType.STANDARD),
                new StarNode("rich_d", "RICH-D", new Vector2(0, -2), NodeType.RICH),
            };
            var edges = new List<StarEdge> {
                new StarEdge("home_base", "rich_a"),
                new StarEdge("home_base", "standard_b"),
                new StarEdge("standard_b", "standard_c"),
                new StarEdge("standard_c", "rich_d"),
            };
            return new StarMapData(nodes, edges);
        }
    }
}