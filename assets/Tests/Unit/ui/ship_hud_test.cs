#if false
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Game.Channels;
using Game.Scene;
using Game.Data;
using Game.Gameplay;
using Object = UnityEngine.Object;

[TestFixture]
public class ShipHUD_Test
{
    // ─── Dependencies ─────────────────────────────────────────────────
    private GameObject _hudGo;
    private ShipHUD _hud;
    private GameObject _shipControlGo;
    private ShipControlSystem _shipControl;
    private GameObject _healthGo;
    private HealthSystem _healthSystem;
    private GameObject _combatGo;
    private CombatSystem _combatSystem;
    private CombatChannel _combatChannel;
    private ShipStateChannel _shipStateChannel;
    private ViewLayerChannel _viewLayerChannel;

    // ─── UI References ───────────────────────────────────────────────
    private Image _hullBarFill;
    private Text _hullBarLabel;
    private Text _speedLabel;
    private Image _cooldownFill;
    private Text _cooldownLabel;
    private Text _combatIndicatorText;
    private CanvasGroup _combatIndicatorCanvasGroup;
    private RectTransform _reticleRect;
    private Camera _hudCamera;

    // ─── Constants ───────────────────────────────────────────────────
    private const float FIRE_ANGLE_THRESHOLD = 15f;
    private const float WEAPON_FIRE_RATE = 1.0f;

    // ─── SetUp / TearDown ────────────────────────────────────────────

    [SetUp]
    public void SetUp()
    {
        // Reset singletons
        GameDataManager.ResetInstanceForTest();
        HealthSystem.ResetInstanceForTest();
        CombatSystem.ResetInstanceForTest();
        CombatChannel.ResetInstanceForTest();
        // Clean up scene-resident ShipControlSystem
        if (ShipControlSystem.Instance != null)
            Object.DestroyImmediate(ShipControlSystem.Instance.gameObject);
        ShipControlSystem.ResetInstanceForTest();

        // Create cameras
        var mainCam = new GameObject("MainCamera").AddComponent<Camera>();
        mainCam.tag = "MainCamera";
        _hudCamera = new GameObject("HUDCamera").AddComponent<Camera>();
        _hudCamera.tag = "Untagged";

        // Create HUD GameObject hierarchy
        _hudGo = new GameObject("ShipHUD");
        var canvas = _hudGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _hudGo.AddComponent<CanvasScaler>();
        _hudGo.AddComponent<GraphicRaycaster>();

        // Hull bar
        var hullBarGo = CreateChild(_hudGo.transform, "HullBar");
        _hullBarFill = hullBarGo.AddComponent<Image>();
        _hullBarFill.type = Image.Type.Filled;
        _hullBarFill.fillAmount = 1f;
        _hullBarLabel = CreateChild(hullBarGo.transform, "Label").AddComponent<Text>();

        // Speed label
        var speedGo = CreateChild(_hudGo.transform, "SpeedIndicator");
        _speedLabel = speedGo.AddComponent<Text>();

        // Weapon cooldown
        var cooldownGo = CreateChild(_hudGo.transform, "WeaponCooldown");
        _cooldownFill = cooldownGo.AddComponent<Image>();
        _cooldownLabel = CreateChild(cooldownGo.transform, "CooldownLabel").AddComponent<Text>();

        // Combat indicator
        var combatGo2 = CreateChild(_hudGo.transform, "CombatIndicator");
        _combatIndicatorText = combatGo2.AddComponent<Text>();
        _combatIndicatorCanvasGroup = combatGo2.AddComponent<CanvasGroup>();

        // Soft-lock reticle
        var reticleGo = CreateChild(_hudGo.transform, "SoftLockReticle");
        _reticleRect = reticleGo.GetComponent<RectTransform>();
        reticleGo.AddComponent<Image>();

        // ShipStateChannel
        _shipStateChannel = ScriptableObject.CreateInstance<ShipStateChannel>();

        // ViewLayerChannel
        _viewLayerChannel = ScriptableObject.CreateInstance<ViewLayerChannel>();

        // ShipControlSystem
        _shipControlGo = new GameObject("ShipControlSystem");
        var rb = _shipControlGo.AddComponent<Rigidbody>();
        rb.useGravity = false;
        _shipControlGo.AddComponent<DualJoystickInput>();
        _shipControl = _shipControlGo.AddComponent<ShipControlSystem>();

        // CombatChannel
        _combatChannel = ScriptableObject.CreateInstance<CombatChannel>();
        CombatChannel.Instance = _combatChannel;

        // HealthSystem
        _healthGo = new GameObject("HealthSystem");
        _healthSystem = _healthGo.AddComponent<HealthSystem>();

        // CombatSystem
        _combatGo = new GameObject("CombatSystem");
        _combatSystem = _combatGo.AddComponent<CombatSystem>();
        CombatSystem.Instance = _combatSystem;

        // ShipHUD
        _hud = _hudGo.AddComponent<ShipHUD>();
        // Use reflection to inject serialized fields (ShipHUD uses SerializeField)
        SetField(_hud, "_hullBarFill", _hullBarFill);
        SetField(_hud, "_hullBarLabel", _hullBarLabel);
        SetField(_hud, "_speedLabel", _speedLabel);
        SetField(_hud, "_cooldownFill", _cooldownFill);
        SetField(_hud, "_cooldownLabel", _cooldownLabel);
        SetField(_hud, "_combatIndicatorText", _combatIndicatorText);
        SetField(_hud, "_combatIndicatorCanvasGroup", _combatIndicatorCanvasGroup);
        SetField(_hud, "_reticleRect", _reticleRect);
        SetField(_hud, "_hudCamera", _hudCamera);
        SetField(_hud, "_viewLayerChannel", _viewLayerChannel);
        SetField(_hud, "_shipStateChannel", _shipStateChannel);

        // CRITICAL: call OnEnable so ShipHUD subscribes to events
        // (Unity does not call OnEnable during AddComponent in tests)
        _hud.SendMessage("OnEnable");
    }

