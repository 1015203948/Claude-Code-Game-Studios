using Game.Gameplay;
using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// WeaponSystem 射速计时器单元测试。
/// 覆盖 Story 005 所有验收标准（AC-1 ~ AC-5），通过 WeaponSystem API 直接验证。
/// </summary>
[TestFixture]
public class FireRateTimer_Test
{
    // ─────────────────────────────────────────────────────────────────
    // Mock dependencies
    // ─────────────────────────────────────────────────────────────────

    private class MockPhysicsQuery : IPhysicsQuery
    {
        public int Raycast(Vector3 origin, Vector3 direction, RaycastHit[] buffer, float maxDistance)
        {
            return 0; // never hit — timer tests don't need hit detection
        }
    }

    private class MockHealthSystem : IHealthSystem
    {
        public void ApplyDamage(string instanceId, float amount, DamageType damageType) { }
    }

    // ─────────────────────────────────────────────────────────────────
    // Test Subject
    // ─────────────────────────────────────────────────────────────────

    private WeaponSystem _weaponSystem;

    [SetUp]
    public void SetUp()
    {
        _weaponSystem = new WeaponSystem(
            fireRate: 1.0f,  // 1 shot/sec = 1.0s cooldown
            range: 200f,
            damage: 8f,
            damageType: DamageType.Physical,
            physics: new MockPhysicsQuery(),
            health: new MockHealthSystem());
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-1: _fireTimer 初始值 = 0f
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void fireTimer_initial_value_is_zero()
    {
        Assert.AreEqual(0f, _weaponSystem.CooldownProgress, 0.001f,
            "CooldownProgress should start at 0 (weapon ready to fire)");
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-1: 每帧 _fireTimer += Time.deltaTime（帧率独立）
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void fireTimer_accumulates_with_deltaTime()
    {
        // Given: timer starts at 0
        // When: multiple frames pass at 60fps (each ≈ 0.0167s)
        _weaponSystem.Tick(0.0167f);
        _weaponSystem.Tick(0.0167f);
        _weaponSystem.Tick(0.0167f);

        // Then: timer accumulated correctly (3 × 0.0167 = 0.0501)
        Assert.That(_weaponSystem.CooldownProgress, Is.EqualTo(0.0501f).Within(0.0001f),
            "Timer should accumulate deltaTime each frame");
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-3: timer ≥ 1/WEAPON_FIRE_RATE 时 TryFire 成功
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void fire_triggers_when_timer_ready()
    {
        // Given: weapon cooldown filled
        _weaponSystem.Tick(1f); // fill full 1.0s cooldown for 1fps
        var targetMap = new Dictionary<Collider, string>();

        // When: fire
        bool fired = _weaponSystem.TryFire(Vector3.zero, Vector3.forward, targetMap);

        // Then: fire succeeds
        Assert.IsTrue(fired, "TryFire should succeed when cooldown is ready");
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-2: timer < cooldown 时 TryFire 不执行
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void fire_blocked_when_cooldown_not_ready()
    {
        // Given: just fired, timer reset to 0
        var targetMap = new Dictionary<Collider, string>();
        _weaponSystem.TryFire(Vector3.zero, Vector3.forward, targetMap);

        // When: try to fire again immediately
        bool fired = _weaponSystem.TryFire(Vector3.zero, Vector3.forward, targetMap);

        // Then: blocked
        Assert.IsFalse(fired, "TryFire should fail when cooldown is not ready");
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-4: timer 累积超过 2× 时最多一次开火（不能"充能"）
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void no_overfire_from_accumulated_timer()
    {
        // Given: timer accumulated for 2 seconds (progress would be 2.0)
        _weaponSystem.Tick(2.0f);

        // When: fire once
        var targetMap = new Dictionary<Collider, string>();
        bool fired1 = _weaponSystem.TryFire(Vector3.zero, Vector3.forward, targetMap);

        // Then: exactly ONE fire, timer reset
        Assert.IsTrue(fired1, "Should fire once when cooldown is ready");
        Assert.IsFalse(_weaponSystem.CanFire, "Should not be ready immediately after firing");

        // When: try again immediately
        bool fired2 = _weaponSystem.TryFire(Vector3.zero, Vector3.forward, targetMap);

        // Then: blocked — no credit for extra time
        Assert.IsFalse(fired2, "Second fire should be blocked — no overfire");
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-5: 60fps 下 1 秒恰好触发 1 次开火（WEAPON_FIRE_RATE = 1.0）
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void fire_once_per_second_at_60fps()
    {
        int fireCount = 0;
        float delta = 1f / 60f; // ~0.0167s per frame
        var targetMap = new Dictionary<Collider, string>();

        // Simulate 1 second = 60 frames (plus 1 extra to cross the threshold)
        for (int i = 0; i < 61; i++)
        {
            _weaponSystem.Tick(delta);
            if (_weaponSystem.TryFire(Vector3.zero, Vector3.forward, targetMap))
                fireCount++;
        }

        Assert.AreEqual(1, fireCount,
            $"At 60fps for ~1 second, exactly 1 fire should occur. Got {fireCount}");
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-5: 帧率独立：30fps 下 1 秒恰好 1 次开火
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void frame_rate_independence_at_30fps()
    {
        int fireCount = 0;
        float delta = 1f / 30f; // ~0.0333s per frame
        var targetMap = new Dictionary<Collider, string>();

        // Simulate 1 second = 30 frames (plus 1 extra to cross the threshold)
        for (int i = 0; i < 31; i++)
        {
            _weaponSystem.Tick(delta);
            if (_weaponSystem.TryFire(Vector3.zero, Vector3.forward, targetMap))
                fireCount++;
        }

        Assert.AreEqual(1, fireCount,
            $"At 30fps for ~1 second, exactly 1 fire should occur (frame-rate independent). Got {fireCount}");
    }
}
