# ADR-0017: Fleet Dispatch System Architecture

## Status
Accepted

## Date
2026-04-15

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Core / Navigation |
| **Knowledge Risk** | LOW — FleetDispatchSystem 是纯数据追踪 + 时间积分，无 Unity 物理/渲染 API；Transit 进度用 float HopProgress 累加，不涉及场景加载 |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `docs/engine-reference/unity/modules/physics.md`（FleetDispatch 不涉及物理） |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | (1) DispatchOrder 在 CockpitScene 卸载后（玩家进入驾驶舱）继续推进；(2) ShipDataModel.Destroy() 在 IN_TRANSIT 时被调用 → DispatchOrder 即时失效；(3) FLEET_TRAVEL_TIME=3s/hop 精度：10 连续 hop 后时间误差 < 0.1s |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0004（Data Model — StarMapData.Pathfinding, ShipDataModel）；ADR-0013（CombatSystem — 战斗触发接口）；ADR-0014（HealthSystem — 无直接依赖，U-4 路径直接调用 ShipDataModel.Destroy） |
| **Enables** | Core Epic — 星图层舰队调令执行；Story 实施 |
| **Blocks** | Core Epic — FleetDispatch 是星图层的核心交互，ShipBuilding 和 ResourceUI 均依赖此系统 |
| **Ordering Note** | FleetDispatchSystem 依赖 StarMapPathfinder（BFS 已实现，ADR-0004 已 Accepted）；CombatSystem（ADR-0013）提供战斗触发接口；建议本 ADR 与 ADR-0013/0014 同时 Proposed |

## Context

### Problem Statement
FleetDispatchSystem 需要管理所有 IN_TRANSIT 飞船的逐跳移动：接收星图 UI 点击发起的派遣指令（DispatchOrder），使用 StarMapPathfinder 计算路径（已实现），每帧推进 HopProgress，在到达目的地时触发 ShipState 转换（IN_TRANSIT → DOCKED 或 IN_COMBAT）。同时需要处理无人值守战斗路径（U-4）：直接调用 ShipDataModel.Destroy()，绕过 HealthSystem。

### Constraints
- **Transit 继续运行**：玩家进入驾驶舱后（StarMapScene 仍然活跃），DispatchOrder 继续推进，不暂停
- **ShipState 单一权威**：FleetDispatchSystem 只负责推进 Transit，不自行改变 ShipState；调用 ShipSystem.SetState() 触发状态转换
- **路径快照**：派遣确认后路径锁定（LockedPath），不受后续星图变化影响
- **DESTROYED 清理**：ShipDataModel.Destroy() 被调用时（IN_TRANSIT 期间），对应 DispatchOrder 必须立即失效
- **无人值守战斗 U-4**：FleetDispatchSystem 在无人值守结算失败时直接调用 ShipDataModel.Destroy()，不经过 HealthSystem

### Requirements
- `DispatchOrder` 数据结构：ShipId、LockedPath（节点列表）、HopProgress、CurrentHopIndex
- `RequestDispatch(shipId, destinationNodeId)` → 创建 DispatchOrder，开始 Transit
- `CancelDispatch(shipId)` → 飞船返回原节点（反向路径），取消当前订单
- `Update()` 每帧推进所有活跃 DispatchOrder（HopProgress += SimClock.DeltaTime）
- 到达目的地（HopProgress ≥ FLEET_TRAVEL_TIME）→ 触发 ShipState 转换
- 节点为 ENEMY 类型 → 触发无人值守战斗（U-4 路径）或驾驶舱战斗

## Decision

### FleetDispatchSystem 架构

```
FleetDispatchSystem（MonoBehaviour 单例，StarMapScene）
├── DispatchOrder Registry（Dictionary<string, DispatchOrder>）
├── RequestDispatch(shipId, destinationNodeId) → 创建订单，开始 Transit
├── CancelDispatch(shipId) → 取消订单，飞船返回
├── Update() — 每帧推进所有活跃 DispatchOrder
└── 订阅：StarMapData.OnNodeOwnershipChanged（节点沦陷 → 取消相关订单）

ShipDataModel — ShipState 转换仅由以下系统触发：
  - FleetDispatchSystem（Transit 完成 → DOCKED 或 IN_COMBAT）
  - CombatSystem（驾驶舱战斗胜利/失败 → IN_COCKPIT 或 DESTROYED）
  - ViewLayerManager（退出驾驶舱 → DOCKED）
```

### DispatchOrder 数据结构

