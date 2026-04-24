using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Game.Channels;
using Gameplay;

namespace Game.Data {
    /// <summary>
    /// Authoritative runtime state for a single ship instance.
    /// Lives in MasterScene as single source of truth (ADR-0004).
    ///
    /// Key invariants:
    ///   - IN_COCKPIT is mutually exclusive: only one ship globally can be IN_COCKPIT
    ///   - DESTROYED is terminal: after entering DESTROYED, no state transitions are allowed
    ///   - IsPlayerControlled is set at construction and is immutable
    /// </summary>
    public class ShipDataModel {
        private readonly ShipStateChannel _shipStateChannel;
        private readonly HullBlueprint _blueprint;

        // 模块装备（每个 SlotType 最多一个，已装备则为 non-null）
        private Dictionary<SlotType, EquipmentModule> _equippedModules = new();

        // 全局仓库（所有未装备模块）
        private static List<EquipmentModule> _inventory = new List<EquipmentModule>();

        /// <summary>Unique identifier for this ship instance.</summary>
        public string InstanceId { get; }

        /// <summary>Blueprint ID this ship was instantiated from.</summary>
        public string BlueprintId { get; }

        /// <summary>Current hull points. Goes from MaxHull → 0.</summary>
        public float CurrentHull { get; private set; }

        /// <summary>Current ship state. See ShipState enum.</summary>
        public ShipState State { get; private set; }

        /// <summary>Node ID this ship is currently docked at. Null if in transit or in cockpit.</summary>
        public string DockedNodeId { get; set; }

        /// <summary>
        /// Carrier instance ID for carrier-type ships. Null for non-carriers.
        /// Set at construction — never changes.
        /// </summary>
        public string CarrierInstanceId { get; }

        /// <summary>
        /// Set at construction — NEVER mutable after initialization.
        /// </summary>
        public bool IsPlayerControlled { get; }

        /// <summary>Maximum hull points from the blueprint.</summary>
        public float MaxHull { get; }

        /// <summary>Blueprint for this ship.</summary>
        public HullBlueprint HullBlueprint => _blueprint;

        // =====================================================================
        // Hull mutation (for HealthSystem external writes)
        // =====================================================================

        /// <summary>
        /// Sets CurrentHull directly. Used by HealthSystem.ApplyDamage.
        /// Clamps to [0, MaxHull].
        /// </summary>
        public void SetHull(float value) {
            CurrentHull = Mathf.Clamp(value, 0f, MaxHull);
        }

        // =====================================================================
        // Construction
        // =====================================================================

        /// <summary>
        /// Creates a new ship data model.
        ///
        /// IsPlayerControlled is set here and is immutable — attempting to modify
        /// it after construction will cause a compilation error (no setter).
        /// </summary>
        public ShipDataModel(
            string instanceId,
            string blueprintId,
            bool isPlayerControlled,
            HullBlueprint blueprint,
            ShipStateChannel shipStateChannel) {

            if (string.IsNullOrEmpty(instanceId)) {
                Debug.LogError("[ShipDataModel] instanceId cannot be null or empty.");
            }
            if (string.IsNullOrEmpty(blueprintId)) {
                Debug.LogError("[ShipDataModel] blueprintId cannot be null or empty.");
            }

            InstanceId = instanceId;
            BlueprintId = blueprintId;
            IsPlayerControlled = isPlayerControlled;
            _shipStateChannel = shipStateChannel;
            _blueprint = blueprint;

            // Initialize from blueprint
            MaxHull = blueprint != null ? blueprint.MaxHull : 100f;
            CurrentHull = MaxHull;

            // Initial state
            State = ShipState.DOCKED;
            DockedNodeId = null;
            CarrierInstanceId = blueprint?.CarrierInstanceId;
        }

        // =====================================================================
        // State transitions
        // =====================================================================

