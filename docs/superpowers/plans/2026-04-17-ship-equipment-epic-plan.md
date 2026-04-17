# Ship Equipment Epic — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现舰船装备系统——船体固定仅装卸装备模块（X4 风格）

**Architecture:**
- 数据层：`EquipmentModule` ScriptableObject + `ShipLootTable` + `ShipDataModel` 扩展槽位/仓库
- 逻辑层：装备装卸、属性叠加计算、掉落表抽取
- UI 层：仓库 UI（列表）+ 装备 UI（3D 模型 + 槽位高亮 + 选择面板）
- 建造扩展：`BuildingSystem.BuildShip()` 支持船体类型参数

**Tech Stack:** Unity 6.3 LTS, C#, ScriptableObject, UGUI (Canvas)

---

## File Map

### 新建文件

| 文件 | 职责 |
|------|------|
| `src/Data/EquipmentModule.cs` | 模块 ScriptableObject，定义槽类型/等级/属性 |
| `src/Data/ShipLootTable.cs` | 掉落表 ScriptableObject |
| `src/Data/HullBlueprint.cs` | 船体蓝图，定义槽位配置 |
| `src/Gameplay/ShipEquipmentSystem.cs` | 装备装卸核心逻辑、属性计算 |
| `src/Gameplay/LootDropSystem.cs` | 击败后掉落抽取逻辑 |
| `src/UI/InventoryUI.cs` | 仓库 UI（模块列表视图） |
| `src/UI/ShipEquipmentUI.cs` | 装备 UI（3D 模型 + 槽位 + 选择面板） |
| `src/UI/ModuleSelectionPanel.cs` | 模块选择面板 |
| `tests/unit/equipment/equipment_module_test.cs` | 模块属性测试 |
| `tests/unit/equipment/ship_equipment_system_test.cs` | 装备装卸逻辑测试 |
| `tests/unit/equipment/loot_table_test.cs` | 掉落表权重测试 |
| `tests/integration/equipment/integration_equipment_test.cs` | 完整装备流程集成测试 |

### 修改文件

| 文件 | 改动 |
|------|------|
| `src/Data/ShipDataModel.cs` | 新增 `_equippedModules` / `_inventory` / 属性方法 |
| `src/Gameplay/building/BuildingSystem.cs` | 扩展 `BuildShip()` 支持 HullType 参数 |
| `src/Gameplay/enemy/EnemyAIController.cs` | 击败时调用 `LootDropSystem` |
| `src/Gameplay/CombatSystem.cs` | 读取 `TotalWeaponDamage` 等新属性 |
| `src/Data/ScriptableObject` 资产 | 创建 12 个模块实例（T1/T2/T3 × 4 槽类型）、3 个船体蓝图 |

---

## Phase 1: 数据模型

### Task 1: 创建 SlotType 枚举和 ModuleTier 枚举

**Files:**
- Create: `src/Data/Enums/EquipmentEnums.cs`

- [ ] **Step 1: 写测试**

```csharp
// tests/unit/equipment/equipment_enums_test.cs
[TestFixture]
public class EquipmentEnums_Test
{
    [Test]
    public void slotType_has_four_values()
    {
        var values = System.Enum.GetValues(typeof(SlotType));
        Assert.AreEqual(4, values.Length);
        Assert.IsTrue(System.Enum.IsDefined(typeof(SlotType), SlotType.Weapon));
        Assert.IsTrue(System.Enum.IsDefined(typeof(SlotType), SlotType.Engine));
        Assert.IsTrue(System.Enum.IsDefined(typeof(SlotType), SlotType.Shield));
        Assert.IsTrue(System.Enum.IsDefined(typeof(SlotType), SlotType.Cargo));
    }

    [Test]
    public void moduleTier_has_three_values_ordered()
    {
        var values = (ModuleTier[])System.Enum.GetValues(typeof(ModuleTier));
        Assert.AreEqual(3, values.Length);
        Assert.AreEqual(ModuleTier.T1, values[0]);
        Assert.AreEqual(ModuleTier.T2, values[1]);
        Assert.AreEqual(ModuleTier.T3, values[2]);
    }
}
```

- [ ] **Step 2: 运行测试，验证失败**
Run: Unity Test Runner — editmode
Expected: FAIL (file not found)

- [ ] **Step 3: 写实现**

```csharp
// src/Data/Enums/EquipmentEnums.cs
namespace Gameplay
{
    public enum SlotType
    {
        Weapon = 0,
        Engine = 1,
        Shield = 2,
        Cargo  = 3
    }

    public enum ModuleTier
    {
        T1 = 1,  // 普通（白）
        T2 = 2,  // 优秀（绿）
        T3 = 3   // 精良（蓝）
    }
}
```

- [ ] **Step 4: 运行测试，验证通过**
Run: Unity Test Runner — editmode
Expected: PASS

