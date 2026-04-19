# ADR-0020: StarMapUI Architecture

## Status
Accepted

## Date
2026-04-17

## Authors
technical-director + game-designer

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | UI / UI Toolkit + Painter2D |
| **Knowledge Risk** | MEDIUM — Painter2D API 在 Unity 6 引入，属于 post-cutoff API。UI Toolkit 运行时在 Unity 6 已 Production-ready。UIDocument + USS 样式为 6.x 标准用法。 |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `docs/engine-reference/unity/modules/ui-toolkit.md` |
| **Post-Cutoff APIs Used** | Painter2D（Unity 6 新 API，用于矢量绘制节点和连线） |
| **Verification Required** | (1) Painter2D 在 Android 低端设备上 ≤20 节点时帧时间 <1ms；(2) UI Toolkit UIDocument 在目标 Android 设备上正确渲染；(3) 捏合缩放手势（Input System）在触屏设备上响应正确 |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0002（Event Communication — OnEnable/OnDisable 配对订阅，Tier 2 C# events）；ADR-0004（Data Model — StarMapData 只读消费）；ADR-0017（Fleet Dispatch — DispatchOrder 数据结构，事件订阅） |
| **Enables** | Core Epic — StarMapUI 实现；触屏交互体验 |
| **Blocks** | 无（StarMapUI 是上层 UI，不阻塞其他系统） |
| **Ordering Note** | ADR-0004（StarMapData）和 ADR-0017（FleetDispatch）应先于本 ADR Accepted |

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| star-map-ui.md §SMUI-1 | 节点渲染（六边形/圆形/菱形） | Painter2D 根据 nodeType 绘制不同形状 |
| star-map-ui.md §SMUI-2 | 连接线渲染 | Painter2D 根据 StarEdge 绘制连线 |
| star-map-ui.md §SMUI-3 | 触控热区 ≥48dp | 热区尺寸不随缩放变化 |
| star-map-ui.md §SMUI-4 | 派遣确认流程 | DispatchCard + FleetDispatchSystem.RequestDispatch() |
| star-map-ui.md §SMUI-5 | 资源角标 | ResourceCorner 订阅 OnResourcesUpdatedChannel |

## Context

星图 UI（StarMapUI）是战略层的渲染和交互层。它将 `StarMapData`（节点图）渲染为玩家可见的星域地图，接收触屏操作（选择节点、派遣舰队），调用 `FleetDispatchSystem.RequestDispatch()`。

现有问题：
- `StarMapUI.cs` 是空壳（36 行），没有实际渲染逻辑
- 没有 ADR 说明 StarMapUI 与其他系统的边界和接口契约
- GDD `star-map-ui.md` 过于详细（693 行），实现需要一份简明的架构决策文档

## Decision Drivers

1. UI Toolkit (UIDocument) 是 Unity 6 推荐的运行时 UI 方案（GDD 要求）
2. Painter2D API 用于矢量绘制节点和连线（零 GC）
3. StarMapUI 必须订阅 FleetDispatchSystem 事件来更新飞船图标
4. 触屏输入必须 ≥48dp 热区（Android 无障碍要求）
5. StarMapData 是只读数据，StarMapUI 不持有任何游戏状态

## Decision

### 组件架构

```
StarMapUI (MonoBehaviour)
├── UIDocument          // 根 UI 文档（UI Toolkit）
├── StarMapViewport     // UIDocument 内容，承载 Painter2D 渲染
├── FleetIconPool       // 对象池，管理飞船图标（IN_TRANSIT）
├── DispatchCard        // 派遣确认卡（玩家确认后调用 FleetDispatchSystem）
└── ResourceCorner      // 资源角标（订阅 OnResourcesUpdatedChannel）
```

**Painter2D 渲染范围**：
- 节点圆/六边形/菱形（`StarNode.position` dp 坐标）
- 连接线（`StarEdge` 两端节点）
- 路径预览线（用户选择派遣目标后）

**非 Painter2D（UGUI 或预制体）**：
- 飞船图标（FleetIconPool，RectTransform 跟随路径）
- 确认卡（Panel + Button，事件触发 FleetDispatchSystem）
- 资源角标（Text + Image）

### 接口契约

| 接口 | 方向 | 描述 |
|------|------|------|
| `StarMapData.Nodes` | 消费 | 只读，O(V) 遍历渲染节点 |
| `StarMapData.Edges` | 消费 | 只读，O(E) 渲染连线 |
| `FleetDispatchSystem.OnDispatchCreated` | 订阅 | 新建飞船图标（IN_TRANSIT） |
| `FleetDispatchSystem.OnOrderClosed` | 订阅 | 移除飞船图标 |
| `OnResourcesUpdatedChannel` | 订阅 | 资源角标更新 |
| `FleetDispatchSystem.RequestDispatch()` | 调用 | 玩家确认派遣 |
| `FleetDispatchSystem.CancelDispatch()` | 调用 | 玩家取消派遣 |

### 渲染坐标系统

节点使用 dp 坐标（`StarNode.position` 是 Vector2，单位 dp）。
渲染时乘以 `zoomScale`（[0.5, 2.0]），触控热区不随缩放变化（始终 ≥48dp）。

### 节点渲染规则（来自 GDD）

| nodeType | 形状 | 尺寸 | 触控热区 |
|----------|------|------|----------|
| HOME_BASE | 六边形 | 56dp | 64dp |
| STANDARD | 圆形 | 44dp | 56dp |
| RICH | 菱形 | 48dp | 60dp |

颜色矩阵（ownership × fogState）：

| fogState | PLAYER | ENEMY | NEUTRAL |
|----------|--------|-------|---------|
| VISIBLE | `#2266CC` | `#FF4400` | `#888888` |
| EXPLORED | 50% 透明 | 50% 透明 | 50% 透明 |
| UNEXPLORED | 不渲染 | 不渲染 | 不渲染 |

### 触控状态机

```
IDLE
 ├─[点击节点]→ NODE_SELECTED（高亮，显示信息卡）
 │                ├─[点击己方飞船]→ SHIP_SELECTED（显示派遣目标）
 │                │                     ├─[点击有效目标节点]→ DISPATCH_CONFIRM
 │                │                     │                       └─[确认]→ RequestDispatch → IDLE
 │                │                     │                       └─[取消]→ IDLE
 │                │                     └─[取消]→ IDLE
 │                └─[点击空白]→ IDLE
 └─[捏合手势]→ 更新 zoomScale
```

### 禁止事项

- StarMapUI 不得直接修改 `StarMapData` 或任何游戏状态
- StarMapUI 不得持有 `FleetDispatchOrder` 引用（只读显示）
- 不得使用 GameObject.Find 或 Transform.Find（依赖场景结构脆弱）

## Consequences

**Positive**：
- StarMapUI 渲染逻辑与数据分离，测试更容易（Mock StarMapData）
- FleetDispatchSystem 解耦，飞船图标独立于派遣逻辑更新
- 触屏热区固定大小，缩放不影响可交互性

**Negative**：
- Painter2D 每次重绘需要完全重新生成顶点（对于 MVP 节点数 ≤20 性能可接受）
- 确认卡是 UGUI，覆盖 UIDocument 需要特殊层管理

## References

- GDD: `design/gdd/star-map-ui.md`（详细交互规格）
- TR-starmapui-001~004: 验收标准
