# ADR-0016: Colony & Building System Architecture

## Status
Accepted

## Date
2026-04-15

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Core / Economy |
| **Knowledge Risk** | LOW — ColonyManager 用协程驱动 tick（`WaitForSecondsRealtime(1f)`）；BuildingSystem 是纯数据结构 + 查询方法；均无 post-cutoff Unity API |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `docs/engine-reference/unity/modules/physics.md`（BuildingSystem 不涉及物理） |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | (1) ColonyManager tick 在低帧率（30fps）和暂停（SimRate=0）时行为正确；(2) 建造扣费原子性：扣费成功但 BuildingInstance 创建失败时资源回滚；(3) ShipyardTier 在节点沦陷后正确重置为 0 |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0004（Data Model — StarMapData.Node, GameDataManager）；ADR-0002（事件通信 — OnResourcesUpdated SO Channel）；ResourceConfig SO |
| **Enables** | Core Epic — 殖民地产出和建筑建造是经济系统的核心；Story 实施 |
| **Blocks** | Core Epic — 殖民地系统是经济基础，FleetDispatch / ShipBuilding / ResourceUI 均依赖此 ADR |
| **Ordering Note** | ColonySystem 的 Tick 逻辑依赖 SimClock（ADR-0012）；建议本 ADR 在 ADR-0012 之后或同时 Proposed |

## Context

### Problem Statement
ColonySystem 需要在 StarMapScene 的 Update() 中以 1 秒为周期驱动资源产出 tick（矿石+能源），调用 BuildingSystem.GetNodeProductionDelta() 累加各节点产出。BuildingSystem 需要管理建筑实例（BuildingInstance）、节点 ShipyardTier，以及响应建造请求（RequestBuild）。两个系统紧耦合：建造改变产出，产出支撑建造。

### Constraints
- **Tick 帧率独立**：`WaitForSecondsRealtime(1f)` 协程，不依赖 `Time.timeScale` 和帧率
- **SimRate 支持**：SimClock.SimRate 控制 tick 速率（0=暂停，1=正常，5=5x，20=20x）
- **原子性建造**：扣费 + 创建 BuildingInstance 必须原子（失败时回滚）
- **ShipyardTier 独占**：ShipyardTier 属于节点，不属于 BuildingInstance（节点沦陷时一起转移）
- **ShipBuilding 依赖**：BuildingSystem 不直接创建飞船，由 ColonyManager.BuildShip 调用 ShipDataModel 工厂

### Requirements
- ColonyManager tick（1 秒周期）→ 调用 BuildingSystem.GetNodeProductionDelta() → 更新 ore/energy 存量
- 存量 clamp：ore ∈ [0, ORE_CAP]；energy 无上限
- OnResourcesUpdated(ResourceSnapshot) — 每 tick 广播
- BuildingSystem.RequestBuild(nodeId, BuildingType) → BuildResult.Success/Fail
- ShipyardTier 维护：建造 Shipyard → Tier=1；升级 → Tier=2（就地升级，不新增实例）
- ShipBuilding：ColonyManager.BuildShip(nodeId) → 调用 ShipDataModel 工厂（通过 ShipBlueprintRegistry）
- CanAfford 预检：DeductResources 之前调用 ResourceConfig.CanAfford

## Decision

### 系统架构

```
ColonyManager（StarMapScene MonoBehaviour）
├── Tick 协程（WaitForSecondsRealtime(1f)，受 SimClock.SimRate 控制）
├── Ore/Energy 存量管理（_oreCurrent, _energyCurrent）
├── DeductResources(ore, energy) — 原子扣费
├── BuildShip(nodeId) → ShipDataModel
├── RefreshProductionCache() — 建造完成后刷新产出缓存
└── 广播：OnResourcesUpdated(ResourceSnapshot)

BuildingSystem（MasterScene 单例，数据驱动）
├── BuildingInstance Registry（Dictionary<string, BuildingInstance>）
├── GetNodeProductionDelta(nodeId) → { orePerSec, energyPerSec }
├── RequestBuild(nodeId, BuildingType) → BuildResult
├── ShipyardTier[nodeId]（Dictionary<string, int>）
└── 订阅：StarMapData.OnNodeOwnershipChanged（节点沦陷 → 清空建筑 + Tier）

ResourceConfig（SO Asset）
└── GetBuildCost(type) / GetProductionRate(type) / GetStorageCap(type)
```

### ColonyManager Tick 执行序列（C-2）

