namespace Game.Data {
    [System.Serializable]
    public class StarEdge {
        public string FromNodeId { get; }
        public string ToNodeId { get; }

        public StarEdge(string fromNodeId, string toNodeId) {
            FromNodeId = fromNodeId;
            ToNodeId = toNodeId;
        }

        public bool Contains(string nodeId) => FromNodeId == nodeId || ToNodeId == nodeId;
        public string Other(string nodeId) => FromNodeId == nodeId ? ToNodeId : FromNodeId;
    }
}