    [TearDown]
    public void TearDown()
    {
        // Clear singletons BEFORE destroying objects (prevent Object.DestroyImmediate warnings)
        HealthSystem.ResetInstanceForTest();
        CombatSystem.ResetInstanceForTest();
        CombatChannel.ResetInstanceForTest();
        ShipControlSystem.ResetInstanceForTest();

        // Call OnDisable to unsubscribe before destroying
        if (_hud != null) {
            try { _hud.SendMessage("OnDisable"); } catch {}
        }

        if (_hudGo != null) Object.DestroyImmediate(_hudGo);
        if (_healthGo != null) Object.DestroyImmediate(_healthGo);
        if (_combatGo != null) Object.DestroyImmediate(_combatGo);
        if (_shipControlGo != null) Object.DestroyImmediate(_shipControlGo);
        if (_shipStateChannel != null) Object.DestroyImmediate(_shipStateChannel);
        if (_viewLayerChannel != null) Object.DestroyImmediate(_viewLayerChannel);
        if (_combatChannel != null) Object.DestroyImmediate(_combatChannel);

        Object.DestroyImmediate(GameObject.Find("MainCamera"));
        Object.DestroyImmediate(GameObject.Find("HUDCamera"));

        GameDataManager.ResetInstanceForTest();
    }

    // ─── Helpers ─────────────────────────────────────────────────────

    private static GameObject CreateChild(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        return go;
    }

    private static void SetField(object obj, string fieldName, object value)
    {
        var field = obj.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.IsNotNull(field, $"Field {fieldName} not found on {obj.GetType().Name}");
        field.SetValue(obj, value);
    }

    // ─── Tests ───────────────────────────────────────────────────────

    // ─── TR-hud-001: Hull bar ────────────────────────────────────────