        /// <summary>
        /// Attempt to transition to a new state.
        ///
        /// Returns true if transition succeeded (State updated + broadcast).
        /// Returns false if:
        ///   - Ship is already DESTROYED (terminal state — all transitions rejected)
        ///   - Transition is not in the legal sequence (see GDD ship-system.md state machine)
        ///   - IN_COCKPIT mutual exclusion violated (another ship is already IN_COCKPIT)
        ///
        /// Broadcasts ShipStateChannel on success.
        /// </summary>
        public bool SetState(ShipState newState) {
            // DESTROYED is terminal — reject all further transitions
            if (State == ShipState.DESTROYED) {
                Debug.LogWarning($"[ShipDataModel] {InstanceId}: DESTROYED is terminal — rejecting SetState({newState})");
                return false;
            }

            // IN_COCKPIT mutual exclusion: reject if another ship is already IN_COCKPIT
            if (newState == ShipState.IN_COCKPIT && !CanEnterCockpit()) {
                Debug.LogWarning($"[ShipDataModel] {InstanceId}: IN_COCKPIT rejected — another ship already in cockpit.");
                return false;
            }

            // Validate legal transition sequence (simplified — full state machine in GDD)
            if (!IsLegalTransition(State, newState)) {
                Debug.LogWarning($"[ShipDataModel] {InstanceId}: Illegal transition {State} → {newState}");
                return false;
            }

            // Atomic update
            ShipState previousState = State;
            State = newState;

            // Update IN_COCKPIT mutual exclusion tracking
            if (newState == ShipState.IN_COCKPIT) {
                GameDataManager.Instance?.SetActiveCockpitShip(InstanceId);
            } else if (previousState == ShipState.IN_COCKPIT) {
                GameDataManager.Instance?.ClearActiveCockpitShip(InstanceId);
            }

            // Broadcast state change
            _shipStateChannel?.Raise((InstanceId, newState));

            return true;
        }

        /// <summary>
        /// Marks ship as destroyed. Irreversible.
        ///
        /// U-4 PATH EVENT DOCUMENTATION:
        /// - DOES fire:   ShipStateChannel (InstanceId, DESTROYED) — for UI/HUD updates
        /// - DOES fire:   ShipStateChannel.Raise() — for EnemyAIController targeting
        /// - Does NOT fire: HealthSystem.OnShipDying — U-4 bypasses HealthSystem by design
        ///
        /// This distinction matters: any system that tracks player death must subscribe to
        /// ShipStateChannel (for U-4 path) in addition to HealthSystem.OnShipDying
        /// (for cockpit combat path). See ADR-0013 / ADR-0017 for context.
        /// </summary>
        public bool Destroy() {
            if (State == ShipState.DESTROYED) return false; // already destroyed

            ShipState previousState = State;
            State = ShipState.DESTROYED;

            // Clear cockpit tracking if we were in cockpit
            if (previousState == ShipState.IN_COCKPIT) {
                GameDataManager.Instance?.ClearActiveCockpitShip(InstanceId);
            }

            _shipStateChannel?.Raise((InstanceId, ShipState.DESTROYED));
            return true;
        }

        // =====================================================================
        // IN_COCKPIT mutual exclusion
        // =====================================================================

        /// <summary>
        /// Checks whether this ship can enter IN_COCKPIT state.
        /// Returns true if: no other ship is currently IN_COCKPIT.
        /// </summary>
        public bool CanEnterCockpit() {
            return !GameDataManager.HasActiveCockpitShip()
                   || GameDataManager.Instance.GetActiveCockpitShipId() == InstanceId;
        }

        // =====================================================================
        // State machine helper
        // =====================================================================

        /// <summary>
        /// Returns true if the transition from current → next is legal.
        /// Simplified state machine from GDD ship-system.md.
        ///
        /// Legal transitions:
        ///   DOCKED → IN_TRANSIT, IN_COCKPIT
        ///   IN_TRANSIT → DOCKED (arrived), IN_COMBAT (enemy contact)
        ///   IN_COCKPIT → DOCKED (exited cockpit), IN_COMBAT
        ///   IN_COMBAT → DOCKED (victory + returned), DESTROYED (defeat)
        ///   DESTROYED → (terminal, no outbound transitions)
        /// </summary>
        private static bool IsLegalTransition(ShipState current, ShipState next) {
            if (current == next) return false; // no self-transition

            return (current, next) switch {
                (ShipState.DOCKED, ShipState.IN_TRANSIT) => true,
                (ShipState.DOCKED, ShipState.IN_COCKPIT) => true,
                (ShipState.IN_TRANSIT, ShipState.DOCKED) => true,
                (ShipState.IN_TRANSIT, ShipState.IN_COMBAT) => true,
                (ShipState.IN_COCKPIT, ShipState.DOCKED) => true,
                (ShipState.IN_COCKPIT, ShipState.IN_COMBAT) => true,
                (ShipState.IN_COMBAT, ShipState.DOCKED) => true,
                (ShipState.IN_COMBAT, ShipState.DESTROYED) => true,
                _ => false,
            };
        }

        // =====================================================================
        // Runtime state interface (for GameDataManager serialization)
        // =====================================================================