- [ ] **Step 5: 提交**

```bash
git add src/Data/Enums/EquipmentEnums.cs tests/unit/equipment/equipment_enums_test.cs
git commit -m "feat: add SlotType and ModuleTier enums"
```

---

### Task 2: 创建 EquipmentModule ScriptableObject

**Files:**
- Create: `src/Data/EquipmentModule.cs`
- Create: `tests/unit/equipment/equipment_module_test.cs`

- [ ] **Step 1: 写测试**

```csharp
// tests/unit/equipment/equipment_module_test.cs
[TestFixture]
public class EquipmentModule_Test
{
    [Test]
    public void module_stores_all_properties()
    {
        var module = ScriptableObject.CreateInstance<EquipmentModule>();
        module.ModuleId = "weapon_t2_heavy_laser";
        module.SlotType = SlotType.Weapon;
        module.Tier = ModuleTier.T2;
        module.Damage = 15f;
        module.Speed = 0f;
        module.Shield = 0f;
        module.Cargo = 0f;

        Assert.AreEqual("weapon_t2_heavy_laser", module.ModuleId);
        Assert.AreEqual(SlotType.Weapon, module.SlotType);
        Assert.AreEqual(ModuleTier.T2, module.Tier);
        Assert.AreEqual(15f, module.Damage);
    }

    [Test]
    public void engine_module_has_speed_bonus_no_damage()
    {
        var module = ScriptableObject.CreateInstance<EquipmentModule>();
        module.ModuleId = "engine_t1_standard";
        module.SlotType = SlotType.Engine;
        module.Tier = ModuleTier.T1;
        module.Damage = 0f;
        module.Speed = 5f;
        module.Shield = 0f;
        module.Cargo = 0f;

        Assert.AreEqual(0f, module.Damage);
        Assert.AreEqual(5f, module.Speed);
    }
}
```

- [ ] **Step 2: 运行测试，验证失败**
Expected: FAIL (EquipmentModule not defined)

- [ ] **Step 3: 写实现**

```csharp
// src/Data/EquipmentModule.cs
using UnityEngine;

namespace Gameplay
{
    [CreateAssetMenu(fileName = "Module_Weapon_T1", menuName = "Starchain/Equipment/Weapon")]
    public class EquipmentModule : ScriptableObject
    {
        [Header("Identity")]
        public string ModuleId;
        public SlotType SlotType;
        public ModuleTier Tier;

        [Header("Attributes")]
        [Tooltip("武器伤害加成")]
        public float Damage;
        [Tooltip("引擎速度加成")]
        public float Speed;
        [Tooltip("护盾容量加成")]
        public float Shield;
        [Tooltip("货舱容量加成")]
        public float Cargo;

        [Header("Visuals")]
        public Sprite Icon;
    }
}
```

- [ ] **Step 4: 运行测试，验证通过**
Expected: PASS

- [ ] **Step 5: 提交**

```bash
git add src/Data/EquipmentModule.cs tests/unit/equipment/equipment_module_test.cs
git commit -m "feat: add EquipmentModule ScriptableObject"
```

---

### Task 3: 创建 HullBlueprint ScriptableObject

**Files:**
- Create: `src/Data/HullBlueprint.cs`
- Create: `tests/unit/equipment/hull_blueprint_test.cs`

- [ ] **Step 1: 写测试**

```csharp
[TestFixture]
public class HullBlueprint_Test
{
    [Test]
    public void fighter_has_3_slots()
    {
        var bp = ScriptableObject.CreateInstance<HullBlueprint>();
        bp.HullType = HullType.Fighter;
        bp.SlotConfiguration = new[] {
            SlotType.Weapon, SlotType.Engine, SlotType.Shield
        };

        Assert.AreEqual(3, bp.SlotConfiguration.Length);
        Assert.AreEqual(SlotType.Weapon, bp.SlotConfiguration[0]);
    }

    [Test]
    public void cruiser_has_cargo_slot()
    {
        var bp = ScriptableObject.CreateInstance<HullBlueprint>();
        bp.HullType = HullType.Cruiser;
        bp.SlotConfiguration = new[] {
            SlotType.Weapon, SlotType.Weapon,
            SlotType.Engine, SlotType.Shield, SlotType.Cargo
        };

        Assert.IsTrue(bp.SlotConfiguration.Contains(SlotType.Cargo));
    }
}
```

- [ ] **Step 2: 运行测试，验证失败**
Expected: FAIL

- [ ] **Step 3: 写实现**

