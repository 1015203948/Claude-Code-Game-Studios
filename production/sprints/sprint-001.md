# Sprint 1 -- 2026-04-19 to 2026-05-02

## Sprint Goal

**垂直切片（Vertical Slice）**: 让星图 → 进入驾驶舱 → 战斗 → 结算的完整核心循环可玩。

> "我能在手机上完成一次完整的'指挥舰队 → 进入驾驶舱 → 亲自战斗 → 返回星图'的体验。"

---

## Capacity

- Total days: 12
- Buffer (20%): 2.4 days reserved for unplanned work
- Available: 9.6 days

**假设**: 单人开发 + AI 辅助，每日有效开发时间约 4-6 小时。

---

## Tasks

### Must Have（Critical Path）
| ID | Task | Story | Agent | Est. Days | 依赖 | Acceptance Criteria |
|----|------|-------|-------|-----------|------|-------------------|
| M1 | StarMapData 初始化 | Story 002 | programmer | 0.5d | — | 初始星图有 ≥3 个节点（HOME + 2 STANDARD），HOME 已被探索 |
| M2 | ViewLayer Manager 验收 | Story 011 | programmer | 0.5d | — | ViewLayer 枚举完整，STARMAP↔COCKPIT 切换工作，单元测试通过 |
| M3 | Health ApplyDamage | Story 001 | programmer | 0.5d | — | ApplyDamage(Hull=30,8)→22，OnHullChanged 广播，单元测试通过 |
| M4 | Health 死亡序列 | Story 002 | programmer | 0.5d | M3 | Hull=0 时 OnShipDying 广播一次，ShipState→DESTROYED |
| M5 | 战斗状态机 | Story 004 | programmer | 1d | M3,M4 | IN_COCKPIT→IN_COMBAT→结算（胜/负），单元测试通过 |
| M6 | 武器射速计时器 | Story 005 | programmer | 0.5d | M5 | 1.0发/秒，60帧后恰好触发60次，帧率独立 |
| M7 | 武器 Raycast 检测 | Story 006 | programmer | 0.5d | M6 | RaycastNonAlloc 命中→ApplyDamage，零 GC |
| M8 | 无人值守战斗结算 | Story 007 | programmer | 0.5d | M5 | P/E 各减1，胜方节点更新，负方 DestroyShip，单元测试通过 |
| M9 | 敌人生成 | Story 009 | programmer | 0.5d | M4 | SpawnEnemy×2，角间距≥90°，位置距玩家150m |
| M10 | 敌方 AI | Story 010 | programmer | 1d | M9 | APPROACHING→FLANKING→DYING 完整状态机，零 GC |
| M11 | 飞船控制物理 | Story 020 | programmer | 1d | M2 | Rigidbody2D.AddForce 移动，MoveRotation 旋转，边界约束 |
| M12 | 双虚拟摇杆输入 | Story 015 | programmer | 1d | M11 | 左摇杆=移动，右摇杆=瞄准，touchId 追踪正确 |
| M13 | **集成构建** | — | programmer | 1d | M5,M10,M12 | 垂直切片构建：星图→进入驾驶舱→敌人出现→自动战斗→结算 |

**Must Have 总计**: 8.5 天 | **剩余 Buffer**: ~4.1 天（含 20% 预留）

---

### Should Have
| ID | Task | Story | Agent | Est. Days | 依赖 | Acceptance Criteria |
|----|------|-------|-------|-----------|------|-------------------|
| S1 | SimClock Core | Story 008 | programmer | 1d | — | 时间累加，Pause/Resume，可变倍率 |
| S2 | FleetDispatchSystem | Story 012 | programmer | 1d | M1 | RequestDispatch→IN_TRANSIT→Arrival，单元测试通过 |
| S3 | Fleet Transit | Story 013 | programmer | 0.5d | S2 | 飞行中状态图标显示 |
| S4 | Fleet Cancel | Story 014 | programmer | 0.5d | S3 | CancelDispatch→IN_TRANSIT→DOCKED |
| S5 | Fleet Arrival | Story 015 | programmer | 0.5d | S3 | 到达→IN_DOCKED→ShipState 广播 |

**Should Have 总计**: 3.5 天

---

### Nice to Have
| ID | Task | Story | Agent | Est. Days | 依赖 |
|----|------|-------|-------|-----------|------|
| N1 | 软锁定系统 | Story 021 | programmer | 1d | M10 |
| N2 | 摄像机跟随 | Story 022 | programmer | 0.5d | M11 |
| N3 | 飞船状态广播 | Story 003 | programmer | 0.5d | M4 |

---

## Carryover from Previous Sprint

**无** — 这是第一个冲刺。

---

## Risks
| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| 敌方 AI PHYSICS.Raycast 在 Android 性能差 | 中 | 高 | 使用 RaycastNonAlloc 预分配，Profiler 验证 |
| 双摇杆手感不达预期 | 中 | 中 | Sprint 1 内必须真机测试（M12 完成后） |
| ViewLayer 切换和 SceneManager 加载时序问题 | 低 | 高 | ADR-0001 已定义顺序，单元测试覆盖 |
| HealthSystem 与 CombatSystem 接口不对齐 | 低 | 高 | M3/M4 先完成，暴露接口契约 |

---

## Dependencies on External Factors
- **Unity 编辑器**: 所有实现依赖本地 Unity 6.3 LTS 环境
- **ADR 契约**: M5/M10/M12 依赖 ADR-0013/ADR-0014/ADR-0015/ADR-0018 定义的接口
- **触屏设备**: 双摇杆手感需在真机验证，编辑器模拟不充分

---

## Definition of Done for this Sprint
- [ ] All Must Have tasks completed
- [ ] All tasks pass acceptance criteria
- [ ] QA plan exists (`production/qa/qa-plan-sprint-001.md`)
- [ ] All Logic/Integration stories have passing unit/integration tests
- [ ] Smoke check passed (`/smoke-check sprint`)
- [ ] QA sign-off report: APPROVED or APPROVED WITH CONDITIONS (`/team-qa sprint`)
- [ ] No S1 or S2 bugs in delivered features
- [ ] Design documents updated for any deviations
- [ ] Code reviewed and merged
