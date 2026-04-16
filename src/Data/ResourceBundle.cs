using System;
using UnityEngine;

namespace Game.Data {
    [Serializable]
    public struct ResourceBundle {
        public int Ore;
        public int Energy;

        public ResourceBundle(int ore, int energy) {
            Ore = ore;
            Energy = energy;
        }

        public static ResourceBundle operator +(ResourceBundle a, ResourceBundle b) =>
            new ResourceBundle(a.Ore + b.Ore, a.Energy + b.Energy);

        public static ResourceBundle operator -(ResourceBundle a, ResourceBundle b) =>
            new ResourceBundle(a.Ore - b.Ore, a.Energy - b.Energy);

        public override string ToString() => $"ResourceBundle(Ore={Ore}, Energy={Energy})";
    }
}
