using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using Object = UnityEngine.Object;

/// <summary>
/// ShipControlSystem Soft Lock + FireRequested 单元测试。
/// 覆盖 Story 021 所有验收标准（L-1 ~ L-3）。
/// </summary>
[TestFixture]
public class SoftLock_Test
{
    // ─────────────────────────────────────────────────────────────────
    // Fields
    // ─────────────────────────────────────────────────────────────────

    private GameObject _shipGo;
    private Rigidbody _rb;
    private ShipControlSystem _scs;
    private DualJoystickInput _input;
    private GameObject _inputGo;
    private ShipStateChannel _shipStateChannel;
    private ViewLayerChannel _viewLayerChannel;
    private EnemySystem _enemySystem;
    private GameObject _enemySystemGo;

    private int _fireRequestedCallCount;

    // ─────────────────────────────────────────────────────────────────
    // SetUp / TearDown
    // ─────────────────────────────────────────────────────────────────

    [SetUp]
    public void SetUp()
    {
        _fireRequestedCallCount = 0;

        _shipStateChannel = ScriptableObject.CreateInstance<ShipStateChannel>();
        _viewLayerChannel = ScriptableObject.CreateInstance<ViewLayerChannel>();

        _enemySystemGo = new GameObject("EnemySystem");
        _enemySystem = _enemySystemGo.AddComponent<EnemySystem>();

        _shipGo = new GameObject("Ship");
        _rb = _shipGo.AddComponent<Rigidbody>();
        _rb.useGravity = false;
        _rb.constraints = RigidbodyConstraints.None;

        _inputGo = new GameObject("DualJoystickInput");
        _input = _inputGo.AddComponent<DualJoystickInput>();

        _scs = _shipGo.AddComponent<ShipControlSystem>();

        SetField(_scs, "_shipStateChannel", _shipStateChannel);
        SetField(_scs, "_dualJoystick", _input);
        SetField(_scs, "_thrustPower", 15f);
        SetField(_scs, "_turnSpeed", 120f);

        // Default: IN_COCKPIT so _inputEnabled=true
        SetField(_scs, "_inputEnabled", true);

        // Subscribe to FireRequested
        _scs = _scs;
        var eventField = typeof(ShipControlSystem).GetField("FireRequested",
            BindingFlags.NonPublic | BindingFlags.Instance);
        // Subscribe via the event directly
        _scs.FireRequested += OnFireRequested;
    }

    [TearDown]
    public void TearDown()
    {
        _scs.FireRequested -= OnFireRequested;
        if (_shipGo != null) Object.DestroyImmediate(_shipGo);
        if (_inputGo != null) Object.DestroyImmediate(_inputGo);
        if (_shipStateChannel != null) Object.DestroyImmediate(_shipStateChannel);
        if (_viewLayerChannel != null) Object.DestroyImmediate(_viewLayerChannel);
        if (_enemySystemGo != null) Object.DestroyImmediate(_enemySystemGo);
        if (EnemySystem.Instance != null) EnemySystem.Instance = null;
    }

    private void OnFireRequested()
    {
        _fireRequestedCallCount++;
    }

    // ─────────────────────────────────────────────────────────────────
    // Reflection Helpers
    // ─────────────────────────────────────────────────────────────────