```csharp
public class DispatchOrder {
    public string OrderId;                    // "order_[uuid]"
    public string ShipId;                     // 关联飞船 InstanceId
    public string OriginNodeId;               // 派遣起点节点
    public string DestinationNodeId;          // 目标节点
    public List<string> LockedPath;          // 快照路径（节点ID列表）
    public int CurrentHopIndex;              // 当前所处 hop 索引（0 = 刚从 Origin 出发）
    public float HopProgress;                 // 当前 hop 累计时间（秒）
    public bool IsReturning;                  // 是否在返回途中（CancelDispatch 触发）
    public float Timestamp;                   // 创建时间（用于存档）
}
```

### RequestDispatch 流程

```csharp
public DispatchOrder RequestDispatch(string shipId, string destinationNodeId) {
    // 1. 验证飞船状态
    var ship = ShipDataModel.GetShip(shipId);
    if (ship == null || ship.State != ShipState.DOCKED) {
        Debug.LogWarning($"[FleetDispatch] {shipId} is not DOCKED — cannot dispatch.");
        return null;
    }

    // 2. 路径计算（StarMapPathfinder BFS）
    var path = StarMapPathfinder.FindPath(ship.DockedNodeId, destinationNodeId);
    if (path == null || path.Count < 1) {
        Debug.LogWarning($"[FleetDispatch] No path found from {ship.DockedNodeId} to {destinationNodeId}");
        return null;
    }

    // 3. 创建 DispatchOrder
    var order = new DispatchOrder {
        OrderId = $"order_{Guid.NewGuid():N}",
        ShipId = shipId,
        OriginNodeId = ship.DockedNodeId,
        DestinationNodeId = destinationNodeId,
        LockedPath = path,         // 快照，不受后续星图变化影响
        CurrentHopIndex = 0,
        HopProgress = 0f,
        IsReturning = false,
    };

    // 4. 注册到 Registry
    _orders[order.OrderId] = order;

    // 5. 更新 ShipState → IN_TRANSIT
    ship.SetState(ShipState.IN_TRANSIT);

    // 6. 广播派遣事件（UI 更新用）
    OnDispatchCreated?.Invoke(order);

    return order;
}
```

### Update() — Transit 推进

```csharp
// FleetDispatchSystem.cs
private const float FLEET_TRAVEL_TIME = 3.0f; // 秒/hop

void Update() {
    if (SimClock.Instance == null) return;
    float simDelta = SimClock.Instance.DeltaTime; // 帧率独立
    if (simDelta <= 0f) return;

    // 复制快照（避免迭代中修改）
    var orders = _orders.Values.ToList();
    foreach (var order in orders) {
        AdvanceOrder(order, simDelta);
    }
}

void AdvanceOrder(DispatchOrder order, float delta) {
    if (order.IsReturning) {
        // 返回逻辑
        AdvanceReturn(order, delta);
        return;
    }

    // Transit 逻辑
    order.HopProgress += delta;

    while (order.HopProgress >= FLEET_TRAVEL_TIME) {
        order.HopProgress -= FLEET_TRAVEL_TIME;
        order.CurrentHopIndex++;

        if (order.CurrentHopIndex >= order.LockedPath.Count - 1) {
            // 到达目的地
            ArrivedAtDestination(order);
            return;
        }
    }
}

void ArrivedAtDestination(DispatchOrder order) {
    string arrivalNodeId = order.LockedPath[^1];
    var node = StarMapData.GetNode(arrivalNodeId);
    var ship = ShipDataModel.GetShip(order.ShipId);

    if (ship == null || ship.State == ShipState.DESTROYED) {
        // EC-8：ShipDataModel.Destroy() 已调用（U-4 路径），清理孤立订单
        CloseOrder(order);
        return;
    }

    // 更新飞船位置
    ship.DockedNodeId = arrivalNodeId;

    if (node.NodeType == StarNodeType.ENEMY) {
        // 触发战斗
        if (ship.IsPlayerControlled) {
            // 玩家飞船在驾驶舱中：触发驾驶舱战斗（CombatSystem 接管）
            CombatSystem.Instance.BeginCombat(order.ShipId, arrivalNodeId);
        } else {
            // 无人值守：U-4 路径
            ResolveUnattendedCombat(order.ShipId, arrivalNodeId, node);
        }
    } else {
        // 占领节点 → DOCKED
        ship.SetState(ShipState.DOCKED);
        StarMapSystem.OnFleetArrived(order.ShipId, arrivalNodeId);
    }

    CloseOrder(order);
}
```

### CancelDispatch — 返回逻辑

```csharp
public void CancelDispatch(string shipId) {
    var order = _orders.Values.FirstOrDefault(o => o.ShipId == shipId);
    if (order == null) return;

    // 计算反向路径（Origin → CurrentHop-1）
    var returnPath = order.LockedPath.Take(order.CurrentHopIndex + 1).Reverse().ToList();

    order.LockedPath = returnPath;
    order.CurrentHopIndex = 0;
    order.HopProgress = 0f;
    order.IsReturning = true;
}
```

### 无人值守战斗 U-4 路径

