# X4 飞船模型导入指南

## 模型文件

| 文件 | 大小 | 来源 | 状态 |
|------|------|------|------|
| `Ships/ship_fighter_geom.glb` | 27 MB | Blender 导出（无材质） | 可用 |
| `Ships/ship_destroyer.glb` | 187 MB | Blender 导出（完整材质） | 可用 |
| `Ships/ship_arg_s_fighter_01.dae` | 13 MB | X4 DAE 转换包（S级战斗机） | ✅ 验证通过，Unity 可直接导入 |
| `Ships/ship_arg_l_destroyer_01.dae` | 14 MB | X4 DAE 转换包（L级驱逐舰） | ✅ 验证通过，Unity 可直接导入 |
| `Ships/ship_bor_s_fighter_01.blend` | 315 MB | X4 原始 .blend（需 Blender） | 可用 |
| `Ships/ship_bor_l_destroyer_01.blend` | 382 MB | X4 原始 .blend（需 Blender） | 可用 |
| `D:\X4 mod tool\ship_bor_s_fighter_01.blend` | 315 MB | X4 原始 .blend | 可用（需 Blender） |
| `D:\X4 mod tool\ship_bor_l_destroyer_01.blend` | 382 MB | X4 原始 .blend | 可用（需 Blender） |

## 推荐导入方式

### 方式 A：直接导入 .blend（推荐）

Unity 可以直接导入 .blend 文件（需要安装 Blender）。

1. **复制 .blend 到项目**
   ```
   复制 D:\X4 mod tool\ship_bor_s_fighter_01.blend → Assets/Models/Ships/
   复制 D:\X4 mod tool\ship_bor_l_destroyer_01.blend → Assets/Models/Ships/
   ```

2. **Unity 自动导入**
   - 打开 Unity，等待导入完成
   - 在 Project 窗口中，展开 .blend 文件
   - 你会看到里面的网格和材质

3. **优势**
   - 保留完整材质
   - 保留所有 LOD
   - 无需中间转换

### 方式 B：使用预导出的 GLB

项目已包含 GLB 文件（`Assets/Models/Ships/`），可直接使用。

**注意**：`ship_fighter_geom.glb` 没有材质（Blender 4.2 GLTF 导出 Bug），需要在 Unity 中手动分配材质。

## 快速配置（使用 Editor 工具）

1. 打开 Unity Editor
2. 菜单栏 → **Tools → Ship Model Setup**
3. 将导入的模型拖到 "Fighter Model" 槽位
4. 将 `EnemyShip.prefab` 拖到 "EnemyShip Prefab" 槽位
5. 在 Hierarchy 中选择 CockpitScene 的 PlayerShip，拖到 "PlayerShip" 槽位
6. 点击 **Configure** 按钮

## 手动配置

### EnemyShip 预制体

1. 打开 `Assets/Prefabs/EnemyShip.prefab`
2. 删除默认的 Cube（MeshFilter + MeshRenderer）
3. 将 fighter 模型拖为 EnemyShip 的子物体
4. 调整 Scale：`.blend` 文件单位很大，通常需要 `0.01` 左右
5. 调整位置，使模型中心与碰撞体对齐

### PlayerShip（CockpitScene）

1. 打开 `Assets/Scenes/CockpitScene.unity`
2. 选择 PlayerShip GameObject
3. 添加 fighter 模型作为子物体
4. 同样调整 Scale 和位置

## 材质说明

### Destroyer（驱逐舰）
- GLB 包含完整 PBR 材质
- 导入后自动识别

### Fighter（战斗机）
- GLB 无材质（导出 Bug）
- 导入后模型为灰色
- 建议创建一个简单材质：
  - Shader: URP/Lit
  - Base Color: `#4488CC`（蓝灰色）
  - Metallic: 0.6
  - Smoothness: 0.4

## 性能优化

| 模型 | 顶点数 | 建议 |
|------|--------|------|
| Fighter | ~50K | 移动端可用 |
| Destroyer | ~200K | 建议仅用于 PC/高端设备 |

如需降低多边形数：
1. 在 Unity 中选择模型
2. Inspector → Model → Mesh Compression: High
3. 或启用 LOD Group

## 文件位置

```
Assets/
└── Models/
    └── Ships/
        ├── ship_fighter_geom.glb        # 战斗机（无材质）
        ├── ship_destroyer.glb           # 驱逐舰（完整材质）
        └── README.md                    # 本文件
```

## 法律声明

X4: Foundations 资产版权归 Egosoft GmbH 所有。
本工具仅供个人学习研究使用，不得传播或用于商业目的。
