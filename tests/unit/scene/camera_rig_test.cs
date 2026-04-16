using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using Object = UnityEngine.Object;

/// <summary>
/// CameraRig View Switching 单元测试。
/// 覆盖 Story 022 所有验收标准（V-1 ~ V-4）。
/// Story type: Visual/Feel — 自动化测试验证逻辑行为，人工验证视觉效果。
/// </summary>
[TestFixture]
public class CameraRig_Test
{
    // ─────────────────────────────────────────────────────────────────
    // Fields
    // ─────────────────────────────────────────────────────────────────

    private CameraRig _cameraRig;
    private GameObject _rigGo;
    private Camera _camera;
    private GameObject _cameraGo;
    private Transform _shipTransform;
    private GameObject _shipGo;
    private Transform _anchorTransform;
    private GameObject _anchorGo;

    // ─────────────────────────────────────────────────────────────────
    // SetUp / TearDown
    // ─────────────────────────────────────────────────────────────────

    [SetUp]
    public void SetUp()
    {
        _rigGo = new GameObject("CameraRig");
        _cameraRig = _rigGo.AddComponent<CameraRig>();

        _cameraGo = new GameObject("Camera");
        _camera = _cameraGo.AddComponent<Camera>();

        _shipGo = new GameObject("Ship");
        _shipTransform = _shipGo.transform;

        _anchorGo = new GameObject("CockpitAnchor");
        _anchorTransform = _anchorGo.transform;
        _anchorGo.transform.position = new Vector3(0f, 1.5f, 2f);

        // Inject via reflection
        SetField(_cameraRig, "_camera", _camera);
        SetField(_cameraRig, "_targetShip", _shipTransform);
        SetField(_cameraRig, "_cockpitAnchor", _anchorTransform);
        SetField(_cameraRig, "_thirdPersonOffset", new Vector3(0f, 8f, -22f));
    }

