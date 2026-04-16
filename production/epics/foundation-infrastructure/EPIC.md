# Epic: Foundation Infrastructure

> **Layer**: Foundation
> **Architecture Modules**: 数据模型框架 · 事件总线 · SimClock
> **GDD**: `design/gdd/ship-system.md` · `design/gdd/star-map-system.md` · `design/gdd/dual-perspective-switching.md`
> **Governing ADRs**: ADR-0002 · ADR-0004 · ADR-0012
> **Control Manifest Version**: 2026-04-14
> **Status**: Ready
> **Stories**: 10 stories created — see table below

## Stories

| # | Story | Type | Status | ADR | Test Evidence |
|---|-------|------|--------|-----|---------------|
| 001 | ResourceConfig SO | Logic | Ready | ADR-0004 | `tests/unit/resource/resource_config_test.cs` |
| 002 | StarMapData Graph Structure | Logic | Ready | ADR-0004 | `tests/unit/starmap/starmap_data_test.cs` |
| 003 | StarMapPathfinder BFS | Logic | Ready | ADR-0004 | `tests/unit/starmap/pathfinder_test.cs` |
| 004 | ShipBlueprint + Registry | Logic | Ready | ADR-0004 | `tests/unit/ship/blueprint_registry_test.cs` |
| 005 | ShipDataModel Runtime Authority | Integration | Ready | ADR-0004 | `tests/integration/ship/ship_data_model_test.cs` |
| 006 | SO Channel Architecture | Integration | Ready | ADR-0002 | `tests/integration/event/channel_architecture_test.cs` |
| 007 | ViewLayerChannel + ShipStateChannel | Integration | Ready | ADR-0002/ADR-0004 | `tests/integration/event/viewlayer_shipstate_channel_test.cs` |
| 008 | SimClock Core DeltaTime | Logic | Ready | ADR-0012 | `tests/unit/simclock/simclock_delta_time_test.cs` |
| 009 | SimClock Save/Load Archive | Integration | Ready | ADR-0012 | `tests/integration/simclock/simclock_save_load_test.cs` |
| 010 | ColonySystem Production Tick | Integration | Ready | ADR-0012 | `tests/integration/colony/colony_production_tick_test.cs` |

---

## Overview

本 Epic 实现游戏的基础设旇数据架构，不依赖任何其他层。所有 Feature/Core/Presentation 层系统都依赖此处定义的数据模型、事件总线和模拟时钟。

**三个模块各自独立，可并行实现：**

- **数据模型框架**：只读 Config SO（Inspector 配置）+ 运行时状态 C# 类（MasterScene 持有）
- **事件总线**：ScriptableObject Channel 跨场景通信规范 + C# event Action<T> 同场景规范
- **SimClock**：策略层时间单例（`DeltaTime = Time.unscaledDeltaTime × SimRate`），与驾驶舱物理解耦

---

## Module A: 数据模型框架

### Owns
- `GameDataManager` 单例（MasterScene）
- `ShipDataModel`：飞船运行时状态（Hull、位置、ShipState）
- `ShipBlueprint` / `ShipBlueprintRegistry`：飞船静态配置
- `StarMapData`：星图图结构 G=(V,E)
- `ResourceConfig`：资源产出/上限配置
- `BuildingRegistry`、`DispatchOrderRegistry`

### Exposes
- `GameDataManager.Instance` — 全局数据读写入口
- `ShipDataModel.Find(shipId)` — 查飞船实例
- `StarMapData.GetNode(nodeId)` — 查节点
- `ResourceConfig.CanAfford(cost)` — 纯函数

### Key Constraints
- Config SO 位于 `assets/data/config/`，运行时只读
- 运行时状态为纯 C# 类，不允许存 ScriptableObject
- BFS 寻路：`StarMapPathfinder.FindPath()` — O(V+E)，确定性

### Governing ADR
- **ADR-0004**（数据模型架构）— 数据双层分离、GameDataManager 单例

---

## Module B: 事件总线

### Owns
- `assets/data/channels/` 下的所有 SO Channel 资产

### Key Channels

| Channel 名 | 用途 | 广播者 | 订阅者 |
|-----------|------|--------|--------|
| `ViewLayerChannel` | 视角切换广播 | ViewLayerManager | StarMapUI、ShipHUD、ShipInputManager |
| `ShipStateChannel` | 飞船状态变化 | ShipDataModel | StarMapUI、ShipHUD |
| `CombatChannel` | 战斗事件 | CombatSystem | StarMapSystem、ShipHUD |
| `ColonyShipChannel` | 舰船建造完成 | ColonySystem | ShipSystem |
| `SimRateChangedChannel` | 时间倍率变化 | SimClock | SimRateDisplay UI |

