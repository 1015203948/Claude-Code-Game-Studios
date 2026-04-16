namespace MyGame
{
    /// <summary>
    /// Defines the categories of damage that can be dealt in combat.
    /// Used for damage calculation, resistance matching, and VFX routing.
    /// </summary>
    public enum DamageType
    {
        /// <summary>Physical damage from melee and projectile impacts.</summary>
        Physical,

        /// <summary>Energy damage from lasers, plasma, and EMP bursts.</summary>
        Energy,

        /// <summary>Explosive damage from grenades, missiles, and barrels.</summary>
        Explosive,

        /// <summary>Burn damage from fire and plasma DoT effects.</summary>
        Burn,

        /// <summary>Corrosive damage that reduces armor over time.</summary>
        Corrosive,

        /// <summary>Piercing damage that ignores a portion of armor.</summary>
        Piercing
    }
}
