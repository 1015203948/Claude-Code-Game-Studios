# 飞船战斗系统 (Ship Combat System)

> **Status**: Designed
> **GDD Created**: 2026-04-12
> **Last Updated**: 2026-04-15 (reconstructed from ADR-0013 and session logs after accidental content loss)
> **Systems Index**: #8 / 18

---

## 1. Overview

飞船战斗系统是《星链霸权》驾驶舱层的核心游戏性系统，处理两类完全不同的作战模式：**驾驶舱实时战斗**（玩家亲自操控时的前向弹道射击）和**无人值守自动结算**（玩家不在线时星图层的自动战斗）。两种模式共享"战斗"概念但数据路径完全不同：驾驶舱战斗依赖 HealthSystem 的生命值追踪和 EnemySystem 的 AI；无人值守战斗在单帧内同步完成，不经过 HealthSystem。

---

## 2. Player Fantasy

**驾驶舱战斗**让玩家感受到"驾驭宇宙战舰"的临场感——软锁定准星自动追踪进入射程的敌舰，按住推进摇杆的同时武器自动开火，敌舰爆炸时产生视觉反馈。**无人值守战斗**则让玩家在离开游戏后帝国依然运转，舰队遭遇敌人时自动结算，归来时或在星图上看到占领结果，或在驾驶舱中面对新的敌人。

---

## 3. Detailed Rules

### B — 驾驶舱战斗（BuKai Engagement）

**B-1: 战斗触发**
- 触发时机：FleetDispatchSystem 舰队到达 ENEMY 类型节点
- 玩家在驾驶舱（ShipState = IN_COCKPIT）→ 进入 IN_COMBAT
- 玩家在星图（ShipState = DOCKED）→ 保持 DOCKED，战斗以无人值守路径处理

**B-2: 自动开火**
- MVP 无手动射击按钮
- 每帧检测 `aimAngle ≤ FIRE_ANGLE_THRESHOLD(15°)` 时触发 FireWeapon()
- `aimAngle` 由 ShipControlSystem 每帧计算并暴露（只读，供 CombatSystem 订阅）

**B-3: 射速控制**
- `_fireTimer += Time.deltaTime`（帧率独立，不使用实时间隔）
- 达到 `1f / WEAPON_FIRE_RATE` 时触发一次 Raycast 射击，重置 `_fireTimer = 0f`

**B-4: 命中检测**
- `Physics.RaycastNonAlloc` 预分配缓冲区（零 GC）
- 命中碰撞体后调用 `HealthSystem.ApplyDamage(enemyId, BASE_DAMAGE, KINETIC)`
- 武器射程：`WEAPON_RANGE = 200m`
- 快速移动目标使用 `CollisionDetectionMode.ContinuousDynamic` 防穿透

**B-5: 战斗状态机**

```
IDLE（无战斗）
  ↓ BeginCombat()
COMBAT_ACTIVE
  生成 2 个敌方实例（EnemySystem.SpawnEnemy）
  订阅 HealthSystem.OnShipDying
  广播 CombatChannel.RaiseBegin()
  ↓ OnShipDying 且敌方全灭
COMBAT_VICTORY
  销毁所有敌方实例
  ShipDataModel.SetState(IN_COCKPIT)
  广播 CombatChannel.RaiseVictory(nodeId)
  → IDLE
  ↓ OnShipDying 且玩家 HP=0
COMBAT_DEFEAT
  ShipDataModel.Destroy()（不走 HealthSystem）
  广播 CombatChannel.RaiseDefeat(nodeId)
  → IDLE
```

**B-6: 弹道视觉**
- LineRenderer 沿 Raycast 方向绘制，颜色 `#FFDD44`
- 命中时播放命中火花 VFX（颜色 `#FF8800`，0.1s）

---

### U — 无人值守战斗（Unattended Combat）

**U-1: 触发时机**
- FleetDispatchSystem 舰队到达 ENEMY 节点时，若 ShipState = DOCKED（非 IN_COCKPIT），触发无人值守结算

**U-2: 结算公式**
- 每轮双方各损失 1 艘：`P--, E--`，循环直到一方归零
- 平局判定：双方同时归零 → 判玩家 DEFEAT（保守设计）

