# Epic: Core Gameplay Systems — Colony, Fleet Dispatch, Combat, Enemy, Health

> **Layer**: Core + Feature (混合层)
> **GDD**: ship-health-system.md, building-system.md, colony-system.md, ship-combat-system.md, enemy-system.md, fleet-dispatch-system.md
> **Architecture Module**: Core/Feature gameplay systems
> **Status**: In Progress (stories created)
> **Stories**: 23 created — run `/story-readiness` to begin implementation

## Overview

本 Epic 实现了《星链霸权》的核心游戏循环：从殖民地资源产出 → 舰队派遣 → 战斗触发 → 敌人 AI → 生命值管理 的完整闭环。包含五个紧耦合系统：ColonySystem（经济 tick）、FleetDispatchSystem（星图 Transit）、CombatSystem（驾驶舱战斗状态管理）、EnemySystem（敌方 AI）、HealthSystem（生命值仲裁）。这一 Epic 是 MVP 验证的核心——没有它，驾驶舱战斗和星图调度都是空谈。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|------------|
| ADR-0013: Combat System Architecture | CombatSystem 管理 IN_COMBAT 状态、胜负判定、武器射击；与 HealthSystem/EnemySystem 解耦 | LOW |
| ADR-0014: Health System Architecture | ApplyDamage + H-5 死亡序列；ShipState 门控（DOCKED/IN_TRANSIT 静默拒绝） | LOW |
| ADR-0015: Enemy System Architecture | EnemyAIController MonoBehaviour + UpdateAI() 状态机；零 GC 物理查询 | MEDIUM |
| ADR-0016: Colony & Building Architecture | ColonyManager SimClock tick；BuildingSystem 原子建造；ShipyardTier 节点独占 | LOW |
| ADR-0017: Fleet Dispatch Architecture | DispatchOrder Registry；LockedPath 快照；Transit SimRate 控制；U-4 无人值守路径 | LOW |

## GDD Requirements

