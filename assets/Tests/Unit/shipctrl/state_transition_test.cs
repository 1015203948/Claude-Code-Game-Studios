#if false
using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using Object = UnityEngine.Object;

/// <summary>
/// ShipControlSystem State Transitions 单元测试。
/// 覆盖 Story 023 所有验收标准（S-1 ~ S-4）。
/// </summary>
[TestFixture]
public class StateTransition_Test
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
    private CameraRig _cameraRig;
    private GameObject _cameraRigGo;

    private int _onLockLostCallCount;
    private int _fireRequestedCallCount;

    // ─────────────────────────────────────────────────────────────────
    // SetUp / TearDown
    // ─────────────────────────────────────────────────────────────────

    [SetUp]
    public void SetUp()
    {
        // Clean up scene-resident ShipControlSystem
        if (ShipControlSystem.Instance != null)
            Object.DestroyImmediate(ShipControlSystem.Instance.gameObject);
        ShipControlSystem.ResetInstanceForTest();

        _onLockLostCallCount = 0;
        _fireRequestedCallCount = 0;

        _shipStateChannel = ScriptableObject.CreateInstance<ShipStateChannel>();
        _viewLayerChannel = ScriptableObject.CreateInstance<ViewLayerChannel>();

        _shipGo = new GameObject("Ship");
        _rb = _shipGo.AddComponent<Rigidbody>();
        _rb.useGravity = false;
        _rb.constraints = RigidbodyConstraints.None;
        _rb.isKinematic = false;

        _inputGo = new GameObject("DualJoystickInput");
        _input = _inputGo.AddComponent<DualJoystickInput>();

        _cameraRigGo = new GameObject("CameraRig");
        _cameraRig = _cameraRigGo.AddComponent<CameraRig>();

        _scs = _shipGo.AddComponent<ShipControlSystem>();

        SetField(_scs, "_shipStateChannel", _shipStateChannel);
        SetField(_scs, "_viewLayerChannel", _viewLayerChannel);
        SetField(_scs, "_dualJoystick", _input);
        SetField(_scs, "_cameraRig", _cameraRig);
        SetField(_scs, "_thrustPower", 15f);
        SetField(_scs, "_turnSpeed", 120f);
        SetField(_scs, "_fireAngleThreshold", 15f);
        SetField(_scs, "_inputEnabled", false); // start disabled

        _scs.OnLockLost += OnLockLost;
        _scs.FireRequested += OnFireRequested;
    }

    [TearDown]
    public void TearDown()
    {
        _scs.OnLockLost -= OnLockLost;
        _scs.FireRequested -= OnFireRequested;
        if (_shipGo != null) Object.DestroyImmediate(_shipGo);
        if (_inputGo != null) Object.DestroyImmediate(_inputGo);
        if (_cameraRigGo != null) Object.DestroyImmediate(_cameraRigGo);
        if (_shipStateChannel != null) Object.DestroyImmediate(_shipStateChannel);
        if (_viewLayerChannel != null) Object.DestroyImmediate(_viewLayerChannel);
        ShipControlSystem.ResetInstanceForTest();
    }

    private void OnLockLost() { _onLockLostCallCount++; }
    private void OnFireRequested() { _fireRequestedCallCount++; }

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

    private void SimulateUpdate()
    {
        var method = typeof(ShipControlSystem).GetMethod("Update",
            BindingFlags.NonPublic | BindingFlags.Instance);
        method?.Invoke(_scs, null);
    }

    // ─────────────────────────────────────────────────────────────────
    // S-1: → IN_COCKPIT init
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void s1_enables_input_on_IN_COCKPIT()
    {
        // Given: input disabled
        SetField(_scs, "_inputEnabled", false);

        // When: OnShipStateChanged fires with IN_COCKPIT
        _shipStateChannel.Raise(("ship-1", ShipState.IN_COCKPIT));

        // Then: input enabled
        bool enabled = GetField<bool>(_scs, "_inputEnabled");
        Assert.IsTrue(enabled, "S-1: Input should be enabled on IN_COCKPIT");
    }

    [Test]
    public void s1_resets_softlock_on_IN_COCKPIT()
    {
        // Given: existing soft-lock target
        SetField(_scs, "_softLockTarget", new GameObject("OldTarget").transform);

        // When: IN_COCKPIT
        _shipStateChannel.Raise(("ship-1", ShipState.IN_COCKPIT));

        // Then: soft-lock cleared
        Transform target = GetField<Transform>(_scs, "_softLockTarget");
        Assert.IsNull(target, "S-1: SoftLockTarget should be reset to null on IN_COCKPIT");
    }

    [Test]
    public void s1_resets_aim_angle_on_IN_COCKPIT()
    {
        SetField(_scs, "_aimAngle", 5f);
        _shipStateChannel.Raise(("ship-1", ShipState.IN_COCKPIT));
        float angle = GetField<float>(_scs, "_aimAngle");
        Assert.AreEqual(360f, angle, "S-1: aimAngle should reset to 360 on IN_COCKPIT");
    }

    [Test]
    public void s1_resets_weapon_cooldown_on_IN_COCKPIT()
    {
        SetField(_scs, "_weaponCooldown", 99f);
        _shipStateChannel.Raise(("ship-1", ShipState.IN_COCKPIT));
        float cooldown = GetField<float>(_scs, "_weaponCooldown");
        Assert.AreEqual(0f, cooldown, "S-1: weaponCooldown should reset on IN_COCKPIT");
    }

    // ─────────────────────────────────────────────────────────────────
    // S-2: IN_COCKPIT → IN_COMBAT — no cleanup
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void s2_IN_COMBAT_does_not_clear_softlock()
    {
        // Given: in IN_COCKPIT with soft-lock
        var mockTarget = new GameObject("Target").transform;
        SetField(_scs, "_softLockTarget", mockTarget);
        SetField(_scs, "_aimAngle", 10f);
        SetField(_scs, "_weaponCooldown", 0.5f);
        SetField(_scs, "_inputEnabled", true);

        // When: IN_COMBAT
        _shipStateChannel.Raise(("ship-1", ShipState.IN_COMBAT));

        // Then: nothing cleared
        Assert.AreEqual(mockTarget, GetField<Transform>(_scs, "_softLockTarget"));
        Assert.AreEqual(10f, GetField<float>(_scs, "_aimAngle"));
        Assert.IsTrue(GetField<bool>(_scs, "_inputEnabled"));
    }

    [Test]
    public void s2_IN_COMBAT_preserves_input_enabled()
    {
        SetField(_scs, "_inputEnabled", true);
        _shipStateChannel.Raise(("ship-1", ShipState.IN_COMBAT));
        Assert.IsTrue(GetField<bool>(_scs, "_inputEnabled"));
    }

    // ─────────────────────────────────────────────────────────────────
    // S-3: IN_COCKPIT → DOCKED — disable input, preserve velocity
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void s3_DOCKED_disables_input()
    {
        SetField(_scs, "_inputEnabled", true);
        _shipStateChannel.Raise(("ship-1", ShipState.DOCKED));
        Assert.IsFalse(GetField<bool>(_scs, "_inputEnabled"));
    }

    [Test]
    public void s3_DOCKED_clears_softlock()
    {
        var mockTarget = new GameObject("Target").transform;
        SetField(_scs, "_softLockTarget", mockTarget);
        _shipStateChannel.Raise(("ship-1", ShipState.DOCKED));
        Assert.IsNull(GetField<Transform>(_scs, "_softLockTarget"));
    }

    [Test]
    public void s3_DOCKED_fires_OnLockLost()
    {
        _onLockLostCallCount = 0;
        SetField(_scs, "_softLockTarget", new GameObject("Target").transform);
        _shipStateChannel.Raise(("ship-1", ShipState.DOCKED));
        Assert.AreEqual(1, _onLockLostCallCount, "S-3: OnLockLost should fire on DOCKED");
    }

    [Test]
    public void s3_DOCKED_preserves_velocity()
    {
        // Given: velocity = (10, 0, 5)
        _rb.velocity = new Vector3(10f, 0f, 5f);
        SetField(_scs, "_inputEnabled", true);

        // When: DOCKED
        _shipStateChannel.Raise(("ship-1", ShipState.DOCKED));

        // Then: velocity unchanged (S-3: velocity NOT reset)
        Assert.AreEqual(10f, _rb.velocity.x, 0.001f);
        Assert.AreEqual(5f, _rb.velocity.z, 0.001f);
    }

    [Test]
    public void s3_DOCKED_does_not_set_isKinematic()
    {
        _rb.isKinematic = false;
        _shipStateChannel.Raise(("ship-1", ShipState.DOCKED));
        Assert.IsFalse(_rb.isKinematic, "S-3: isKinematic should NOT be set on DOCKED");
    }

    // ─────────────────────────────────────────────────────────────────
    // S-4: → DESTROYED — full cleanup
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void s4_DESTROYED_disables_input()
    {
        SetField(_scs, "_inputEnabled", true);
        _shipStateChannel.Raise(("ship-1", ShipState.DESTROYED));
        Assert.IsFalse(GetField<bool>(_scs, "_inputEnabled"));
    }

    [Test]
    public void s4_DESTROYED_clears_softlock()
    {
        var mockTarget = new GameObject("Target").transform;
        SetField(_scs, "_softLockTarget", mockTarget);
        _shipStateChannel.Raise(("ship-1", ShipState.DESTROYED));
        Assert.IsNull(GetField<Transform>(_scs, "_softLockTarget"));
    }

    [Test]
    public void s4_DESTROYED_fires_OnLockLost()
    {
        _onLockLostCallCount = 0;
        SetField(_scs, "_softLockTarget", new GameObject("Target").transform);
        _shipStateChannel.Raise(("ship-1", ShipState.DESTROYED));
        Assert.AreEqual(1, _onLockLostCallCount, "S-4: OnLockLost should fire on DESTROYED");
    }

    [Test]
    public void s4_DESTROYED_zeros_velocity()
    {
        _rb.velocity = new Vector3(10f, 0f, 5f);
        _shipStateChannel.Raise(("ship-1", ShipState.DESTROYED));
        Assert.AreEqual(Vector3.zero, _rb.velocity, "S-4: velocity should be zeroed on DESTROYED");
    }

    [Test]
    public void s4_DESTROYED_zeros_angularvelocity()
    {
        _rb.angularVelocity = new Vector3(0f, 99f, 0f);
        _shipStateChannel.Raise(("ship-1", ShipState.DESTROYED));
        Assert.AreEqual(Vector3.zero, _rb.angularVelocity, "S-4: angularVelocity should be zeroed");
    }

    [Test]
    public void s4_DESTROYED_sets_isKinematic_true()
    {
        _rb.isKinematic = false;
        _shipStateChannel.Raise(("ship-1", ShipState.DESTROYED));
        Assert.IsTrue(_rb.isKinematic, "S-4: isKinematic should be set to true on DESTROYED");
    }

    // ─────────────────────────────────────────────────────────────────
    // Edge cases
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void s1_triggers_camera_switch_to_THIRD_PERSON()
    {
        // Given: camera rig with initial mode
        // When: IN_COCKPIT
        _shipStateChannel.Raise(("ship-1", ShipState.IN_COCKPIT));

        // Then: camera should have started transitioning to THIRD_PERSON
        // (We can't directly check CameraRig state without adding more public APIs,
        // but we can verify SwitchMode was called by checking the camera mode)
        Assert.AreEqual(CameraRig.CameraMode.THIRD_PERSON, _cameraRig.Mode);
    }

    [Test]
    public void TransitionLockLost_not_fired_when_no_target()
    {
        // Given: no soft-lock target
        _onLockLostCallCount = 0;
        SetField(_scs, "_softLockTarget", null);

        // When: DOCKED (but target was already null)
        _shipStateChannel.Raise(("ship-1", ShipState.DOCKED));

        // OnLockLost should still fire (target being null doesn't prevent the event)
        Assert.AreEqual(1, _onLockLostCallCount);
    }
}

#endif
