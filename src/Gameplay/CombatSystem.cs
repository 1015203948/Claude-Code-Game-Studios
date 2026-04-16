using UnityEngine;
using System.Collections.Generic;
using Game.Data;
using Game.Channels;
using Game.Gameplay;
using MyGame;

//// TODO (Story 009): Replace MockEnemySystem with real EnemySystem.Instance calls

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
    // Fire Rate Timer
    // ─────────────────────────────────────────────────────────────────

    /// <summary>Time accumulator for fire rate limiting. Frame-rate independent.</summary>
    private float _fireTimer = 0f;

    private const float FIRE_ANGLE_THRESHOLD = 15f;   // degrees
    private const float WEAPON_FIRE_RATE = 1.0f;     // shots/sec
    private const float WEAPON_RANGE = 200f;         // meters
    private const float BASE_DAMAGE = 8f;            // HP per hit

    /// <summary>Pre-allocated RaycastNonAlloc buffer — zero GC in combat loop.</summary>
    private readonly RaycastHit[] _hits = new RaycastHit[1];

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
    /// Fires weapon if cooldown elapsed.
    /// </summary>
    private void OnFireRequested()
    {
        if (_state != CombatState.COMBAT_ACTIVE) return;
        if (_fireTimer < (1f / WEAPON_FIRE_RATE)) return;

        FireWeapon();
        _fireTimer = 0f;
    }

    private void Update()
    {
        if (_state != CombatState.COMBAT_ACTIVE) return;

        // AC-2: Frame-rate independent timer via Time.deltaTime accumulation
        _fireTimer += Time.deltaTime;
    }

    // ─────────────────────────────────────────────────────────────────
    // Fire Weapon
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fires the player's weapon. Handles fire rate cooldown and Raycast hit detection.
    /// Zero GC: uses pre-allocated RaycastHit[1] buffer.
    /// </summary>
    private void FireWeapon()
    {
        Vector3 fireOrigin = transform.position + transform.forward * 1f;
        int count = Physics.RaycastNonAlloc(
            fireOrigin,
            transform.forward,
            _hits,
            WEAPON_RANGE);

        if (count > 0 && _enemyColliders.TryGetValue(_hits[0].collider, out var enemyId)) {
            HealthSystem.Instance?.ApplyDamage(enemyId, BASE_DAMAGE, DamageType.Physical);
            Debug.Log($"[CombatSystem] Hit {enemyId} — applied {BASE_DAMAGE} damage.");
        }
    }

    /// <summary>
    /// Returns true if the current aim angle is within the fire threshold.
    /// Uses ShipControlSystem aim direction when available (Story 021).
    /// </summary>
    private bool IsAimAngleWithinThreshold()
    {
        // Story 021 will provide a FireRequested event.
        // Until then, use ShipControlSystem aim magnitude as a proxy.
        if (ShipControlSystem.Instance != null) {
            return ShipControlSystem.Instance.GetAimDirection().magnitude > 0.9f;
        }
        return false;
    }

    // ─────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────

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
        _fireTimer = 0f;

        // 生成 2 个敌方实例
        _enemyIds.Clear();
        _enemyColliders.Clear();
        Vector3 playerPos = transform.position;
        if (EnemySystem.Instance != null) {
            _enemyIds.Add(EnemySystem.Instance.SpawnEnemy("generic_v1", playerPos, 0));
            _enemyIds.Add(EnemySystem.Instance.SpawnEnemy("generic_v1", playerPos, 1));
        }

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
        _fireTimer = 0f;
    }

    // ─────────────────────────────────────────────────────────────────
    // Enemy Collider Registration (for Raycast hit resolution)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Registers an enemy collider for Raycast hit resolution.
    /// Call this when EnemySystem spawns an enemy with a collider.
    /// TODO (Story 009): EnemySystem will call this automatically on spawn.
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
            // U-4 路径：绕过 HealthSystem，直接 DestroyShip
            GameDataManager.Instance.GetShip(_playerShipId)?.Destroy();
            CombatChannel.Instance.RaiseDefeat(_nodeId);
            Debug.Log($"[CombatSystem] Combat DEFEAT at node {_nodeId}.");
        } else if (_enemyIds.Contains(instanceId)) {
            // 敌方死亡 → 检查是否全灭
            _enemyIds.Remove(instanceId);
            if (_enemyIds.Count == 0) {
                // 胜利
                _state = CombatState.COMBAT_VICTORY;
                // 状态恢复为 IN_COCKPIT
                GameDataManager.Instance.GetShip(_playerShipId)?.SetState(ShipState.IN_COCKPIT);
                CombatChannel.Instance.RaiseVictory(_nodeId);
                Debug.Log($"[CombatSystem] Combat VICTORY at node {_nodeId}.");
            }
        }

        // 无论胜负，战斗都结束
        EndCombat();
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

// ─────────────────────────────────────────────────────────────────
// Mock Systems（Story 009 实施前使用）
// ─────────────────────────────────────────────────────────────────

/// <summary>
/// MockEnemySystem — Story 009 实施前用。
/// 临时模拟 EnemySystem 的 SpawnEnemy / DespawnEnemy 接口。
/// </summary>
public static class MockEnemySystem
{
    private static int _spawnCounter = 0;

    public static string SpawnEnemy(string blueprintId, Vector3 spawnPos)
    {
        _spawnCounter++;
        return $"mock-enemy-{_spawnCounter}";
    }

    public static void DespawnEnemy(string instanceId)
    {
        // No-op for mock
    }
}