```csharp
// ResolveUnattendedCombat — 无人值守战斗结算
void ResolveUnattendedCombat(string shipId, string nodeId, StarNode node) {
    int playerFleetSize = GetPlayerShipsOnNode(nodeId); // 当前节点玩家舰队数量
    int enemyFleetSize = 2; // MVP 固定 2 个敌方

    // F-1: unattended_combat_result
    int P = playerFleetSize;
    int E = enemyFleetSize;
    while (P > 0 && E > 0) {
        P -= 1;
        E -= 1;
    }

    if (E <= 0 && P > 0) {
        // 胜利：占领节点
        StarMapSystem.OnCombatVictory(nodeId);
        // 玩家飞船状态 → DOCKED（占领后）
        var ship = ShipDataModel.GetShip(shipId);
        if (ship != null) ship.SetState(ShipState.DOCKED);
    } else {
        // 失败（U-4）：直接 Destroy，绕过 HealthSystem
        foreach (var s in GetPlayerShipsOnNode(nodeId)) {
            ShipDataModel.GetShip(s)?.Destroy(); // 不经过 HealthSystem
        }
        StarMapSystem.OnCombatDefeat(nodeId);
    }
}
```

### ShipDataModel.Destroy() 的 DESTROYED 清理

```csharp
// U-4 路径：ResolveUnattendedDefeat 调用 ship.Destroy() 后，
// 直接调用 OnShipDestroyed?.Invoke(shipId)（FleetDispatch 内部事件，非 HealthSystem）
public void OnShipDestroyed(string shipId) {
    var order = _orders.Values.FirstOrDefault(o => o.ShipId == shipId);
    if (order != null) {
        Debug.Log($"[FleetDispatch] Removing orphaned order {order.OrderId} for destroyed ship {shipId}");
        CloseOrder(order);
    }
}
```

### 关键接口

| 接口 | 调用方 | 提供方 | 说明 |
|------|--------|--------|------|
| `FleetDispatchSystem.RequestDispatch(shipId, dest)` | StarMapUI | FleetDispatchSystem | 发起派遣，返回 DispatchOrder |
| `FleetDispatchSystem.CancelDispatch(shipId)` | StarMapUI | FleetDispatchSystem | 取消派遣，返回原节点 |
| `StarMapPathfinder.FindPath(from, to)` | FleetDispatchSystem | StarMapPathfinder | BFS 路径计算 |
| `ShipDataModel.SetState(IN_TRANSIT/DOCKED)` | FleetDispatchSystem | ShipDataModel | Transit 状态转换 |
| `ShipDataModel.Destroy()` | FleetDispatchSystem（U-4） | ShipDataModel | 无人值守失败路径 |
| `CombatSystem.BeginCombat(shipId, nodeId)` | FleetDispatchSystem | CombatSystem | 触发驾驶舱战斗 |

## Alternatives Considered

### Alternative 1: 每艘 IN_TRANSIT 飞船自己持有 DispatchOrder，自己 Update() 驱动移动
- **Description**: ShipDataModel 或飞船 MonoBehaviour 持有 DispatchOrder 逻辑，在自己的 Update() 中推进 HopProgress
- **Pros**: 分散化，每艘船自己管自己
- **Cons**: StarMapScene 需要追踪所有 Transit 飞船状态（跨系统查询）；难以取消订单（需要遍历所有飞船）
- **Rejection Reason**: FleetDispatchSystem 是 StarMapScene 的核心系统，集中管理所有派遣订单更符合 GDD D-1~D-4 的设计意图；Transit 推进需要在 SimRate=0 时暂停，集中控制更方便

### Alternative 2: DispatchOrder 用协程实现，不在 Update() 中驱动
- **Description**: 每个 DispatchOrder 用 `StartCoroutine` 协程，协程内部用 `WaitForSecondsRealtime(FLEET_TRAVEL_TIME)` 等待 hop 完成
- **Pros**: 代码线性，hop 进度容易阅读
- **Cons**: 协程难以从外部取消（CancelDispatch 需要持有协程句柄）；SimRate 控制需要在协程内部访问 SimClock，不够自然
- **Rejection Reason**: Update() 驱动更适合 SimRate 控制（simDelta 直接传入）；CancelDispatch 需要即时修改状态，协程的暂停点不够灵活

## Consequences

### Positive
- 集中 DispatchOrder Registry：所有 Transit 订单在同一个 Dictionary 中，取消和查询都方便
- SimRate 控制：Update() 中的 simDelta 来自 SimClock，DESTROYED 时暂停推进
- 路径快照：LockedPath 不受星图变化影响，Transit 期间拓扑稳定
- U-4 路径清晰：无人值守失败直接调用 ShipDataModel.Destroy()，不绕 HealthSystem