### Key Constraints
- **订阅对**：所有订阅必须在 `OnEnable()` / `OnDisable()` 中配对，禁止在 Awake/Start 中订阅
- **异步规范**：跨 `await` 边界的 UniTask 方法必须传递 `this.destroyCancellationToken`
- **Tier 分类**：
  - Tier 1（跨场景）→ SO Channel
  - Tier 2（同场景异系统）→ C# event Action<T>
  - Tier 3（同内）→ 直接方法调用

### Governing ADR
- **ADR-0002**（事件通信架构）— 三层分级、订阅对规范

---

## Module C: SimClock

### Owns
- `SimClock` MonoBehaviour 单例（MasterScene，`DontDestroyOnLoad`）

### Interface
```csharp
public class SimClock : MonoBehaviour
{
    public static SimClock Instance { get; private set; }
    public float SimRate { get; private set; }  // {0, 1, 5, 20}
    public float DeltaTime => Time.unscaledDeltaTime * SimRate;  // 策略层用此
    public void SetRate(float rate);  // 校验值域，广播 SimRateChangedChannel
}
```

### Key Constraints
- `Time.timeScale` **永远不修改**（保持 1）
- 策略层（StarMapScene、ColonySystem、FleetDispatch）用 `SimClock.Instance.DeltaTime`
- 驾驶舱物理（FixedUpdate、Rigidbody.AddForce）用 `Time.deltaTime`
- Script Execution Order = -1000（最早初始化）
- SimRate 存档：写入 `ShipDataModel.SaveData`

### Governing ADR
- **ADR-0012**（SimClock 架构）— Time.timeScale 禁止修改、DeltaTime 公式

---

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-resource-001 | 资源配置数据结构（ORE/ENERGY 类型、上限） | ADR-0004 ✅ |
| TR-resource-002 | 资源 CanAfford 纯函数 | ADR-0004 ✅ |
| TR-resource-003 | 资源计算逻辑（产出/消耗） | ADR-0004 ✅ |
| TR-starmap-001 | 星图图结构 G=(V,E)，邻接表 | ADR-0004 ✅ |
| TR-starmap-002 | 节点可见性 IsVisible(nodeId) 逻辑 | ADR-0004 ✅ |
| TR-ship-001 | ShipBlueprint SO（MaxHull/ThrustPower/TurnSpeed） | ADR-0004 ✅ |
| TR-ship-004 | ShipDataModel 权威数据（Hull/State/位置） | ADR-0004 ✅ |
| TR-ship-005 | ShipBlueprintRegistry 单例查询 | ADR-0004 ✅ |
| TR-event-001 | 跨场景事件必须用 SO Channel | ADR-0002 ✅（框架级） |
| TR-event-002 | 订阅 OnEnable/OnDisable 配对 | ADR-0002 ✅（框架级） |
| TR-event-003 | 异步方法传 destroyCancellationToken | ADR-0002 ✅（框架级） |
| TR-dvs-009 | SimClock.DeltaTime = Time.unscaledDeltaTime × SimRate | ADR-0012 ✅ |

> 注：TR-event-001~003 为框架级需求，在 `tr-registry.yaml` 中未正式登记，ADR-0002 已覆盖。

---

## Definition of Done

本 Epic 完成的条件：
- 所有 Story 实现、审查、通过 `/story-done` 关闭
- `design/gdd/ship-system.md` 的所有 AC 验证通过
- `design/gdd/star-map-system.md` 的所有 AC 验证通过
- `design/gdd/dual-perspective-switching.md` 中 SimClock 相关 AC（AC-DVS-21/22）验证通过
- Logic 类 Story 有通过测试文件（`tests/unit/`）
- Integration 类 Story 有通过测试或文档化 playtest（`tests/integration/`）

---

## Next Step

Run `/create-stories foundation-infrastructure` to break this epic into implementable stories.

---

## Epic B 依赖说明

Epic B（Foundation Runtime）依赖本 Epic：
- Scene Management 需要 SO Channel 广播 ViewLayerChanged
- Input System 需要 ShipInputChannel 读取当前帧输入
- SimClock 为 Scene Management 提供时间基准
