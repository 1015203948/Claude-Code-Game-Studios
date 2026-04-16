using System;

namespace Game.Data {
    [Serializable]
    public readonly struct ResourceSnapshot {
        public readonly int Ore;
        public readonly int Energy;
        public readonly int OreDelta;
        public readonly int EnergyDelta;

        public ResourceSnapshot(int ore, int energy) {
            Ore = ore;
            Energy = energy;
            OreDelta = 0;
            EnergyDelta = 0;
        }

        public ResourceSnapshot(int ore, int energy, int oreDelta, int energyDelta) {
            Ore = ore;
            Energy = energy;
            OreDelta = oreDelta;
            EnergyDelta = energyDelta;
        }
    }
}
