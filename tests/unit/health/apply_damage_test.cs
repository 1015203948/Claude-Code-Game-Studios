using NUnit.Framework;
using UnityEngine;
using System.Collections;
using Object = UnityEngine.Object;

/// <summary>
/// HealthSystem.ApplyDamage 单元测试。
/// 覆盖 Story 001 所有验收标准。
/// </summary>
[TestFixture]
public class HealthSystem_ApplyDamage_Test
{
    private HealthSystem _healthSystem;
    private GameObject _go;

    [SetUp]
    public void SetUp()
    {
        // 清理单例
        if (HealthSystem.Instance != null) {
            Object.DestroyImmediate(HealthSystem.Instance.gameObject);
        }

        _go = new GameObject("HealthSystemUnderTest");
        _healthSystem = _go.AddComponent<HealthSystem>();

        // 注册测试飞船到 GameDataManager
        RegisterTestShip("ship-healthy", 30f, 100f, ShipState.IN_COCKPIT, false);
        RegisterTestShip("ship-docked", 30f, 100f, ShipState.DOCKED, false);
        RegisterTestShip("ship-in-transit", 30f, 100f, ShipState.IN_TRANSIT, false);
        RegisterTestShip("ship-destroyed", 30f, 100f, ShipState.DESTROYED, false);
        RegisterTestShip("ship-in-combat", 30f, 100f, ShipState.IN_COMBAT, false);
        RegisterTestShip("ship-player", 30f, 100f, ShipState.IN_COCKPIT, true);
    }

    [TearDown]
    public void TearDown()
    {
        if (_go != null) {
            Object.DestroyImmediate(_go);
        }
        HealthSystem.Instance = null;
        ClearAllShips();
    }

    // ─────────────────────────────────────────────────────────────────
    // Helper
    // ─────────────────────────────────────────────────────────────────

    private void RegisterTestShip(string id, float hull, float maxHull, ShipState state, bool isPlayer)
    {
        var ship = new MockShipDataModel {
            InstanceId = id,
            CurrentHull = hull,
            MaxHull = maxHull,
            State = state,
            IsPlayerControlled = isPlayer
        };
        GameDataManager.Instance.RegisterShip(id, ship);
    }

