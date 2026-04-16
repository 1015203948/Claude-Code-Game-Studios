using System.Collections.Generic;

namespace Game.Data {
    /// <summary>
    /// Singleton registry for ShipBlueprint lookup.
    /// Registration happens via Editor or initialization code calling Register().
    /// </summary>
    public class ShipBlueprintRegistry {
        public static ShipBlueprintRegistry Instance { get; private set; }

        private readonly Dictionary<string, ShipBlueprint> _blueprints;

        public ShipBlueprintRegistry() {
            Instance = this;
            _blueprints = new Dictionary<string, ShipBlueprint>();
        }

        public void Register(ShipBlueprint bp) {
            if (bp == null || string.IsNullOrEmpty(bp.BlueprintId)) return;
            _blueprints[bp.BlueprintId] = bp;
        }

        public ShipBlueprint GetBlueprint(string blueprintId) {
            return _blueprints.TryGetValue(blueprintId, out var bp) ? bp : null;
        }

        public IReadOnlyList<ShipBlueprint> GetAllBlueprints() {
            return new List<ShipBlueprint>(_blueprints.Values);
        }

        public int Count => _blueprints.Count;
    }
}