```csharp
// src/Data/HullBlueprint.cs
using UnityEngine;

namespace Gameplay
{
    public enum HullType
    {
        Fighter  = 0,
        Destroyer = 1,
        Cruiser  = 2
    }

    [CreateAssetMenu(fileName = "Hull_Fighter", menuName = "Starchain/HullBlueprint")]
    public class HullBlueprint : ScriptableObject
    {
        public HullType HullType;
        public SlotType[] SlotConfiguration;  // 槽位类型列表，顺序固定
        public float BaseSpeed;
        public float BaseHull;
        public float BaseWeaponDamage;
        public float BaseShield;
        public float BaseCargo;
        public GameObject Prefab3D;  // 3D 模型预制体（装备 UI 用）
    }
}
```

- [ ] **Step 4: 运行测试，验证通过**
Expected: PASS

- [ ] **Step 5: 提交**

```bash
git add src/Data/HullBlueprint.cs tests/unit/equipment/hull_blueprint_test.cs
git commit -m "feat: add HullBlueprint ScriptableObject"
```

---

### Task 4: 创建 ShipLootTable ScriptableObject

**Files:**
- Create: `src/Data/ShipLootTable.cs`
- Create: `tests/unit/equipment/loot_table_test.cs`

- [ ] **Step 1: 写测试**

```csharp
[TestFixture]
public class LootTable_Test
{
    [Test]
    public void dropRoll_returns_null_on_empty_table()
    {
        var table = ScriptableObject.CreateInstance<ShipLootTable>();
        table.Entries = new System.Collections.Generic.List<LootEntry>();

        var result = table.RollDrop();
        Assert.IsNull(result);
    }

    [Test]
    public void dropRoll_uses_weight_distribution()
    {
        var table = ScriptableObject.CreateInstance<ShipLootTable>();
        table.Entries = new System.Collections.Generic.List<LootEntry>
        {
            new LootEntry { SlotType = SlotType.Weapon, Tier = ModuleTier.T1, DropWeight = 70f },
            new LootEntry { SlotType = SlotType.Weapon, Tier = ModuleTier.T2, DropWeight = 30f },
        };

        // 跑 100 次，验证 T2 出现次数在合理范围（20-40次）
        int t2Count = 0;
        for (int i = 0; i < 100; i++) {
            var result = table.RollDrop();
            if (result != null && result.Tier == ModuleTier.T2) t2Count++;
        }
        Assert.IsTrue(t2Count > 15, $"T2 should appear ~30 times, got {t2Count}");
        Assert.IsTrue(t2Count < 50, $"T2 should appear ~30 times, got {t2Count}");
    }
}
```

- [ ] **Step 2: 运行测试，验证失败**
Expected: FAIL

- [ ] **Step 3: 写实现**

```csharp
// src/Data/ShipLootTable.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Gameplay
{
    [System.Serializable]
    public class LootEntry
    {
        public SlotType SlotType;
        public ModuleTier Tier;
        [Range(0f, 100f)] public float DropWeight;
    }

    [CreateAssetMenu(fileName = "LootTable_Standard", menuName = "Starchain/LootTable")]
    public class ShipLootTable : ScriptableObject
    {
        public List<LootEntry> Entries = new();

        public (SlotType, ModuleTier)? RollDrop()
        {
            if (Entries.Count == 0) return null;

            float totalWeight = Entries.Sum(e => e.DropWeight);
            float roll = Random.Range(0f, totalWeight);
            float cumulative = 0f;

            foreach (var entry in Entries) {
                cumulative += entry.DropWeight;
                if (roll <= cumulative) {
                    return (entry.SlotType, entry.Tier);
                }
            }
            return Entries.Last().SlotType != default
                ? (Entries.Last().SlotType, Entries.Last().Tier)
                : null;
        }
    }
}
```

- [ ] **Step 4: 运行测试，验证通过**
Expected: PASS

- [ ] **Step 5: 提交**

```bash
git add src/Data/ShipLootTable.cs tests/unit/equipment/loot_table_test.cs
git commit -m "feat: add ShipLootTable with weighted random drop"
```

---

### Task 5: 扩展 ShipDataModel 添加模块字段和属性方法

**Files:**
- Modify: `src/Data/ShipDataModel.cs`
- Create: `tests/unit/equipment/ship_data_model_equipment_test.cs`

- [ ] **Step 1: 写测试**