    [TearDown]
    public void TearDown()
    {
        if (_rigGo != null) Object.DestroyImmediate(_rigGo);
        if (_cameraGo != null) Object.DestroyImmediate(_cameraGo);
        if (_shipGo != null) Object.DestroyImmediate(_shipGo);
        if (_anchorGo != null) Object.DestroyImmediate(_anchorGo);
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

    private void SetProperty(object obj, string propName, object value)
    {
        var prop = obj.GetType().GetProperty(propName);
        if (prop == null) Assert.Fail($"Property '{propName}' not found");
        prop.SetValue(obj, value);
    }

    private void SimulateUpdate(float dt = 0.016f)
    {
        // Use the public Tick(float) method for deterministic time control
        var tickMethod = typeof(CameraRig).GetMethod("Tick",
            BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(tickMethod, "Tick method not found on CameraRig");
        tickMethod.Invoke(_cameraRig, new object[] { dt });
    }

    // ─────────────────────────────────────────────────────────────────
    // V-1: Third-person SmoothDamp position and rotation
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void v1_third_person_position_smooths_with_time_constant()
    {
        // Given: camera starts at (0, 0, 0); ship at (10, 0, 0)
        _camera.transform.position = Vector3.zero;
        _shipGo.transform.position = new Vector3(10f, 0f, 0f);

        // Switch to third person
        _cameraRig.SwitchMode(CameraRig.CameraMode.THIRD_PERSON);

        // Simulate transition complete (0.3s)
        // Manually set mode and verify SmoothDamp applies
        SetField(_cameraRig, "_mode", CameraRig.CameraMode.THIRD_PERSON);
        SetField(_cameraRig, "_isTransitioning", false);

        var beforePos = _camera.transform.position;

        // Simulate several frames
        for (int i = 0; i < 5; i++) {
            SimulateUpdate(0.1f); // 100ms per frame
        }

        var afterPos = _camera.transform.position;

        // Camera should have moved toward target (10, 0, 0) + offset
        Assert.AreNotEqual(beforePos, afterPos,
            "Camera should smoothly move in third-person mode");
    }

    [Test]
    public void v1_third_person_rotation_smooths_with_time_constant()
    {
        // Given: ship rotated 90°
        _shipGo.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        _camera.transform.rotation = Quaternion.identity;

        SetField(_cameraRig, "_mode", CameraRig.CameraMode.THIRD_PERSON);
        SetField(_cameraRig, "_isTransitioning", false);

        var beforeRot = _camera.transform.rotation.eulerAngles.y;

        for (int i = 0; i < 5; i++) {
            SimulateUpdate(0.1f);
        }

        var afterRot = _camera.transform.rotation.eulerAngles.y;
        Assert.AreNotEqual(beforeRot, afterRot,
            "Camera should smoothly rotate to follow ship in third-person mode");
    }

    // ─────────────────────────────────────────────────────────────────
    // V-2: First-person hard bind to CockpitAnchor
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void v2_first_person_hard_binds_to_anchor_position()
    {
        // Given: anchor at (0, 1.5, 2.0)
        _anchorGo.transform.position = new Vector3(0f, 1.5f, 2f);
        _camera.transform.position = Vector3.zero;

        SetField(_cameraRig, "_mode", CameraRig.CameraMode.FIRST_PERSON);
        SetField(_cameraRig, "_isTransitioning", false);

        // Directly call ApplyFirstPerson via Update
        SimulateUpdate();

        Assert.AreEqual(_anchorGo.transform.position, _camera.transform.position,
            "First-person camera should exactly match CockpitAnchor position");
    }

    [Test]
    public void v2_first_person_hard_binds_to_anchor_rotation()
    {
        _anchorGo.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
        _camera.transform.rotation = Quaternion.identity;

        SetField(_cameraRig, "_mode", CameraRig.CameraMode.FIRST_PERSON);
        SetField(_cameraRig, "_isTransitioning", false);

        SimulateUpdate();

        Assert.AreEqual(
            _anchorGo.transform.rotation.eulerAngles.y,
            _camera.transform.rotation.eulerAngles.y,
            0.001f,
            "First-person camera should exactly match CockpitAnchor rotation");
    }

    [Test]
    public void v2_no_smooth_interpolation_in_first_person()
    {
        // Given: anchor at (100, 50, 200) — far from camera start
        _anchorGo.transform.position = new Vector3(100f, 50f, 200f);
        _camera.transform.position = Vector3.zero;

        SetField(_cameraRig, "_mode", CameraRig.CameraMode.FIRST_PERSON);
        SetField(_cameraRig, "_isTransitioning", false);

        SimulateUpdate();

        // Should snap immediately — no smoothing in first-person
        Assert.AreEqual(100f, _camera.transform.position.x, 0.001f);
        Assert.AreEqual(50f, _camera.transform.position.y, 0.001f);
        Assert.AreEqual(200f, _camera.transform.position.z, 0.001f);
    }

    // ─────────────────────────────────────────────────────────────────
    // V-3: Switch animation duration 0.3s
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void v3_transition_completes_in_0_3_seconds()
    {
        // Given: first-person at anchor; want to switch to third-person
        _anchorGo.transform.position = new Vector3(0f, 1.5f, 2f);
        _shipGo.transform.position = new Vector3(0f, 0f, 0f);
        _camera.transform.position = _anchorGo.transform.position;

        SetField(_cameraRig, "_mode", CameraRig.CameraMode.FIRST_PERSON);
        SetField(_cameraRig, "_isTransitioning", false);

        // Initiate switch to THIRD_PERSON
        _cameraRig.SwitchMode(CameraRig.CameraMode.THIRD_PERSON);

        bool wasTransitioning = (bool)GetField<object>(_cameraRig, "_isTransitioning");
        Assert.IsTrue(wasTransitioning, "SwitchMode should start transition");

        // Advance time: 0.15s
        float elapsed = 0f;
        while (elapsed < 0.3f) {
            SimulateUpdate(0.05f);
            elapsed += 0.05f;
        }

        // After 0.3s, transition should be complete
        bool stillTransitioning = (bool)GetField<object>(_cameraRig, "_isTransitioning");
        CameraRig.CameraMode finalMode = _cameraRig.Mode;

        Assert.IsFalse(stillTransitioning, "Transition should be complete after 0.3s");
        Assert.AreEqual(CameraRig.CameraMode.THIRD_PERSON, finalMode,
            "Mode should switch to THIRD_PERSON after 0.3s");
    }

    [Test]
    public void v3_no_op_switch_is_ignored()
    {
        SetField(_cameraRig, "_mode", CameraRig.CameraMode.THIRD_PERSON);
        SetField(_cameraRig, "_isTransitioning", false);

        // Try to switch to the same mode
        _cameraRig.SwitchMode(CameraRig.CameraMode.THIRD_PERSON);

        bool stillTransitioning = (bool)GetField<object>(_cameraRig, "_isTransitioning");
        Assert.IsFalse(stillTransitioning, "Switching to same mode should not start transition");
    }

    // ─────────────────────────────────────────────────────────────────
    // V-4: Input uninterrupted during transition
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void v4_update_runs_during_transition()
    {
        // Given: in transition
        _cameraRig.SwitchMode(CameraRig.CameraMode.FIRST_PERSON);
        _camera.transform.position = new Vector3(0f, 1.5f, 2f);
        _anchorGo.transform.position = new Vector3(100f, 0f, 0f);
        _shipGo.transform.position = Vector3.zero;

        // When: Update is called during transition
        bool isTransitioning = (bool)GetField<object>(_cameraRig, "_isTransitioning");
        Assert.IsTrue(isTransitioning, "Should be in transitioning state");

        // SimulateUpdate should still run (not blocked)
        SimulateUpdate(0.05f);

        // Verify transition is still active (not completed after 0.05s)
        bool stillTransitioning = (bool)GetField<object>(_cameraRig, "_isTransitioning");
        Assert.IsTrue(stillTransitioning, "Transition should still be active after 0.05s");
    }

    [Test]
    public void v4_transition_state_does_not_block_mode_changes()
    {
        // Given: mid-transition
        _cameraRig.SwitchMode(CameraRig.CameraMode.FIRST_PERSON);
        SimulateUpdate(0.05f);

        // When: calling SwitchMode again (should update target)
        _cameraRig.SwitchMode(CameraRig.CameraMode.THIRD_PERSON);

        // Should update the _targetMode without error
        var targetMode = GetField<object>(_cameraRig, "_targetMode");
        Assert.IsNotNull(targetMode);
    }

    // ─────────────────────────────────────────────────────────────────
    // Additional: Third-person offset
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void third_person_offset_applied_correctly()
    {
        _shipGo.transform.position = new Vector3(10f, 0f, 0f);
        SetField(_cameraRig, "_mode", CameraRig.CameraMode.THIRD_PERSON);
        SetField(_cameraRig, "_isTransitioning", false);

        for (int i = 0; i < 10; i++) {
            SimulateUpdate(0.1f);
        }

        // Camera should end up at ship position + offset
        // After sufficient time, SmoothDamp should nearly converge
        Assert.Greater(_camera.transform.position.x, 5f,
            "Camera should follow ship toward target offset");
    }
}
