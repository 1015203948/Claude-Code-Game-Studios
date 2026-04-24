using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// Encapsulates weapon firing mechanics: cooldown, raycast hit detection, and damage application.
    /// Plain C# class (no MonoBehaviour) for unit-testability.
    ///
    /// Consumed by CombatSystem during combat. Config is injected at construction time;
    /// damage value should come from ShipDataModel.TotalWeaponDamage so that equipment modules
    /// and hull blueprints drive combat numbers.
    /// </summary>
    public class WeaponSystem
    {
        // ─────────────────────────────────────────────────────────────────
        // Config (injected)
        // ─────────────────────────────────────────────────────────────────

        private readonly float _fireRate;      // shots/sec
        private readonly float _range;         // meters
        private readonly float _damage;        // per shot
        private readonly DamageType _damageType;

        // ─────────────────────────────────────────────────────────────────
        // Runtime state
        // ─────────────────────────────────────────────────────────────────

        private float _fireTimer;
        private readonly RaycastHit[] _hits;   // pre-allocated, zero GC

        // ─────────────────────────────────────────────────────────────────
        // Dependencies (injected)
        // ─────────────────────────────────────────────────────────────────

        private readonly IPhysicsQuery _physics;
        private readonly IHealthSystem _health;

        // ─────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Cooldown progress [0..1]: 0 = just fired, 1 = fully recharged.
        /// </summary>
        public float CooldownProgress
        {
            get
            {
                float cooldownDuration = 1f / _fireRate;
                return Mathf.Min(_fireTimer / cooldownDuration, 1f);
            }
        }

        /// <summary>
        /// True if the weapon has cooled down enough to fire.
        /// </summary>
        public bool CanFire => _fireTimer >= (1f / _fireRate);

        /// <summary>
        /// Creates a new WeaponSystem with the given config and dependencies.
        /// </summary>
        public WeaponSystem(
            float fireRate,
            float range,
            float damage,
            DamageType damageType,
            IPhysicsQuery physics,
            IHealthSystem health)
        {
            _fireRate = fireRate;
            _range = range;
            _damage = damage;
            _damageType = damageType;
            _physics = physics;
            _health = health;
            _hits = new RaycastHit[1]; // zero GC — same size as original CombatSystem
            _fireTimer = 0f;
        }

        /// <summary>
        /// Advances the cooldown timer. Call from CombatSystem.Update().
        /// </summary>
        public void Tick(float deltaTime)
        {
            _fireTimer += deltaTime;
        }

        /// <summary>
        /// Attempts to fire the weapon.
        /// Returns true if a shot was actually fired (cooldown was ready).
        /// </summary>
        /// <param name="origin">World-space fire origin.</param>
        /// <param name="direction">World-space fire direction (should be normalized).</param>
        /// <param name="targetMap">Collider → enemy instanceId mapping from CombatSystem.</param>
        public bool TryFire(Vector3 origin, Vector3 direction, Dictionary<Collider, string> targetMap)
        {
            float cooldownDuration = 1f / _fireRate;
            if (_fireTimer < cooldownDuration)
                return false;

            _fireTimer = 0f;

            int count = _physics.Raycast(origin, direction, _hits, _range);
            if (count > 0 && targetMap.TryGetValue(_hits[0].collider, out var enemyId))
            {
                _health.ApplyDamage(enemyId, _damage, _damageType);
                Debug.Log($"[WeaponSystem] Hit {enemyId} — applied {_damage} {_damageType} damage.");
            }

            return true;
        }

        /// <summary>
        /// Resets the cooldown timer to zero. Used when combat begins.
        /// </summary>
        public void ResetCooldown()
        {
            _fireTimer = 0f;
        }
    }
}
