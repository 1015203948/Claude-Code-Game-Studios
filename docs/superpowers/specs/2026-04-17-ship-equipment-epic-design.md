# Ship Equipment Epic — Design Specification

## 1. Concept & Vision

星链霸权的舰船装备系统遵循 X4: Foundations 的设计哲学：**船体是框架，装备是灵魂。** 玩家不更换船体，而是通过装卸模块来改变舰船的作战特性。同一艘驱逐舰，装上重火力模块就是炮舰，装上护盾模块就是肉盾。战斗掉落高级模块是驱动玩家持续出战的动力。

## 2. Core Design

### 船体类型（固定，不可更改）

| 船体 | 槽位配置 | 物理特性 | 来源 |
|------|---------|---------|------|
| Fighter（战斗机） | Weapon×1, Engine×1, Shield×1 | 高速、低甲 | Shipyard 建造 |
| Destroyer（驱逐舰） | Weapon×2, Engine×1, Shield×1 | 中速、中甲 | Shipyard 建造 |
| Cruiser（巡洋舰） | Weapon×2, Engine×1, Shield×1, Cargo×1 | 低速、高甲 | Shipyard 建造 |

- 每艘船的槽位类型和数量**由船体决定**，船体本身不可改装
- 槽位类型：Weapon（武器）、Engine（引擎）、Shield（护盾）、Cargo（货舱，仅 Cruiser）

### 模块系统

**模块分层（颜色稀有度）：**

| 等级 | 颜色 | 属性倍率 | 掉落概率 |
|------|------|---------|---------|
| T1（白） | 普通 | 1.0× | 建造获得 |
| T2（绿） | 优秀 | 1.3× | 击败 T1 敌人 |
| T3（蓝） | 精良 | 1.6× | 击败 T2 敌人 |

**模块列表（每种槽位）：**

*Weapon 模块：*
- T1: Light Laser（+10 伤害）
- T2: Heavy Laser（+15 伤害）
- T3: Plasma Cannon（+25 伤害）

*Engine 模块：*
- T1: Standard Thruster（+5 速度）
- T2: Military Thruster（+8 速度）
- T3: Elite Thruster（+12 速度）

*Shield 模块：*
- T1: Light Shield（+20 护盾）
- T2: Medium Shield（+35 护盾）
- T3: Heavy Shield（+55 护盾）

*Cargo 模块（Cruiser 专属）：*
- T1: Standard Cargo（+50 货舱）
- T2: Expanded Cargo（+100 货舱）
- T3: Industrial Cargo（+200 货舱）

### 模块属性叠加

所有已装备模块的属性在 `ShipDataModel` 中直接叠加，战斗系统下一帧立即生效，无需重启或刷新。

```
实际武器伤害 = BaseWeaponDamage + Σ(Weapon模块.damage)
实际最高速度 = BaseSpeed + Σ(Engine模块.speed)
实际护盾值   = BaseShield + Σ(Shield模块.shield)
```

### 来源：建造 + 掉落

**建造（Shipyard）：**
- 消耗资源（金属+晶体）购买空白船体
- 建造时间：即时（设计文档中可在后台完成）

**战斗掉落：**
- 击败敌舰后，掉落表随机产出 1 个未装备模块
- 掉落模块进入玩家仓库
- T3 模块掉落率低（T3 敌人击败才掉落 T3）

**仓库（Inventory）：**
- 所有未装备模块存在全局仓库
- 仓库容量无上限

## 3. 交互界面（X4 风格）

### 装备界面流程

1. **打开方式**：星图点击舰队中某艘船 → 选择"装备"
2. **主视图**：3D 船体模型居中，所有槽位高亮显示（不同颜色代表槽类型）
3. **槽位点击**：点击槽位 → 右侧滑出该类型模块列表（来自仓库）
4. **装上模块**：点击模块 → 从仓库移到槽位，3D 模型对应位置显示图标
5. **卸下模块**：点击已装备槽位 → 模块返回仓库
6. **关闭**：返回星图，所有改动立即生效

### 槽位颜色映射

| 槽类型 | 高亮颜色 |
|--------|---------|
| Weapon | 红色 |
| Engine | 蓝色 |
| Shield | 绿色 |
| Cargo | 黄色 |

### 快捷操作

- **快速装卸**：长按槽位 → 弹出快速选择（最近使用的 3 个模块）
- **对比模式**：选中模块后悬停，可看到与当前装备的差值（+/-）