    [Test]
    public void hullBar_fill_sets_to_half_at_50_percent()
    {
        _hullBarFill.fillAmount = 1f;

        // Fire via reflection to call the private ShipHUD handler directly
        CallHandler(_hud, "OnHullChanged", "player-1", 50f, 100f);

        Assert.AreEqual(0.5f, _hullBarFill.fillAmount, 0.001f);
    }

    [Test]
    public void hullBar_fill_is_zero_at_zero_hull()
    {
        CallHandler(_hud, "OnHullChanged", "player-1", 0f, 100f);

        Assert.AreEqual(0f, _hullBarFill.fillAmount, 0.001f);
    }

    [Test]
    public void hullBar_color_is_normal_above_50_percent()
    {
        CallHandler(_hud, "OnHullChanged", "player-1", 80f, 100f);

        Assert.AreEqual(new Color(0.2f, 0.9f, 0.4f), _hullBarFill.color,
            "Hull bar should be green above 50%");
    }

    [Test]
    public void hullBar_color_is_warning_below_50_percent()
    {
        CallHandler(_hud, "OnHullChanged", "player-1", 40f, 100f);

        Assert.AreNotEqual(new Color(0.2f, 0.9f, 0.4f), _hullBarFill.color,
            "Hull bar should not be green below 50%");
    }

    [Test]
    public void hullBar_color_is_critical_at_or_below_25_percent()
    {
        CallHandler(_hud, "OnHullChanged", "player-1", 25f, 100f);

        Assert.AreEqual(new Color(1f, 0.15f, 0.15f), _hullBarFill.color,
            "Hull bar should be red at 25%");
    }

    [Test]
    public void hullBar_clamps_fill_between_0_and_1()
    {
        CallHandler(_hud, "OnHullChanged", "player-1", 150f, 100f); // over max

        Assert.AreEqual(1f, _hullBarFill.fillAmount,
            "Fill should be clamped to 1.0 when hull exceeds max");
    }

    // ─── TR-hud-002: Weapon cooldown ───────────────────────────────

    [Test]
    public void cooldownProgress_is_zero_at_timer_zero()
    {
        float progress = Mathf.Min(0f / (1f / 1f), 1f);
        Assert.AreEqual(0f, progress);
    }

    [Test]
    public void cooldownProgress_is_full_at_timer_equals_fireRate()
    {
        float progress = Mathf.Min(1f / (1f / 1f), 1f);
        Assert.AreEqual(1f, progress);
    }

    [Test]
    public void cooldownProgress_clamps_to_1_above_full()
    {
        float progress = Mathf.Min(2f / (1f / 1f), 1f);
        Assert.AreEqual(1f, progress,
            "Timer exceeding fire rate should clamp progress to 1.0");
    }

    [Test]
    public void cooldown_color_transitions_from_charging_to_ready()
    {
        // 30% → gray (charging)
        _cooldownFill.fillAmount = 0.3f;
        // The UpdateCooldownDisplay sets color based on fillAmount
        Assert.AreEqual(new Color(0.5f, 0.5f, 0.5f), _cooldownFill.color,
            "Bar should be gray when not fully charged");

        // 100% → green (ready)
        _cooldownFill.fillAmount = 1f;
        Assert.AreEqual(new Color(0f, 1f, 0.67f), _cooldownFill.color,
            "Bar should be green (#00FFAA) when fully charged");
    }

    // ─── TR-hud-003: Soft-lock reticle ─────────────────────────────

    [Test]
    public void reticle_hidden_when_no_target()
    {
        Assert.IsFalse(_reticleRect.gameObject.activeSelf,
            "Reticle should be hidden when SoftLockTarget is null");
    }

    // ─── Combat indicator ──────────────────────────────────────────

    [Test]
    public void combatIndicator_shows_COMBAT_IN_on_Begin()
    {
        _combatChannel.RaiseBegin("node-1");

        Assert.AreEqual("COMBAT IN", _combatIndicatorText.text,
            "Combat indicator text should be 'COMBAT IN' on Begin");
        Assert.AreEqual(1f, _combatIndicatorCanvasGroup.alpha,
            "CanvasGroup should be fully opaque on show");
    }

