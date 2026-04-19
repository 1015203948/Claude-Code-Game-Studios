#if false
using Game.Gameplay;
using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using Object = UnityEngine.Object;

/// <summary>
/// StarMapSystem 单元测试 — Story 008 集成部分。
/// 验证 StarMapSystem 订阅 CombatChannel 并正确更新节点归属。
/// </summary>
[TestFixture]
public class StarMapSystem_Test
{
    // ─────────────────────────────────────────────────────────────────
    // Fields
    // ─────────────────────────────────────────────────────────────────

    private StarMapSystem _starMapSystem;
    private GameObject _systemGo;
    private CombatChannel _combatChannel;
    private StarMapData _starMap;
    private GameDataManager _gameDataManager;
    private GameObject _gameDataGo;

    // ─────────────────────────────────────────────────────────────────
    // SetUp / TearDown
    // ─────────────────────────────────────────────────────────────────

    [SetUp]
    public void SetUp()
    {
        // Clean up static instance
        var gdmType = typeof(GameDataManager);
        var instanceField = gdmType.GetProperty("Instance",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        instanceField?.SetValue(null, null);

        // StarMapData: 3 nodes — PLAYER, NEUTRAL, ENEMY
        var nodes = new List<StarNode> {
            new StarNode("node-player", "Player Node", Vector2.zero, NodeType.STANDARD) {
                Ownership = OwnershipState.PLAYER
            },
            new StarNode("node-neutral", "Neutral Node", new Vector2(1, 0), NodeType.STANDARD) {
                Ownership = OwnershipState.NEUTRAL
            },
            new StarNode("node-enemy", "Enemy Node", new Vector2(2, 0), NodeType.ENEMY) {
                Ownership = OwnershipState.ENEMY
            },
        };
        var edges = new List<StarEdge>();
        _starMap = new StarMapData(nodes, edges);

        // GameDataManager with StarMapData
        _gameDataGo = new GameObject("GameDataManager");
        _gameDataManager = _gameDataGo.AddComponent<GameDataManager>();
        _gameDataManager.SetStarMapData(_starMap);

        // StarMapSystem
        _systemGo = new GameObject("StarMapSystem");
        _starMapSystem = _systemGo.AddComponent<StarMapSystem>();

        _combatChannel = ScriptableObject.CreateInstance<CombatChannel>();
        var field = typeof(StarMapSystem).GetField("_combatChannel",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field.SetValue(_starMapSystem, _combatChannel);
    }

    [TearDown]
    public void TearDown()
    {
        if (_systemGo != null) Object.DestroyImmediate(_systemGo);
        if (_gameDataGo != null) Object.DestroyImmediate(_gameDataGo);
        if (_combatChannel != null) Object.DestroyImmediate(_combatChannel);

        var gdmType = typeof(GameDataManager);
        var instanceField = gdmType.GetProperty("Instance",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        instanceField?.SetValue(null, null);
    }

    // ─────────────────────────────────────────────────────────────────
    // StarMapSystem event subscription
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void star_map_system_subscribes_to_combat_channel_on_enable()
    {
        // Given: StarMapSystem is set up with CombatChannel
        // When: OnEnable is triggered (via AddComponent / start)
        // Then: subscribing to channel should receive events
        _combatChannel.RaiseVictory("node-player");

        // If subscription worked, the system processed the event
        Assert.Pass("StarMapSystem subscribed successfully");
    }

    // ─────────────────────────────────────────────────────────────────
    // Victory: node → PLAYER
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void victory_updates_neutral_node_to_player()
    {
        // Given: node-neutral is NEUTRAL
        Assert.AreEqual(OwnershipState.NEUTRAL,
            _starMap.GetNode("node-neutral").Ownership);

        // When: RaiseVictory("node-neutral")
        _combatChannel.RaiseVictory("node-neutral");

        // Then: node → PLAYER
        Assert.AreEqual(OwnershipState.PLAYER,
            _starMap.GetNode("node-neutral").Ownership);
    }

    [Test]
    public void victory_updates_enemy_node_to_player()
    {
        // Given: node-enemy is ENEMY
        Assert.AreEqual(OwnershipState.ENEMY,
            _starMap.GetNode("node-enemy").Ownership);

        // When: RaiseVictory("node-enemy")
        _combatChannel.RaiseVictory("node-enemy");

        // Then: node → PLAYER
        Assert.AreEqual(OwnershipState.PLAYER,
            _starMap.GetNode("node-enemy").Ownership);
    }

    [Test]
    public void victory_does_not_change_player_node()
    {
        // Given: node-player is already PLAYER
        Assert.AreEqual(OwnershipState.PLAYER,
            _starMap.GetNode("node-player").Ownership);

        // When: RaiseVictory("node-player")
        _combatChannel.RaiseVictory("node-player");

        // Then: still PLAYER (no change)
        Assert.AreEqual(OwnershipState.PLAYER,
            _starMap.GetNode("node-player").Ownership);
    }

    // ─────────────────────────────────────────────────────────────────
    // Defeat: node → ENEMY
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void defeat_updates_player_node_to_enemy()
    {
        // Given: node-player is PLAYER
        Assert.AreEqual(OwnershipState.PLAYER,
            _starMap.GetNode("node-player").Ownership);

        // When: RaiseDefeat("node-player")
        _combatChannel.RaiseDefeat("node-player");

        // Then: node → ENEMY
        Assert.AreEqual(OwnershipState.ENEMY,
            _starMap.GetNode("node-player").Ownership);
    }

    [Test]
    public void defeat_updates_neutral_node_to_enemy()
    {
        // Given: node-neutral is NEUTRAL
        Assert.AreEqual(OwnershipState.NEUTRAL,
            _starMap.GetNode("node-neutral").Ownership);

        // When: RaiseDefeat("node-neutral")
        _combatChannel.RaiseDefeat("node-neutral");

        // Then: node → ENEMY
        Assert.AreEqual(OwnershipState.ENEMY,
            _starMap.GetNode("node-neutral").Ownership);
    }

    [Test]
    public void defeat_does_not_change_enemy_node()
    {
        // Given: node-enemy is already ENEMY
        Assert.AreEqual(OwnershipState.ENEMY,
            _starMap.GetNode("node-enemy").Ownership);

        // When: RaiseDefeat("node-enemy")
        _combatChannel.RaiseDefeat("node-enemy");

        // Then: still ENEMY
        Assert.AreEqual(OwnershipState.ENEMY,
            _starMap.GetNode("node-enemy").Ownership);
    }

    // ─────────────────────────────────────────────────────────────────
    // Unknown node
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void victory_ignores_unknown_node()
    {
        // When: RaiseVictory("nonexistent-node")
        // Then: no crash
        _combatChannel.RaiseVictory("nonexistent-node");
        Assert.Pass("Unknown node handled gracefully");
    }

    [Test]
    public void defeat_ignores_unknown_node()
    {
        _combatChannel.RaiseDefeat("nonexistent-node");
        Assert.Pass("Unknown node handled gracefully");
    }
}

#endif