> ⚠️ 注意：TR-registry 中以下系统（Combat、Health、Enemy、FleetDispatch）尚无 TR-ID 条目。
> Colony 系统已有 TR-colony-001~003。以下列表为基于 GDD 内容的手动追踪，待 architecture-review 更新 registry。

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-ship-health-001 | ApplyDamage 路径：finalDamage → Clamp → SetCurrentHull → OnHullChanged/死亡序列 | ADR-0014 ✅ |
| TR-ship-health-002 | DOCKED/IN_TRANSIT 状态不接受伤害（静默忽略） | ADR-0014 ✅ |
| TR-ship-health-003 | H-5 死亡序列：OnShipDying → DestroyShip → OnPlayerShipDestroyed → OnShipDestroyed | ADR-0014 ✅ |
| TR-colony-001 | ColonyManager tick：SimClock.DeltaTime 累加，≥1s 触发产出计算 | ADR-0016 ✅ |
| TR-colony-002 | ore clamp [0, ORE_CAP]，energy 无上限；每 tick 广播 OnResourcesUpdated | ADR-0016 ✅ |
| TR-colony-003 | ColonyShipChannel 广播建造完成事件 | ADR-0016 ✅ |
| TR-combat-001 | CombatSystem.BeginCombat：IN_COCKPIT → IN_COMBAT；胜负判定；EndCombat | ADR-0013 ✅ |
| TR-combat-002 | 武器射击：weapon_fire_rate_timer + Physics.RaycastNonAlloc 零 GC | ADR-0013 ✅ |
| TR-enemy-001 | SpawnEnemy × 2（ai-0, ai-1）；SPAWNING → APPROACHING → FLANKING → DYING → Despawn | ADR-0015 ✅ |
| TR-enemy-002 | 零 GC 物理查询：OverlapSphereNonAlloc + RaycastNonAlloc | ADR-0015 ✅ |
| TR-fleet-001 | RequestDispatch → DispatchOrder 创建 → ShipState IN_TRANSIT | ADR-0017 ✅ |
| TR-fleet-002 | Update() 每帧推进 HopProgress += SimDeltaTime | ADR-0017 ✅ |
| TR-fleet-003 | CancelDispatch 返回原节点（反向路径） | ADR-0017 ✅ |
| TR-fleet-004 | U-4 无人值守战斗失败 → ShipDataModel.Destroy() 绕过 HealthSystem | ADR-0017 ✅ |
| — | ShipControlSystem 操控系统（无 ADR，Feature 层，依赖本 Epic） | ❌ No ADR |

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | HealthSystem — ApplyDamage + ShipState Gate | Logic | Complete | ADR-0014 |
| 002 | HealthSystem — Death Sequence H-5 | State Machine | Complete | ADR-0014 |
| 003 | HealthSystem — HullRatio + OnHullChanged Broadcast | Integration | Complete | ADR-0014 |
| 004 | CombatSystem — Cockpit State Machine | State Machine | Ready | ADR-0013 |
| 005 | CombatSystem — Fire Rate Timer + Auto-Fire | Logic | Ready | ADR-0013 |
| 006 | CombatSystem — Raycast Hit Detection (Zero GC) | Logic | Ready | ADR-0013 |
| 007 | CombatSystem — Unattended Combat U-4 | Logic | ADR-0013 |
| 008 | CombatSystem — CombatChannel Broadcast | Integration | Ready | ADR-0013 |
| 009 | EnemySystem — SpawnEnemy × 2 + Position | Logic | Ready | ADR-0015 |
| 010 | EnemySystem — AI State Machine (APPROACHING → FLANKING → DYING) | State Machine | Ready | ADR-0015 |
| 011 | EnemySystem — Physics Queries Zero GC | Logic | Ready | ADR-0015 |
| 012 | FleetDispatch — DispatchOrder Creation + State Transition | Logic | Ready | ADR-0017 |
| 013 | FleetDispatch — Transit Hop Advancement | Logic | Ready | ADR-0017 |
| 014 | FleetDispatch — CancelDispatch Return Path | Logic | Ready | ADR-0017 |
| 015 | FleetDispatch — Enemy Arrival + U-4 Path | Integration | Ready | ADR-0017 |
| 016 | ColonySystem — Resource Tick + OnResourcesUpdated | Integration | Ready | ADR-0016 |
| 017 | ColonySystem — BuildShip + DeductResources Atomicity | Logic | Ready | ADR-0016 |
| 018 | BuildingSystem — RequestBuild Atomicity + RefreshProductionCache | Logic | Ready | ADR-0016 |
| 019 | ShipControlSystem — Physics Core (Thrust + Velocity Clamp + Turn) | Logic | Ready | ADR-0018 |
| 020 | ShipControlSystem — Input Processing (Dead Zone + Aim Assist + Multi-Touch) | Logic | Ready | ADR-0018 |
| 021 | ShipControlSystem — Soft Lock + FireRequested | Integration | Ready | ADR-0018 |
| 022 | ShipControlSystem — Camera Rig View Switching | Visual/Feel | Ready | ADR-0018 |
| 023 | ShipControlSystem — State Init/Cleanup (S-1~S-4) | State Machine | Ready | ADR-0018 |

## Definition of Done

This epic is complete when:
- All 5 ADRs (0013–0017) reach `Status: Accepted` (run `/architecture-review` in a fresh session)
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from referenced GDDs are verified
- All Logic and Integration stories have passing test files in `tests/`
- All Visual/Feel and UI stories have evidence docs with sign-off in `production/qa/evidence/`
- ShipControlSystem has a governing ADR (run `/architecture-decision ship-control-system-architecture`)

## Epic Boundary Notes

**本 Epic 不包含：**
- ShipControlSystem（Feature 层，无 ADR）— 依赖本 Epic 的 CombatSystem
- StarMapUI 和 ShipHUD（Presentation 层）— 依赖本 Epic 的所有系统
- 双视角切换 SWITCHING_SHIP 序列（Presentation 层）— 依赖 CombatSystem.BeginCombat

**依赖链：**
```
ShipDataModel (Foundation)
  └→ HealthSystem (ADR-0014) ← ADR-0013 CombatSystem
  └→ FleetDispatchSystem (ADR-0017) ← ADR-0016 Colony+Building
  └→ EnemySystem (ADR-0015) → CombatSystem (ADR-0013)
  └→ ColonySystem (ADR-0016) → BuildingSystem (ADR-0016)
```

## Next Step

1. Run `/story-readiness production/epics/core-gameplay/story-001-health-apply-damage.md` to begin implementing stories in dependency order
2. Stories 001-003 (HealthSystem) → 004-008 (CombatSystem) → 009-011 (EnemySystem) → 012-015 (FleetDispatch) → 016-018 (Colony+Building) → 019-023 (ShipControlSystem)
3. Stories 019-023 (ShipControlSystem) depend on foundation-runtime stories 011-016 being DONE first