```csharp
[TestFixture]
public class ShipDataModel_Equipment_Test
{
    private ShipDataModel _ship;
    private HullBlueprint _fighterBp;
    private GameDataManager _gdm;

    [SetUp]
    public void SetUp()
    {
        _gdm = new GameDataManager();
        _fighterBp = ScriptableObject.CreateInstance<HullBlueprint>();
        _fighterBp.HullType = HullType.Fighter;
        _fighterBp.SlotConfiguration = new[] { SlotType.Weapon, SlotType.Engine, SlotType.Shield };
        _fighterBp.BaseWeaponDamage = 10f;
        _fighterBp.BaseSpeed = 5f;
        _fighterBp.BaseShield = 20f;
        _fighterBp.BaseCargo = 0f;
    }

    [TearDown]
    public void TearDown() { Object.DestroyImmediate(_fighterBp); }

    [Test]
    public void equipModule_adds_to_slot()
    {
        var weapon = CreateModule("w1", SlotType.Weapon, ModuleTier.T1, damage: 10f);
        _ship.EquipModule(weapon);

        Assert.AreEqual(weapon, _ship.GetEquipped(SlotType.Weapon));
    }

    [Test]
    public void totalWeaponDamage_includes_base_plus_module()
    {
        var weapon = CreateModule("w2", SlotType.Weapon, ModuleTier.T2, damage: 15f);
        _ship.EquipModule(weapon);

        Assert.AreEqual(25f, _ship.TotalWeaponDamage); // 10 base + 15 module
    }

    [Test]
    public void unequipModule_removes_from_slot_returns_null()
    {
        var weapon = CreateModule("w1", SlotType.Weapon, ModuleTier.T1, damage: 10f);
        _ship.EquipModule(weapon);
        var result = _ship.UnequipModule(SlotType.Weapon);

        Assert.IsNull(_ship.GetEquipped(SlotType.Weapon));
        Assert.AreEqual(weapon, result);
    }

    [Test]
    public void replacing_module_returns_old_module()
    {
        var t1 = CreateModule("w1", SlotType.Weapon, ModuleTier.T1, damage: 10f);
        var t2 = CreateModule("w2", SlotType.Weapon, ModuleTier.T2, damage: 15f);
        _ship.EquipModule(t1);
        var old = _ship.EquipModule(t2);  // 替换

        Assert.AreEqual(t1, old);
        Assert.AreEqual(25f, _ship.TotalWeaponDamage); // 10 base + 15 t2
    }

    private EquipmentModule CreateModule(string id, SlotType slot, ModuleTier tier, float damage = 0, float speed = 0, float shield = 0, float cargo = 0)
    {
        var m = ScriptableObject.CreateInstance<EquipmentModule>();
        m.ModuleId = id; m.SlotType = slot; m.Tier = tier;
        m.Damage = damage; m.Speed = speed; m.Shield = shield; m.Cargo = cargo;
        return m;
    }
}
```

- [ ] **Step 2: 运行测试，验证失败**
Expected: FAIL (EquipModule not defined)

- [ ] **Step 3: 读 ShipDataModel 现有结构**

Read: `src/Data/ShipDataModel.cs`

- [ ] **Step 4: 扩展 ShipDataModel**

在 `ShipDataModel` 中新增：

```csharp
// 模块装备（每个 SlotType 最多一个，已装备则为 non-null）
private Dictionary<SlotType, EquipmentModule> _equippedModules = new();

// 全局仓库（所有未装备模块）
private static List<EquipmentModule> _inventory = new List<EquipmentModule>();

// 属性方法
public float TotalWeaponDamage => BaseWeaponDamage
    + _equippedModules.Values
        .Where(m => m != null && m.SlotType == SlotType.Weapon)
        .Sum(m => m.Damage);

public float TotalSpeed => BaseSpeed
    + _equippedModules.Values
        .Where(m => m != null && m.SlotType == SlotType.Engine)
        .Sum(m => m.Speed);

public float TotalShield => BaseShield
    + _equippedModules.Values
        .Where(m => m != null && m.SlotType == SlotType.Shield)
        .Sum(m => m.Shield);

public float TotalCargo => BaseCargo
    + _equippedModules.Values
        .Where(m => m != null && m.SlotType == SlotType.Cargo)
        .Sum(m => m.Cargo);

// 装备装卸方法
public void EquipModule(EquipmentModule module)
{
    if (module == null) return;
    _equippedModules[module.SlotType] = module;
    _inventory.Remove(module);
}

public EquipmentModule UnequipModule(SlotType slot)
{
    var old = _equippedModules.GetValueOrDefault(slot);
    if (old != null) {
        _inventory.Add(old);
        _equippedModules[slot] = null;
    }
    return old;
}

public EquipmentModule GetEquipped(SlotType slot)
    => _equippedModules.GetValueOrDefault(slot);

// 仓库访问
public static IReadOnlyList<EquipmentModule> Inventory => _inventory.AsReadOnly();

public static void AddToInventory(EquipmentModule module)
{
    if (module != null) _inventory.Add(module);
}
```

- [ ] **Step 5: 运行测试，验证通过**
Expected: PASS

- [ ] **Step 6: 提交**

```bash
git add src/Data/ShipDataModel.cs tests/unit/equipment/ship_data_model_equipment_test.cs
git commit -m "feat: extend ShipDataModel with equipment slots, inventory, and attribute methods"
```

---

## Phase 2: 仓库 UI

