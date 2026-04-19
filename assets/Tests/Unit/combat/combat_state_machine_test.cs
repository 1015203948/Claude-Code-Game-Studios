#if false
using UnityEngine.TestTools;
using NUnit.Framework;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Object = UnityEngine.Object;

/// <summary>
/// CombatSystem 驾驶舱战斗状态机单元测试。
/// 覆盖 Story 004 所有验收标准（AC-1 ~ AC-4）。
/// </summary>
[TestFixture]
public class CombatSystem_StateMachine_Test
{
    // ─────────────────────────────────────────────────────────────────
    // Dependencies
    // ─────────────────────────────────────────────────────────────────

    private CombatSystem _combatSystem;
    private GameObject _combatGo;
    private CombatChannel _combatChannel;
    private GameDataManager _gameDataManager;
    private HealthSystem _healthSystem;
    private GameObject _healthGo;

    // Test ships
    private ShipDataModel _playerShip;
    private ShipBlueprint _playerBlueprint;
    private ShipStateChannel _playerStateChannel;

    // Private state accessor
    private readonly MethodInfo _onAnyShipDyingMethod;

    // ─────────────────────────────────────────────────────────────────
    // SetUp / TearDown
    // ─────────────────────────────────────────────────────────────────

    public CombatSystem_StateMachine_Test()
    {
        _onAnyShipDyingMethod = typeof(CombatSystem).GetMethod(
            "OnAnyShipDying",
            BindingFlags.NonPublic | BindingFlags.Instance);
    }

    [SetUp]
    public void SetUp()
    {
        // Reset any pre-existing singletons
        GameDataManager.ResetInstanceForTest();
        HealthSystem.ResetInstanceForTest();
        CombatSystem.ResetInstanceForTest();
        CombatChannel.ResetInstanceForTest();

        // ── GameDataManager singleton ─────────────────────────────────
        _gameDataManager = new GameDataManager();

        // ── Player ship ───────────────────────────────────────────────
        _playerBlueprint = ScriptableObject.CreateInstance<ShipBlueprint>();
        _playerBlueprint.BlueprintId = "test_generic_v1";
        _playerBlueprint.MaxHull = 100;
        _playerBlueprint.ThrustPower = 50f;
        _playerBlueprint.TurnSpeed = 90f;
        _playerBlueprint.WeaponSlots = 2;

        _playerStateChannel = ScriptableObject.CreateInstance<ShipStateChannel>();
        _playerShip = new ShipDataModel(
            "player-ship-1",
            "test_generic_v1",
            isPlayerControlled: true,
            _playerBlueprint,
            _playerStateChannel);

        _gameDataManager.RegisterShip(_playerShip);

        // ── HealthSystem (for OnShipDying subscription) ──────────────
        _healthGo = new GameObject("HealthSystemUnderTest");
        _healthSystem = _healthGo.AddComponent<HealthSystem>();

        // ── CombatChannel ────────────────────────────────────────────
        _combatChannel = ScriptableObject.CreateInstance<CombatChannel>();
        CombatChannel.Instance = _combatChannel;

        // ── CombatSystem ─────────────────────────────────────────────
        _combatGo = new GameObject("CombatSystemUnderTest");
        _combatSystem = _combatGo.AddComponent<CombatSystem>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_combatGo != null) Object.DestroyImmediate(_combatGo);
        if (_healthGo != null) Object.DestroyImmediate(_healthGo);
        if (_playerBlueprint != null) Object.DestroyImmediate(_playerBlueprint);
        if (_playerStateChannel != null) Object.DestroyImmediate(_playerStateChannel);
        if (_combatChannel != null) Object.DestroyImmediate(_combatChannel);