**U-3: 胜方处理**
- 占领该 ENEMY 节点
- 舰队状态 → DOCKED

**U-4: 败方处理（关键）**
- 直接调用 `ShipDataModel.DestroyShip(shipId)`
- **绕过 HealthSystem**——不触发 OnShipDying、OnPlayerShipDestroyed、OnShipDestroyed
- 原因：无人值守时驾驶舱不加载，无需播放 VFX/SFX

**U-5: 战斗结果广播**
- `CombatChannel.RaiseVictory(nodeId)` 或 `CombatChannel.RaiseDefeat(nodeId)`
- StarMapScene 的 StarMapSystem 订阅并更新节点归属

---

### V — 视角切换与战斗（View-switching During Combat）

**V-1: IN_COMBAT 期间进入驾驶舱**
- 若玩家在战斗期间点击"返回驾驶舱"，切换序列正常执行
- 战斗状态（COMBAT_ACTIVE）保持，切换完成后 CockpitScene 继续战斗逻辑

**V-2: IN_COMBAT 期间返回星图**
- 玩家主动返回星图 → 战斗中断，舰队保持 IN_TRANSIT 状态（不触发无人值守）
- 舰队继续向目的地移动

**V-3: IN_COMBAT 期间飞船被摧毁**
- 强制退出驾驶舱，ViewLayer → STARMAP，ShipState → DESTROYED
- 节点归属更新由 CombatChannel.RaiseDefeat 触发

**V-4: MVP 排除：战斗中途切换视角**
- 本版本不允许在 IN_COMBAT 和 STARMAP 之间直接切换（V-2 是唯一例外路径）
- 未来版本（Post-MVP）可支持切换视角同时保持 COMBAT_ACTIVE

---

## 4. Formulas

### weapon_fire_rate_timer
```
fire_ready = (_fireTimer >= (1f / WEAPON_FIRE_RATE))
_fireTimer += Time.deltaTime
if fire_ready AND aimAngle <= FIRE_ANGLE_THRESHOLD:
    FireWeapon()
    _fireTimer = 0f
```

### unattended_combat_result(P, E)
```
while P > 0 AND E > 0:
    P -= 1
    E -= 1
if E <= 0: return VICTORY
else: return DEFEAT
```

### aim_angle (provided by ShipControlSystem)
```
toEnemy = (enemy.position - ship.position).normalized
aimAngle = Vector3.Angle(ship.forward, toEnemy)
```

---

## 5. Edge Cases

| 情况 | 处理方式 |
|------|---------|
| 多艘敌人在 LOCK_RANGE 内 | 软锁定系统选取最近的一个（详见 Ship Control System GDD） |
| aimAngle 刚好 = FIRE_ANGLE_THRESHOLD | 视为满足条件（≤ 15°） |
| 敌人在 1 帧内穿越 WEAPON_RANGE | 使用 ContinuousDynamic CCD 防止穿透 |
| 无人值守战斗平局（P=1, E=1） | 判 DEFEAT（保守设计，负于平局） |
| 驾驶舱加载时 CockpitScene 内无 Active 战斗 | CombatSystem 保持 IDLE |
| _fireTimer 累积超过 2× WEAPON_FIRE_RATE | 最多一次开火（不能"充能"） |
| 玩家在战斗期间摧毁自己（调试命令） | CombatSystem 收到 OnShipDying 后判定败 |

---

## 6. Dependencies

### 上游依赖

| 系统 | 依赖内容 | 接口 |
|------|---------|------|
| **HealthSystem** | ApplyDamage 伤害入口；OnShipDying 事件订阅 | `HealthSystem.Instance.ApplyDamage()`；`HealthSystem.OnShipDying` |
| **EnemySystem** | 敌人生成/销毁；目标位置查询 | `EnemySystem.SpawnEnemy()` / `DespawnEnemy()` |
| **ShipControlSystem** | aimAngle 只读计算 | `ShipControlSystem.aimAngle` |
| **ShipDataModel** | ShipState 转换；DestroyShip | `ShipDataModel.SetState()` / `Destroy()` |

### 下游依赖

| 系统 | 依赖内容 | 接口 |
|------|---------|------|
| **StarMapSystem** | 战斗结果更新节点归属 | `CombatChannel.RaiseVictory/Defeat` |
| **ShipHUD** | 武器冷却状态显示 | 订阅 _fireTimer 变化 |

