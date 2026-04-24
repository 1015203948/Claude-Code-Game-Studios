using NUnit.Framework;
using Game.Channels;
using UnityEngine;

/// <summary>
/// WeaponFiredChannel 单元测试。
/// 验证 payload 构造和事件广播。
/// </summary>
[TestFixture]
public class WeaponFiredChannel_Test
{
    private WeaponFiredChannel _channel;

    [SetUp]
    public void SetUp()
    {
        _channel = ScriptableObject.CreateInstance<WeaponFiredChannel>();
        WeaponFiredChannel.ResetInstanceForTest();
    }

    [TearDown]
    public void TearDown()
    {
        WeaponFiredChannel.ResetInstanceForTest();
        if (_channel != null) Object.DestroyImmediate(_channel);
    }

    [Test]
    public void payload_stores_fire_origin_and_direction()
    {
        var payload = new WeaponFiredPayload(
            new Vector3(0f, 0f, 0f),
            new Vector3(0f, 0f, 1f),
            null,
            false);

        Assert.AreEqual(Vector3.zero, payload.Origin);
        Assert.AreEqual(Vector3.forward, payload.Direction);
        Assert.IsFalse(payload.Hit);
        Assert.IsNull(payload.HitPoint);
    }

    [Test]
    public void payload_stores_hit_data()
    {
        var hitPoint = new Vector3(10f, 5f, 20f);
        var payload = new WeaponFiredPayload(
            Vector3.zero,
            Vector3.forward,
            hitPoint,
            true,
            "enemy_001",
            25f);

        Assert.IsTrue(payload.Hit);
        Assert.AreEqual(hitPoint, payload.HitPoint);
        Assert.AreEqual("enemy_001", payload.TargetId);
        Assert.AreEqual(25f, payload.Damage, 0.001f);
    }

    [Test]
    public void raise_invokes_subscribers()
    {
        bool received = false;
        Vector3? receivedHitPoint = null;

        _channel.Subscribe(p => {
            received = true;
            receivedHitPoint = p.HitPoint;
        });

        var hitPoint = new Vector3(5f, 3f, 10f);
        _channel.Raise(new WeaponFiredPayload(Vector3.zero, Vector3.forward, hitPoint, true));

        Assert.IsTrue(received, "Subscriber should be invoked");
        Assert.AreEqual(hitPoint, receivedHitPoint, "Hit point should match");
    }

    [Test]
    public void unsubscribe_stops_receiving_events()
    {
        int count = 0;
        System.Action<WeaponFiredPayload> handler = p => count++;

        _channel.Subscribe(handler);
        _channel.Raise(new WeaponFiredPayload(Vector3.zero, Vector3.forward, null, false));
        Assert.AreEqual(1, count);

        _channel.Unsubscribe(handler);
        _channel.Raise(new WeaponFiredPayload(Vector3.zero, Vector3.forward, null, false));
        Assert.AreEqual(1, count, "Should not receive after unsubscribe");
    }

    [Test]
    public void damage_defaults_to_zero()
    {
        var payload = new WeaponFiredPayload(Vector3.zero, Vector3.forward, null, false);
        Assert.AreEqual(0f, payload.Damage, "Damage should default to 0");
    }
}