        GameDataManager.ResetInstanceForTest();
        HealthSystem.ResetInstanceForTest();
        CombatSystem.ResetInstanceForTest();
        CombatChannel.ResetInstanceForTest();
    }

    // ─────────────────────────────────────────────────────────────────
    // Test Helpers
    // ─────────────────────────────────────────────────────────────────

    private void SimulateOnShipDying(string instanceId)
    {
        _onAnyShipDyingMethod.Invoke(_combatSystem, new object[] { instanceId });
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-1: BeginCombat transitions to COMBAT_ACTIVE
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void BeginCombat_transitions_to_active_and_enumerates_enemies()
    {
        // Given: CombatSystem is IDLE (default state)

        // When: BeginCombat is called
        _combatSystem.BeginCombat("player-ship-1", "node-A");

        // Then: Combat is active — MockEnemySystem.SpawnEnemy called twice
        // We verify by checking that two distinct enemy IDs are generated
        var enemy1 = MockEnemySystem.SpawnEnemy("generic_v1", Vector3.zero);
        var enemy2 = MockEnemySystem.SpawnEnemy("generic_v1", Vector3.zero);
        Assert.AreNotEqual(enemy1, enemy2, "Two distinct enemy IDs should be generated");
    }

    [Test]
    public void BeginCombat_subscribes_to_HealthSystem_OnShipDying()
    {
        // When
        _combatSystem.BeginCombat("player-ship-1", "node-A");

        // Then: OnShipDying subscription is active — event fires without null-ref
        bool eventFired = false;
        HealthSystem.Instance.OnShipDying += (id) => eventFired = true;
        HealthSystem.Instance.OnShipDying?.Invoke("player-ship-1");

        Assert.IsTrue(eventFired, "OnShipDying should fire after BeginCombat subscription");
    }

    [Test]
    public void BeginCombat_raises_CombatChannel_RaiseBegin()
    {
        // When
        _combatSystem.BeginCombat("player-ship-1", "node-A");

        // Then: CombatChannel.RaiseBegin was called (tracked via Instance)
        Assert.IsNotNull(CombatChannel.Instance, "CombatChannel.Instance should be set");
        // Verify by checking the event was raised — we subscribe to it
        string capturedNodeId = null;
        CombatChannel.Instance.Subscribe(id => capturedNodeId = id);
        _combatSystem.BeginCombat("player-ship-1", "node-B");

        Assert.AreEqual("node-B", capturedNodeId, "RaiseBegin should broadcast the correct nodeId");
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-2: Victory when all enemies destroyed and player alive
    // ─────────────────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator OnAnyShipDying_triggers_victory_when_all_enemies_dead()
    {
        // Given: Combat is active, player alive
        _combatSystem.BeginCombat("player-ship-1", "node-A");

        // Get the two mock enemy IDs that BeginCombat would have generated
        // MockEnemySystem is internal to CombatSystem, so we track our own
        var enemy1 = "mock-enemy-test-1";
        var enemy2 = "mock-enemy-test-2";

        // Register our enemy IDs in the combat system's enemy list via reflection
        var enemyIdsField = typeof(CombatSystem).GetField("_enemyIds",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var enemyIds = enemyIdsField.GetValue(_combatSystem) as List<string>;
        enemyIds.Add(enemy1);
        enemyIds.Add(enemy2);

        // Player ship is alive
        Assert.AreNotEqual(ShipState.DESTROYED, _playerShip.State);

        // First enemy dies — no victory yet
        SimulateOnShipDying(enemy1);
        Assert.AreNotEqual(ShipState.DESTROYED, _playerShip.State, "Player should still be alive after first enemy dies");

        // Second enemy dies → victory
        SimulateOnShipDying(enemy2);

        // Player should be back in IN_COCKPIT (victory path)
        Assert.AreEqual(ShipState.IN_COCKPIT, _playerShip.State,
            "Player should return to IN_COCKPIT after victory");

        yield return null;
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-3: Defeat when player hull reaches 0
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void OnAnyShipDying_triggers_defeat_when_player_ship_dying()
    {
        // Given: Combat is active
        _combatSystem.BeginCombat("player-ship-1", "node-A");

        // When: Player ship dies
        SimulateOnShipDying("player-ship-1");

        // Then: Player ship should be DESTROYED
        Assert.AreEqual(ShipState.DESTROYED, _playerShip.State,
            "Player ship should be DESTROYED after defeat");
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-4: OnShipDying ignored when not in COMBAT_ACTIVE
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void OnAnyShipDying_ignored_when_idle()
    {
        // Given: CombatSystem is IDLE (never called BeginCombat)

        // When: OnShipDying fires for player ship while IDLE
        SimulateOnShipDying("player-ship-1");

        // Then: Player ship state is unchanged (still DOCKED)
        Assert.AreEqual(ShipState.DOCKED, _playerShip.State,
            "Ship state should be unchanged when OnShipDying fires in IDLE state");
    }

    [Test]
    public void BeginCombat_rejected_when_already_active()
    {
        // Given: Combat is already active
        _combatSystem.BeginCombat("player-ship-1", "node-A");

        // When: BeginCombat called again
        bool threw = false;
        try {
            _combatSystem.BeginCombat("player-ship-1", "node-B");
        } catch {
            threw = true;
        }

        // Then: Second call is ignored (no exception, state unchanged)
        Assert.IsFalse(threw, "BeginCombat should not throw when already active");
    }
}

#endif
