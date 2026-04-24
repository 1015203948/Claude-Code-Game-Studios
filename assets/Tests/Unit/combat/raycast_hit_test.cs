using Game.Gameplay;
using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// WeaponSystem 结构验证和基础行为测试。
/// 覆盖 AC-1（零 GC raycast buffer）和其他结构化断言。
/// 注意：由于 EditMode 下无法可靠构造 RaycastHit（Unity 6 EntityId 限制），
/// 命中/未命中的行为验证在 PlayMode 集成测试中进行。
/// </summary>
[TestFixture]
public class RaycastHit_Test
{
    // ─────────────────────────────────────────────────────────────────
    // Mock dependencies
    // ─────────────────────────────────────────────────────────────────

    private class MockPhysicsQuery : IPhysicsQuery
    {
        public int Raycast(Vector3 origin, Vector3 direction, RaycastHit[] buffer, float maxDistance)
        {
            return 0;
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
            fireRate: 1f,
            range: 200f,
            damage: 8f,
            damageType: DamageType.Physical,
            physics: new MockPhysicsQuery(),
            health: new MockHealthSystem());
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-1: RaycastHit[1] pre-allocated — verified by code review
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void raycast_buffer_is_preallocated_class_member()
    {
        var hitsField = typeof(WeaponSystem).GetField(
            "_hits",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(hitsField, "_hits field should exist on WeaponSystem");

        var hits = hitsField.GetValue(_weaponSystem) as RaycastHit[];
        Assert.IsNotNull(hits, "_hits should be a RaycastHit array");
        Assert.AreEqual(1, hits.Length, "_hits should be pre-allocated with length 1");
    }

    [Test]
    public void hits_buffer_is_readonly_to_prevent_reassignment()
    {
        var hitsField = typeof(WeaponSystem).GetField(
            "_hits",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(hitsField);
        Assert.IsTrue(hitsField.IsInitOnly,
            "_hits should be readonly to prevent accidental reallocation in combat loop");
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-2: TryFire delegates to IPhysicsQuery (mock verifies call)
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void try_fire_calls_physics_raycast()
    {
        var trackedPhysics = new TrackedMockPhysics();
        var ws = new WeaponSystem(1f, 200f, 8f, DamageType.Physical, trackedPhysics, new MockHealthSystem());
        ws.Tick(1f); // fill cooldown

        ws.TryFire(Vector3.zero, Vector3.forward, new Dictionary<Collider, string>());

        Assert.AreEqual(1, trackedPhysics.RaycastCallCount, "Raycast should be called once");
    }

    [Test]
    public void try_fire_skips_raycast_during_cooldown()
    {
        var trackedPhysics = new TrackedMockPhysics();
        var ws = new WeaponSystem(1f, 200f, 8f, DamageType.Physical, trackedPhysics, new MockHealthSystem());

        ws.TryFire(Vector3.zero, Vector3.forward, new Dictionary<Collider, string>());

        Assert.AreEqual(0, trackedPhysics.RaycastCallCount, "Raycast should NOT be called during cooldown");
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-3: Range passed to physics
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void range_200m_passed_to_physics()
    {
        var trackedPhysics = new TrackedMockPhysics();
        var ws = new WeaponSystem(1f, 200f, 8f, DamageType.Physical, trackedPhysics, new MockHealthSystem());
        ws.Tick(1f);

        ws.TryFire(Vector3.zero, Vector3.forward, new Dictionary<Collider, string>());

        Assert.AreEqual(200f, trackedPhysics.LastMaxDistance, 0.001f,
            "Range should be 200m");
    }

    [Test]
    public void custom_range_passed_to_physics()
    {
        var trackedPhysics = new TrackedMockPhysics();
        var ws = new WeaponSystem(1f, 500f, 8f, DamageType.Physical, trackedPhysics, new MockHealthSystem());
        ws.Tick(1f);

        ws.TryFire(Vector3.zero, Vector3.forward, new Dictionary<Collider, string>());

        Assert.AreEqual(500f, trackedPhysics.LastMaxDistance, 0.001f,
            "Range should be 500m (config-driven, not hardcoded)");
    }

    // ─────────────────────────────────────────────────────────────────
    // Unregistered collider: no damage call
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void miss_does_not_call_health_system()
    {
        var mockHealth = new TrackedMockHealthSystem();
        var ws = new WeaponSystem(1f, 200f, 8f, DamageType.Physical, new MockPhysicsQuery(), mockHealth);
        ws.Tick(1f);

        ws.TryFire(Vector3.zero, Vector3.forward, new Dictionary<Collider, string>());

        Assert.AreEqual(0, mockHealth.ApplyDamageCallCount, "No damage on miss");
    }

    // ─────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────

    private class TrackedMockPhysics : IPhysicsQuery
    {
        public int RaycastCallCount { get; private set; }
        public float LastMaxDistance { get; private set; }

        public int Raycast(Vector3 origin, Vector3 direction, RaycastHit[] buffer, float maxDistance)
        {
            RaycastCallCount++;
            LastMaxDistance = maxDistance;
            return 0; // always miss
        }
    }

    private class TrackedMockHealthSystem : IHealthSystem
    {
        public int ApplyDamageCallCount { get; private set; }
        public void ApplyDamage(string instanceId, float amount, DamageType damageType)
        {
            ApplyDamageCallCount++;
        }
    }
}
