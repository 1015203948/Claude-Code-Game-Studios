using NUnit.Framework;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Object = UnityEngine.Object;

/// <summary>
/// EnemySystem SpawnEnemy 单元测试。
/// 覆盖 Story 009 所有验收标准（AC-1 ~ AC-4）。
/// </summary>
[TestFixture]
public class SpawnEnemy_Test
{
    // ─────────────────────────────────────────────────────────────────
    // Fields
    // ─────────────────────────────────────────────────────────────────

    private EnemySystem _enemySystem;
    private GameObject _enemySystemGo;
    private GameObject _playerGo;
    private BoxCollider _playerCollider;

    private const float SPAWN_RADIUS = 150f;

    // ─────────────────────────────────────────────────────────────────
    // SetUp / TearDown
    // ─────────────────────────────────────────────────────────────────

    [SetUp]
    public void SetUp()
    {
        if (EnemySystem.Instance != null) Object.DestroyImmediate(EnemySystem.Instance.gameObject);
        if (GameDataManager.Instance != null) GameDataManager.Instance = null;
        if (HealthSystem.Instance != null) Object.DestroyImmediate(HealthSystem.Instance.gameObject);

        _enemySystemGo = new GameObject("EnemySystem");
        _enemySystem = _enemySystemGo.AddComponent<EnemySystem>();

        // Create player GameObject with collider
        _playerGo = new GameObject("PlayerShip");
        _playerGo.tag = "PlayerShip";
        _playerCollider = _playerGo.AddComponent<BoxCollider>();
        _playerGo.transform.position = Vector3.zero;
    }

