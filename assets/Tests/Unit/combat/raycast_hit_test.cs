#if false
using Game.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;
using System.Reflection;
using Object = UnityEngine.Object;

/// <summary>
/// CombatSystem Raycast 命中检测单元测试。
/// 覆盖 Story 006 验收标准（AC-1 ~ AC-3）。
///
/// AC-1（零 GC）：通过代码审查验证 RaycastHit[1] 预分配。
/// AC-2（命中检测）：通过 ApplyDamage 实际扣血验证调用链。
/// AC-3（射程 200m）：通过边界距离测试验证射程限制。
/// </summary>
[TestFixture]
public class RaycastHit_Test
{
    // ─────────────────────────────────────────────────────────────────
    // Fields
    // ─────────────────────────────────────────────────────────────────

    private CombatSystem _combatSystem;
    private GameObject _combatGo;
    private HealthSystem _healthSystem;
    private GameObject _healthGo;
    private GameObject _enemyGo;
    private BoxCollider _enemyCollider;
    private GameDataManager _gameDataManager;
    private ShipDataModel _playerShip;
    private ShipDataModel _enemyShip;
    private ShipBlueprint _playerBlueprint;
    private ShipStateChannel _playerStateChannel;
    private CombatChannel _combatChannel;
    private ShipControlSystem _shipControlSystem;
    private GameObject _shipControlGo;

    private MethodInfo _fireWeaponMethod;

    // ─────────────────────────────────────────────────────────────────
    // Constants
    // ─────────────────────────────────────────────────────────────────

    private const float BASE_DAMAGE = 8f;
    private const float ENEMY_MAX_HULL = 100f;

    // ─────────────────────────────────────────────────────────────────
    // SetUp / TearDown
    // ─────────────────────────────────────────────────────────────────

