# Story 009: SimClock Archive Integration

> **Epic**: foundation-infrastructure
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Manifest Version**: 2026-04-14
> **Estimate**: 1-2 hours

## Context

**GDD**: `design/gdd/dual-perspective-switching.md`（规则 S-10）
**Requirement**: `TR-dvs-009`

**ADR Governing Implementation**: ADR-0012 — SimRate 存档
**ADR Decision Summary**: ShipDataModel.SaveData 包含 SimRate 字段；存档时写入当前值；读档时恢复（默认 1）。

**Engine**: Unity 6.3 LTS | **Risk**: LOW

**Control Manifest Rules (Foundation)**:
- Required: SimRate 写入 ShipDataModel.SaveData

---

## Acceptance Criteria

*From `design/gdd/dual-perspective-switching.md` GDD S-10 + ADR-0012:*

- [ ] ShipDataModel.SaveData 包含 `simRate: float` 字段
- [ ] 存档时：`SaveData.simRate = SimClock.Instance.SimRate`
- [ ] 读档时：`SimClock.Instance.SetRate(saveData.simRate)`（若 simRate 无效则用默认值 1）
- [ ] 新存档默认 SimRate = 1
- [ ] SaveData 结构支持序列化（JSON 或二进制）

---

## Implementation Notes

1. **SaveData 结构**：
   ```csharp
   [Serializable]
   public struct SaveData {
       public string ActiveShipId;
       public float SimRate;  // 新增
       public List<ShipSaveData> Ships;
       public List<NodeSaveData> Nodes;
       // ...
   }
   ```

2. **存档时序**：
   ```
   GameDataManager.Save()
     → ShipDataModel.CollectSaveData()
       → saveData.simRate = SimClock.Instance.SimRate
   ```

3. **读档时序**：
   ```
   GameDataManager.Load(saveData)
     → SimClock.Instance.SetRate(saveData.simRate)  // 触发广播，UI 更新
     → GameDataManager.RestoreState(saveData)
   ```

4. **默认值保护**：若 `saveData.simRate` 不在 {0, 1, 5, 20}，强制设为 1

---

## Out of Scope

- GameDataManager 的完整存档/读档实现（可由 Story 005 扩展）
- UI 层订阅 SimRateChangedChannel 更新显示（Presentation 层）

---

## QA Test Cases

- **AC-1: 存档写入 SimRate**
  - Given: SimClock.SimRate = 5
  - When: `GameDataManager.Save()`
  - Then: saveData.simRate == 5f

- **AC-2: 读档恢复 SimRate**
  - Given: SimClock.SimRate = 1；saveData.simRate = 5
  - When: `GameDataManager.Load(saveData)`
  - Then: SimClock.SimRate == 5f

- **AC-3: 非法存档值默认回 1**
  - Given: saveData.simRate = 99（无效值）
  - When: `GameDataManager.Load(saveData)`
  - Then: SimClock.SimRate == 1f；无 Assert 崩溃

- **AC-4: 新存档默认 SimRate = 1**
  - Given: 新建存档
  - When: `GameDataManager.NewGame()` → `Save()`
  - Then: saveData.simRate == 1f

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/simclock/simclock_save_load_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 008（SimClock 核心须已完成）
- Unlocks: Presentation 层 SimRateDisplay UI（消费 SimRateChangedChannel）
