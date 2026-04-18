namespace Game.Gameplay
{
    /// <summary>
    /// Tracks cumulative damage history for a single damage event.
     /// Each hit or tick produces one instance; the calculator
    /// accumulates multiple instances into a final result.
    /// </summary>
    public class DamageClass
    {
        /// <summary>Raw damage before any modifiers.</summary>
        public float BaseDamage;

        /// <summary>Type of damage for resistance matching.</summary>
        public DamageType Type;

        /// <summary>Armor-piercing value; reduces target armor by this amount.</summary>
        public float ArmorPierce;

        /// <summary>Critical hit flag; set by DamageCalculator on crit.</summary>
        public bool IsCrit;

        /// <summary>Creates an empty instance with zeroed fields.</summary>
        public DamageClass() { }

        /// <summary>
        /// Creates an instance with the given values.
        /// </summary>
        public DamageClass(float baseDamage, DamageType type, float armorPierce = 0f)
        {
            BaseDamage = baseDamage;
            Type = type;
            ArmorPierce = armorPierce;
        }
    }
}