    [SetUp]
    public void SetUp()
    {
        // Reset singletons
        GameDataManager.ResetInstanceForTest();
        HealthSystem.ResetInstanceForTest();
        CombatSystem.ResetInstanceForTest();
        CombatChannel.ResetInstanceForTest();
        // Clean up scene-resident ShipControlSystem
        if (ShipControlSystem.Instance != null)
            Object.DestroyImmediate(ShipControlSystem.Instance.gameObject);
        ShipControlSystem.ResetInstanceForTest();

        // GameDataManager
        _gameDataManager = new GameDataManager();

        // Player ship
        _playerBlueprint = ScriptableObject.CreateInstance<ShipBlueprint>();
        _playerBlueprint.BlueprintId = "test_v1";
        _playerBlueprint.MaxHull = 100;
        _playerBlueprint.ThrustPower = 50f;
        _playerBlueprint.TurnSpeed = 90f;
        _playerBlueprint.WeaponSlots = 2;

        _playerStateChannel = ScriptableObject.CreateInstance<ShipStateChannel>();
        _playerShip = new ShipDataModel(
            "player-ship-1", "test_v1",
            isPlayerControlled: true,
            _playerBlueprint,
            _playerStateChannel);
        _gameDataManager.RegisterShip(_playerShip);

        // Enemy ship (for ApplyDamage target)
        var enemyBlueprint = ScriptableObject.CreateInstance<ShipBlueprint>();
        enemyBlueprint.BlueprintId = "enemy_v1";
        enemyBlueprint.MaxHull = 100;
        enemyBlueprint.ThrustPower = 50f;
        enemyBlueprint.TurnSpeed = 90f;
        enemyBlueprint.WeaponSlots = 2;

        var enemyStateChannel = ScriptableObject.CreateInstance<ShipStateChannel>();
        _enemyShip = new ShipDataModel(
            "enemy-ship-1", "enemy_v1",
            isPlayerControlled: false,
            enemyBlueprint,
            enemyStateChannel);
        _gameDataManager.RegisterShip(_enemyShip);

        // CombatChannel
        _combatChannel = ScriptableObject.CreateInstance<CombatChannel>();
        CombatChannel.Instance = _combatChannel;

        // HealthSystem
        _healthGo = new GameObject("HealthSystem");
        _healthSystem = _healthGo.AddComponent<HealthSystem>();

        // ShipControlSystem
        _shipControlGo = new GameObject("ShipControlSystem");
        _shipControlSystem = _shipControlGo.AddComponent<ShipControlSystem>();

        // CombatSystem GameObject (positioned at origin, facing +Z by default)
        _combatGo = new GameObject("CombatSystem");
        _combatGo.transform.position = Vector3.zero;
        _combatGo.transform.rotation = Quaternion.identity;
        _combatSystem = _combatGo.AddComponent<CombatSystem>();

        // Enemy collider GameObject (to be positioned in tests)
        _enemyGo = new GameObject("Enemy");
        _enemyCollider = _enemyGo.AddComponent<BoxCollider>();
        _enemyCollider.size = new Vector3(2f, 2f, 2f);

        // Register enemy collider → instance ID mapping
        _combatSystem.RegisterEnemyCollider(_enemyCollider, _enemyShip.InstanceId);

        // Get FireWeapon method
        _fireWeaponMethod = typeof(CombatSystem).GetMethod(
            "FireWeapon",
            BindingFlags.NonPublic | BindingFlags.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        if (_combatGo != null) Object.DestroyImmediate(_combatGo);
        if (_healthGo != null) Object.DestroyImmediate(_healthGo);
        if (_enemyGo != null) Object.DestroyImmediate(_enemyGo);
        if (_shipControlGo != null) Object.DestroyImmediate(_shipControlGo);
        if (_playerBlueprint != null) Object.DestroyImmediate(_playerBlueprint);
        if (_playerStateChannel != null) Object.DestroyImmediate(_playerStateChannel);
        if (_combatChannel != null) Object.DestroyImmediate(_combatChannel);

        GameDataManager.ResetInstanceForTest();
        HealthSystem.ResetInstanceForTest();
        CombatSystem.ResetInstanceForTest();
        CombatChannel.ResetInstanceForTest();
        ShipControlSystem.ResetInstanceForTest();
    }

    // ─────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────

    private void FireWeapon() => _fireWeaponMethod.Invoke(_combatSystem, null);

    private void PositionEnemyAtDistance(float meters)
    {
        _enemyGo.transform.position = new Vector3(0f, 0f, meters);
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-1: RaycastHit[1] pre-allocated — verified by code review
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Zero GC: _hits is declared as class member, not local variable.
    /// Verified by reading the field declaration.
    /// </summary>
    [Test]
    public void raycast_buffer_is_preallocated_class_member()
    {
        var hitsField = typeof(CombatSystem).GetField(
            "_hits",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(hitsField, "_hits field should exist on CombatSystem");

        var hits = hitsField.GetValue(_combatSystem) as RaycastHit[];
        Assert.IsNotNull(hits, "_hits should be a RaycastHit array");
        Assert.AreEqual(1, hits.Length, "_hits should be pre-allocated with length 1");
    }

    [Test]
    public void hits_buffer_is_readonly_to_prevent_reassignment()
    {
        var hitsField = typeof(CombatSystem).GetField(
            "_hits",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(hitsField);
        Assert.IsTrue(hitsField.IsInitOnly,
            "_hits should be readonly to prevent accidental reallocation in combat loop");
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-2: Hit detection calls ApplyDamage with (enemyId, 8f, KINETIC→Physical)
    // ─────────────────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator fire_weapon_applies_8_damage_on_enemy_hit()
    {
        // Given: enemy at 50m, registered; hull at 100
        PositionEnemyAtDistance(50f);
        _combatSystem.BeginCombat("player-ship-1", "node-A");
        Assert.AreEqual(ENEMY_MAX_HULL, _enemyShip.CurrentHull, 0.001f);

        // When: FireWeapon is called
        FireWeapon();
        yield return null;

        // Then: enemy took BASE_DAMAGE (8f) hull damage
        Assert.AreEqual(ENEMY_MAX_HULL - BASE_DAMAGE, _enemyShip.CurrentHull, 0.001f,
            $"Hit should apply {BASE_DAMAGE}f damage, leaving hull at {ENEMY_MAX_HULL - BASE_DAMAGE}");
    }

    [UnityTest]
    public IEnumerator fire_weapon_does_not_affect_hull_on_miss()
    {
        // Given: no enemy in range, hull at 100
        _combatSystem.BeginCombat("player-ship-1", "node-A");
        float hullBefore = _enemyShip.CurrentHull;

        // When: FireWeapon is called with no enemy in range
        FireWeapon();
        yield return null;

        // Then: hull unchanged
        Assert.AreEqual(hullBefore, _enemyShip.CurrentHull, 0.001f,
            "Hull should be unchanged when raycast misses");
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-3: WEAPON_RANGE = 200m respected
    // ─────────────────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator hit_detected_at_exactly_200m()
    {
        // Given: enemy at exactly 200m
        PositionEnemyAtDistance(200f);
        _combatSystem.BeginCombat("player-ship-1", "node-A");

        // When: FireWeapon is called
        FireWeapon();
        yield return null;

        // Then: hit detected (hull reduced by BASE_DAMAGE)
        Assert.AreEqual(ENEMY_MAX_HULL - BASE_DAMAGE, _enemyShip.CurrentHull, 0.001f,
            "Enemy at exactly 200m should be detected");
    }

    [UnityTest]
    public IEnumerator hit_not_detected_beyond_200m()
    {
        // Given: enemy at 201m (just beyond range)
        PositionEnemyAtDistance(201f);
        _combatSystem.BeginCombat("player-ship-1", "node-A");

        // When: FireWeapon is called
        FireWeapon();
        yield return null;

        // Then: no hit (hull unchanged)
        Assert.AreEqual(ENEMY_MAX_HULL, _enemyShip.CurrentHull, 0.001f,
            "Enemy at 201m should be beyond WEAPON_RANGE and not hit");
    }

    // ─────────────────────────────────────────────────────────────────
    // Additional: RegisterEnemyCollider null-safety
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void register_enemy_collider_ignores_null_collider()
    {
        Assert.DoesNotThrow(() => {
            _combatSystem.RegisterEnemyCollider(null, "any-id");
        });
    }

    [Test]
    public void register_enemy_collider_ignores_null_instance_id()
    {
        Assert.DoesNotThrow(() => {
            _combatSystem.RegisterEnemyCollider(_enemyCollider, null);
        });
    }
}

#endif
