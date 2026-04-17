using UnityEngine;
using UnityEngine.UI;
using Game.Channels;
using Game.Scene;
using Game.Data;
using Game.Gameplay;

namespace Game.UI {
    /// <summary>
    /// Ship HUD — 驾驶舱态势感知显示层。
    ///
    /// 订阅事件（ADR-0002 Tier 2 C# events，OnEnable/OnDisable 配对）：
    /// - HealthSystem.OnHullChanged → 血条
    /// - ShipControlSystem.OnAimAngleChanged / OnLockLost → 瞄准角、锁定状态
    /// - CombatChannel → 战斗状态变化
    ///
    /// 每帧轮询（显示层需要即时更新）：
    /// - ShipControlSystem.GetHorizontalSpeed() → 速度表
    /// - CombatSystem.FireCooldownProgress → 武器冷却
    ///
    /// 实现 TR-hud-001~TR-hud-003。
    /// </summary>
    public class ShipHUD : MonoBehaviour
    {
        // ─── UI Element Bindings ───────────────────────────────────────
        [Header("Hull Bar")]
        [SerializeField] private Image _hullBarFill;
        [SerializeField] private Text _hullBarLabel;

        [Header("Speed Indicator")]
        [SerializeField] private Text _speedLabel;

        [Header("Weapon Cooldown")]
        [SerializeField] private Image _cooldownFill;
        [SerializeField] private Text _cooldownLabel;

        [Header("Combat Indicator")]
        [SerializeField] private Text _combatIndicatorText;
        [SerializeField] private CanvasGroup _combatIndicatorCanvasGroup;

        [Header("Soft-Lock Reticle")]
        [SerializeField] private RectTransform _reticleRect;
        [SerializeField] private Camera _hudCamera;
        [SerializeField] private float _reticleSize = 40f;

        // ─── Channel References ─────────────────────────────────────────
        [Header("Channel References")]
        [SerializeField] private ViewLayerChannel _viewLayerChannel;
        [SerializeField] private ShipStateChannel _shipStateChannel;

        // ─── State ─────────────────────────────────────────────────────
        private string _activeShipId;
        private Transform _softLockTarget;
        private float _combatIndicatorTimer;
        private const float COMBAT_INDICATOR_DURATION = 2f; // seconds

        // ─── Constants ─────────────────────────────────────────────────
        private const float LOW_HULL_THRESHOLD = 0.25f;
        private static readonly Color HULL_NORMAL_COLOR = new Color(0.2f, 0.9f, 0.4f);
        private static readonly Color HULL_WARNING_COLOR = new Color(1f, 0.6f, 0.1f);
        private static readonly Color HULL_CRITICAL_COLOR = new Color(1f, 0.15f, 0.15f);
        private static readonly Color COOLDOWN_READY_COLOR = new Color(0f, 1f, 0.67f); // #00FFAA
        private static readonly Color COOLDOWN_CHARGING_COLOR = new Color(0.5f, 0.5f, 0.5f);

        // ─── Unity Lifecycle ───────────────────────────────────────────

        private void OnEnable()
        {
            _viewLayerChannel.Subscribe(OnViewLayerChanged);
            _shipStateChannel.Subscribe(OnShipStateChanged);

            if (HealthSystem.Instance != null) {
                HealthSystem.Instance.OnHullChanged += OnHullChanged;
            }
            if (ShipControlSystem.Instance != null) {
                ShipControlSystem.Instance.OnAimAngleChanged += OnAimAngleChanged;
                ShipControlSystem.Instance.OnLockLost += OnLockLost;
                ShipControlSystem.Instance.FireRequested += OnFireRequested;
            }
            if (CombatChannel.Instance != null) {
                CombatChannel.Instance.Subscribe(OnCombatStateChanged);
            }
        }

        private void OnDisable()
        {
            _viewLayerChannel.Unsubscribe(OnViewLayerChanged);
            _shipStateChannel.Unsubscribe(OnShipStateChanged);

            if (HealthSystem.Instance != null) {
                HealthSystem.Instance.OnHullChanged -= OnHullChanged;
            }
            if (ShipControlSystem.Instance != null) {
                ShipControlSystem.Instance.OnAimAngleChanged -= OnAimAngleChanged;
                ShipControlSystem.Instance.OnLockLost -= OnLockLost;
                ShipControlSystem.Instance.FireRequested -= OnFireRequested;
            }
            if (CombatChannel.Instance != null) {
                CombatChannel.Instance.Unsubscribe(OnCombatStateChanged);
            }
        }

        private void Start()
        {
            // Initialize UI to hidden until first data arrives
            if (_combatIndicatorCanvasGroup != null) {
                _combatIndicatorCanvasGroup.alpha = 0f;
            }
        }

        private void Update()
        {
            UpdateSpeedDisplay();
            UpdateCooldownDisplay();
            UpdateReticlePosition();
            UpdateCombatIndicatorFade();
        }

        // ─── Event Handlers ────────────────────────────────────────────

        private void OnViewLayerChanged(ViewLayer newLayer)
        {
            // STARMAP: hide cockpit HUD
            // COCKPIT: show cockpit HUD
            // COCKPIT_WITH_OVERLAY: show HUD + overlay
            bool visible = newLayer == ViewLayer.COCKPIT || newLayer == ViewLayer.COCKPIT_WITH_OVERLAY;
            gameObject.SetActive(visible);
        }