## 4. 数据模型扩展

### ShipDataModel 新增字段

```csharp
// 已装备模块（槽位类型 → 模块实例，无则为 null）
private Dictionary<SlotType, EquipmentModule> _equippedModules;

// 仓库（所有未装备模块）
private static List<EquipmentModule> _inventory = new();

// 属性计算（每次获取时实时叠加）
public float TotalWeaponDamage => BaseWeaponDamage + _equippedModules
    .Where(kv => kv.Key == SlotType.Weapon && kv.Value != null)
    .Sum(kv => kv.Value.Damage);
```

### EquipmentModule ScriptableObject

```csharp
[CreateAssetMenu(fileName = "Module_Weapon_T1", menuName = "Starchain/Equipment/Weapon")]
public class EquipmentModule : ScriptableObject
{
    public string ModuleId;
    public SlotType SlotType;     // Weapon/Engine/Shield/Cargo
    public ModuleTier Tier;        // T1/T2/T3
    public float Damage;           // Weapon
    public float Speed;            // Engine
    public float Shield;           // Shield
    public float Cargo;            // Cargo
    public Sprite Icon;
}
```

### 掉落表（ShipLootTable）

```csharp
[CreateAssetMenu]
public class ShipLootTable : ScriptableObject
{
    public List<LootEntry> Entries;
}

[System.Serializable]
public class LootEntry
{
    public ModuleTier Tier;
    public SlotType SlotType;
    [Range(0, 100)] public float DropWeight;  // 权重
}
```

击败敌舰时：按权重随机抽取 1 个模块 → 实例化 → 加入仓库。

## 5. 系统依赖

```
ShipDataModel          ← 新增模块字段和方法
BuildingSystem         ← 扩展 BuildShip() 支持船体类型参数
FleetDispatchSystem    ← 已依赖 ShipDataModel，模块变更透明
CombatSystem           ← 读取 TotalWeaponDamage 等属性，已透明
EnemyAIController     ← 击败时触发掉落
InventoryUI            ← 新建（仓库 UI）
ShipEquipmentUI        ← 新建（3D 模型 + 槽位界面）
```

**被依赖关系：**
- `ShipDataModel` 变更 → `CombatSystem` 读取新属性（透明）
- `BuildingSystem` 扩展 → 不影响现有功能

## 6. 实现顺序

### Phase 1：数据模型
1. 创建 `EquipmentModule` ScriptableObject
2. 创建 `ShipLootTable` ScriptableObject
3. `ShipDataModel` 新增模块字段和加减方法
4. 建造流程扩展（接受船体类型参数）

### Phase 2：仓库 UI
5. 仓库 UI（列表视图，显示所有未装备模块）
6. 仓库容量逻辑（无上限）

### Phase 3：装备界面
7. 3D 船体模型加载（根据船体类型）
8. 槽位高亮显示
9. 点击槽位 → 侧边模块选择面板
10. 装上/卸下逻辑

### Phase 4：掉落与整合
11. 敌舰击败 → 掉落逻辑
12. 战斗结束后奖励结算 UI
13. 完整流程测试

## 7. GDD 依赖

本 Epic 需要以下 GDD 存在并完成：
- `design/gdd/ship-system.md` — 已有 ShipDataModel
- `design/gdd/ship-combat-system.md` — 已有 CombatSystem 属性读取
- `design/gdd/building.md` — 已有 Shipyard 建造流程
- `design/gdd/fleet-dispatch-system.md` — 已有舰队管理

**新建 GDD：**
- `design/gdd/ship-equipment-system.md` — 本 Epic 专用设计文档

## 8. Acceptance Criteria

- [ ] AC-1：建造界面可以选择 Fighter/Destroyer/Cruiser 船体，消耗资源后船出现在舰队
- [ ] AC-2：装备界面显示 3D 船体模型，槽位高亮（红/蓝/绿/黄）
- [ ] AC-3：点击 Weapon 槽显示仓库中所有 Weapon 模块（T1/T2/T3）
- [ ] AC-4：装上 T3 武器后，CombatSystem 的伤害输出立即增加
- [ ] AC-5：击败敌舰后随机获得 1 个模块（根据掉落表权重）
- [ ] AC-6：卸下模块返回仓库，装上另一个模块替换旧模块
- [ ] AC-7：Cruiser 的 Cargo 槽影响货舱容量（影响资源携带量）
- [ ] AC-8：舱内改装不重启、不读档，数据实时生效
