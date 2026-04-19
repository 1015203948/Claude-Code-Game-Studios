#if false
using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using Object = UnityEngine.Object;
using System.Reflection;

/// <summary>
/// CombatChannel 单元测试。
/// 覆盖 Story 008 所有验收标准（AC-1 ~ AC-4）。
/// </summary>
[TestFixture]
public class CombatChannel_Test
{
    // ─────────────────────────────────────────────────────────────────
    // Fields
    // ─────────────────────────────────────────────────────────────────

    private CombatChannel _channel;
    private List<CombatPayload> _receivedPayloads;
    private int _victoryCallCount;
    private int _defeatCallCount;

    // ─────────────────────────────────────────────────────────────────
    // SetUp / TearDown
    // ─────────────────────────────────────────────────────────────────

    [SetUp]
    public void SetUp()
    {
        _receivedPayloads = new List<CombatPayload>();
        _victoryCallCount = 0;
        _defeatCallCount = 0;

        _channel = ScriptableObject.CreateInstance<CombatChannel>();

        _channel.Subscribe(payload => {
            _receivedPayloads.Add(payload);
        });
    }

    [TearDown]
    public void TearDown()
    {
        if (_channel != null) Object.DestroyImmediate(_channel);
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-1: CombatChannel SO with RaiseBegin/RaiseVictory/RaiseDefeat
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void ac1_channel_exists()
    {
        Assert.IsNotNull(_channel);
    }

    [Test]
    public void ac1_raise_begin_broadcasts()
    {
        _channel.RaiseBegin("node-A");
        Assert.GreaterOrEqual(_receivedPayloads.Count, 1);
    }

    [Test]
    public void ac1_raise_victory_broadcasts()
    {
        _channel.RaiseVictory("node-A");
        Assert.GreaterOrEqual(_receivedPayloads.Count, 1);
    }

    [Test]
    public void ac1_raise_defeat_broadcasts()
    {
        _channel.RaiseDefeat("node-A");
        Assert.GreaterOrEqual(_receivedPayloads.Count, 1);
    }

    [Test]
    public void ac1_raise_begin_payload_has_node_id()
    {
        _channel.RaiseBegin("node-X");
        Assert.AreEqual("node-X", _receivedPayloads[^1].NodeId);
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-2: RaiseVictory fired once on victory
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void ac2_raise_victory_broadcasts_victory_result()
    {
        _channel.RaiseVictory("node-V");
        Assert.AreEqual(CombatResult.Victory, _receivedPayloads[^1].Result);
    }

    [Test]
    public void ac2_raise_victory_broadcasts_correct_node()
    {
        _channel.RaiseVictory("node-V");
        Assert.AreEqual("node-V", _receivedPayloads[^1].NodeId);
    }

    [Test]
    public void ac2_raise_victory_each_call_fires_once()
    {
        _channel.RaiseVictory("n1");
        _channel.RaiseVictory("n2");
        var victories = _receivedPayloads.FindAll(p => p.Result == CombatResult.Victory);
        Assert.AreEqual(2, victories.Count);
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-3: RaiseDefeat fired once on defeat
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void ac3_raise_defeat_broadcasts_defeat_result()
    {
        _channel.RaiseDefeat("node-D");
        Assert.AreEqual(CombatResult.Defeat, _receivedPayloads[^1].Result);
    }

    [Test]
    public void ac3_raise_defeat_broadcasts_correct_node()
    {
        _channel.RaiseDefeat("node-D");
        Assert.AreEqual("node-D", _receivedPayloads[^1].NodeId);
    }

    [Test]
    public void ac3_victory_and_defeat_are_distinct_events()
    {
        _channel.RaiseVictory("node-A");
        _channel.RaiseDefeat("node-B");

        var results = _receivedPayloads.ConvertAll(p => p.Result);
        CollectionAssert.Contains(results, CombatResult.Victory);
        CollectionAssert.Contains(results, CombatResult.Defeat);
    }

    // ─────────────────────────────────────────────────────────────────
    // AC-4: SO Channel subscription pairs correctly
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void ac4_subscriber_receives_event()
    {
        _channel.RaiseVictory("node-1");
        Assert.AreEqual(1, _receivedPayloads.Count);
    }

    [Test]
    public void ac4_multiple_subscribers_all_receive()
    {
        int extraCount = 0;
        _channel.Subscribe(payload => { extraCount++; });

        _channel.RaiseVictory("node-M");

        Assert.AreEqual(1, _receivedPayloads.Count);
        Assert.AreEqual(1, extraCount);
    }

    [Test]
    public void ac4_unsubscribe_stops_receiving()
    {
        System.Action<CombatPayload> handler = p => { _victoryCallCount++; };
        _channel.Subscribe(handler);
        _channel.RaiseVictory("n1");

        _channel.Unsubscribe(handler);
        _channel.RaiseVictory("n2");

        Assert.AreEqual(1, _victoryCallCount);
    }

    // ─────────────────────────────────────────────────────────────────
    // Additional: CombatResult enum
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void extra_combat_result_enum_has_victory_and_defeat()
    {
        Assert.IsTrue(Enum.IsDefined(typeof(CombatResult), CombatResult.Victory));
        Assert.IsTrue(Enum.IsDefined(typeof(CombatResult), CombatResult.Defeat));
    }

    [Test]
    public void extra_combat_payload_carries_both_fields()
    {
        var payload = new CombatPayload("test-node", CombatResult.Victory);
        Assert.AreEqual("test-node", payload.NodeId);
        Assert.AreEqual(CombatResult.Victory, payload.Result);
    }
}

#endif
