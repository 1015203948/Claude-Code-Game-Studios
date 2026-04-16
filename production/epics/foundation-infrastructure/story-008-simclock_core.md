# Story 008: SimClock Core — DeltaTime Formula & SetRate

> **Epic**: foundation-infrastructure
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: 2026-04-14
> **Estimate**: 2 hours

## Context

**GDD**: `design/gdd/dual-perspective-switching.md`（公式 D-DVS-4）
**Requirement**: `TR-dvs-009`

**ADR Governing Implementation**: ADR-0012 — SimClock Architecture
**ADR Decision Summary**: `SimClock.DeltaTime = Time.unscaledDeltaTime × SimRate`；`Time.timeScale` 永远不修改；SimRate ∈ {0, 1, 5, 20}；SetRate() 校验值域并广播 SimRateChangedChannel。

**Engine**: Unity 6.3 LTS | **Risk**: LOW

**Control Manifest Rules (Foundation)**:
- Required: `Time.timeScale` 永远不修改（保持 1）
- Required: SimClock Script Execution Order = -1000（最早初始化）
- Required: 策略层系统用 `SimClock.Instance.DeltaTime`

---

## Acceptance Criteria

*From `design/gdd/dual-perspective-switching.md` + ADR-0012:*

- [ ] SimRate 初始值为 1
- [ ] `SetRate(0)`：`SimRate = 0`，DeltaTime = 0；策略层Update仍执行但DeltaTime=0
- [ ] `SetRate(1)`：`SimRate = 1`，DeltaTime = Time.unscaledDeltaTime × 1
- [ ] `SetRate(5)`：`SimRate = 5`，DeltaTime = Time.unscaledDeltaTime × 5
- [ ] `SetRate(20)`：`SimRate = 20`，DeltaTime = Time.unscaledDeltaTime × 20
- [ ] `SetRate(非法值)`（如 2、3、-1）：Assert 失败或静默忽略（不修改 SimRate）
- [ ] `SetRate(任意合法值)`：广播 `SimRateChangedChannel`
- [ ] SimRateChangedChannel 位于 `Assets/Data/Channels/SimRateChangedChannel.asset`

---

## Implementation Notes

*From ADR-0012:*

1. **SimClock 单例**：
   ```csharp
   public class SimClock : MonoBehaviour {
       public static SimClock Instance { get; private set; }
       public float SimRate { get; private set; } = 1f;
       public float DeltaTime => Time.unscaledDeltaTime * SimRate;
       public float SimRateChanged;  // SO Channel

       private void Awake() {
           Instance = this;
           DontDestroyOnLoad(gameObject);
       }

       public void SetRate(float rate) {
           Debug.Assert(IsValidRate(rate), $"Invalid SimRate: {rate}");
           if (!IsValidRate(rate)) return;
           SimRate = rate;
           _simRateChangedChannel.Raise(rate);
       }

       private static bool IsValidRate(float r) =>
           r == 0f || r == 1f || r == 5f || r == 20f;
   }
   ```

2. **Script Execution Order**：在 Unity Editor 中设置 SimClock GameObject 的 Script Execution Order = -1000

3. **禁止 Time.timeScale**：整个项目中禁止任何代码修改 `Time.timeScale`（用 Grep 检查项目中无此引用）

4. **DeltaTime 是 property 而非 field**：每次读取计算，无缓存

---

## Out of Scope

- 策略层系统（ColonySystem、FleetDispatch）使用 SimClock.DeltaTime（Core 层）
- SimRate 存档/读档（Story 009）

---

## QA Test Cases

- **AC-1: DeltaTime 公式正确**
  - Given: SimClock 实例，SimRate = 5
  - When: `DeltaTime` property 被读取
  - Then: 返回 `Time.unscaledDeltaTime * 5`（误差 < 0.001f）

- **AC-2: SetRate 广播 SimRateChanged**
  - Given: SimRateChangedChannel 已订阅
  - When: `SetRate(5)`
  - Then: 订阅者收到 `5f`

- **AC-3: 非法 SimRate 拒绝**
  - Given: SimRate = 1
  - When: `SetRate(2)`
  - Then: SimRate 保持 1；不广播

- **AC-4: SimRate = 0 时 DeltaTime = 0**
  - Given: SimClock，SimRate = 0
  - When: `DeltaTime`
  - Then: 返回 `0f`

- **AC-5: 初始 SimRate = 1**
  - Given: 新 SimClock 实例
  - When: 实例化后不调用 SetRate
  - Then: SimRate == 1f

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/simclock/simclock_delta_time_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 006（Channel 资产创建）
- Unlocks: Story 010（ColonySystem Production Tick 使用 SimClock.DeltaTime）