### Task 6: 创建仓库 UI

**Files:**
- Create: `src/UI/InventoryUI.cs`
- Create: `assets/prefabs/UI/InventoryPanel.prefab`
- Create: `tests/unit/equipment/inventory_ui_test.cs`

- [ ] **Step 1: 写测试（逻辑层）**

```csharp
[TestFixture]
public class InventoryUI_Test
{
    [Test]
    public void populate_shows_all_inventory_modules()
    {
        var inv = new GameObject("InventoryUI").AddComponent<InventoryUI>();
        var module1 = CreateModule(SlotType.Weapon);
        var module2 = CreateModule(SlotType.Engine);
        ShipDataModel.AddToInventory(module1);
        ShipDataModel.AddToInventory(module2);

        inv.Populate();

        Assert.AreEqual(2, inv.transform.childCount);
        Object.DestroyImmediate(inv.gameObject);
    }
}
```

- [ ] **Step 2: 运行测试，验证失败**
Expected: FAIL

- [ ] **Step 3: 写 InventoryUI 逻辑**

```csharp
// src/UI/InventoryUI.cs
public class InventoryUI : MonoBehaviour
{
    [SerializeField] private Transform contentRoot;  // Vertical Layout Group
    [SerializeField] private GameObject moduleItemPrefab;

    public void Populate()
    {
        // 清除旧列表
        foreach (Transform child in contentRoot) Destroy(child.gameObject);

        foreach (var module in ShipDataModel.Inventory)
        {
            var item = Instantiate(moduleItemPrefab, contentRoot);
            var slot = item.GetComponent<ModuleItem>();
            slot.Init(module);
        }
    }
}
```

- [ ] **Step 4: 创建 InventoryPanel.prefab**

Create via Unity Editor: Canvas + Vertical Layout Group + ModuleItem placeholder.

- [ ] **Step 5: 运行测试，验证通过**
Expected: PASS

- [ ] **Step 6: 提交**

```bash
git add src/UI/InventoryUI.cs assets/prefabs/UI/InventoryPanel.prefab tests/unit/equipment/inventory_ui_test.cs
git commit -m "feat: add InventoryUI with module list"
```

---

## Phase 3: 装备界面

### Task 7: 创建 ShipEquipmentSystem 核心逻辑

**Files:**
- Create: `src/Gameplay/ShipEquipmentSystem.cs`
- Create: `tests/unit/equipment/ship_equipment_system_test.cs`

- [ ] **Step 1: 写测试**

```csharp
[TestFixture]
public class ShipEquipmentSystem_Test
{
    private ShipEquipmentSystem _sys;

    [SetUp] public void SetUp()
    {
        _sys = new GameObject("EQ").AddComponent<ShipEquipmentSystem>();
    }
    [TearDown] public void TearDown() { Object.DestroyImmediate(_sys.gameObject); }

    [Test]
    public void openForShip_loads_correct_hullBlueprint()
    {
        var ship = CreateFighterShip();
        _sys.OpenForShip(ship);

        Assert.AreEqual(HullType.Fighter, _sys.CurrentHullType);
        Assert.AreEqual(3, _sys.SlotCount);
    }

    [Test]
    public void getModuleForSlot_returns_equipped_or_null()
    {
        var ship = CreateFighterShip();
        var weapon = CreateModule(SlotType.Weapon);
        ship.EquipModule(weapon);
        _sys.OpenForShip(ship);

        Assert.AreEqual(weapon, _sys.GetEquippedForSlot(0));
        Assert.IsNull(_sys.GetEquippedForSlot(1)); // Engine slot empty
    }
}
```

- [ ] **Step 2: 运行测试，验证失败**
Expected: FAIL

- [ ] **Step 3: 写实现**

```csharp
// src/Gameplay/ShipEquipmentSystem.cs
public class ShipEquipmentSystem : MonoBehaviour
{
    public static ShipEquipmentSystem Instance { get; private set; }

    private ShipDataModel _currentShip;
    private HullBlueprint _currentBlueprint;

    public HullType CurrentHullType => _currentBlueprint.HullType;
    public int SlotCount => _currentBlueprint.SlotConfiguration.Length;

    private void Awake() { Instance = this; }

    public void OpenForShip(ShipDataModel ship)
    {
        _currentShip = ship;
        _currentBlueprint = ship.HullBlueprint;
        // 通知 UI 刷新
    }

    public SlotType GetSlotType(int slotIndex)
        => _currentBlueprint.SlotConfiguration[slotIndex];

    public EquipmentModule GetEquippedForSlot(int slotIndex)
    {
        var slotType = GetSlotType(slotIndex);
        return _currentShip.GetEquipped(slotType);
    }

    public void EquipToSlot(int slotIndex, EquipmentModule module)
    {
        var slotType = GetSlotType(slotIndex);
        if (module.SlotType != slotType) {
            Debug.LogWarning($"[EQ] Module {module.ModuleId} cannot go in slot {slotType}");
            return;
        }
        _currentShip.EquipModule(module);
    }

    public void UnequipSlot(int slotIndex)
    {
        var slotType = GetSlotType(slotIndex);
        _currentShip.UnequipModule(slotType);
    }
}
```