```csharp
// ColonyManager.cs
private float _tickAccumulator = 0f;

void Update() {
    if (SimClock.Instance == null) return;
    float simDelta = SimClock.Instance.DeltaTime;  // SimRate=0 时为 0
    if (simDelta <= 0f) return;

    _tickAccumulator += simDelta;
    while (_tickAccumulator >= 1f) {
        _tickAccumulator -= 1f;
        ExecuteTick();
    }
}

void ExecuteTick() {
    // T-1：快照所有 PLAYER 节点
    var playerNodes = StarMapData.GetAllNodesByOwner(Player);

    // T-2：累加各节点产出
    int totalOreProduction = 0;
    int totalEnergyProduction = 0;
    foreach (var node in playerNodes) {
        var delta = BuildingSystem.Instance.GetNodeProductionDelta(node.Id);
        totalOreProduction += delta.orePerSec;
        totalEnergyProduction += delta.energyPerSec;
    }

    // T-3：更新存量
    _oreCurrent = Mathf.Clamp(_oreCurrent + totalOreProduction, 0, ORE_CAP);
    _energyCurrent += totalEnergyProduction;  // energy 无上限

    // T-4：广播
    _onResourcesUpdatedChannel.Raise(new ResourceSnapshot {
        Ore = _oreCurrent,
        Energy = _energyCurrent,
        NetOreRate = totalOreProduction,
        NetEnergyRate = totalEnergyProduction,
    });
}
```

### 建造请求（BuildingSystem.RequestBuild）

```csharp
public enum BuildResult {
    Success,
    InsufficientResources,
    InvalidNode,
    ShipyardTierTooLow,
    Unknown
}

public BuildResult RequestBuild(string nodeId, BuildingType type) {
    // 1. 资源充足检查
    var (oreCost, energyCost) = ResourceConfig.Instance.GetBuildCost(type);
    if (!ColonyManager.Instance.CanAfford(oreCost, energyCost)) {
        return BuildResult.InsufficientResources;
    }

    // 2. 节点有效性检查
    var node = StarMapData.GetNode(nodeId);
    if (node == null || node.Ownership != Player) {
        return BuildResult.InvalidNode;
    }

    // 3. ShipyardTier 前提条件检查
    if (type == BuildingType.ShipyardUpgrade && GetShipyardTier(nodeId) < 1) {
        return BuildResult.ShipyardTierTooLow;
    }

    // 4. 原子扣费（ColonyManager 扣费）
    if (!ColonyManager.Instance.DeductResources(oreCost, energyCost)) {
        return BuildResult.InsufficientResources;  // 扣费失败
    }

    // 5. 创建 BuildingInstance
    try {
        var instance = new BuildingInstance {
            InstanceId = $"building_{Guid.NewGuid():N}",
            NodeId = nodeId,
            Type = type,
            IsActive = true,
        };
        AddBuilding(instance);

        // 6. 更新 ShipyardTier（如适用）
        if (type == BuildingType.Shipyard) {
            SetShipyardTier(nodeId, 1);
        } else if (type == BuildingType.ShipyardUpgrade) {
            SetShipyardTier(nodeId, GetShipyardTier(nodeId) + 1);
        }

        // 7. 刷新产出缓存
        ColonyManager.Instance.RefreshProductionCache();

        return BuildResult.Success;
    } catch {
        // EC-BUILD-01：实例创建失败 → 资源已扣，需回滚
        ColonyManager.Instance.RestoreResources(oreCost, energyCost);
        return BuildResult.Unknown;
    }
}
```

### ShipyardTier 管理

```csharp
// BuildingSystem.cs
private readonly Dictionary<string, int> _shipyardTier = new Dictionary<string, int>();

public int GetShipyardTier(string nodeId) {
    return _shipyardTier.TryGetValue(nodeId, out var tier) ? tier : 0;
}

public void SetShipyardTier(string nodeId, int tier) {
    _shipyardTier[nodeId] = tier;
}

// 节点沦陷时清空
public void OnNodeCaptured(string nodeId, Faction newOwner) {
    if (newOwner != Player) {
        ClearBuildings(nodeId);       // 移除所有 BuildingInstance
        _shipyardTier[nodeId] = 0;   // ShipyardTier 归零
    }
}
```

### GetNodeProductionDelta

```csharp
// BuildingSystem.cs
public ProductionDelta GetNodeProductionDelta(string nodeId) {
    if (!_productionCache.TryGetValue(nodeId, out var cached)) {
        return ProductionDelta.Zero;
    }
    return cached;
}

public void RefreshProductionCache() {
    _productionCache.Clear();
    foreach (var node in StarMapData.GetAllPlayerNodes()) {
        int oreOut = 0;
        int energyOut = 0;
        foreach (var b in GetBuildings(node.Id)) {
            if (!b.IsActive) continue;
            var rate = ResourceConfig.Instance.GetProductionRate(b.Type);
            oreOut += rate.orePerSec;
            energyOut += rate.energyPerSec;
        }
        _productionCache[node.Id] = new ProductionDelta(oreOut, energyOut);
    }
}
```

