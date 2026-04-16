# Story 002: StarMapData — Graph Data Structure

> **Epic**: foundation-infrastructure
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: 2026-04-14
> **Estimate**: 1-2 hours

## Context

**GDD**: `design/gdd/star-map-system.md`
**Requirement**: `TR-starmap-001`, `TR-starmap-003`

**ADR Governing Implementation**: ADR-0004 — Two-Layer Data Architecture
**ADR Decision Summary**: Layer 2 runtime 状态（StarMapData）为纯 C# 类，由 MasterScene 的 GameDataManager 持有；BFS 寻路由 `StarMapPathfinder.FindPath()` 实现，O(V+E)。

**Engine**: Unity 6.3 LTS | **Risk**: LOW

**Control Manifest Rules (Foundation)**:
- Required: 运行时状态为纯 C# 类，不允许存 ScriptableObject
- Required: StarMapData 由 GameDataManager.Instance 持有

---

## Acceptance Criteria

*From `design/gdd/star-map-system.md`:*

- [ ] `StarMapData` 持有 `List<StarNode>` 和 `List<StarEdge>` + 邻接索引
- [ ] MVP 初始地图：5 节点 diamond 布局（HOME_BASE 居中，4 个 STANDARD/RICH 邻居）
- [ ] `GetNeighbors(nodeId)` 返回相邻节点列表，只读
- [ ] `AreAdjacent(nodeA, nodeB)` 返回 bool，只读
- [ ] `GetNode(nodeId)` 返回 StarNode 或 null，只读
- [ ] 节点 fogState 初始化为 UNEXPLORED（HOME_BASE 除外）

---

## Implementation Notes

*From ADR-0004 Implementation Guidelines:*

1. **StarMapData 结构**：
   ```csharp
   public class StarMapData {
       public List<StarNode> Nodes { get; }
       public List<StarEdge> Edges { get; }
       public Dictionary<string, List<string>> AdjacencyIndex;  // nodeId → [neighborIds]
   }
   ```

2. **邻接索引**：在 `StarMapData.BuildAdjacencyIndex()` 中构建，O(V+E)，初始化时调用一次

3. **只读约束**：所有查询方法不修改内部状态；如需修改（如节点所有权变更），通过 GameDataManager 的专门方法操作

4. **MVP diamond 布局**：
   ```
         [RICH-A]
            |
   [HOME_BASE] — [STANDARD-B]
            |
         [STANDARD-C]
            |
         [RICH-D]
   ```
   HOME_BASE 坐标 (0,0)，RICH-A (0,1)，STANDARD-B (1,0)，STANDARD-C (0,-1)，RICH-D (0,-2)

---

## Out of Scope

- Story 003（StarMapPathfinder）：BFS 寻路逻辑
- Story 007（ViewLayerChannel）：星图 UI 订阅节点变更广播
- AC-STAR-09~16：迷雾逻辑由 ColonySystem 和 FleetDispatch 触发，不在此 Story

---

## QA Test Cases

- **AC-1: 5 节点初始化**
  - Given: 新建 StarMapData
  - When: 调用初始化（加载 MVP diamond 数据）
  - Then: Nodes.Count = 5；HOME_BASE 节点 fogState = EXPLORED；其余 = UNEXPLORED

- **AC-2: AreAdjacent 正确**
  - Given: 加载后的 StarMapData
  - When: `AreAdjacent(HOME_BASE, RICH_A)`；`AreAdjacent(HOME_BASE, STANDARD_C)`
  - Then: 两者均返回 `true`；`AreAdjacent(RICH_A, STANDARD_C)` 返回 `false`（不相邻）

- **AC-3: GetNeighbors 返回正确邻接**
  - Given: 加载后的 StarMapData
  - When: `GetNeighbors(HOME_BASE)`
  - Then: 返回列表包含 RICH_A 和 STANDARD_C（2 个邻居）

- **AC-4: GetNode 找不到返回 null**
  - Given: 加载后的 StarMapData
  - When: `GetNode("non_existent_id")`
  - Then: 返回 `null`

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/starmap/starmap_data_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: None
- Unlocks: Story 003（Pathfinder 依赖 StarMapData）；Story 007（UI 订阅节点数据）