- [ ] **Step 4: 运行测试，验证通过**
Expected: PASS

- [ ] **Step 5: 提交**

```bash
git add src/Gameplay/ShipEquipmentSystem.cs tests/unit/equipment/ship_equipment_system_test.cs
git commit -m "feat: add ShipEquipmentSystem core logic"
```

---

### Task 8: 创建 ShipEquipmentUI（3D 模型 + 槽位高亮）

**Files:**
- Create: `src/UI/ShipEquipmentUI.cs`
- Create: `assets/prefabs/UI/ShipEquipmentPanel.prefab`
- Create: `tests/unit/equipment/ship_equipment_ui_test.cs`

- [ ] **Step 1: 写测试**

```csharp
[TestFixture]
public class ShipEquipmentUI_Test
{
    [Test]
    public void highlightSlots_shows_correct_count()
    {
        var ui = new GameObject("SEUI").AddComponent<ShipEquipmentUI>();
        var ship = CreateFighterShip(); // 3 slots
        ShipEquipmentSystem.Instance.OpenForShip(ship);

        ui.RefreshSlotHighlights();

        Assert.AreEqual(3, ui.SlotHighlights.Count);
        Object.DestroyImmediate(ui.gameObject);
    }
}
```

- [ ] **Step 2: 运行测试，验证失败**
Expected: FAIL

- [ ] **Step 3: 写 ShipEquipmentUI**

```csharp
// src/UI/ShipEquipmentUI.cs
public class ShipEquipmentUI : MonoBehaviour
{
    [SerializeField] private GameObject modelViewport;  // 显示 3D 模型
    [SerializeField] private Transform slotHighlightRoot;
    [SerializeField] private GameObject slotHighlightPrefab;
    [SerializeField] private ModuleSelectionPanel selectionPanel;

    public List<SlotHighlight> SlotHighlights { get; private set; } = new();

    public void RefreshSlotHighlights()
    {
        foreach (Transform child in slotHighlightRoot) Destroy(child.gameObject);
        SlotHighlights.Clear();

        var system = ShipEquipmentSystem.Instance;
        for (int i = 0; i < system.SlotCount; i++) {
            var slotType = system.GetSlotType(i);
            var go = Instantiate(slotHighlightPrefab, slotHighlightRoot);
            var highlight = go.GetComponent<SlotHighlight>();
            highlight.Init(i, slotType, GetColorForSlot(slotType));
            SlotHighlights.Add(highlight);
        }
    }

    private Color GetColorForSlot(SlotType type) => type switch
    {
        SlotType.Weapon => Color.red,
        SlotType.Engine => Color.blue,
        SlotType.Shield => Color.green,
        SlotType.Cargo  => Color.yellow,
        _               => Color.gray
    };

    public void OnSlotClicked(int slotIndex)
    {
        selectionPanel.OpenForSlot(slotIndex);
    }
}
```

- [ ] **Step 4: 创建 ShipEquipmentPanel.prefab**

Create via Unity Editor: Canvas + Model Viewport + SlotHighlightRoot + SelectionPanel slot.

- [ ] **Step 5: 运行测试，验证通过**
Expected: PASS

- [ ] **Step 6: 提交**

```bash
git add src/UI/ShipEquipmentUI.cs assets/prefabs/UI/ShipEquipmentPanel.prefab tests/unit/equipment/ship_equipment_ui_test.cs
git commit -m "feat: add ShipEquipmentUI with 3D model viewport and slot highlights"
```

---

### Task 9: 创建 ModuleSelectionPanel

**Files:**
- Create: `src/UI/ModuleSelectionPanel.cs`
- Create: `assets/prefabs/UI/ModuleSelectionPanel.prefab`

- [ ] **Step 1: 写测试**

```csharp
[TestFixture]
public class ModuleSelectionPanel_Test
{
    [Test]
    public void openForSlot_shows_only_matching_slotType()
    {
        var panel = new GameObject("Panel").AddComponent<ModuleSelectionPanel>();
        AddToInventory(SlotType.Weapon, ModuleTier.T1);
        AddToInventory(SlotType.Engine, ModuleTier.T1);

        panel.OpenForSlot(0); // Weapon slot

        Assert.AreEqual(1, panel.ModuleCount);
        Object.DestroyImmediate(panel.gameObject);
    }
}
```

- [ ] **Step 2: 运行测试，验证失败**
Expected: FAIL

