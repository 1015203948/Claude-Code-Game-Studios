# Story 001: ResourceConfig — Config Data Structure

> **Epic**: foundation-infrastructure
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Manifest Version**: 2026-04-14
> **Estimate**: 1-2 hours

## Context

**GDD**: `design/gdd/resource-system.md`
**Requirement**: `TR-resource-001`

**ADR Governing Implementation**: ADR-0004 — Two-Layer Data Architecture
**ADR Decision Summary**: Layer 1 为只读 Config SO（Inspector 配置），Layer 2 为运行时状态 C# 类；Config SO 位于 `assets/data/config/`，运行时只读不修改。

**Engine**: Unity 6.3 LTS | **Risk**: LOW

**Control Manifest Rules (Foundation)**:
- Required: Config SO 位于 `assets/data/config/`，运行时只读
- Forbidden: 运行时状态不允许存 ScriptableObject

---

## Acceptance Criteria

*From `design/gdd/resource-system.md`:*

- [ ] `ResourceConfig` ScriptableObject 包含 ORE_CAP、ENERGY_CAP、ORE_PER_MINE、ENERGY_PER_COLONY 常量
- [ ] `CanAfford(ResourceBundle cost)` 纯函数返回 bool，不修改状态
- [ ] 每 tick ore accumulation clamp 到 [0, ORE_CAP] 范围
- [ ] ORE_CAP 在 Inspector 中可配置，默认值待定

---

## Implementation Notes

*From ADR-0004 Implementation Guidelines:*

1. **Config SO 创建**：`Assets/Data/Config/ResourceConfig.asset` 为 ScriptableObject，在 Inspector 中配置所有数值
2. **CanAfford 签名**：`public static bool CanAfford(ResourceBundle cost)` — 纯函数，零副作用
3. **ORE_CAP clamp**：`Mathf.Clamp(oreCurrent + delta, 0, ORE_CAP)` — 每 tick 产出后调用
4. **ResourceBundle 类型**：建议为 `Serializable` struct，包含 `ore` 和 `energy` 两个 int 字段

---

## Out of Scope

- Story 010（ColonySystem）：负责每 tick 调用 ResourceConfig 的 accumulation 逻辑
- ColonySystem 的 OnResourcesUpdated 广播 → Story 006（Event Bus）

---

## QA Test Cases

- **AC-1: ORE_CAP clamp**
  - Given: `oreCurrent = 80`，ORE_CAP = 100，tick 产出 +30
  - When: `oreCurrent = oreCurrent + 30` 然后 clamp
  - Then: 结果 = `100`（clamp 到上限）
  - Edge cases: ORE_CAP = 0（非法数据，应 Assert）；负值产出（不允许）

- **AC-2: CanAfford returns true**
  - Given: 当前资源 ore = 60，energy = 20；cost = {ore = 50, energy = 15}
  - When: `ResourceConfig.CanAfford(cost)`
  - Then: 返回 `true`

- **AC-3: CanAfford returns false（矿石不足）**
  - Given: 当前资源 ore = 40，energy = 20；cost = {ore = 50, energy = 15}
  - When: `ResourceConfig.CanAfford(cost)`
  - Then: 返回 `false`

- **AC-4: CanAfford returns false（能源不足）**
  - Given: 当前资源 ore = 60，energy = 10；cost = {ore = 50, energy = 15}
  - When: `ResourceConfig.CanAfford(cost)`
  - Then: 返回 `false`

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/resource/resource_config_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: None
- Unlocks: Story 004（建造费用引用 CanAfford）、Story 010（ColonySystem accumulation）
