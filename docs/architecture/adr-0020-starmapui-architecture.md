# ADR-0020: StarMapUI Architecture

> **Status**: Accepted
> **Date**: 2026-04-17
> **Authors**: technical-director + game-designer
> **Supersedes**: None
> **Related TRs**: TR-starmapui-001~004

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
- ADR-0002: Event Communication（Tier 2 C# events 规范）
- ADR-0004: Data Model（StarMapData 接口）
- ADR-0017: Fleet Dispatch（DispatchOrder 数据结构）
- TR-starmapui-001~004: 验收标准