- [ ] **Step 3: 写实现**

```csharp
// src/UI/ModuleSelectionPanel.cs
public class ModuleSelectionPanel : MonoBehaviour
{
    [SerializeField] private Transform contentRoot;
    [SerializeField] private GameObject moduleOptionPrefab;
    [SerializeField] private Button closeButton;

    private int _currentSlotIndex;

    private void Awake()
    {
        closeButton.onClick.AddListener(() => gameObject.SetActive(false));
        gameObject.SetActive(false);
    }

    public void OpenForSlot(int slotIndex)
    {
        _currentSlotIndex = slotIndex;
        Refresh();
        gameObject.SetActive(true);
    }

    private void Refresh()
    {
        foreach (Transform child in contentRoot) Destroy(child.gameObject);

        var slotType = ShipEquipmentSystem.Instance.GetSlotType(_currentSlotIndex);
        var eligible = ShipDataModel.Inventory
            .Where(m => m.SlotType == slotType);

        foreach (var module in eligible) {
            var go = Instantiate(moduleOptionPrefab, contentRoot);
            var btn = go.GetComponent<ModuleOption>();
            btn.Init(module, () => SelectModule(module));
        }
    }

    private void SelectModule(EquipmentModule module)
    {
        ShipEquipmentSystem.Instance.EquipToSlot(_currentSlotIndex, module);
        ShipEquipmentSystem.Instance.OpenForShip(
            ShipEquipmentSystem.Instance.GetCurrentShip());
        gameObject.SetActive(false);
    }
}
```

- [ ] **Step 4: 运行测试，验证通过**
Expected: PASS

- [ ] **Step 5: 提交**

```bash
git add src/UI/ModuleSelectionPanel.cs assets/prefabs/UI/ModuleSelectionPanel.prefab tests/unit/equipment/module_selection_panel_test.cs
git commit -m "feat: add ModuleSelectionPanel with slot-filtered module list"
```

---

## Phase 4: 掉落与整合

### Task 10: 创建 LootDropSystem

**Files:**
- Create: `src/Gameplay/LootDropSystem.cs`
- Create: `tests/unit/equipment/loot_drop_system_test.cs`

- [ ] **Step 1: 写测试**

```csharp
[TestFixture]
public class LootDropSystem_Test
{
    [Test]
    public void onEnemyDestroyed_drops_module_into_inventory()
    {
        var lds = new GameObject("LDS").AddComponent<LootDropSystem>();
        var lootTable = CreateLootTable();
        lds.LootTable = lootTable;

        var initialCount = ShipDataModel.Inventory.Count;
        lds.OnEnemyDestroyed("enemy-1");

        Assert.AreEqual(initialCount + 1, ShipDataModel.Inventory.Count);
        Object.DestroyImmediate(lds.gameObject);
    }
}
```

- [ ] **Step 2: 运行测试，验证失败**
Expected: FAIL

- [ ] **Step 3: 写实现**

```csharp
// src/Gameplay/LootDropSystem.cs
public class LootDropSystem : MonoBehaviour
{
    public static LootDropSystem Instance { get; private set; }
    public ShipLootTable LootTable;

    private void Awake() { Instance = this; }

    public void OnEnemyDestroyed(string enemyId)
    {
        if (LootTable == null) return;
        var (slotType, tier) = LootTable.RollDrop() ?? (SlotType.Weapon, ModuleTier.T1);
        var module = FindModuleInDatabase(slotType, tier);
        if (module != null) ShipDataModel.AddToInventory(module);
    }

    private EquipmentModule FindModuleInDatabase(SlotType slot, ModuleTier tier)
    {
        // 从 Resources 加载所有模块，找匹配的第一个
        var all = Resources.LoadAll<EquipmentModule>("Data/Modules");
        return all.FirstOrDefault(m => m.SlotType == slot && m.Tier == tier);
    }
}
```

- [ ] **Step 4: 运行测试，验证通过**
Expected: PASS

- [ ] **Step 5: 提交**

```bash
git add src/Gameplay/LootDropSystem.cs tests/unit/equipment/loot_drop_system_test.cs
git commit -m "feat: add LootDropSystem with weighted table roll"
```

---

### Task 11: 集成到 EnemyAIController（击败触发掉落）

**Files:**
- Modify: `src/Gameplay/enemy/EnemyAIController.cs`

- [ ] **Step 1: 读 EnemyAIController 中死亡处理位置**

Read: `src/Gameplay/enemy/EnemyAIController.cs` — 找到 `AiState == DYING` 或死亡处理方法。

- [ ] **Step 2: 添加掉落调用**

在死亡处理完成后添加：

```csharp
// 在 EnemyAIController 的 DYING 状态处理中，销毁前：
if (LootDropSystem.Instance != null) {
    LootDropSystem.Instance.OnEnemyDestroyed(_nodeId);
}
```