### 飞船建造（ColonyManager.BuildShip）

```csharp
// ColonyManager.cs
public ShipDataModel BuildShip(string nodeId, string blueprintId) {
    // 1. 验证节点有 Shipyard
    int tier = BuildingSystem.Instance.GetShipyardTier(nodeId);
    var blueprint = ShipBlueprintRegistry.Instance.GetBlueprint(blueprintId);
    if (blueprint == null || tier < blueprint.RequiredShipyardTier) {
        Debug.LogWarning($"[ColonyManager] Cannot build {blueprintId} at node {nodeId}: tier {tier} < required {blueprint?.RequiredShipyardTier}");
        return null;
    }

    // 2. 扣费
    var (oreCost, energyCost) = ResourceConfig.Instance.GetBuildCost(blueprintId);
    if (!DeductResources(oreCost, energyCost)) {
        return null;
    }

    // 3. 创建 ShipDataModel（通过 GameDataManager）
    var ship = GameDataManager.Instance.CreateShip(
        blueprintId,
        isPlayerControlled: false,
        dockedNodeId: nodeId);

    // 4. 广播建造完成
    _colonyShipChannel.Raise((ship.InstanceId, nodeId));

    return ship;
}
```

### 关键接口

| 接口 | 调用方 | 提供方 | 说明 |
|------|--------|--------|------|
| `ColonyManager.DeductResources(ore, energy)` | BuildingSystem, BuildShip | ColonyManager | 原子扣费 |
| `ColonyManager.CanAfford(ore, energy)` | BuildingSystem | ResourceConfig | 预检查 |
| `ColonyManager.BuildShip(nodeId, blueprintId)` | UI 层 | ColonyManager | 创建飞船实例 |
| `BuildingSystem.RequestBuild(nodeId, type)` | UI 层 | BuildingSystem | 建筑建造入口 |
| `BuildingSystem.GetNodeProductionDelta(nodeId)` | ColonyManager | BuildingSystem | 每 tick 查询产出 |
| `BuildingSystem.GetShipyardTier(nodeId)` | BuildShip | BuildingSystem | 建造前提条件 |
| `OnResourcesUpdatedChannel` | ColonyManager | ColonyManager | 每 tick 广播资源快照 |

### OnResourcesUpdated SO Channel

```csharp
// ResourceSnapshot.cs
public struct ResourceSnapshot {
    public int Ore;
    public int Energy;
    public int NetOreRate;      // 本 tick 净产出
    public int NetEnergyRate;
}

// ColonyShipChannel — 建造完成事件
public class ColonyShipChannel : GameEvent<(string ShipInstanceId, string NodeId)> {
    // Raise((instanceId, nodeId))
}
```

## Alternatives Considered

### Alternative 1: BuildingSystem 持有 tick 驱动，自己管理所有建筑产出
- **Description**: BuildingSystem 直接在 Update() 中驱动 1 秒 tick，不依赖 ColonyManager
- **Pros**: 建筑系统自洽，不依赖外部 tick 触发
- **Cons**: 两个独立的 tick 系统（Building + Colony）→ 需要协调 SimRate；建筑 tick 和资源累加必须同步
- **Rejection Reason**: GDD colony-system.md C-2 明确「ColonyManager 是 tick 驱动方」；建筑产出是 ColonyManager tick 的输入，不是独立 tick

### Alternative 2: ColonyManager 持有全部 BuildingInstance，不独立 BuildingSystem
- **Description**: ColonyManager 直接管理 BuildingInstance 列表，建造逻辑全内聚
- **Pros**: 简单，单一系统
- **Cons**: 违反单一职责；ShipyardTier 和建造请求与 ColonyManager 的资源管理职责混合
- **Rejection Reason**: BuildingInstance 和 ShipyardTier 是 StarMapData 的跨节点状态，独立 BuildingSystem 便于职责分离和扩展

## Consequences

### Positive
- 清晰的 Tick 驱动：ColonyManager 是 tick 唯一驱动方，受 SimRate 控制
- 建造原子性：扣费 + 创建在同一事务中，失败可回滚
- ShipyardTier 节点级别管理：沦陷时一起清理，不残留
- SimRate=0 正确暂停：Update 中的 while 循环当 simDelta=0 时不执行

### Negative
- BuildingSystem 需要订阅 StarMapData.OnNodeOwnershipChanged 来响应节点沦陷（跨系统数据流）
- RefreshProductionCache 在每次建造后调用，O(building_count) 遍历；MVP 规模可接受

### Risks
- **风险 1**：低帧率（<1fps）时 `_tickAccumulator` 无限累积 → 一次 tick 产出过多
  - 缓解：while 循环会逐 tick 追完，但存量 clamp 到 ORE_CAP；大规模累积由 clamp 兜底