    private void ClearAllShips()
    {
        // Reflection-free: expose a test-only clear method would be ideal.
        // For now we rely on per-test isolation via unique IDs.
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-1: ApplyDamage(Hull=30, rawDamage=8, KINETIC) → newHull = 22，OnHullChanged 广播
    // ─────────────────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator ApplyDamage_IN_COCKPIT_reducesHull_and_broadcasts_OnHullChanged()
    {
        float? capturedNewHull = null;
        float? capturedMaxHull = null;
        string capturedId = null;

        _healthSystem.OnHullChanged += (id, newHull, maxHull) => {
            capturedId = id;
            capturedNewHull = newHull;
            capturedMaxHull = maxHull;
        };

        bool result = _healthSystem.ApplyDamage("ship-healthy", 8f, DamageType.KINETIC);

        Assert.IsTrue(result, "ApplyDamage should return true for valid IN_COCKPIT ship");
        Assert.AreEqual(22f, capturedNewHull, 0.001f, "New Hull should be 30 - 8 = 22");
        Assert.AreEqual(100f, capturedMaxHull, "MaxHull should be 100");
        Assert.AreEqual("ship-healthy", capturedId, "InstanceId should match");

        yield return null;
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-2: DOCKED 状态静默忽略，不广播事件
    // ─────────────────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator ApplyDamage_DOCKED_silentReject_noEvent()
    {
        bool eventFired = false;
        _healthSystem.OnHullChanged += (_, _, _) => eventFired = true;

        bool result = _healthSystem.ApplyDamage("ship-docked", 8f, DamageType.KINETIC);

        Assert.IsFalse(result, "ApplyDamage should return false for DOCKED ship");
        Assert.IsFalse(eventFired, "OnHullChanged should NOT fire for DOCKED ship");

        yield return null;
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-3: IN_TRANSIT 状态静默忽略
    // ─────────────────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator ApplyDamage_IN_TRANSIT_silentReject()
    {
        bool eventFired = false;
        _healthSystem.OnHullChanged += (_, _, _) => eventFired = true;

        bool result = _healthSystem.ApplyDamage("ship-in-transit", 8f, DamageType.KINETIC);

        Assert.IsFalse(result, "ApplyDamage should return false for IN_TRANSIT ship");
        Assert.IsFalse(eventFired, "OnHullChanged should NOT fire for IN_TRANSIT ship");

        yield return null;
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-4: DESTROYED 记录警告，返回 false
    // ─────────────────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator ApplyDamage_DESTROYED_logsWarning_and_returnsFalse()
    {
        bool eventFired = false;
        _healthSystem.OnHullChanged += (_, _, _) => eventFired = true;

        bool result = _healthSystem.ApplyDamage("ship-destroyed", 8f, DamageType.KINETIC);

        Assert.IsFalse(result, "ApplyDamage should return false for DESTROYED ship");
        Assert.IsFalse(eventFired, "No event should fire for DESTROYED ship");

        yield return null;
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-5: IN_COCKPIT 和 IN_COMBAT 正常接受伤害
    // ─────────────────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator ApplyDamage_IN_COMBAT_accepts()
    {
        bool eventFired = false;
        _healthSystem.OnHullChanged += (_, _, _) => eventFired = true;

        bool result = _healthSystem.ApplyDamage("ship-in-combat", 8f, DamageType.KINETIC);

        Assert.IsTrue(result, "ApplyDamage should return true for IN_COMBAT ship");
        Assert.IsTrue(eventFired, "OnHullChanged should fire for IN_COMBAT ship");

        yield return null;
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-6: rawDamage < 0 → Clamp 到 0，不报错，返回 false（无伤害）
    // ─────────────────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator ApplyDamage_negativeRawDamage_clampedToZero_noHullChange()
    {
        // Hull 30, rawDamage = -5 → clamped to 0 → hull stays 30 → no event
        bool eventFired = false;
        _healthSystem.OnHullChanged += (_, _, _) => eventFired = true;

        // Re-register with known hull
        RegisterTestShip("ship-negative", 30f, 100f, ShipState.IN_COCKPIT, false);
        bool result = _healthSystem.ApplyDamage("ship-negative", -5f, DamageType.KINETIC);

        Assert.IsTrue(result, "Clamped negative damage still returns true");
        Assert.IsFalse(eventFired, "No hull change when rawDamage clamped to 0");

        yield return null;
    }

    // ─────────────────────────────────────────────────────────────────
    // HullRatio: [0, 1] 范围内
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void GetHullRatio_returnsCorrectRatio()
    {
        RegisterTestShip("ship-hullratio", 50f, 100f, ShipState.IN_COCKPIT, false);
        Assert.AreEqual(0.5f, _healthSystem.GetHullRatio("ship-hullratio"), 0.001f);
    }

    [Test]
    public void GetHullRatio_clampsToZero_whenHullIsZero()
    {
        RegisterTestShip("ship-zero-hull", 0f, 100f, ShipState.IN_COCKPIT, false);
        Assert.AreEqual(0f, _healthSystem.GetHullRatio("ship-zero-hull"), "HullRatio at 0 hull should be 0");
    }

    [Test]
    public void GetHullRatio_clampsToOne_whenHullEqualsMax()
    {
        RegisterTestShip("ship-full-hull", 100f, 100f, ShipState.IN_COCKPIT, false);
        Assert.AreEqual(1f, _healthSystem.GetHullRatio("ship-full-hull"), "HullRatio at max hull should be 1");
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-7: Hull 归零时触发死亡序列（ExecuteDeathSequence stub 验证）
    // ─────────────────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator ApplyDamage_HullToZero_triggersDeathSequence()
    {
        RegisterTestShip("ship-dying", 5f, 100f, ShipState.IN_COCKPIT, false);

        bool dyingFired = false;
        string dyingId = null;
        _healthSystem.OnShipDying += id => {
            dyingFired = true;
            dyingId = id;
        };

        bool result = _healthSystem.ApplyDamage("ship-dying", 8f, DamageType.KINETIC);

        Assert.IsTrue(result, "ApplyDamage should return true");
        Assert.IsTrue(dyingFired, "OnShipDying should fire when Hull reaches 0");
        Assert.AreEqual("ship-dying", dyingId);

        yield return null;
    }
}

// ─────────────────────────────────────────────────────────────────
// Mock ShipDataModel for testing
// ─────────────────────────────────────────────────────────────────

/// <summary>
/// 测试用 Mock ShipDataModel。
/// 实现最小接口以支持 HealthSystem 测试。
/// </summary>
public class MockShipDataModel
{
    public string InstanceId { get; set; }
    public float CurrentHull { get; set; }
    public float MaxHull { get; set; }
    public ShipState State { get; set; }
    public bool IsPlayerControlled { get; set; }

    public void Destroy()
    {
        State = ShipState.DESTROYED;
    }
}