        private void OnShipStateChanged((string instanceId, ShipState newState) payload)
        {
            _activeShipId = payload.instanceId;

            if (payload.newState == ShipState.IN_COCKPIT || payload.newState == ShipState.IN_COMBAT) {
                RefreshHullDisplay();
            }
        }

        private void OnHullChanged(string instanceId, float currentHull, float maxHull)
        {
            if (!string.IsNullOrEmpty(_activeShipId) && instanceId != _activeShipId) return;

            float ratio = maxHull > 0f ? Mathf.Clamp01(currentHull / maxHull) : 0f;

            if (_hullBarFill != null) {
                _hullBarFill.fillAmount = ratio;
                _hullBarFill.color = GetHullColor(ratio);
            }

            if (_hullBarLabel != null) {
                _hullBarLabel.text = $"{Mathf.CeilToInt(currentHull)} / {Mathf.CeilToInt(maxHull)}";
            }
        }

        private void OnAimAngleChanged(float angleDegrees)
        {
            // Aim angle is broadcast for HUD debug display; not rendered directly
        }

        private void OnLockLost()
        {
            _softLockTarget = null;
            if (_reticleRect != null) {
                _reticleRect.gameObject.SetActive(false);
            }
        }

        private void OnFireRequested()
        {
            // Refresh cooldown display immediately when weapon fires
            if (_cooldownFill != null) {
                _cooldownFill.fillAmount = 0f;
            }
        }

        private void OnCombatStateChanged(CombatChannel.CombatPayload payload)
        {
            switch (payload.Result) {
                case CombatChannel.CombatResult.Begin:
                    ShowCombatIndicator("COMBAT IN");
                    break;
                case CombatChannel.CombatResult.Victory:
                    ShowCombatIndicator("VICTORY");
                    break;
                case CombatChannel.CombatResult.Defeat:
                    ShowCombatIndicator("DEFEAT");
                    break;
            }
        }

        // ─── Per-Frame Updates ─────────────────────────────────────────

        private void UpdateSpeedDisplay()
        {
            if (_speedLabel == null) return;

            float speed = ShipControlSystem.Instance != null
                ? ShipControlSystem.Instance.GetHorizontalSpeed()
                : 0f;

            _speedLabel.text = $"{speed:F1} m/s";
        }

        private void UpdateCooldownDisplay()
        {
            if (_cooldownFill == null) return;

            float progress = CombatSystem.Instance != null
                ? CombatSystem.Instance.FireCooldownProgress
                : 1f;

            _cooldownFill.fillAmount = progress;

            // Color transitions: gray → green as cooldown completes
            _cooldownFill.color = progress >= 1f
                ? COOLDOWN_READY_COLOR
                : COOLDOWN_CHARGING_COLOR;

            if (_cooldownLabel != null) {
                _cooldownLabel.text = progress >= 1f ? "READY" : $"{progress * 100:F0}%";
            }
        }

        private void UpdateReticlePosition()
        {
            if (_reticleRect == null || _hudCamera == null) return;

            // Acquire soft-lock target from ShipControlSystem
            if (ShipControlSystem.Instance != null) {
                _softLockTarget = ShipControlSystem.Instance.SoftLockTarget;
            }

            if (_softLockTarget == null) {
                _reticleRect.gameObject.SetActive(false);
                return;
            }

            // Project world position to screen space
            Vector3 screenPos = _hudCamera.WorldToScreenPoint(_softLockTarget.position);
            bool inFront = screenPos.z > 0f;

            if (!inFront) {
                _reticleRect.gameObject.SetActive(false);
                return;
            }

            // Convert screen [0,1] to viewport RectTransform anchors
            screenPos.z = 0; // Zero z for UI position
            _reticleRect.position = screenPos;
            _reticleRect.sizeDelta = Vector2.one * _reticleSize;
            _reticleRect.gameObject.SetActive(true);
        }

        private void UpdateCombatIndicatorFade()
        {
            if (_combatIndicatorCanvasGroup == null) return;
            if (_combatIndicatorTimer <= 0f) return;

            _combatIndicatorTimer -= Time.deltaTime;
            float alpha = Mathf.Clamp01(_combatIndicatorTimer / COMBAT_INDICATOR_DURATION);
            _combatIndicatorCanvasGroup.alpha = alpha;
        }

        // ─── Helpers ──────────────────────────────────────────────────

        private void RefreshHullDisplay()
        {
            if (HealthSystem.Instance == null || string.IsNullOrEmpty(_activeShipId)) return;
            float ratio = HealthSystem.Instance.GetHullRatio(_activeShipId);
            if (_hullBarFill != null) {
                _hullBarFill.fillAmount = ratio;
                _hullBarFill.color = GetHullColor(ratio);
            }
        }

        private void ShowCombatIndicator(string text)
        {
            if (_combatIndicatorText != null) {
                _combatIndicatorText.text = text;
            }
            if (_combatIndicatorCanvasGroup != null) {
                _combatIndicatorCanvasGroup.alpha = 1f;
            }
            _combatIndicatorTimer = COMBAT_INDICATOR_DURATION;
        }

        private Color GetHullColor(float ratio)
        {
            if (ratio <= LOW_HULL_THRESHOLD) return HULL_CRITICAL_COLOR;
            if (ratio <= 0.5f) return HULL_WARNING_COLOR;
            return HULL_NORMAL_COLOR;
        }
    }
}
