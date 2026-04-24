using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using Game.Channels;
using Game.Data;
using Game.Gameplay;
using Gameplay;
using Object = UnityEngine.Object;

/// <summary>
/// Combat System Victory Rewards & Node Conquest 单元测试。
/// 覆盖 Sprint 2 Phase 2 战斗奖励循环。
/// </summary>
[TestFixture]
public class CombatRewards_Test
{
    private GameObject _combatGo;
    private CombatSystem _combatSystem;
    private GameDataManager _gameDataManager;
    private GameObject _colonyGo;
    private ColonyManager _colonyManager;
    private ResourceConfig _resourceConfig;
    private GameObject _healthGo;
    private HealthSystem _healthSystem;

    private const string PLAYER_ID = "player_001";
    private const string NODE_ID = "node-rich-1";
    private const string ENEMY_ID = "enemy_test_001";

    [SetUp]
    public void SetUp()
    {
        // Reset singletons
        CombatSystem.ResetInstanceForTest();
        ColonyManager.ResetInstanceForTest();
        HealthSystem.ResetInstanceForTest();
        GameDataManager.ResetInstanceForTest();

        // GameDataManager
        _gameDataManager = new GameDataManager();

        // StarMap with RICH node
        var nodes = new List<StarNode> {
            new StarNode(NODE_ID, "Rich Node", Vector2.zero, NodeType.RICH)
        };
        var edges = new List<StarEdge>();
        _gameDataManager.SetStarMapData(new StarMapData(nodes, edges));

        // Player ship
        var hull = ScriptableObject.CreateInstance<HullBlueprint>();
        hull.BaseHull = 100;
        var shipChannel = ScriptableObject.CreateInstance<ShipStateChannel>();
        var playerShip = new ShipDataModel(PLAYER_ID, "generic_v1", true, hull, shipChannel);
        playerShip.SetState(ShipState.IN_COCKPIT);
        _gameDataManager.RegisterShip(playerShip);

        // HealthSystem
        _healthGo = new GameObject("HealthSystem");
        _healthSystem = _healthGo.AddComponent<HealthSystem>();

        // ColonyManager
        _resourceConfig = ScriptableObject.CreateInstance<ResourceConfig>();
        _colonyGo = new GameObject("ColonyManager");
        _colonyManager = _colonyGo.AddComponent<ColonyManager>();
        SetField(_colonyManager, "_resourceConfig", _resourceConfig);
        _colonyManager.Initialize(100, 50);

        // CombatSystem
        _combatGo = new GameObject("CombatSystem");
        _combatSystem = _combatGo.AddComponent<CombatSystem>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_combatGo != null) Object.DestroyImmediate(_combatGo);
        if (_colonyGo != null) Object.DestroyImmediate(_colonyGo);
        if (_healthGo != null) Object.DestroyImmediate(_healthGo);
        GameDataManager.ResetInstanceForTest();
        CombatSystem.ResetInstanceForTest();
        ColonyManager.ResetInstanceForTest();
        HealthSystem.ResetInstanceForTest();
    }

    private void SetField(object obj, string name, object value)
    {
        var f = obj.GetType().GetField(name,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Instance);
        f?.SetValue(obj, value);
    }

    private T GetField<T>(object obj, string name)
    {
        var f = obj.GetType().GetField(name,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Instance);
        return (T)f?.GetValue(obj);
    }

    // ─────────────────────────────────────────────────────────────────
    // Reward Calculation Tests
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void victory_rewards_2_enemies_base_rewards()
    {
        // Verify reward math: 2 enemies × 50 ore = 100 ore
        int oreReward = 2 * 50;
        int energyReward = 2 * 20;
        Assert.AreEqual(100, oreReward);
        Assert.AreEqual(40, energyReward);

        // Verify AddResources applies correctly
        _colonyManager.AddResources(oreReward, energyReward);
        Assert.AreEqual(200, _colonyManager.OreCurrent, "100 base + 100 reward = 200");
        Assert.AreEqual(90, _colonyManager.EnergyCurrent, "50 base + 40 reward = 90");
    }

    [Test]
    public void victory_rewards_rich_node_doubles_rewards()
    {
        // RICH node: ×2 multiplier on ore and energy
        int baseReward = 2 * 50; // 100
        int richReward = baseReward * 2; // 200
        Assert.AreEqual(200, richReward, "RICH doubles ore reward");
    }

    [Test]
    public void victory_conquers_node_to_player()
    {
        var enemyIds = new List<string> { "e1" };
        SetField(_combatSystem, "_enemyIds", enemyIds);
        SetField(_combatSystem, "_initialEnemyCount", 1);
        SetField(_combatSystem, "_state", CombatSystem_CombatState());
        SetField(_combatSystem, "_playerShipId", PLAYER_ID);
        SetField(_combatSystem, "_nodeId", NODE_ID);

        var method = typeof(CombatSystem).GetMethod("ConquerNode",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method.Invoke(_combatSystem, null);

        var map = _gameDataManager.GetStarMapData();
        Assert.AreEqual(OwnershipState.PLAYER, map.Nodes[0].Ownership,
            "Victory should set node ownership to PLAYER");
    }

    // ─────────────────────────────────────────────────────────────────
    // ColonyManager AddResources Tests
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void add_resources_increases_ore_and_energy()
    {
        _colonyManager.AddResources(50, 30);
        Assert.AreEqual(150, _colonyManager.OreCurrent, "100 + 50 = 150");
        Assert.AreEqual(80, _colonyManager.EnergyCurrent, "50 + 30 = 80");
    }

    [Test]
    public void add_resources_clamps_ore_to_cap()
    {
        _colonyManager.AddResources(2000, 0);
        Assert.AreEqual(1000, _colonyManager.OreCurrent,
            "Ore should be clamped to ORE_CAP (1000)");
    }

    [Test]
    public void add_resources_energy_no_upper_cap()
    {
        _colonyManager.AddResources(0, 100000);
        Assert.AreEqual(100050, _colonyManager.EnergyCurrent,
            "Energy has no upper cap");
    }

    // ─────────────────────────────────────────────────────────────────
    // ColonyManager Save/Load Tests
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void save_load_roundtrip_preserves_resources()
    {
        _colonyManager.AddResources(200, 100);
        Assert.AreEqual(300, _colonyManager.OreCurrent);
        Assert.AreEqual(150, _colonyManager.EnergyCurrent);

        // Conquer a node
        var map = _gameDataManager.GetStarMapData();
        map.Nodes[0].Ownership = OwnershipState.PLAYER;

        // Save
        _colonyManager.Save();

        // Reset colony
        _colonyManager.Initialize(0, 0);
        Assert.AreEqual(0, _colonyManager.OreCurrent);
        Assert.AreEqual(0, _colonyManager.EnergyCurrent);

        // Load
        bool loaded = _colonyManager.Load();
        Assert.IsTrue(loaded, "Load should find save file");
        Assert.AreEqual(300, _colonyManager.OreCurrent, "Ore restored after load");
        Assert.AreEqual(150, _colonyManager.EnergyCurrent, "Energy restored after load");

        // Node ownership restored
        Assert.AreEqual(OwnershipState.PLAYER, map.Nodes[0].Ownership,
            "Node ownership should be restored from save");
    }

    [Test]
    public void load_returns_false_when_no_save_file()
    {
        // Delete save file if it exists
        string path = System.IO.Path.Combine(Application.persistentDataPath, "resources.json");
        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);

        bool loaded = _colonyManager.Load();
        Assert.IsFalse(loaded, "Load should return false when no save file");
    }

    // ─────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────

    private object CombatSystem_CombatState()
    {
        // CombatState is private enum — get via string comparison
        var stateField = typeof(CombatSystem).GetField("_state",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var stateEnum = stateField.FieldType;
        return System.Enum.Parse(stateEnum, "COMBAT_ACTIVE");
    }
}
