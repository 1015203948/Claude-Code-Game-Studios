# Technical Preferences

<!-- Populated by /setup-engine. Updated as the user makes decisions throughout development. -->
<!-- All agents reference this file for project-specific standards and conventions. -->

## Engine & Language

- **Engine**: Unity 6.3 LTS
- **Language**: C#
- **Rendering**: Universal Render Pipeline (URP) — 移动端必须使用
- **Physics**: PhysX (Unity 6 默认)

## Input & Platform

<!-- Written by /setup-engine. Read by /ux-design, /ux-review, /test-setup, /team-ui, and /dev-story -->
<!-- to scope interaction specs, test helpers, and implementation to the correct input methods. -->

- **Target Platforms**: Android（手机 / 平板）
- **Input Methods**: Touch
- **Primary Input**: Touch
- **Gamepad Support**: None
- **Touch Support**: Full
- **Platform Notes**: 全触屏优先 UI，所有交互必须支持手指触控；禁止仅依赖 hover 状态的交互；适配手机和平板两种屏幕比例（16:9 和 4:3 均需可用）

## Naming Conventions

- **Classes**: PascalCase（如 `PlayerShip`、`StarMapNode`）
- **Public fields/properties**: PascalCase（如 `MoveSpeed`、`HullPoints`）
- **Private fields**: `_camelCase`（如 `_currentHealth`、`_isWarping`）
- **Methods**: PascalCase（如 `TakeDamage()`、`GetResourceOutput()`）
- **Files**: PascalCase 与类名一致（如 `PlayerShip.cs`）
- **Scenes/Prefabs**: PascalCase（如 `StarMapScene.unity`、`ShipPrefab.prefab`）
- **Constants**: PascalCase 或 UPPER_SNAKE_CASE

## Performance Budgets

- **Target Framerate**: 60fps
- **Frame Budget**: 16.6ms
- **Draw Calls**: < 200（移动端目标）
- **Memory Ceiling**: 待定（视目标设备而定——开发中期设置）

## Testing

- **Framework**: Unity Test Framework（内置 NUnit，支持 EditMode 和 PlayMode 测试）
- **Minimum Coverage**: 待定
- **Required Tests**: Balance formulas, gameplay systems, networking (if applicable)
- **EditMode tests**: 用于逻辑/公式/数据验证（不需要场景加载）
- **PlayMode tests**: 用于跨系统集成测试（需要场景和帧循环）

## Forbidden Patterns

<!-- Add patterns that should never appear in this project's codebase -->
- [None configured yet — add as architectural decisions are made]

## Allowed Libraries / Addons

<!-- Add approved third-party dependencies here -->
- [None configured yet — add as dependencies are approved]

## Architecture Decisions Log

<!-- Quick reference linking to full ADRs in docs/architecture/ -->
- [No ADRs yet — use /architecture-decision to create one]

## Engine Specialists

<!-- Written by /setup-engine when engine is configured. -->
<!-- Read by /code-review, /architecture-decision, /architecture-review, and team skills -->
<!-- to know which specialist to spawn for engine-specific validation. -->

- **Primary**: unity-specialist
- **Language/Code Specialist**: unity-specialist（C# 代码审查 — primary 覆盖）
- **Shader Specialist**: unity-shader-specialist（Shader Graph、HLSL、URP/HDRP 材质）
- **UI Specialist**: unity-ui-specialist（UI Toolkit UXML/USS、UGUI Canvas、运行时 UI）
- **Additional Specialists**: unity-dots-specialist（ECS、Jobs 系统、Burst 编译器）、unity-addressables-specialist（资产加载、内存管理、内容目录）
- **Routing Notes**: 架构决策和通用 C# 代码审查调用 primary。ECS/Jobs/Burst 代码调用 DOTS specialist。渲染和视觉特效调用 shader specialist。所有界面实现调用 UI specialist。资产管理系统调用 Addressables specialist。

### File Extension Routing

<!-- Skills use this table to select the right specialist per file type. -->
<!-- If a row says [TO BE CONFIGURED], fall back to Primary for that file type. -->

| File Extension / Type | Specialist to Spawn |
|-----------------------|---------------------|
| Game code (.cs files) | unity-specialist |
| Shader / material files (.shader, .shadergraph, .mat) | unity-shader-specialist |
| UI / screen files (.uxml, .uss, Canvas prefabs) | unity-ui-specialist |
| Scene / prefab / level files (.unity, .prefab) | unity-specialist |
| Native extension / plugin files (.dll, native plugins) | unity-specialist |
| General architecture review | unity-specialist |
