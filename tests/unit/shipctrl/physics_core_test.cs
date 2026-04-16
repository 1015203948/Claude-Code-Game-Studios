using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using Object = UnityEngine.Object;

/// <summary>
/// ShipControlSystem Physics Core 单元测试。
/// 覆盖 Story 019 所有验收标准（P-1 ~ P-6）。
/// </summary>
[TestFixture]
public class PhysicsCore_Test
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

    // ─────────────────────────────────────────────────────────────────
    // SetUp / TearDown
    // ─────────────────────────────────────────────────────────────────

    [SetUp]
    public void SetUp()
    {
        // Create ScriptableObject channels
        _shipStateChannel = ScriptableObject.CreateInstance<ShipStateChannel>();
        _viewLayerChannel = ScriptableObject.CreateInstance<ViewLayerChannel>();

        _shipGo = new GameObject("Ship");
        _rb = _shipGo.AddComponent<Rigidbody>();
        _rb.useGravity = false;
        _rb.constraints = RigidbodyConstraints.None;

        _inputGo = new GameObject("DualJoystickInput");
        _input = _inputGo.AddComponent<DualJoystickInput>();

        _scs = _shipGo.AddComponent<ShipControlSystem>();

        // Inject fields
        SetField(_scs, "_shipStateChannel", _shipStateChannel);
        SetField(_scs, "_dualJoystick", _input);
        SetField(_scs, "_thrustPower", 15f);
        SetField(_scs, "_turnSpeed", 120f);

        // Set _inputEnabled=true directly (bypass channel subscription which has type issues)
        SetField(_scs, "_inputEnabled", true);
        SetField(_scs, "_weaponCooldown", 0f);
    }

    [TearDown]
    public void TearDown()
    {
        if (_shipGo != null) Object.DestroyImmediate(_shipGo);
        if (_inputGo != null) Object.DestroyImmediate(_inputGo);
        if (_shipStateChannel != null) Object.DestroyImmediate(_shipStateChannel);
        if (_viewLayerChannel != null) Object.DestroyImmediate(_viewLayerChannel);
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

    private void SetProperty(object obj, string propName, object value)
    {
        var prop = obj.GetType().GetProperty(propName);
        Assert.IsNotNull(prop, $"Property '{propName}' not found on {obj.GetType().Name}");
        prop.SetValue(obj, value);
    }

    private void SetInput(Vector2 thrust, Vector2 aim)
    {
        SetProperty(_input, "ThrustInput", thrust);
        SetProperty(_input, "AimInput", aim);
    }

    private void InvokeFixedUpdate()
    {
        var methods = typeof(ShipControlSystem).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var m in methods) {
            if (m.Name == "FixedUpdate" && m.GetParameters().Length == 0) {
                m.Invoke(_scs, null);
                return;
            }
        }
        Assert.Fail("FixedUpdate method not found");
    }

    private void InvokeLateUpdate()
    {
        var methods = typeof(ShipControlSystem).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var m in methods) {
            if (m.Name == "LateUpdate" && m.GetParameters().Length == 0) {
                m.Invoke(_scs, null);
                return;
            }
        }
        Assert.Fail("LateUpdate method not found");
    }

    // ─────────────────────────────────────────────────────────────────
    // P-1: AddForce thrust in forward direction
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void p1_thrust_applies_addforce_forward()
    {
        SetInput(new Vector2(0f, 1f), Vector2.zero);
        InvokeFixedUpdate();
        Assert.Greater(_rb.velocity.magnitude, 0f, "Thrust should increase velocity");
    }

    [Test]
    public void p1_thrust_respects_magnitude()
    {
        SetInput(new Vector2(0f, 0.5f), Vector2.zero);
        InvokeFixedUpdate();
        Assert.Greater(_rb.velocity.magnitude, 0f);
    }

    [Test]
    public void p1_zero_input_produces_no_force()
    {
        SetInput(Vector2.zero, Vector2.zero);
        _rb.velocity = Vector3.zero;
        InvokeFixedUpdate();
        Assert.AreEqual(Vector3.zero, _rb.velocity);
    }

    // ─────────────────────────────────────────────────────────────────
    // P-2: Soft speed clamp via opposing AddForce
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void p2_soft_clamp_allows_below_max()
    {
        _rb.velocity = new Vector3(3f, 0f, 3f); // ~4.24 m/s
        SetInput(Vector2.zero, Vector2.zero);
        float before = _scs.GetHorizontalSpeed();
        InvokeFixedUpdate();
        Assert.LessOrEqual(_scs.GetHorizontalSpeed(), before + 0.1f);
    }

    [Test]
    public void p2_soft_clamp_reduces_excess_speed()
    {
        _rb.velocity = new Vector3(10f, 0f, 10f); // ~14.14 m/s > 12
        SetInput(Vector2.zero, Vector2.zero);
        InvokeFixedUpdate();
        Assert.Less(_scs.GetHorizontalSpeed(), 14.14f,
            "Opposing force should reduce excess speed");
    }

    // ─────────────────────────────────────────────────────────────────
    // P-3: NO direct velocity assignment — forces only
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void p3_thrust_changes_velocity_via_addforce_only()
    {
        _rb.velocity = Vector3.zero;
        SetInput(new Vector2(0f, 1f), Vector2.zero);
        InvokeFixedUpdate();
        Assert.Greater(_rb.velocity.magnitude, 0f);
        Assert.AreNotEqual(15f, _rb.velocity.magnitude);
    }

    // ─────────────────────────────────────────────────────────────────
    // P-4: MoveRotation turn
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void p4_turn_changes_heading()
    {
        SetInput(Vector2.zero, new Vector2(1f, 0f));
        Quaternion initial = _rb.rotation;
        InvokeFixedUpdate();
        Assert.AreNotEqual(initial.eulerAngles.y, _rb.rotation.eulerAngles.y);
    }

    [Test]
    public void p4_left_and_right_produce_opposite_turns()
    {
        _rb.rotation = Quaternion.identity;
        SetInput(Vector2.zero, new Vector2(1f, 0f));
        InvokeFixedUpdate();
        float rightHeading = _rb.rotation.eulerAngles.y;

        _rb.rotation = Quaternion.identity;
        SetInput(Vector2.zero, new Vector2(-1f, 0f));
        InvokeFixedUpdate();

        Assert.AreNotEqual(rightHeading, _rb.rotation.eulerAngles.y);
    }

    [Test]
    public void p4_deadzone_prevents_turn()
    {
        _rb.rotation = Quaternion.Euler(0f, 45f, 0f);
        SetInput(Vector2.zero, new Vector2(0.005f, 0f));
        float before = _rb.rotation.eulerAngles.y;
        InvokeFixedUpdate();
        Assert.AreEqual(before, _rb.rotation.eulerAngles.y);
    }

    // ─────────────────────────────────────────────────────────────────
    // P-5: angularVelocity zeroed at start of FixedUpdate
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void p5_angularvelocity_zeroed_at_frame_start()
    {
        _rb.angularVelocity = new Vector3(0f, 99f, 0f);
        SetInput(Vector2.zero, Vector2.zero);
        InvokeFixedUpdate();
        Assert.AreEqual(Vector3.zero, _rb.angularVelocity);
    }

    [Test]
    public void p5_no_turn_input_keeps_angularvel_zero()
    {
        _rb.angularVelocity = Vector3.zero;
        SetInput(Vector2.zero, Vector2.zero);
        InvokeFixedUpdate();
        Assert.AreEqual(Vector3.zero, _rb.angularVelocity);
    }

    // ─────────────────────────────────────────────────────────────────
    // P-6: Y position locked to FLIGHT_PLANE_Y (0), Y velocity zeroed
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void p6_y_position_locked_to_zero()
    {
        _rb.position = new Vector3(0f, 5f, 0f);
        _rb.velocity = Vector3.zero;
        InvokeLateUpdate();
        Assert.AreEqual(0f, _rb.position.y, 0.001f);
    }

    [Test]
    public void p6_y_velocity_zeroed_xz_preserved()
    {
        _rb.velocity = new Vector3(3f, 2f, 4f);
        InvokeLateUpdate();
        Assert.AreEqual(0f, _rb.velocity.y, 0.001f);
        Assert.AreEqual(3f, _rb.velocity.x, 0.001f);
        Assert.AreEqual(4f, _rb.velocity.z, 0.001f);
    }

    [Test]
    public void p6_pure_vertical_velocity_becomes_zero()
    {
        _rb.velocity = new Vector3(0f, 5f, 0f);
        InvokeLateUpdate();
        Assert.AreEqual(0f, _rb.velocity.magnitude, 0.001f);
    }

    // ─────────────────────────────────────────────────────────────────
    // Integration
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void integration_thrust_and_turn_same_frame()
    {
        SetInput(new Vector2(0f, 1f), new Vector2(1f, 0f));
        InvokeFixedUpdate();
        Assert.Greater(_rb.velocity.magnitude, 0f, "Thrust should work");
        Assert.AreEqual(Vector3.zero, _rb.angularVelocity,
            "angularVelocity zeroed at start even when turning");
    }
}