        /// <summary>
        /// Apply hull damage. Clamps to [0, MaxHull].
        /// </summary>
        public void ApplyDamage(float amount) {
            if (amount < 0) {
                Debug.LogWarning("[ShipDataModel] ApplyDamage: amount must be non-negative.");
                return;
            }
            CurrentHull = Mathf.Clamp(CurrentHull - amount, 0f, MaxHull);

            if (CurrentHull <= 0f) {
                Destroy();
            }
        }

        /// <summary>
        /// Repair hull. Clamps to [0, MaxHull].
        /// </summary>
        public void Repair(float amount) {
            if (amount < 0) {
                Debug.LogWarning("[ShipDataModel] Repair: amount must be non-negative.");
                return;
            }
            CurrentHull = Mathf.Clamp(CurrentHull + amount, 0f, MaxHull);
        }

        // =====================================================================
        // Ship physics parameters (from blueprint)
        // =====================================================================

        /// <summary>Returns this ship's thrust power from its blueprint.</summary>
        public float GetThrustPower() {
            return _blueprint?.ThrustPower ?? 15f;
        }

        /// <summary>Returns this ship's turn speed from its blueprint.</summary>
        public float GetTurnSpeed() {
            return _blueprint?.TurnSpeed ?? 120f;
        }

        // =====================================================================
        // Equipment system
        // =====================================================================

        /// <summary>All equipped weapon modules.</summary>
        public IEnumerable<EquipmentModule> EquippedWeapons => _equippedModules.Values
            .Where(m => m != null && m.SlotType == SlotType.Weapon);

        /// <summary>Total weapon damage = base + all equipped weapon modules.</summary>
        public float TotalWeaponDamage => (_blueprint?.BaseWeaponDamage ?? 0f)
            + EquippedWeapons.Sum(m => m.Damage);

        /// <summary>Total weapon fire rate = base + all equipped weapon modules.</summary>
        public float TotalFireRate => (_blueprint?.BaseFireRate ?? 1f)
            + EquippedWeapons.Sum(m => m.FireRate);

        /// <summary>Total weapon range = max of all equipped weapon modules (or hull base if none).</summary>
        public float TotalRange
        {
            get
            {
                var weapons = EquippedWeapons.ToList();
                if (weapons.Count == 0) return _blueprint?.BaseRange ?? 200f;
                return weapons.Max(m => m.Range);
            }
        }

        /// <summary>Total speed = base + all equipped engine modules.</summary>
        public float TotalSpeed => (_blueprint?.BaseSpeed ?? 0f)
            + _equippedModules.Values
                .Where(m => m != null && m.SlotType == SlotType.Engine)
                .Sum(m => m.Speed);

        /// <summary>Total shield = base + all equipped shield modules.</summary>
        public float TotalShield => (_blueprint?.BaseShield ?? 0f)
            + _equippedModules.Values
                .Where(m => m != null && m.SlotType == SlotType.Shield)
                .Sum(m => m.Shield);

        /// <summary>Total cargo = base + all equipped cargo modules.</summary>
        public float TotalCargo => (_blueprint?.BaseCargo ?? 0f)
            + _equippedModules.Values
                .Where(m => m != null && m.SlotType == SlotType.Cargo)
                .Sum(m => m.Cargo);

        /// <summary>
        /// Equip a module into its slot. Replaces any existing module in that slot.
        /// The replaced module is returned; caller can add it back to inventory.
        /// </summary>
        public EquipmentModule EquipModule(EquipmentModule module) {
            if (module == null) return null;
            var old = _equippedModules.GetValueOrDefault(module.SlotType);
            _equippedModules[module.SlotType] = module;
            _inventory.Remove(module);
            return old;
        }

        /// <summary>
        /// Unequip a module from a slot and return it to inventory.
        /// </summary>
        public EquipmentModule UnequipModule(SlotType slot) {
            var old = _equippedModules.GetValueOrDefault(slot);
            if (old != null) {
                _inventory.Add(old);
                _equippedModules[slot] = null;
            }
            return old;
        }

        /// <summary>Get the equipped module in a slot, or null if empty.</summary>
        public EquipmentModule GetEquipped(SlotType slot)
            => _equippedModules.GetValueOrDefault(slot);

        // =====================================================================
        // Static inventory access
        // =====================================================================

        /// <summary>All modules currently in the global inventory (not equipped).</summary>
        public static IReadOnlyList<EquipmentModule> Inventory => _inventory.AsReadOnly();

        /// <summary>Add a module to the global inventory.</summary>
        public static void AddToInventory(EquipmentModule module) {
            if (module != null) _inventory.Add(module);
        }

        /// <summary>Clear all modules from the global inventory. For testing.</summary>
        public static void ClearInventory() {
            _inventory.Clear();
        }
    }
}