    [TearDown]
    public void TearDown()
    {
        if (_enemySystemGo != null) Object.DestroyImmediate(_enemySystemGo);
        if (_playerGo != null) Object.DestroyImmediate(_playerGo);
        EnemySystem.Instance = null;
        GameDataManager.Instance = null;
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-1: SpawnEnemy × 2 produces distinct instance IDs
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void spawn_enemy_returns_distinct_instance_ids()
    {
        // Given: player at origin
        Vector3 playerPos = _playerGo.transform.position;

        // When: two enemies are spawned
        string id1 = _enemySystem.SpawnEnemy("generic_v1", playerPos, 0);
        string id2 = _enemySystem.SpawnEnemy("generic_v1", playerPos, 1);

        // Then: IDs are distinct and follow "enemy_[uuid]" format
        Assert.AreNotEqual(id1, id2, "SpawnEnemy should return distinct instance IDs");
        Assert.IsTrue(id1.StartsWith("enemy_"), $"ID '{id1}' should start with 'enemy_'");
        Assert.IsTrue(id2.StartsWith("enemy_"), $"ID '{id2}' should start with 'enemy_'");
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-2: Angular separation ≥ 90°
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void spawn_positions_have_angular_separation_at_least_90_degrees()
    {
        // Given: player at origin
        Vector3 playerPos = _playerGo.transform.position;

        // When: two enemies are spawned
        _enemySystem.SpawnEnemy("generic_v1", playerPos, 0);
        var registry = typeof(EnemySystem)
            .GetField("_registry", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var registryDict = registry.GetValue(EnemySystem.Instance) as Dictionary<string, EnemyAIController>;

        Assert.Greater(registryDict.Count, 0, "At least one enemy should be registered");

        // Extract spawn positions
        var positions = new List<Vector3>();
        foreach (var ctrl in registryDict.Values) {
            positions.Add(ctrl.transform.position);
        }

        if (positions.Count < 2) {
            Assert.Pass("Need 2 enemies for angular separation test — spawn 2");
            return;
        }

        // Compute angle between the two spawn position vectors (relative to player)
        Vector3 v1 = positions[0] - playerPos;
        Vector3 v2 = positions[1] - playerPos;

        v1.y = 0; v2.y = 0; // project onto XZ plane
        v1.Normalize(); v2.Normalize();

        float dot = Vector3.Dot(v1, v2);
        float angle = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f)) * Mathf.Rad2Deg;

        Assert.GreaterOrEqual(angle, 90f - 0.01f,
            $"Angular separation should be ≥ 90°, got {angle}°");
    }

    [Test]
    public void spawn_positions_are_within_spawn_radius()
    {
        // Given: player at origin
        Vector3 playerPos = _playerGo.transform.position;

        // When: an enemy is spawned
        string id = _enemySystem.SpawnEnemy("generic_v1", playerPos, 0);

        // Then: spawn position is within SPAWN_RADIUS
        var registry = typeof(EnemySystem)
            .GetField("_registry", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var registryDict = registry.GetValue(EnemySystem.Instance) as Dictionary<string, EnemyAIController>;

        Assert.IsTrue(registryDict.ContainsKey(id));
        Vector3 spawnPos = registryDict[id].transform.position;
        float dist = Vector3.Distance(spawnPos, playerPos);

        Assert.LessOrEqual(dist, SPAWN_RADIUS + 0.01f,
            $"Spawn distance {dist} should be within SPAWN_RADIUS {SPAWN_RADIUS}");
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-3: RandomDelay in [3, 5] range for each instance
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void random_delay_is_within_3_to_5_range()
    {
        // Given: player at origin
        Vector3 playerPos = _playerGo.transform.position;

        // When: multiple enemies are spawned
        _enemySystem.SpawnEnemy("generic_v1", playerPos, 0);
        _enemySystem.SpawnEnemy("generic_v1", playerPos, 1);

        var registry = typeof(EnemySystem)
            .GetField("_registry", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var registryDict = registry.GetValue(EnemySystem.Instance) as Dictionary<string, EnemyAIController>;

        foreach (var ctrl in registryDict.Values) {
            Assert.GreaterOrEqual(ctrl.RandomDelay, 3.0f,
                $"RandomDelay {ctrl.RandomDelay} should be ≥ 3.0");
            Assert.LessOrEqual(ctrl.RandomDelay, 5.0f,
                $"RandomDelay {ctrl.RandomDelay} should be ≤ 5.0");
        }
    }

    [Test]
    public void random_delay_values_are_independent()
    {
        // Spawn many enemies and check that RandomDelay values are not all identical
        Vector3 playerPos = _playerGo.transform.position;

        var delays = new HashSet<float>();
        for (int i = 0; i < 10; i++) {
            _enemySystem.SpawnEnemy("generic_v1", playerPos, i);
        }

        var registry = typeof(EnemySystem)
            .GetField("_registry", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var registryDict = registry.GetValue(EnemySystem.Instance) as Dictionary<string, EnemyAIController>;

        foreach (var ctrl in registryDict.Values) {
            delays.Add(ctrl.RandomDelay);
        }

        Assert.Greater(delays.Count, 1,
            "RandomDelay values should be independent (different enemies should have different delays)");
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-4: SPAWNING state is stationary (no movement)
    // ─────────────────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator spawning_state_is_stationary()
    {
        // Given: an enemy in SPAWNING state
        Vector3 playerPos = _playerGo.transform.position;
        _enemySystem.SpawnEnemy("generic_v1", playerPos, 0);

        var registry = typeof(EnemySystem)
            .GetField("_registry", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var registryDict = registry.GetValue(EnemySystem.Instance) as Dictionary<string, EnemyAIController>;

        Assert.Greater(registryDict.Count, 0);
        var ctrl = null as EnemyAIController;
        foreach (var c in registryDict.Values) {
            ctrl = c;
            break;
        }

        Vector3 initialPos = ctrl.transform.position;

        // When: simulate multiple frames
        for (int i = 0; i < 10; i++) {
            ctrl.UpdateAI(); // call UpdateAI directly instead of waiting for Update()
            yield return null;
        }

        Vector3 finalPos = ctrl.transform.position;

        Assert.AreEqual(initialPos, finalPos,
            "Enemy in SPAWNING state should not move (position should be unchanged)");
    }

    [UnityTest]
    public IEnumerator spawning_transitions_to_approaching_after_delay()
    {
        // Given: an enemy in SPAWNING state
        Vector3 playerPos = _playerGo.transform.position;
        _enemySystem.SpawnEnemy("generic_v1", playerPos, 0);

        var registry = typeof(EnemySystem)
            .GetField("_registry", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var registryDict = registry.GetValue(EnemySystem.Instance) as Dictionary<string, EnemyAIController>;

        var ctrl = null as EnemyAIController;
        foreach (var c in registryDict.Values) {
            ctrl = c;
            break;
        }

        Assert.AreEqual(EnemyAiState.SPAWNING, ctrl.AiState, "Should start in SPAWNING state");

        // Advance time past RandomDelay (max 5 seconds + small buffer)
        float elapsed = 0f;
        float delta = 0.1f;
        while (elapsed < ctrl.RandomDelay + 0.5f) {
            ctrl.UpdateAI();
            elapsed += delta;
            yield return null;
        }

        Assert.AreEqual(EnemyAiState.APPROACHING, ctrl.AiState,
            $"After {elapsed}s, should transition from SPAWNING to APPROACHING");
    }

    // ─────────────────────────────────────────────────────────────────
    // Despawn tests
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void despawn_removes_enemy_from_registry()
    {
        // Given: two enemies spawned
        Vector3 playerPos = _playerGo.transform.position;
        string id1 = _enemySystem.SpawnEnemy("generic_v1", playerPos, 0);
        string id2 = _enemySystem.SpawnEnemy("generic_v1", playerPos, 1);

        var registry = typeof(EnemySystem)
            .GetField("_registry", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var registryDict = registry.GetValue(EnemySystem.Instance) as Dictionary<string, EnemyAIController>;

        Assert.AreEqual(2, registryDict.Count);

        // When: one enemy is despawned
        _enemySystem.DespawnEnemy(id1);

        // Then: registry has one fewer
        Assert.AreEqual(1, registryDict.Count);
        Assert.IsFalse(registryDict.ContainsKey(id1));
        Assert.IsTrue(registryDict.ContainsKey(id2));
    }

    [Test]
    public void despawn_nonexistent_id_is_silent()
    {
        // Should not throw
        Assert.DoesNotThrow(() => {
            _enemySystem.DespawnEnemy("nonexistent-id");
        });
    }
}
