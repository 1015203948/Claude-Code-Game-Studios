using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Data;
using Game.Channels;
using Game.Gameplay;
using Gameplay;

/// <summary>
/// 驾驶舱战斗系统 — 管理战斗状态机、胜负判定。
/// 订阅 HealthSystem.OnShipDying，在驾驶舱内进行战斗仲裁。
/// 挂载于 CockpitScene。
///
/// Story 005: FireRequested 订阅（替代原有的 aim magnitude 轮询）：
/// - 订阅 ShipControlSystem.FireRequested 事件
/// - _fireTimer 在 BeginCombat 时重置，在 COMBAT_ACTIVE 期间每帧累加
/// - FireRequested 触发时满足 timer 条件则 FireWeapon()
/// </summary>
public class CombatSystem : MonoBehaviour
{
    public static CombatSystem Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────────
    // Weapon System
    // ─────────────────────────────────────────────────────────────────

    private WeaponSystem _weaponSystem;

    /// <summary>Maps enemy colliders to their instance IDs for hit resolution.</summary>
    private readonly Dictionary<Collider, string> _enemyColliders = new Dictionary<Collider, string>();

    // ─────────────────────────────────────────────────────────────────
    // Combat State
    // ─────────────────────────────────────────────────────────────────

    private enum CombatState { IDLE, COMBAT_ACTIVE, COMBAT_VICTORY, COMBAT_DEFEAT }

    private CombatState _state = CombatState.IDLE;
    private string _playerShipId;
    private string _nodeId;
    private List<string> _enemyIds = new List<string>();
    private int _initialEnemyCount;

    // ─────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>Test hook: resets Instance to null for test isolation. Do NOT use in production.</summary>
    internal static void ResetInstanceForTest() => Instance = null;

    private void OnEnable()
    {
        if (ShipControlSystem.Instance != null) {
            ShipControlSystem.Instance.FireRequested += OnFireRequested;
        }
    }

    private void OnDisable()
    {
        if (ShipControlSystem.Instance != null) {
            ShipControlSystem.Instance.FireRequested -= OnFireRequested;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Fire Rate Logic (Story 005)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Handler for ShipControlSystem.FireRequested (Story 021).
    /// Delegates to WeaponSystem for cooldown and firing.
    /// </summary>
    private void OnFireRequested()
    {
        if (_state != CombatState.COMBAT_ACTIVE) return;
        if (_weaponSystem == null) return;

        Transform playerTransform = ShipControlSystem.Instance != null
            ? ShipControlSystem.Instance.transform
            : transform;

        Vector3 fireOrigin = playerTransform.position + playerTransform.forward * 1f;
        _weaponSystem.TryFire(fireOrigin, playerTransform.forward, _enemyColliders);
    }

    private void Update()
    {
        if (_state != CombatState.COMBAT_ACTIVE) return;

        // AC-2: Frame-rate independent timer via Time.deltaTime accumulation
        _weaponSystem?.Tick(Time.deltaTime);
    }

    // ─────────────────────────────────────────────────────────────────
    // Public API (for HUD consumers)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fire cooldown progress [0..1]: 0 = just fired, 1 = fully recharged.
    /// </summary>
    public float FireCooldownProgress => _weaponSystem?.CooldownProgress ?? 0f;

    /// <summary>
    /// 开始战斗。
    /// 生成 2 个敌方实例，订阅 OnShipDying，广播战斗开始。
    /// </summary>
    public void BeginCombat(string shipId, string nodeId)
    {
        if (_state != CombatState.IDLE) {
            Debug.LogWarning($"[CombatSystem] BeginCombat called but state is {_state}, ignoring.");
            return;
        }

        _state = CombatState.COMBAT_ACTIVE;
        _playerShipId = shipId;
        _nodeId = nodeId;

        // Resolve player ship weapon config
        var ship = GameDataManager.Instance?.GetShip(shipId);
        float fireRate = ship?.TotalFireRate ?? 1f;
        float range = ship?.TotalRange ?? 200f;
        float damage = ship?.TotalWeaponDamage ?? 8f;
        DamageType damageType = DamageType.Physical;
        var equippedWeapons = ship?.EquippedWeapons;
        if (equippedWeapons != null) {
            foreach (var module in equippedWeapons) {
                damageType = module.DamageType;
                break;
            }
        }

        _weaponSystem = new WeaponSystem(
            fireRate, range, damage, damageType,
            new UnityPhysicsQuery(),
            new HealthSystemAdapter());

        // 生成 2 个敌方实例
        _enemyIds.Clear();
        _enemyColliders.Clear();
        Vector3 playerPos = transform.position;
        if (EnemySystem.Instance != null) {
            _enemyIds.Add(EnemySystem.Instance.SpawnEnemy("generic_v1", playerPos, 0));
            _enemyIds.Add(EnemySystem.Instance.SpawnEnemy("generic_v1", playerPos, 1));
        }
        _initialEnemyCount = _enemyIds.Count;

        // 订阅 HealthSystem.OnShipDying
        if (HealthSystem.Instance != null) {
            HealthSystem.Instance.OnShipDying += OnAnyShipDying;
        }

        // 广播战斗开始
        CombatChannel.Instance.RaiseBegin(_nodeId);

        Debug.Log($"[CombatSystem] Combat started at node {_nodeId} with {_enemyIds.Count} enemies.");
    }

    /// <summary>
    /// 结束战斗，清理状态。
    /// </summary>
    public void EndCombat()
    {
        if (_state == CombatState.IDLE) return;

        // 取消订阅
        if (HealthSystem.Instance != null) {
            HealthSystem.Instance.OnShipDying -= OnAnyShipDying;
        }

        // 销毁所有敌方实例
        foreach (var enemyId in _enemyIds) {
            EnemySystem.Instance?.DespawnEnemy(enemyId);
        }

        _enemyIds.Clear();
        _enemyColliders.Clear();
        _state = CombatState.IDLE;
        _playerShipId = null;
        _nodeId = null;
        _weaponSystem = null;
    }

    // ─────────────────────────────────────────────────────────────────
    // Enemy Collider Registration (for Raycast hit resolution)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Registers an enemy collider for Raycast hit resolution.
    /// Called automatically by EnemySystem.SpawnEnemy — do not call manually.
    /// </summary>
    public void RegisterEnemyCollider(Collider c, string instanceId)
    {
        if (c == null || string.IsNullOrEmpty(instanceId)) return;
        _enemyColliders[c] = instanceId;
    }

    // ─────────────────────────────────────────────────────────────────
    // Victory/Defeat Detection
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// OnShipDying 回调 — 判定是胜利还是失败。
    /// AC: OnShipDying 事件需判断是敌方死亡还是玩家死亡。
    /// </summary>
    private void OnAnyShipDying(string instanceId)
    {
        if (_state != CombatState.COMBAT_ACTIVE) return;

        if (IsPlayerShip(instanceId)) {
            // 玩家死亡 → 败
            _state = CombatState.COMBAT_DEFEAT;
            GameDataManager.Instance.GetShip(_playerShipId)?.Destroy();
            CombatChannel.Instance.RaiseDefeat(_nodeId);
            Debug.Log($"[CombatSystem] Combat DEFEAT at node {_nodeId}.");

            EndCombat();
            StartCoroutine(ReturnToStarMapDelayed(3f));
        } else if (_enemyIds.Contains(instanceId)) {
            // 敌方死亡 → 检查是否全灭
            _enemyIds.Remove(instanceId);
            if (_enemyIds.Count == 0) {
                // 胜利
                _state = CombatState.COMBAT_VICTORY;
                GameDataManager.Instance.GetShip(_playerShipId)?.SetState(ShipState.IN_COCKPIT);

                // Grant combat rewards
                GrantVictoryRewards();

                // Conquer node — set ownership to PLAYER
                ConquerNode();

                CombatChannel.Instance.RaiseVictory(_nodeId);
                Debug.Log($"[CombatSystem] Combat VICTORY at node {_nodeId}.");

                EndCombat();
                StartCoroutine(ReturnToStarMapDelayed(2f));
            }
        }
    }

    /// <summary>
    /// Returns to star map after a delay, allowing victory/defeat UI to display.
    /// </summary>
    private IEnumerator ReturnToStarMapDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);
        Game.Scene.ViewLayerManager.Instance?.RequestReturnToStarMap();
    }

