# Story 010: ColonySystem Production Tick Integration

> **Epic**: foundation-infrastructure
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Manifest Version**: 2026-04-14
> **Estimate**: 2-3 hours

## Context

**GDD**: `design/gdd/colony-system.md`（生产 tick）；`design/gdd/resource-system.md`（ORE_CAP）
**Requirement**: `TR-colony-001`, `TR-colony-002`, `TR-colony-003`

**ADR Governing Implementation**: ADR-0012 — 策略层系统用 SimClock.DeltaTime；ADR-0002 — 跨 await 传 destroyCancellationToken
**ADR Decision Summary**: ColonySystem 的 tick 循环用 `SimClock.Instance.DeltaTime`；每 tick 广播 `OnResourcesUpdated`；ORE accumulation clamp 到 [0, ORE_CAP]。

**Engine**: Unity 6.3 LTS | **Risk**: LOW

**Control Manifest Rules (Foundation)**:
- Required: 策略层系统必须用 `SimClock.Instance.DeltaTime`
- Required: 所有跨 await 的 async UniTask 必须传 `this.destroyCancellationToken`

---

## Acceptance Criteria

*From `design/gdd/colony-system.md`:*

- [ ] ColonyManager 在 StarMapScene 的 `Update()` 中驱动 tick 循环
- [ ] tick 间隔：`SimClock.Instance.DeltaTime` 累加到 ≥ 1.0s 时触发一次产出计算
- [ ] 每 tick 广播 `OnResourcesUpdated(ResourceSnapshot)`（ore、energy 当前值）
- [ ] ore 产出 clamp 到 [0, ORE_CAP]；energy 无上限
- [ ] ColonyShipChannel 广播建造完成事件（ShipInstanceId + NodeId）
- [ ] OnResourcesUpdatedChannel 位于 `Assets/Data/Channels/OnResourcesUpdatedChannel.asset`

---

## Implementation Notes

*From ADR-0012 + GDD colony-system.md:*

1. **Tick 循环**：
   ```csharp
   private float _accumulator;

   private void Update() {
       _accumulator += SimClock.Instance.DeltaTime;  // 用 SimClock！
       if (_accumulator >= 1.0f) {
           _accumulator -= 1.0f;
           Tick();
       }
   }

   private void Tick() {
       int oreGain = CalculateOreOutput();
       _oreCurrent = Mathf.Clamp(_oreCurrent + oreGain, 0, ORE_CAP);
       _onResourcesUpdatedChannel.Raise(new ResourceSnapshot(_oreCurrent, _energyCurrent));
   }
   ```

2. **禁止直接用 Time.deltaTime**：在 ColonySystem 的 Update 中直接用 `Time.deltaTime` 是 BLOCKING 违规（违反 ADR-0012）

3. **WaitForSecondsRealtime 替代**：GDD 提到用 `WaitForSecondsRealtime(1f)`，但完整实现应基于 SimClock.DeltaTime accumulator（如上），因为 WaitForSecondsRealtime 无法与 SimRate 联动

4. **ORE_CAP clamp**：`Mathf.Clamp(value, 0, ORE_CAP)`

5. **ResourceSnapshot**：
   ```csharp
   [Serializable]
   public readonly struct ResourceSnapshot {
       public readonly int Ore;
       public readonly int Energy;
       public ResourceSnapshot(int ore, int energy) { Ore = ore; Energy = energy; }
   }
   ```

---

## Out of Scope

- UI 层订阅 OnResourcesUpdated 更新 HUD 显示（Presentation 层）
- BuildingRegistry 接入 ColonySystem 产出计算（Core 层）

---

## QA Test Cases

- **AC-1: DeltaTime 驱动 tick**
  - Given: SimClock.SimRate = 1，_accumulator = 0
  - When: 经过 1.0 秒真实时间
  - Then: Tick() 被调用 1 次

- **AC-2: SimRate = 5 时 tick 加速**
  - Given: SimClock.SimRate = 5，_accumulator = 0
  - When: 经过 1.0 秒真实时间
  - Then: Tick() 被调用 5 次（每次 SimRate=5 → 1 秒内积累 5 秒等效时间）

- **AC-3: SimRate = 0 时 tick 暂停**
  - Given: SimClock.SimRate = 0，_accumulator = 0
  - When: 经过 10 秒真实时间
  - Then: Tick() 不被调用（accumulator 不增长）

- **AC-4: ORE_CAP clamp**
  - Given: ORE_CAP = 100，oreCurrent = 95，tick 产出 +10
  - When: Tick()
  - Then: oreCurrent = 100（不是 105）

- **AC-5: OnResourcesUpdated 广播**
  - Given: OnResourcesUpdatedChannel 已订阅
  - When: Tick()
  - Then: 订阅者收到 ResourceSnapshot(Ore=当前值, Energy=当前值)

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/colony/colony_production_tick_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001（ORE_CAP 配置）；Story 008（SimClock 核心）
- Unlocks: Core 层 ColonySystem 完整实现（建筑产出计算、Energy 消耗等）