### Negative
- FleetDispatchSystem 需要订阅 ShipDataModel.OnDestroyed 事件来清理孤立订单
- 每帧遍历所有活跃订单：O(order_count)，MVP 规模小（<50 订单），可接受

### Risks
- **风险 1**：玩家在飞船 Transit 期间摧毁自己的飞船（通过作弊或调试命令）→ 孤立 DispatchOrder
  - 缓解：ShipDataModel.Destroy() → FleetDispatchSystem.OnShipDestroyed() 回调清理
- **风险 2**：节点在 Transit 期间被敌人占领（AI 调度或节点转换）→ LockedPath 上的中间节点状态变化
  - 缓解：LockedPath 是快照；中间节点状态变化不影响在途订单
- **风险 3**：CancelDispatch 产生反向路径经过同一节点多次
  - 缓解：Take(CurrentHopIndex + 1) 保证不重复；已在实现中处理

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| fleet-dispatch-system.md §D-1 | RequestDispatch 派遣确认 → ShipState → IN_TRANSIT | RequestDispatch() → ship.SetState(IN_TRANSIT) |
| fleet-dispatch-system.md §D-3 | 路径快照 LockedPath | 创建 DispatchOrder 时复制 path，不受后续星图变化影响 |
| fleet-dispatch-system.md §D-4 | DispatchOrder 数据结构 | DispatchOrder class 完全实现所有字段 |
| fleet-dispatch-system.md §D-5 | 每帧推进 HopProgress += deltaTime | AdvanceOrder() 实现，simDelta 来自 SimClock |
| fleet-dispatch-system.md §D-5 | 到达目的地触发状态转换 | ArrivedAtDestination() 实现 IN_TRANSIT → DOCKED/IN_COMBAT |
| fleet-dispatch-system.md §D-6 | CancelDispatch 返回原节点 | CancelDispatch() → IsReturning + Reverse path |
| fleet-dispatch-system.md §EC-8 | ShipDataModel.DESTROYED → 孤立订单清理 | OnShipDestroyed() 回调 + CloseOrder()（幂等） |
| ship-combat-system.md §U-4 | 无人值守战斗失败 → DestroyShip() 绕过 HealthSystem | ResolveUnattendedCombat() 直接调用 ShipDataModel.Destroy() |
| TR-starmap-001 | StarMapData 路径计算 | 使用 StarMapPathfinder.FindPath() |

## Performance Implications

| 项目 | 影响 | 缓解 |
|------|------|------|
| **CPU** | 每帧 O(order_count) 遍历所有活跃 DispatchOrder | MVP order_count ≤ 50；可接受 |
| **Memory** | DispatchOrder Registry：Dictionary<string, DispatchOrder> | 每个订单约 ~200B；100 订单 = ~20KB |
| **Load Time** | FleetDispatchSystem 在 StarMapScene 激活时创建 | StarMapScene 已常驻 |

## Migration Plan

FleetDispatchSystem 依赖已实现的 Foundation 层：
- StarMapPathfinder（ADR-0004，BFS 已实现）
- ShipDataModel（ADR-0004，已实现 ShipState 转换）

实施顺序：
1. 创建 `FleetDispatchSystem.cs`（MonoBehaviour 单例，StarMapScene）
2. 实现 DispatchOrder 数据结构
3. 实现 RequestDispatch（路径计算 + ShipState 转换）
4. 实现 Update() + AdvanceOrder（Transit 推进）
5. 实现 CancelDispatch（返回逻辑）
6. 实现 ArrivedAtDestination（状态转换 + 战斗触发）
7. 实现 ResolveUnattendedCombat（U-4 路径）
8. 实现 OnShipDestroyed 回调（孤立订单清理）
9. 实现存档（DispatchOrder 持久化到 SaveData）

## Validation Criteria

| 验证条件 | 验证方法 |
|----------|----------|
| RequestDispatch → ShipState = IN_TRANSIT | 单元测试 |
| Update() 10 ticks × 3s/hop = 30s 后到达目的地 | 单元测试（mock SimClock.DeltaTime） |
| SimRate=0 时 Transit 不推进 | 集成测试 |
| CancelDispatch → IsReturning=true + 反向路径 | 单元测试 |
| ShipDataModel.Destroy() → 孤立订单被清理 | 集成测试 |
| ENEMY 节点到达 → U-4 路径触发 | 集成测试 |
| 路径快照：创建后星图变化不影响 LockedPath | 单元测试 |

## Related Decisions

- [ADR-0004: Data Model Architecture](adr-0004-data-model-architecture.md) — StarMapPathfinder、ShipDataModel
- [ADR-0013: Combat System Architecture](adr-0013-combat-system-architecture.md) — BeginCombat 接口
- [ADR-0014: Health System Architecture](adr-0014-health-system-architecture.md) — U-4 路径绕过 HealthSystem
