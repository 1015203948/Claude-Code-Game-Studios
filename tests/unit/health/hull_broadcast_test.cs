using NUnit.Framework;
using UnityEngine;
using System.Collections;
using Object = UnityEngine.Object;

/// <summary>
/// HealthSystem HullRatio + OnHullChanged 广播单元测试。
/// 覆盖 Story 003 所有验收标准。
/// </summary>
[TestFixture]
public class HealthSystem_HullBroadcast_Test
{
    private HealthSystem _healthSystem;
    private GameObject _go;

    [SetUp]
    public void SetUp()
    {
        if (HealthSystem.Instance != null) {
            Object.DestroyImmediate(HealthSystem.Instance.gameObject);
        }

        _go = new GameObject("HealthSystemUnderTest");
        _healthSystem = _go.AddComponent<HealthSystem>();

        RegisterTestShip("ship-hullratio", 50f, 100f, ShipState.IN_COCKPIT, false);
        RegisterTestShip("ship-broadcast", 30f, 100f, ShipState.IN_COCKPIT, false);
        RegisterTestShip("ship-zero-hull", 1f, 100f, ShipState.IN_COCKPIT, false);
        RegisterTestShip("ship-null", 0f, 100f, ShipState.IN_COCKPIT, false);
    }

    [TearDown]
    public void TearDown()
    {
        if (_go != null) {
            Object.DestroyImmediate(_go);
        }
        HealthSystem.Instance = null;
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

    // ─────────────────────────────────────────────────────────────────
    // AC-1: HullRatio = CurrentHull / MaxHull，返回值在 [0.0, 1.0] 范围内
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void GetHullRatio_returns_0_when_hull_is_zero()
    {
        RegisterTestShip("hull-zero", 0f, 100f, ShipState.IN_COCKPIT, false);
        Assert.AreEqual(0f, _healthSystem.GetHullRatio("hull-zero"), 0.001f);
    }

    [Test]
    public void GetHullRatio_returns_1_when_hull_equals_max()
    {
        RegisterTestShip("hull-full", 100f, 100f, ShipState.IN_COCKPIT, false);
        Assert.AreEqual(1f, _healthSystem.GetHullRatio("hull-full"), 0.001f);
    }

    [Test]
    public void GetHullRatio_returns_0_5_when_hull_is_half()
    {
        RegisterTestShip("hull-half", 50f, 100f, ShipState.IN_COCKPIT, false);
        Assert.AreEqual(0.5f, _healthSystem.GetHullRatio("hull-half"), 0.001f);
    }

    [Test]
    public void GetHullRatio_clamps_result_to_0_to_1_range()
    {
        // Overkill hull
        RegisterTestShip("hull-over", 150f, 100f, ShipState.IN_COCKPIT, false);
        Assert.AreEqual(1f, _healthSystem.GetHullRatio("hull-over"), 0.001f, "HullRatio should clamp to 1.0 when CurrentHull > MaxHull");
    }

    [Test]
    public void GetHullRatio_returns_zero_for_missing_ship()
    {
        Assert.AreEqual(0f, _healthSystem.GetHullRatio("non-existent-ship"), "Should return 0 for missing ship");
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-2: Hull 从 30→22 时广播 OnHullChanged(instanceId, 22, 100)
    // ─────────────────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator OnHullChanged_broadcasts_with_correct_values()
    {
        string capturedId = null;
        float capturedNewHull = 0f;
        float capturedMaxHull = 0f;

        _healthSystem.OnHullChanged += (id, newHull, maxHull) => {
            capturedId = id;
            capturedNewHull = newHull;
            capturedMaxHull = maxHull;
        };

        // Hull=30, rawDamage=8 → newHull=22, maxHull=100
        bool result = _healthSystem.ApplyDamage("ship-broadcast", 8f, DamageType.KINETIC);

        Assert.IsTrue(result, "ApplyDamage should return true");
        Assert.AreEqual("ship-broadcast", capturedId, "InstanceId should match");
        Assert.AreEqual(22f, capturedNewHull, 0.001f, "New Hull should be 22");
        Assert.AreEqual(100f, capturedMaxHull, "MaxHull should be 100");

        yield return null;
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-3: Hull 从 22→0 时不广播 OnHullChanged（走死亡序列）
    // AC-5: OnHullChanged 每次 Hull > 0 变化时广播，不遗漏
    // ─────────────────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator OnHullChanged_not_fired_when_Hull_reaches_zero()
    {
        bool hullChangedFired = false;
        bool deathSequenceFired = false;

        _healthSystem.OnHullChanged += (_, _, _) => hullChangedFired = true;
        _healthSystem.OnShipDying += _ => deathSequenceFired = true;

        // Hull=1, rawDamage=2 → Hull=0 → death sequence, NOT OnHullChanged
        bool result = _healthSystem.ApplyDamage("ship-zero-hull", 2f, DamageType.KINETIC);

        Assert.IsTrue(result, "ApplyDamage should return true");
        Assert.IsFalse(hullChangedFired, "OnHullChanged should NOT fire when Hull reaches 0");
        Assert.IsTrue(deathSequenceFired, "OnShipDying should fire instead when Hull reaches 0");

        yield return null;
    }

    [UnityTest]
    public IEnumerator OnHullChanged_fires_on_every_positive_hull_change()
    {
        int broadcastCount = 0;
        _healthSystem.OnHullChanged += (_, _, _) => broadcastCount++;

        // First hit: 30 → 22
        _healthSystem.ApplyDamage("ship-broadcast", 8f, DamageType.KINETIC);
        Assert.AreEqual(1, broadcastCount, "OnHullChanged should fire after first hit");

        // Re-register to reset hull
        RegisterTestShip("ship-second", 30f, 100f, ShipState.IN_COCKPIT, false);
        broadcastCount = 0;
        _healthSystem.OnHullChanged += (_, _, _) => broadcastCount++;

        // Second hit: 30 → 15
        _healthSystem.ApplyDamage("ship-second", 15f, DamageType.KINETIC);
        Assert.AreEqual(1, broadcastCount, "OnHullChanged should fire for each hull change");

        yield return null;
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-4: HealthSystem.Instance 单例在 MasterScene 激活时可用
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void HealthSystem_singleton_accessible()
    {
        Assert.IsNotNull(HealthSystem.Instance, "HealthSystem.Instance should be accessible after Awake");
        Assert.AreSame(_healthSystem, HealthSystem.Instance, "HealthSystem.Instance should return the instance");
    }
}
