# Story 003: StarMapPathfinder — BFS Pathfinding

> **Epic**: foundation-infrastructure
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: 2026-04-14
> **Estimate**: 2-3 hours

## Context

**GDD**: `design/gdd/star-map-system.md`
**Requirement**: `TR-starmap-001`

**ADR Governing Implementation**: ADR-0004 — BFS pathfinding via `StarMapPathfinder.FindPath()`
**ADR Decision Summary**: BFS 寻路 O(V+E)，确定性字典序 tie-breaking。

**Engine**: Unity 6.3 LTS | **Risk**: LOW

**Control Manifest Rules (Foundation)**:
- Required: BFS 寻路由 `StarMapPathfinder.FindPath()` 实现，O(V+E)

---

## Acceptance Criteria

*From `design/gdd/star-map-system.md` AC-STAR-17（接口契约）：*

- [ ] `FindPath(originId, destinationId)` 返回 `List<string>`（节点 ID 列表），按访问顺序排列
- [ ] 若 origin == destination，返回只含 origin 的列表
- [ ] 若两者不相邻且无边可达，返回空列表
- [ ] BFS 字典序 tie-breaking：队列中同层节点按 nodeId 字母序出队
- [ ] 路径长度 = 边数（hop 数）

---

## Implementation Notes

*From ADR-0004:*

1. **BFS 实现**：
   ```csharp
   public static List<string> FindPath(StarMapData map, string originId, string destId)
   {
       if (originId == destId) return new List<string> { originId };
       var queue = new Queue<string>();
       var visited = new HashSet<string>();
       var parent = new Dictionary<string, string>();
       queue.Enqueue(originId);
       visited.Add(originId);
       while (queue.Count > 0) {
           // 字典序出队：排序后取最小
           var nodeId = GetLexicographicallyMin(queue.ToList());
           queue.Dequeue();
           foreach (var neighbor in map.GetNeighbors(nodeId)) {
               if (!visited.Contains(neighbor)) {
                   visited.Add(neighbor);
                   parent[neighbor] = nodeId;
                   if (neighbor == destId) return ReconstructPath(parent, originId, destId);
                   queue.Enqueue(neighbor);
               }
           }
       }
       return new List<string>(); // 不可达
   }
   ```

2. **字典序 tie-breaking**：每次扩展邻居时，对邻居列表按 nodeId 排序后再入队，保证 BFS 队列始终字母序最小优先

3. **路径重建**：`ReconstructPath(parent, origin, dest)` 从 dest 回溯到 origin

4. **性能要求**：V ≤ 20 节点，O(V+E) < 0.1ms

---

## Out of Scope

- FleetDispatch 触发 FindPath 的调用时机（Core/Feature 层）
- 路径可视化（UI Presentation 层）

---

## QA Test Cases

- **AC-1: origin == destination**
  - Given: StarMapData（MVP 5 节点）
  - When: `FindPath(HOME_BASE, HOME_BASE)`
  - Then: 返回 `["HOME_BASE"]`

- **AC-2: 直接相邻节点**
  - Given: StarMapData（MVP 5 节点），HOME_BASE 与 RICH_A 相邻
  - When: `FindPath(HOME_BASE, RICH_A)`
  - Then: 返回 `["HOME_BASE", "RICH_A"]`

- **AC-3: 多跳路径**
  - Given: StarMapData（MVP 5 节点），HOME → STANDARD_C → RICH_D
  - When: `FindPath(HOME_BASE, RICH_D)`
  - Then: 返回 `["HOME_BASE", "STANDARD_C", "RICH_D"]`（3 步）

- **AC-4: 不相邻不可达**
  - Given: StarMapData（MVP 5 节点），RICH_A 与 STANDARD_C 不相邻
  - When: `FindPath(RICH_A, STANDARD_C)`
  - Then: 返回空列表 `[]`

- **AC-5: 字典序 tie-breaking**
  - Given: 扩展邻居时队列排序
  - When: BFS 在同层有多个节点时
  - Then: 先扩展字母序最小的 nodeId（`"node_a"` 在 `"node_b"` 之前）

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/starmap/pathfinder_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002（StarMapData 须已完成）
- Unlocks: FleetDispatch（Core 层）调用 FindPath