- **风险 2**：ShipyardTier 和 BuildingInstance 分离管理 → 节点沦陷时必须同步清理两者
  - 缓解：OnNodeCaptured() 同时清空 BuildingInstance 和 ShipyardTier
- **风险 3**：SimRate 快速切换（0→20）时 _tickAccumulator 跳跃
  - 缓解：simDelta = SimClock.Instance.DeltaTime 控制；20x 时单帧最多累积 20 ticks

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| colony-system.md §C-2 | Tick 执行序列：T-1 快照节点→T-2 累加产出→T-3 更新存量→T-4 广播 | ExecuteTick() 完全实现 T-1~T-4 |
| colony-system.md §C-2 | 协程 `WaitForSecondsRealtime(1f)` | Update() + _tickAccumulator 替代协程，避免 MonoBehaviour 生命周期问题 |
| colony-system.md §C-2 | SimClock.DeltaTime 控制 tick | ColonyManager.Update() 使用 SimClock.Instance.DeltaTime |
| colony-system.md §C-3 | 建造流程：扣费→创建实例→刷新产出缓存 | RequestBuild() 和 BuildShip() 完全实现 |
| building-system.md §B-2 | 建造流程：ResourceConfig.GetBuildCost() → DeductResources → BuildingInstance | RequestBuild() 实现 B-2 全部步骤 |
| building-system.md §B-2 | ShipyardTier 独占：Shipyard → 1，Upgrade → +1 | SetShipyardTier() 实现 B-2 ShipyardTier 规则 |
| building-system.md §EC-BUILD-01 | 扣费成功但创建失败 → 资源回滚 | try/catch + RestoreResources() 实现 |
| building-system.md §is_valid_build_request | ShipyardTier 前提检查 | RequestBuild() 内检查 |
| TR-resource-003 | ore ∈ [0, ORE_CAP]，energy 无上限 | tick_ore_update 用 Mathf.Clamp；energy 无 clamp |

## Performance Implications

| 项目 | 影响 | 缓解 |
|------|------|------|
| **CPU** | 每 tick 遍历所有 PLAYER 节点 × 所有活跃 Building | MVP 规模小；建筑数量有限 |
| **CPU** | RefreshProductionCache 每建造后 O(building_count) 遍历 | MVP 可接受；后续可优化为增量更新 |
| **Memory** | BuildingInstance Registry：Dictionary<string, BuildingInstance> | MVP 规模可接受 |
| **Load Time** | BuildingSystem 和 ColonyManager 在 StarMapScene 加载时激活 | StarMapScene 本身已常驻 |

## Migration Plan

本 ADR 依赖已实现的 Foundation 层：
- SimClock（ADR-0012）：已实现，Tick 用 SimClock.DeltaTime
- StarMapData（ADR-0004）：已实现，Tick 读取节点
- ResourceConfig SO：已有资产
- GameDataManager（ADR-0004）：CreateShip 方法已有（空实现）

实施顺序：
1. 补全 ColonyManager.BuildShip() → 调用 GameDataManager.CreateShip
2. 补全 ColonyManager.DeductResources() / RestoreResources()
3. 实现 BuildingSystem（BuildingInstance Registry + RequestBuild）
4. 实现 BuildingSystem.GetNodeProductionDelta()
5. 实现 ShipyardTier 管理
6. 实现 OnResourcesUpdatedChannel 广播

## Validation Criteria

| 验证条件 | 验证方法 |
|----------|----------|
| Tick 1 秒周期：10 次 tick 后 _tickAccumulator 追平 | 单元测试（mock SimClock.DeltaTime） |
| SimRate=0 时 Tick 不执行 | 集成测试 |
| SimRate=5 时 Tick 加速 5 倍 | 集成测试 |
| ore 存量 clamp 到 [0, ORE_CAP] | 单元测试 |
| energy 存量无上限 | 单元测试 |
| RequestBuild 扣费成功但创建失败 → 资源回滚 | 单元测试（mock 创建失败） |
| ShipyardTier=0 时无法建造 generic_v1（RequiredShipyardTier=1） | 单元测试 |
| BuildShip 成功 → ColonyShipChannel.Raise() 广播 | 集成测试 |

## Related Decisions

- [ADR-0004: Data Model Architecture](adr-0004-data-model-architecture.md) — StarMapData.Node、GameDataManager 持有关系
- [ADR-0002: Event Communication Architecture](adr-0002-event-communication-architecture.md) — OnResourcesUpdated SO Channel Tier 1 规范
- [ADR-0012: SimClock Architecture](adr-0012-simclock-architecture.md) — DeltaTime 来源