    private void SetField(object obj, string fieldName, object value)
    {
        var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"Field '{fieldName}' not found on {obj.GetType().Name}");
        field.SetValue(obj, value);
    }

    private T GetField<T>(object obj, string fieldName)
    {
        var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        return (T)field.GetValue(obj);
    }

    private void InvokeUpdate()
    {
        var method = typeof(ShipControlSystem).GetMethod("Update",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method);
        method.Invoke(_scs, null);
    }

    private void InvokeUpdateSoftLock()
    {
        var method = typeof(ShipControlSystem).GetMethod("UpdateSoftLock",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method);
        method.Invoke(_scs, null);
    }

    private float CallCalculateAimAngle()
    {
        var method = typeof(ShipControlSystem).GetMethod("CalculateAimAngle",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method);
        return (float)method.Invoke(_scs, null);
    }

    private EnemyAIController CreateEnemyAt(Vector3 position)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.position = position;
        var controller = go.AddComponent<EnemyAIController>();
        // Register in EnemySystem's registry so GetNearestEnemyInRange finds it
        string instanceId = $"enemy_{System.Guid.NewGuid():N}";
        _enemySystem.RegisterEnemyForTest(controller, instanceId);
        return controller;
    }

    // ─────────────────────────────────────────────────────────────────
    // L-1: SoftLockTarget selects nearest in LOCK_RANGE (80m)
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void l1_selects_nearest_enemy_in_range()
    {
        // Given: enemy-A at 60m; enemy-B at 40m
        var enemyA = CreateEnemyAt(new Vector3(60f, 0f, 0f));
        var enemyB = CreateEnemyAt(new Vector3(0f, 0f, 40f));
        _shipGo.transform.position = Vector3.zero;

        // When: UpdateSoftLock runs (target is null)
        InvokeUpdateSoftLock();

        // Then: nearest (enemy-B at 40m) is selected
        Transform target = GetField<Transform>(_scs, "_softLockTarget");
        Assert.AreEqual(enemyB.transform, target);
    }

    [Test]
    public void l1_ignores_enemy_outside_range()
    {
        // Given: enemy-A at 60m (in range); enemy-B at 100m (out of range)
        var enemyA = CreateEnemyAt(new Vector3(60f, 0f, 0f));
        var enemyB = CreateEnemyAt(new Vector3(100f, 0f, 0f));
        _shipGo.transform.position = Vector3.zero;

        InvokeUpdateSoftLock();

        Transform target = GetField<Transform>(_scs, "_softLockTarget");
        Assert.AreEqual(enemyA.transform, target);
        Assert.AreNotEqual(enemyB.transform, target);
    }

    [Test]
    public void l1_no_target_when_no_enemies_in_range()
    {
        // Given: only enemy at 200m (out of LOCK_RANGE=80)
        var enemy = CreateEnemyAt(new Vector3(200f, 0f, 0f));
        _shipGo.transform.position = Vector3.zero;

        InvokeUpdateSoftLock();

        Transform target = GetField<Transform>(_scs, "_softLockTarget");
        Assert.IsNull(target);
    }

    // ─────────────────────────────────────────────────────────────────
    // L-2: SoftLockTarget persists until out of range or destroyed
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void l2_target_persists_when_closer_enemy_enters_range()
    {
        // Given: enemy-A at 60m (already locked)
        var enemyA = CreateEnemyAt(new Vector3(60f, 0f, 0f));
        _shipGo.transform.position = Vector3.zero;
        InvokeUpdateSoftLock();
        Assert.AreEqual(enemyA.transform, GetField<Transform>(_scs, "_softLockTarget"));

        // When: enemy-B at 30m enters range
        var enemyB = CreateEnemyAt(new Vector3(0f, 0f, 30f));

        // EnemyA still at 60m (in range), should NOT switch
        InvokeUpdateSoftLock();

        Assert.AreEqual(enemyA.transform, GetField<Transform>(_scs, "_softLockTarget"),
            "Target should NOT switch when existing target is still in range");
    }

    [Test]
    public void l2_target_cleared_when_target_leaves_range()
    {
        // Given: enemy-A at 60m (locked)
        var enemyA = CreateEnemyAt(new Vector3(60f, 0f, 0f));
        _shipGo.transform.position = Vector3.zero;
        InvokeUpdateSoftLock();
        Assert.AreEqual(enemyA.transform, GetField<Transform>(_scs, "_softLockTarget"));

        // When: enemy-A moves to 100m (outside LOCK_RANGE=80)
        enemyA.transform.position = new Vector3(100f, 0f, 0f);

        InvokeUpdateSoftLock();

        Assert.IsNull(GetField<Transform>(_scs, "_softLockTarget"),
            "Target should be cleared when it leaves LOCK_RANGE");
    }

    [Test]
    public void l2_new_target_acquired_after_clear()
    {
        // Given: enemy-A at 60m (locked), then enemy-A leaves range
        var enemyA = CreateEnemyAt(new Vector3(60f, 0f, 0f));
        var enemyB = CreateEnemyAt(new Vector3(30f, 0f, 0f));
        _shipGo.transform.position = Vector3.zero;

        InvokeUpdateSoftLock();
        enemyA.transform.position = new Vector3(100f, 0f, 0f); // move out of range

        // When: UpdateSoftLock runs
        InvokeUpdateSoftLock();

        // Then: enemy-B (nearest remaining) is selected
        Assert.AreEqual(enemyB.transform, GetField<Transform>(_scs, "_softLockTarget"));
    }

    // ─────────────────────────────────────────────────────────────────
    // L-3: FireRequested fires when aimAngle ≤ 15°
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void l3_fire_requested_within_threshold()
    {
        // Given: enemy at 30m ahead (ship facing it), within threshold
        var enemy = CreateEnemyAt(new Vector3(0f, 0f, 30f));
        _shipGo.transform.position = Vector3.zero;
        _shipGo.transform.rotation = Quaternion.identity; // facing +Z

        _fireRequestedCallCount = 0;
        SetField(_scs, "_softLockTarget", enemy.transform);

        // When: Update runs
        InvokeUpdate();

        // Then: FireRequested fired (aimAngle should be ~0°)
        Assert.AreEqual(1, _fireRequestedCallCount,
            "FireRequested should fire when aimAngle ≤ 15°");
    }

    [Test]
    public void l3_no_fire_when_aim_angle_above_threshold()
    {
        // Given: enemy at 45° off-center (aimAngle > 15°)
        var enemy = CreateEnemyAt(new Vector3(30f, 0f, 30f)); // ~45° angle
        _shipGo.transform.position = Vector3.zero;
        _shipGo.transform.rotation = Quaternion.identity; // facing +Z

        _fireRequestedCallCount = 0;
        SetField(_scs, "_softLockTarget", enemy.transform);

        InvokeUpdate();

        Assert.AreEqual(0, _fireRequestedCallCount,
            "FireRequested should NOT fire when aimAngle > 15°");
    }

    [Test]
    public void l3_no_fire_when_no_target()
    {
        _fireRequestedCallCount = 0;
        SetField(_scs, "_softLockTarget", null);

        InvokeUpdate();

        Assert.AreEqual(0, _fireRequestedCallCount);
    }

    [Test]
    public void l3_aim_angle_calculated_correctly()
    {
        // Given: ship at origin facing +Z (0°), enemy at (0, 0, 30)
        var enemy = CreateEnemyAt(new Vector3(0f, 0f, 30f));
        _shipGo.transform.position = Vector3.zero;
        _shipGo.transform.rotation = Quaternion.identity; // facing +Z
        SetField(_scs, "_softLockTarget", enemy.transform);

        float angle = CallCalculateAimAngle();

        Assert.AreEqual(0f, angle, 0.5f, "Enemy directly ahead → angle ≈ 0°");
    }

    [Test]
    public void l3_aim_angle_30_degrees()
    {
        // Enemy at (50, 0, 87) relative → ~30° off center
        var enemy = CreateEnemyAt(new Vector3(50f, 0f, 87f));
        _shipGo.transform.position = Vector3.zero;
        _shipGo.transform.rotation = Quaternion.identity;
        SetField(_scs, "_softLockTarget", enemy.transform);

        float angle = CallCalculateAimAngle();

        Assert.AreEqual(30f, angle, 2f,
            "Enemy at 30° should give aimAngle ≈ 30°");
    }

    [Test]
    public void l3_aim_angle_360_when_no_target()
    {
        SetField(_scs, "_softLockTarget", null);
        float angle = CallCalculateAimAngle();
        Assert.AreEqual(360f, angle);
    }

    // ─────────────────────────────────────────────────────────────────
    // OnAimAngleChanged broadcast
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void on_aim_angle_changed_fired_each_frame()
    {
        var enemy = CreateEnemyAt(new Vector3(0f, 0f, 30f));
        _shipGo.transform.position = Vector3.zero;
        _shipGo.transform.rotation = Quaternion.identity;
        SetField(_scs, "_softLockTarget", enemy.transform);

        float? lastAngle = null;
        int changeCount = 0;
        _scs.OnAimAngleChanged += (angle) => {
            changeCount++;
            lastAngle = angle;
        };

        // Call Update twice
        InvokeUpdate();
        InvokeUpdate();

        Assert.Greater(changeCount, 0, "OnAimAngleChanged should fire each Update");
    }
}
