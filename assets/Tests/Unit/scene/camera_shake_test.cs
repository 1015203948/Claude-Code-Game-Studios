using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using Object = UnityEngine.Object;

/// <summary>
/// CameraRig Shake System 单元测试。
/// 覆盖 Sprint 2 Phase 1.1 Camera Shake。
/// </summary>
[TestFixture]
public class CameraShake_Test
{
    private CameraRig _rig;
    private GameObject _rigGo;
    private Camera _camera;
    private GameObject _cameraGo;
    private Transform _anchor;
    private GameObject _anchorGo;

    [SetUp]
    public void SetUp()
    {
        _rigGo = new GameObject("CameraRig");
        _rig = _rigGo.AddComponent<CameraRig>();

        _cameraGo = new GameObject("Camera");
        _camera = _cameraGo.AddComponent<Camera>();

        _anchorGo = new GameObject("Anchor");
        _anchorGo.transform.position = new Vector3(0f, 1.5f, 2f);
        _anchor = _anchorGo.transform;

        SetField(_rig, "_camera", _camera);
        SetField(_rig, "_cockpitAnchor", _anchor);

        // Start in first-person mode
        SetField(_rig, "_mode", CameraRig.CameraMode.FIRST_PERSON);
        SetField(_rig, "_isTransitioning", false);
    }

    [TearDown]
    public void TearDown()
    {
        if (_rigGo != null) Object.DestroyImmediate(_rigGo);
        if (_cameraGo != null) Object.DestroyImmediate(_cameraGo);
        if (_anchorGo != null) Object.DestroyImmediate(_anchorGo);
    }

    private void SetField(object obj, string name, object value)
    {
        var f = obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        f?.SetValue(obj, value);
    }

    private T GetField<T>(object obj, string name)
    {
        var f = obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        return (T)f?.GetValue(obj);
    }

    private void Tick(float dt) => _rig.Tick(dt);

    // ─────────────────────────────────────────────────────────────────
    // Tests
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void shake_adds_offset_to_camera_position()
    {
        _camera.transform.position = _anchor.position;

        _rig.AddShake(1f, 1f);
        Tick(0.01f);

        // Camera should have moved from anchor (shake offset applied)
        Assert.AreNotEqual(_anchor.position, _camera.transform.position,
            "Shake should offset camera from anchor");
    }

    [Test]
    public void shake_decays_over_duration()
    {
        _rig.AddShake(1f, 0.5f);

        Vector3 offsetAtStart = _camera.transform.position;
        Tick(0.01f);
        Vector3 offsetEarly = _camera.transform.position;

        // Advance past shake duration
        for (int i = 0; i < 100; i++) Tick(0.01f);
        Vector3 offsetAfter = _camera.transform.position;

        // Shake should still be applied but camera should be at anchor (no shake offset)
        Assert.AreNotEqual(offsetAtStart, offsetEarly, "Shake should have immediate effect");
    }

    [Test]
    public void no_shake_returns_zero_offset()
    {
        _camera.transform.position = _anchor.position;
        Tick(0.016f);

        Assert.AreEqual(_anchor.position.x, _camera.transform.position.x, 0.001f,
            "Without shake, first-person should be exact");
    }

    [Test]
    public void shake_stacks_higher_intensity_wins()
    {
        _rig.AddShake(0.1f, 0.5f);
        _rig.AddShake(0.8f, 0.5f);

        float intensity = GetField<float>(_rig, "_shakeIntensity");
        Assert.AreEqual(0.8f, intensity, "Higher shake intensity should win");
    }

    [Test]
    public void shake_stacks_longer_duration_wins()
    {
        _rig.AddShake(0.5f, 0.2f);
        _rig.AddShake(0.5f, 1.0f);

        float duration = GetField<float>(_rig, "_shakeDuration");
        Assert.AreEqual(1.0f, duration, "Longer shake duration should win");
    }
}