### 与其他战斗文档的关系

- **enemy-system.md**: 敌方 AI 和实例管理；本 GDD 定义战斗流程，EnemySystem 提供敌人生成和 AI 执行
- **ship-health-system.md**: 生命值追踪；本 GDD 的 B-4 路径依赖 HealthSystem.ApplyDamage
- **ship-control-system.md**: aimAngle 由该系统计算并暴露；本 GDD 的 B-2 消费该值

---

## 7. Tuning Knobs

| 常量 | 推荐值 | 安全范围 | 说明 |
|------|--------|---------|------|
| `WEAPON_FIRE_RATE` | 1.0 shots/sec | [0.5, 3.0] | 射速过高会让战斗节奏太快；过低缺乏爽感 |
| `WEAPON_RANGE` | 200m | [100m, 500m] | 影响战术空间——远射程适合伏击 |
| `FIRE_ANGLE_THRESHOLD` | 15° | [5°, 30°] | 过低导致软锁定几乎不可能命中；过高让自动开火过于容易 |
| `BASE_DAMAGE` | 8 HP | [1, 20] | 影响战斗时长；100 HP 飞船需要约 13 次命中 |
| `ACCURACY_COEFF` | 1.0 | [0.0, 1.0] | MVP 固定 1.0；未来可扩展距离衰减或散射 |

> **原型验证**：以上所有值均为起始参考值，需在 Vertical Slice 阶段通过真机 playtest 调整。

---

## 8. Acceptance Criteria

**AC-B1: 驾驶舱自动开火**
- GIVEN 敌人在 WEAPON_RANGE 内且 aimAngle ≤ FIRE_ANGLE_THRESHOLD
- WHEN 武器就绪（_fireTimer ≥ 1/WEAPON_FIRE_RATE）
- THEN FireWeapon() 执行，LineRenderer 绘制弹道，HealthSystem.ApplyDamage 被调用

**AC-B2: 射速计时**
- WHEN 游戏以 60fps 运行恰好 1 秒
- THEN _fireTimer = 1.0f，恰好触发 1 次开火（WEAPON_FIRE_RATE = 1.0）
- THEN _fireTimer 重置为 0f

**AC-B3: 命中检测零 GC**
- GIVEN 连续触发 1000 次 FireWeapon
- THEN Unity Profiler Memory 面板显示 0 GC Allocations（Physics.RaycastNonAlloc 预分配缓冲区）

**AC-U1: 无人值守胜利**
- GIVEN P=3（玩家 3 艘），E=2（敌方 2 艘）
- WHEN 执行 unattended_combat_result
- THEN 返回 VICTORY（P=1, E=0）

**AC-U2: 无人值守失败 U-4 路径**
- GIVEN P=1, E=3
- WHEN 玩家失败
- THEN ShipDataModel.DestroyShip 被调用，HealthSystem.OnShipDying 未被触发

**AC-B4: 战斗胜利**
- GIVEN 敌方 2 个实例全灭（OnShipDying × 2）
- WHEN 玩家 HP > 0
- THEN COMBAT_VICTORY 触发，ShipState → IN_COCKPIT，CombatChannel.RaiseVictory 广播

**AC-B5: 战斗失败**
- GIVEN 玩家 HP=0（OnShipDying 触发且实例为玩家飞船）
- THEN COMBAT_DEFEAT 触发，ShipDataModel.Destroy()，CombatChannel.RaiseDefeat 广播

**AC-B6: 帧率独立**
- GIVEN 同一战斗分别在 30fps 和 60fps 下运行
- THEN 相同时间内开火次数差异 ≤ 5%（Time.deltaTime 累积精度）

---

## Open Questions

| # | 问题 | 状态 |
|---|------|------|
| OQ-1 | 多艘敌人在近距离时软锁定选取规则（最近 vs. 威胁最高） | 待 Vertical Slice 原型验证 |
| OQ-2 | 战斗期间是否有加速时间选项（SimRate > 1） | MVP 排除 |
| OQ-3 | 敌方武器类型是否与玩家不同（不同伤害值或弹道） | MVP 排除 |
