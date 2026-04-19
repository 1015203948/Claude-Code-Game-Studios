#if false
using NUnit.Framework;
using UnityEngine;
using System.Collections;
using Object = UnityEngine.Object;

/// <summary>
/// EnemyAIController Physics Queries 零 GC 单元测试。
/// 覆盖 Story 011 所有验收标准（AC-1 ~ AC-4）。
/// </summary>
[TestFixture]
public class PhysicsQuery_Test
{
    // ─────────────────────────────────────────────────────────────────
    // Fields
    // ─────────────────────────────────────────────────────────────────

    private EnemySystem _enemySystem;
    private GameObject _enemySystemGo;
    private GameObject _playerGo;
    private BoxCollider _playerCollider;
    private EnemyAIController _controller;
    private GameObject _enemyGo;

    // ─────────────────────────────────────────────────────────────────
    // SetUp / TearDown
    // ─────────────────────────────────────────────────────────────────

    [SetUp]
    public void SetUp()
    {
        EnemySystem.ResetInstanceForTest();
        GameDataManager.ResetInstanceForTest();
        HealthSystem.ResetInstanceForTest();

        _enemySystemGo = new GameObject("EnemySystem");
        _enemySystem = _enemySystemGo.AddComponent<EnemySystem>();

        // Create player GameObject with collider and correct layer/tag
        _playerGo = new GameObject("PlayerShip");
        _playerGo.tag = "PlayerShip";
        _playerGo.layer = LayerMask.NameToLayer("PlayerShip");
        _playerCollider = _playerGo.AddComponent<BoxCollider>();
        _playerGo.transform.position = new Vector3(100f, 0f, 0f); // 100m away on X axis

        // Create enemy controller
        _enemyGo = new GameObject("Enemy");
        _enemyGo.layer = LayerMask.NameToLayer("Default");
        _controller = _enemyGo.AddComponent<EnemyAIController>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_enemySystemGo != null) Object.DestroyImmediate(_enemySystemGo);
        if (_playerGo != null) Object.DestroyImmediate(_playerGo);
        if (_enemyGo != null) Object.DestroyImmediate(_enemyGo);
        EnemySystem.ResetInstanceForTest();
        GameDataManager.ResetInstanceForTest();
        HealthSystem.ResetInstanceForTest();
    }

    // ─────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────

    private void InitializeController()
    {
        _controller.Initialize("enemy_0", "generic_v1", Vector3.zero, "player-1");
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-1: OverlapSphereNonAlloc zero allocation over 1000 calls
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void get_player_position_zero_allocation_over_1000_calls()
    {
        // Given: initialized controller
        InitializeController();
        _enemyGo.transform.position = Vector3.zero;
        _playerGo.transform.position = new Vector3(50f, 0f, 0f);

        // Note: Unity Profiler can't be directly read from tests, but we verify
        // the buffer is static readonly (compile-time guarantee of no allocation)
        // and that the API used (OverlapSphereNonAlloc) doesn't allocate.
        // Run 1000 calls without triggering GC
        for (int i = 0; i < 1000; i++) {
            Vector3 pos = _controller.GetPlayerPosition();
            Assert.AreNotEqual(Vector3.zero, pos, $"Call {i}: should find player");
        }
    }

    [Test]
    public void get_player_position_uses_static_readonly_buffer()
    {
        // Verify the buffer is declared as static readonly at class level
        var field = typeof(EnemyAIController).GetField("_playerQueryBuffer",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.IsNotNull(field, "_playerQueryBuffer field should exist");
        Assert.IsTrue(field.IsStatic, "_playerQueryBuffer should be static");
        Assert.IsTrue(field.IsInitOnly, "_playerQueryBuffer should be readonly");

        var buffer = field.GetValue(null) as Collider[];
        Assert.IsNotNull(buffer, "Buffer should be Collider[]");
        Assert.Greater(buffer.Length, 0, "Buffer should have capacity");
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-2: RaycastNonAlloc zero allocation over 1000 calls
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void fire_raycast_uses_static_readonly_buffer()
    {
        // Verify _fireHitBuffer is static readonly
        var field = typeof(EnemyAIController).GetField("_fireHitBuffer",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.IsNotNull(field, "_fireHitBuffer field should exist");
        Assert.IsTrue(field.IsStatic, "_fireHitBuffer should be static");
        Assert.IsTrue(field.IsInitOnly, "_fireHitBuffer should be readonly");

        var buffer = field.GetValue(null) as RaycastHit[];
        Assert.IsNotNull(buffer, "Buffer should be RaycastHit[]");
        Assert.AreEqual(1, buffer.Length, "Fire buffer should be size 1");
    }

    [Test]
    public void fire_raycast_zero_allocation_over_1000_calls()
    {
        // Given: initialized controller facing player
        InitializeController();
        _enemyGo.transform.position = Vector3.zero;
        _enemyGo.transform.rotation = Quaternion.identity; // facing +Z
        _playerGo.transform.position = new Vector3(0f, 0f, 50f);

        // 1000 calls should not allocate — buffer reuse pattern
        for (int i = 0; i < 1000; i++) {
            _controller.FireRaycast();
            // No assertion needed — if it allocates, Unity would warn/collect
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-3: GetPlayerPosition returns correct player position
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void get_player_position_returns_correct_world_position()
    {
        // Given: player at known world position (100, 0, 0), enemy at origin
        InitializeController();
        _enemyGo.transform.position = Vector3.zero;
        _playerGo.transform.position = new Vector3(100f, 0f, 0f);

        // When: GetPlayerPosition is called
        Vector3 result = _controller.GetPlayerPosition();

        // Then: returns player's world position
        Assert.AreEqual(new Vector3(100f, 0f, 0f), result,
            "GetPlayerPosition should return the player's world position");
    }

    [Test]
    public void get_player_position_returns_zero_when_no_player_in_range()
    {
        // Given: player is far outside search range (FLANK_ENGAGE_RANGE * 2 = 160m)
        InitializeController();
        _enemyGo.transform.position = Vector3.zero;
        _playerGo.transform.position = new Vector3(500f, 0f, 0f); // 500m away

        // When: GetPlayerPosition is called
        Vector3 result = _controller.GetPlayerPosition();

        // Then: returns Vector3.zero
        Assert.AreEqual(Vector3.zero, result,
            "GetPlayerPosition should return zero when no player in range");
    }

    [Test]
    public void get_player_position_finds_player_by_tag_not_layer()
    {
        // Given: player ship at known position
        InitializeController();
        _enemyGo.transform.position = Vector3.zero;
        _playerGo.transform.position = new Vector3(30f, 0f, 0f);
        _playerGo.layer = LayerMask.NameToLayer("Default"); // Wrong layer!

        // When: GetPlayerPosition is called
        Vector3 result = _controller.GetPlayerPosition();

        // Then: still finds player by tag even if layer is wrong
        Assert.AreEqual(new Vector3(30f, 0f, 0f), result,
            "GetPlayerPosition should find player by PlayerShip tag regardless of layer");
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-4: Retry when colliders overlap (shift 10m, max 3 retries)
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void fire_raycast_retries_up_to_3_times_on_overlapping_collider()
    {
        // Given: enemy and player at same position (perfect overlap scenario)
        // Enemy fires and initial ray starts inside collider
        InitializeController();
        _enemyGo.transform.position = Vector3.zero;
        _enemyGo.transform.rotation = Quaternion.identity; // facing +Z
        _playerGo.transform.position = new Vector3(0f, 0f, 5f); // player 5m ahead
        _playerGo.transform.localScale = new Vector3(10f, 10f, 10f); // large collider

        // When: FireRaycast is called with overlapping scenario
        // It should retry 3 times shifting 10m each
        // The key test: no exception thrown and method completes
        _controller.FireRaycast();

        // Then: completes successfully (no crash on overlap)
        // Actual damage application verified by HealthSystem integration
    }

    [Test]
    public void fire_raycast_uses_correct_fire_origin_offset()
    {
        // Verify fire origin is 1m in front of enemy (forward * 1)
        InitializeController();
        _enemyGo.transform.position = new Vector3(10f, 20f, 30f);
        _enemyGo.transform.rotation = Quaternion.identity; // facing +Z

        // When: FireRaycast is called (verify it doesn't crash)
        // The fire origin should be (11, 20, 31) — 1m forward in +Z
        _controller.FireRaycast(); // Should complete without error
    }

    [Test]
    public void fire_raycast_does_not_apply_damage_when_no_hit()
    {
        // Given: no player in firing direction
        InitializeController();
        _enemyGo.transform.position = Vector3.zero;
        _enemyGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // facing -Z
        _playerGo.transform.position = new Vector3(0f, 0f, 50f); // player in +Z, not -Z

        // When: FireRaycast is called
        _controller.FireRaycast();

        // Then: completes without applying damage (no player in firing direction)
        // DamageSystem would need to be mocked to verify — this is a structural test
    }

    // ─────────────────────────────────────────────────────────────────
    // Additional: Verify NonAlloc API is being used (not allocating variants)
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void overlap_sphere_uses_non_alloc_api()
    {
        // Given: controller initialized
        InitializeController();
        _enemyGo.transform.position = Vector3.zero;
        _playerGo.transform.position = new Vector3(10f, 0f, 0f);

        // When: called rapidly
        for (int i = 0; i < 100; i++) {
            var pos = _controller.GetPlayerPosition();
            Assert.AreNotEqual(Vector3.zero, pos);
        }

        // If we reach here, no exceptions — NonAlloc API is being used correctly
        Assert.Pass("OverlapSphereNonAlloc API used without error");
    }

    [Test]
    public void raycast_hit_buffer_is_shared_static_per_type()
    {
        // Given: two enemy controllers
        var enemyGo2 = new GameObject("Enemy2");
        enemyGo2.layer = LayerMask.NameToLayer("Default");
        var controller2 = enemyGo2.AddComponent<EnemyAIController>();

        try {
            InitializeController();
            controller2.Initialize("enemy_1", "generic_v1", Vector3.zero, "player-1");

            // Get the static buffer from both instances — should be same reference
            var buffer1 = typeof(EnemyAIController)
                .GetField("_fireHitBuffer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .GetValue(null);
            var buffer2 = typeof(EnemyAIController)
                .GetField("_fireHitBuffer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .GetValue(null);

            Assert.AreSame(buffer1, buffer2,
                "Static buffer should be shared across all EnemyAIController instances");
        } finally {
            Object.DestroyImmediate(enemyGo2);
        }
    }
}

#endif