    // ─────────────────────────────────────────────────────────────────
    // Victory Rewards & Node Conquest
    // ─────────────────────────────────────────────────────────────────

    private const int BASE_ORE_REWARD  = 50;
    private const int BASE_ENERGY_REWARD = 20;

    private void GrantVictoryRewards()
    {
        var colony = ColonyManager.Instance;
        if (colony == null) return;

        int oreReward = _initialEnemyCount * BASE_ORE_REWARD;
        int energyReward = _initialEnemyCount * BASE_ENERGY_REWARD;

        // NodeType multiplier
        var node = GetCombatNode();
        if (node != null && node.NodeType == NodeType.RICH) {
            oreReward *= 2;
            energyReward *= 2;
        }

        colony.AddResources(oreReward, energyReward);
        colony.Save();
        Debug.Log($"[CombatSystem] Reward: +{oreReward} Ore, +{energyReward} Energy from victory at node {_nodeId}");

        // Broadcast reward notification
        LootNotificationChannel.Instance?.Raise($"+{oreReward} Ore  +{energyReward} Energy");
    }

    private void ConquerNode()
    {
        var node = GetCombatNode();
        if (node == null) return;

        var previousOwnership = node.Ownership;
        node.Ownership = OwnershipState.PLAYER;
        Debug.Log($"[CombatSystem] Node {_nodeId} conquered: {previousOwnership} → PLAYER");
    }

    private StarNode GetCombatNode()
    {
        var map = GameDataManager.Instance?.GetStarMapData();
        if (map == null) return null;
        foreach (var node in map.Nodes) {
            if (node.Id == _nodeId) return node;
        }
        return null;
    }

    // ─────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// AC: 胜条件（OnShipDying 且敌方全部 HP=0）。
    /// AC: 败条件（OnShipDying 且玩家 HP=0）。
    /// </summary>
    private bool IsPlayerShip(string instanceId)
    {
        var ship = GameDataManager.Instance.GetShip(instanceId);
        return ship != null && ship.IsPlayerControlled;
    }

    /// <summary>
    /// 计算敌人生成位置 — 以玩家位置为中心，半径 200m 圆弧分布。
    /// </summary>
    private Vector3 ComputeSpawnPos(int index)
    {
        // 固定偏移，用于测试
        return new Vector3(index == 0 ? -50f : 50f, 0f, 200f);
    }
}
