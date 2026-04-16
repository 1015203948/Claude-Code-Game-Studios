# Story 008: CombatSystem — CombatChannel Broadcast

> **Epic**: core-gameplay
> **Status**: Ready
> **Layer**: Core
> **Type**: Integration
> **Manifest Version**: 2026-04-14

## Context

**GDD**: `design/gdd/ship-combat-system.md`
**Requirement**: `TR-combat-005`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0013: Combat System Architecture
**ADR Decision Summary**: CombatChannel 为 Tier 1 SO Channel；RaiseBegin() 在 SceneManager.sceneLoaded 回调后调用；RaiseVictory/RaiseDefeat 跨 CockpitScene → StarMapScene 广播。

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: SO Channel 无 post-cutoff API 依赖。

**Control Manifest Rules (this layer)**:
- Required: CombatChannel 为 ScriptableObject Channel（Tier 1）
- Forbidden: RaiseBegin 必须在 sceneLoaded 回调后调用
- Guardrail: SO Channel 零反射，~200B/channel

---

## Acceptance Criteria

*From GDD `design/gdd/ship-combat-system.md` V-1~V-4, CombatChannel definition:*

- [ ] CombatChannel 为 CreateAssetMenu SO Channel，RaiseBegin/RaiseVictory/RaiseDefeat 三个事件
- [ ] RaiseBegin() 在战斗开始（CockpitScene 完全加载后）调用
- [ ] RaiseVictory(nodeId) 在战斗胜利后调用一次
- [ ] RaiseDefeat(nodeId) 在战斗失败后调用一次
- [ ] StarMapScene 的 StarMapSystem 订阅并更新节点归属
- [ ] 驾驶舱内 HUD 订阅以更新 UI

---

## Implementation Notes

*Derived from ADR-0013 Decision section:*

```csharp
// assets/data/channels/CombatChannel.asset（Tier 1 SO Channel）
[CreateAssetMenu(fileName = "CombatChannel", menuName = "Game/Channels/CombatChannel")]
public class CombatChannel : GameEvent<(string NodeId, CombatResult Result)> {
    // RaiseBegin() — 战斗开始（CockpitScene 完全加载后调用）
    // RaiseVictory(string nodeId) — 战斗胜利
    // RaiseDefeat(string nodeId) — 战斗失败
}

// CombatResult 枚举
public enum CombatResult { Victory, Defeat }
```

CombatChannel 实例挂载为 SO（ScriptableObject）；订阅使用 OnEnable/OnDisable 配对（ADR-0002 ADV-01）。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 004: CombatSystem 内部调用 RaiseBegin/RaiseVictory/RaiseDefeat 的时机
- Story 016 (StarMapSystem): 节点归属更新逻辑

---

## QA Test Cases

*Written by qa-lead at story creation.*

- **AC-1**: RaiseBegin called after scene loaded
  - Given: CombatChannel asset exists
  - When: RaiseBegin("node-A") is called
  - Then: channel event is broadcast with (NodeId="node-A", Result=RaiseBegin)

- **AC-2**: RaiseVictory fired once on victory
  - Given: COMBAT_VICTORY state reached
  - When: CombatChannel.RaiseVictory("node-A") is called
  - Then: event broadcast once; no duplicate

- **AC-3**: RaiseDefeat fired once on defeat
  - Given: COMBAT_DEFEAT state reached
  - When: CombatChannel.RaiseDefeat("node-A") is called
  - Then: event broadcast once; no duplicate

- **AC-4**: SO Channel subscription pairs correctly
  - Given: StarMapSystem subscribes OnEnable, unsubscribes OnDisable
  - When: CockpitScene loads/unloads
  - Then: subscription active only while scene is loaded

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/unit/combat/combat_channel_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: CombatChannel SO asset creation; Story 004 (state machine integration)
- Unlocks: StarMapSystem node ownership update integration