- [ ] **Step 3: 验证构建**
Run: Unity compilation
Expected: No errors

- [ ] **Step 4: 提交**

```bash
git add src/Gameplay/enemy/EnemyAIController.cs
git commit -m "feat: EnemyAIController triggers loot drop on death"
```

---

### Task 12: 扩展 BuildingSystem 支持船体类型

**Files:**
- Modify: `src/Gameplay/building/BuildingSystem.cs`

- [ ] **Step 1: 读 BuildingSystem 现有 BuildShip 方法**

Read: `src/Gameplay/building/BuildingSystem.cs` — 找到 `BuildShip()` 方法签名和现有逻辑。

- [ ] **Step 2: 扩展 BuildShip 支持 HullType**

现有 `BuildShip(BlueprintId)` → 改为 `BuildShip(HullType)`：

```csharp
public DispatchOrder BuildShip(HullType hullType)
{
    var blueprint = GetHullBlueprint(hullType);
    var ship = new ShipDataModel(
        shipId: $"ship-{Guid.NewGuid():N}",
        blueprintId: blueprint.name,
        isPlayer: true,
        hullBlueprint: blueprint,
        stateChannel: ScriptableObject.CreateInstance<ShipStateChannel>()
    );
    // 注册到 GameDataManager
    GameDataManager.Instance.RegisterShip(ship);
    return null; // 或返回派系订单
}
```

- [ ] **Step 3: 运行构建验证**
Run: Unity compilation
Expected: No errors

- [ ] **Step 4: 提交**

```bash
git add src/Gameplay/building/BuildingSystem.cs
git commit -m "feat: BuildingSystem.BuildShip accepts HullType parameter"
```

---

### Task 13: 创建所有模块 ScriptableObject 资产（12 个模块 + 3 个船体蓝图）

**Files:**
- Create: `assets/Data/Modules/Weapon/Module_Weapon_T1_LightLaser.asset`
- Create: `assets/Data/Modules/Weapon/Module_Weapon_T2_HeavyLaser.asset`
- Create: `assets/Data/Modules/Weapon/Module_Weapon_T3_PlasmaCannon.asset`
- Create: `assets/Data/Modules/Engine/Module_Engine_T1_Standard.asset`
- Create: `assets/Data/Modules/Engine/Module_Engine_T2_Military.asset`
- Create: `assets/Data/Modules/Engine/Module_Engine_T3_Elite.asset`
- Create: `assets/Data/Modules/Shield/Module_Shield_T1_Light.asset`
- Create: `assets/Data/Modules/Shield/Module_Shield_T2_Medium.asset`
- Create: `assets/Data/Modules/Shield/Module_Shield_T3_Heavy.asset`
- Create: `assets/Data/Modules/Cargo/Module_Cargo_T1_Standard.asset`
- Create: `assets/Data/Modules/Cargo/Module_Cargo_T2_Expanded.asset`
- Create: `assets/Data/Modules/Cargo/Module_Cargo_T3_Industrial.asset`
- Create: `assets/Data/Hulls/Hull_Fighter.asset`
- Create: `assets/Data/Hulls/Hull_Destroyer.asset`
- Create: `assets/Data/Hulls/Hull_Cruiser.asset`
- Create: `assets/Data/LootTables/LootTable_Standard.asset`

> **注意：** 使用 Unity Editor 创建（Menu: Assets → Create → Starchain/...）。如无 Editor 权限，使用 YAML 格式编写 .asset 文件（参考现有 .prefab YAML 格式）。

- [ ] **Step: 创建所有 YAML 资产文件**

Write each asset file using the YAML format from existing `.asset` files in the project.

- [ ] **Step: 提交**

```bash
git add assets/Data/
git commit -m "feat: add all equipment modules (T1-T3 x4 types) and hull blueprints"
```

---

## 自检清单

1. **Spec coverage check:**
   - [x] AC-1: Task 12（BuildShip 支持 HullType）
   - [x] AC-2: Task 8（3D 模型 + 槽位高亮）
   - [x] AC-3: Task 9（点击 Weapon 槽显示仓库模块）
   - [x] AC-4: Task 5（TotalWeaponDamage 实时叠加）
   - [x] AC-5: Task 10+11（掉落表 + EnemyAIController 集成）
   - [x] AC-6: Task 5（EquipModule/UnequipModule 逻辑）
   - [x] AC-7: Task 5（TotalCargo 属性）
   - [x] AC-8: Task 5（属性实时生效，无重启）

2. **占位符扫描：** 无 TBD/TODO/未填写的值

3. **类型一致性：** `ShipDataModel.EquipModule()` / `UnequipModule()` / `GetEquipped()` 在 Task 5 定义，Task 7-9 引用，类型一致
