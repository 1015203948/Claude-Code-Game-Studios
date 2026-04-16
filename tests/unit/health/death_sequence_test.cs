using NUnit.Framework;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Object = UnityEngine.Object;

/// <summary>
/// HealthSystem.ExecuteDeathSequence 单元测试（H-5 死亡序列）。
/// 覆盖 Story 002 所有验收标准。
/// </summary>
[TestFixture]
public class HealthSystem_DeathSequence_Test
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

        RegisterTestShip("player-ship", 5f, 100f, ShipState.IN_COCKPIT, true);
        RegisterTestShip("enemy-ship", 5f, 100f, ShipState.IN_COCKPIT, false);
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
    // AC-1: OnShipDying 在同一帧内只广播一次（不多次）
    // AC-8: Hull=0 触发死亡序列（复用 ApplyDamage 路径）
    // ─────────────────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator ExecuteDeathSequence_fires_OnShipDying_once()
    {
        int dyingCount = 0;
        string dyingId = null;

        _healthSystem.OnShipDying += id => {
            dyingCount++;
            dyingId = id;
        };

        // Hull=5 + rawDamage=8 → Hull=0 → 触发死亡序列
        bool result = _healthSystem.ApplyDamage("player-ship", 8f, DamageType.KINETIC);

        Assert.IsTrue(result, "ApplyDamage should return true when Hull reaches 0");
        Assert.AreEqual(1, dyingCount, "OnShipDying should fire exactly once");
        Assert.AreEqual("player-ship", dyingId, "OnShipDying should pass correct instanceId");

        yield return null;
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-2: Step 2 — 调用 ShipDataModel.DestroyShip（状态变为 DESTROYED）
    // ─────────────────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator ExecuteDeathSequence_sets_ShipState_to_DESTROYED()
    {
        _healthSystem.OnShipDying += _ => { };

        _healthSystem.ApplyDamage("player-ship", 8f, DamageType.KINETIC);

        var ship = GameDataManager.Instance.GetShip("player-ship");
        Assert.AreEqual(ShipState.DESTROYED, ship.State, "ShipState should be DESTROYED after death sequence");

        yield return null;
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-3: Step 3 — 玩家飞船 IsPlayerControlled==true → 广播 OnPlayerShipDestroyed
    // AC-6: 非玩家飞船（enemy）→ 跳过 Step 3
    // ─────────────────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator ExecuteDeathSequence_player_ship_fires_OnPlayerShipDestroyed()
    {
        bool playerDestroyedFired = false;
        string playerDestroyedId = null;

        _healthSystem.OnPlayerShipDestroyed += id => {
            playerDestroyedFired = true;
            playerDestroyedId = id;
        };

        _healthSystem.ApplyDamage("player-ship", 8f, DamageType.KINETIC);

        Assert.IsTrue(playerDestroyedFired, "OnPlayerShipDestroyed should fire for player ship");
        Assert.AreEqual("player-ship", playerDestroyedId, "OnPlayerShipDestroyed should pass correct instanceId");

        yield return null;
    }

    [UnityTest]
    public IEnumerator ExecuteDeathSequence_enemy_ship_skips_OnPlayerShipDestroyed()
    {
        bool playerDestroyedFired = false;
        _healthSystem.OnPlayerShipDestroyed += _ => playerDestroyedFired = true;

        _healthSystem.ApplyDamage("enemy-ship", 8f, DamageType.KINETIC);

        Assert.IsFalse(playerDestroyedFired, "OnPlayerShipDestroyed should NOT fire for enemy ship");

        yield return null;
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-4: Step 4 — 广播 OnShipDestroyed（通用销毁完成）
    // ─────────────────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator ExecuteDeathSequence_fires_OnShipDestroyed()
    {
        bool destroyedFired = false;
        string destroyedId = null;

        _healthSystem.OnShipDestroyed += id => {
            destroyedFired = true;
            destroyedId = id;
        };

        _healthSystem.ApplyDamage("enemy-ship", 8f, DamageType.KINETIC);

        Assert.IsTrue(destroyedFired, "OnShipDestroyed should fire after death sequence");
        Assert.AreEqual("enemy-ship", destroyedId, "OnShipDestroyed should pass correct instanceId");

        yield return null;
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-5: 四步骤顺序 Step1 → Step2 → Step3/4，无重排
    // AC-7: OnShipDying 在 Step 2 之前广播（供 CombatSystem 订阅者检测胜负）
    // ─────────────────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator ExecuteDeathSequence_steps_fire_in_correct_order()
    {
        var invocationOrder = new List<string>();

        _healthSystem.OnShipDying += _ => invocationOrder.Add("OnShipDying");
        _healthSystem.OnPlayerShipDestroyed += _ => invocationOrder.Add("OnPlayerShipDestroyed");
        _healthSystem.OnShipDestroyed += _ => invocationOrder.Add("OnShipDestroyed");

        _healthSystem.ApplyDamage("player-ship", 8f, DamageType.KINETIC);

        Assert.AreEqual(4, invocationOrder.Count, "Should have 4 events in order list");
        Assert.AreEqual("OnShipDying", invocationOrder[0], "Step 1: OnShipDying fires first");
        Assert.AreEqual("OnPlayerShipDestroyed", invocationOrder[1], "Step 2: OnPlayerShipDestroyed fires second for player ship");
        Assert.AreEqual("OnShipDestroyed", invocationOrder[2], "Step 3: OnShipDestroyed fires last");

        yield return null;
    }

    [UnityTest]
    public IEnumerator ExecuteDeathSequence_enemy_steps_fire_in_correct_order()
    {
        var invocationOrder = new List<string>();

        _healthSystem.OnShipDying += _ => invocationOrder.Add("OnShipDying");
        _healthSystem.OnShipDestroyed += _ => invocationOrder.Add("OnShipDestroyed");

        _healthSystem.ApplyDamage("enemy-ship", 8f, DamageType.KINETIC);

        Assert.AreEqual(2, invocationOrder.Count, "Enemy ship should have 2 events");
        Assert.AreEqual("OnShipDying", invocationOrder[0], "Step 1: OnShipDying fires first for enemy");
        Assert.AreEqual("OnShipDestroyed", invocationOrder[1], "Step 2: OnShipDestroyed fires for enemy (no Step 3)");

        yield return null;
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-9: 快速连续 ApplyDamage 不重复触发死亡序列
    // (Hull=1 时连续两次伤害)
    // ─────────────────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator ExecuteDeathSequence_rapid_damage_calls_fire_death_sequence_once()
    {
        RegisterTestShip("rapid-damage-ship", 1f, 100f, ShipState.IN_COCKPIT, false);

        int dyingCount = 0;
        _healthSystem.OnShipDying += _ => dyingCount++;

        // First hit: Hull=1 - 1 = 0 → triggers death
        _healthSystem.ApplyDamage("rapid-damage-ship", 1f, DamageType.KINETIC);
        // Second hit on already-destroyed ship → should be rejected
        _healthSystem.ApplyDamage("rapid-damage-ship", 1f, DamageType.KINETIC);

        Assert.AreEqual(1, dyingCount, "OnShipDying should fire exactly once even with rapid successive hits");

        yield return null;
    }
}