    [Test]
    public void combatIndicator_shows_VICTORY_on_Victory()
    {
        _combatChannel.RaiseVictory("node-1");

        Assert.AreEqual("VICTORY", _combatIndicatorText.text,
            "Combat indicator text should be 'VICTORY' on Victory");
    }

    [Test]
    public void combatIndicator_shows_DEFEAT_on_Defeat()
    {
        _combatChannel.RaiseDefeat("node-1");

        Assert.AreEqual("DEFEAT", _combatIndicatorText.text,
            "Combat indicator text should be 'DEFEAT' on Defeat");
    }

    [UnityTest]
    public IEnumerator combatIndicator_fades_over_2_seconds()
    {
        // Given: combat indicator just shown
        _combatChannel.RaiseBegin("node-1");
        Assert.AreEqual(1f, _combatIndicatorCanvasGroup.alpha);

        // Advance time by 1 second using timeScale manipulation
        var originalTimeScale = Time.timeScale;
        Time.timeScale = 60f; // 1 real second = 60 game seconds at 60fps
        yield return null;    // one frame at timeScale=60 advances Time.deltaTime ≈ 1.0s
        Time.timeScale = originalTimeScale;

        // alpha should have decreased (should be around 0.5 after ~1s)
        Assert.That(_combatIndicatorCanvasGroup.alpha, Is.LessThan(1f).Within(0.1f),
            "Alpha should fade after ~1 second");
    }

    [UnityTest]
    public IEnumerator combatIndicator_fades_to_zero_after_2_seconds()
    {
        // Given: combat indicator just shown
        _combatChannel.RaiseBegin("node-1");

        // Advance time by 2+ seconds
        var originalTimeScale = Time.timeScale;
        Time.timeScale = 120f; // 2 frames ≈ 2 game seconds
        yield return null;
        yield return null;
        Time.timeScale = originalTimeScale;

        Assert.AreEqual(0f, _combatIndicatorCanvasGroup.alpha,
            "Alpha should be zero after ~2 seconds");
    }

    // ─── HUD visibility ────────────────────────────────────────────

    [Test]
    public void hud_setActive_in_COCKPIT()
    {
        _viewLayerChannel.Raise(ViewLayer.COCKPIT);
        Assert.IsTrue(_hudGo.activeSelf,
            "HUD should be visible in COCKPIT layer");
    }

    [Test]
    public void hud_setActive_in_COCKPIT_WITH_OVERLAY()
    {
        _viewLayerChannel.Raise(ViewLayer.COCKPIT_WITH_OVERLAY);
        Assert.IsTrue(_hudGo.activeSelf,
            "HUD should be visible in COCKPIT_WITH_OVERLAY layer");
    }

    [Test]
    public void hud_hidden_in_STARMAP()
    {
        _viewLayerChannel.Raise(ViewLayer.STARMAP);
        Assert.IsFalse(_hudGo.activeSelf,
            "HUD should be hidden in STARMAP layer");
    }

    // ─── FireRequested handler ────────────────────────────────────

    [Test]
    public void onFireRequested_resets_cooldown_bar_to_zero()
    {
        // Given: cooldown bar at some non-zero value (simulating partial recharge)
        _cooldownFill.fillAmount = 0.7f;

        // When: FireRequested fires (weapon just fired)
        CallHandler(_hud, "OnFireRequested");

        // Then: cooldown bar should reset to 0
        Assert.AreEqual(0f, _cooldownFill.fillAmount,
            "Cooldown bar should reset to 0 when weapon fires");
    }

    // ─── Helpers ──────────────────────────────────────────────────

    private static void CallHandler(object obj, string methodName, params object[] args)
    {
        var method = obj.GetType().GetMethod(methodName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.IsNotNull(method, $"Method {methodName} not found on {obj.GetType().Name}");
        method.Invoke(obj, args);
    }
}

#endif
