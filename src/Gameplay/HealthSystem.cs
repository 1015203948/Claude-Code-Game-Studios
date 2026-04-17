using UnityEngine;

/// <summary>
/// 飞船生命值系统 — 驾驶舱战斗层核心仲裁者。
/// 管理所有飞船的 Hull 追踪，接收伤害输入，在 Hull 归零时触发死亡序列。
/// 挂载于 MasterScene（单例），跨场景可用。
/// </summary>
public class HealthSystem : MonoBehaviour
{
    public static HealthSystem Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────────
    // Events（Tier 2 C# events — 零 GC）
    // ─────────────────────────────────────────────────────────────────

    /// <summary>Hull 变化时广播（newHull > 0 时）。</summary>
    public event System.Action<string, float, float> OnHullChanged;

    /// <summary>Hull 归零时触发（H-5 Step 1）。</summary>
    public event System.Action<string> OnShipDying;

    /// <summary>仅玩家飞船 Hull=0 时触发（H-5 Step 3）。</summary>
    public event System.Action<string> OnPlayerShipDestroyed;

    /// <summary>通用销毁完成广播（H-5 Step 4）。</summary>
    public event System.Action<string> OnShipDestroyed;

    // ─────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // ─────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// 应用伤害到指定飞船。
    /// DOCKED / IN_TRANSIT：静默忽略，返回 false。
    /// DESTROYED：记录警告日志，返回 false。
    /// newHull == 0：触发死亡序列（H-5）。
    /// </summary>
    /// <returns>true if damage was applied; false if rejected.</returns>
    public bool ApplyDamage(string instanceId, float rawDamage, DamageType damageType)
    {
        // 负值保护
        if (rawDamage < 0f) {
            Debug.LogWarning($"[HealthSystem] ApplyDamage: rawDamage must be >= 0. Clamping to 0.");
            rawDamage = 0f;
        }

        var ship = GameDataManager.Instance.GetShip(instanceId);
        if (ship == null) {
            Debug.LogWarning($"[HealthSystem] ApplyDamage: ship {instanceId} not found.");
            return false;
        }

        ShipState state = ship.State;
        if (state == ShipState.DESTROYED) {
            Debug.LogWarning($"[HealthSystem] {instanceId}: DESTROYED — ApplyDamage rejected.");
            return false;
        }
        if (state != ShipState.IN_COCKPIT && state != ShipState.IN_COMBAT) {
            // DOCKED / IN_TRANSIT — 静默忽略
            return false;
        }

        float maxHull = ship.MaxHull;
        float newHull = Mathf.Clamp(ship.CurrentHull - rawDamage, 0f, maxHull);
        ship.CurrentHull = newHull;

        if (newHull <= 0f) {
            ExecuteDeathSequence(instanceId);
        } else {
            OnHullChanged?.Invoke(instanceId, newHull, maxHull);
        }
        return true;
    }

    /// <summary>
    /// Hull 比率 = CurrentHull / MaxHull，范围 [0, 1]。
    /// </summary>
    public float GetHullRatio(string instanceId)
    {
        var ship = GameDataManager.Instance.GetShip(instanceId);
        if (ship == null) return 0f;
        return Mathf.Clamp01(ship.CurrentHull / ship.MaxHull);
    }

    // ─────────────────────────────────────────────────────────────────
    // Death Sequence（H-5）— Story 002 填充完整实现
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// 死亡序列（H-5）：同帧按序执行。
    /// Step 1 → OnShipDying → Step 2 → DestroyShip → Step 3 → [IsPlayerControlled?] → OnPlayerShipDestroyed → Step 4 → OnShipDestroyed。
    /// </summary>
    private void ExecuteDeathSequence(string instanceId)
    {
        // Step 1：广播 OnShipDying
        OnShipDying?.Invoke(instanceId);

        // Step 2：调用 DestroyShip
        var ship = GameDataManager.Instance.GetShip(instanceId);
        if (ship != null) {
            ship.Destroy();
        }

        // Step 3：如果是玩家飞船
        if (ship != null && ship.IsPlayerControlled) {
            OnPlayerShipDestroyed?.Invoke(instanceId);
        }

        // Step 4：广播 OnShipDestroyed
        OnShipDestroyed?.Invoke(instanceId);
    }
}
