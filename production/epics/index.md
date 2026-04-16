# Epics Index

**Last Updated**: 2026-04-14
**Engine**: Unity 6.3 LTS
**Control Manifest Version**: 2026-04-14

| Epic | Layer | Architecture Modules | GDD | Stories | Status |
|------|-------|---------------------|-----|---------|--------|
| `foundation-infrastructure` | Foundation | 数据模型框架 · 事件总线 · SimClock | ship-system, star-map-system, dual-perspective-switching | Not yet created | ✅ Ready |
| `foundation-runtime` | Foundation | 场景管理 · 输入系统 · 叠加渲染 | dual-perspective-switching, ship-control-system | Not yet created | ✅ Ready |
| `core-gameplay` | Core+Feature | 殖民地 · 舰队调度 · 战斗 · 敌人 · 生命值 | ship-health, building, colony, ship-combat, enemy, fleet-dispatch | Not yet created | ✅ Ready |

---

## Epic Dependency Graph

```
foundation-infrastructure  ←（Epic A，基础设施）
  ├─ 数据模型框架（GameDataManager、ShipDataModel、Blueprint、StarMapData）
  ├─ 事件总线（SO Channel 规范）
  └─ SimClock（策略层时间单例）

foundation-runtime  ←（Epic B，依赖 Epic A）
  ├─ 场景管理（ViewLayerManager、CockpitScene 加载/卸载）
  ├─ 输入系统（ShipInputManager、双 ActionMap 切换）
  └─ 叠加渲染（StarMapOverlayController、ScreenOverlay）
```

---

## Layer Progress

| Layer | Epics | Status |
|-------|-------|--------|
| Foundation | 2 | ✅ 2/2 epic created |
| Core+Feature | 1 | ✅ 1/1 epic created (stories blocked until ADRs Accepted) |
| Feature | — | Not started |
| Presentation | — | Not started |

---

## Next Steps

1. `/create-stories foundation-infrastructure` — Epic A story breakdown
2. `/create-stories foundation-runtime` — Epic B story breakdown
3. `/architecture-review` — validate ADR-0013~0017 and advance to Accepted (fresh session)
4. `/create-stories core-gameplay` — Epic C story breakdown (after ADRs Accepted)
5. `/gate-check production` — Pre-Production → Production gate
