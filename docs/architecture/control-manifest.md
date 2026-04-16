# Control Manifest

> **Engine**: Unity 6.3 LTS
> **Last Updated**: 2026-04-14
> **Manifest Version**: 2026-04-14
> **ADRs Covered**: ADR-0001, ADR-0002, ADR-0003, ADR-0004, ADR-0007, ADR-0012
> **Status**: Active — regenerate with `/create-control-manifest update` when ADRs change

`Manifest Version` is the date this manifest was generated. Story files embed
this date when created. `/story-readiness` compares a story's embedded version
to this field to detect stories written against stale rules. Always matches
`Last Updated` — they are the same date, serving different consumers.

This manifest is a programmer's quick-reference extracted from all Accepted ADRs,
technical preferences, and engine reference docs. For the reasoning behind each
rule, see the referenced ADR.

---

## Foundation Layer Rules

*Applies to: scene management, event architecture, save/load, engine initialisation*

### Required Patterns

- **Scene topology must be MasterScene + StarMapScene + CockpitScene** — MasterScene and StarMapScene always loaded; CockpitScene additive on-demand — source: ADR-0001
- **ShipDataModel lives in MasterScene** as single authoritative source for hull/state — source: ADR-0001
- **ViewLayerManager manages ViewLayer enum** (STARMAP, COCKPIT, COCKPIT_WITH_OVERLAY) with `_isSwitching` flag — source: ADR-0001
- **Camera switching: use `Camera.enabled = false/true`** — NOT `camera.gameObject.SetActive(false)` — source: ADR-0001
- **UI hiding: UI Toolkit uses `style.display = DisplayStyle.None/Flex`**; UGUI Canvas uses `canvas.enabled` — source: ADR-0001
- **CockpitScene preload to 90%** at ON_SHIP_SELECT event — source: ADR-0001
- **Await UnloadSceneAsync AsyncOperation completion** before cleaning up references — source: ADR-0001
- **Tier 1 event communication (cross-scene) must use ScriptableObject Channel** stored in `assets/data/channels/` — source: ADR-0002
- **All event subscriptions must use OnEnable/OnDisable pairs** — NEVER use Awake/Start to subscribe with only OnDestroy to unsubscribe — source: ADR-0002 ADV-01/ADV-02
- **All async UniTask methods crossing await boundary must pass `this.destroyCancellationToken`** — source: ADR-0002 ADV-03
- **EnhancedTouchSupport ownership: ONLY ShipInputManager may call Enable()/Disable()** — no other system — source: ADR-0003
- **SimClock Script Execution Order must be -1000** (earliest) — source: ADR-0012

### Forbidden Approaches

- **Never use `camera.gameObject.SetActive(false)`** — causes URP to perform invalid Culling Pass — source: ADR-0001
- **Never use UnityEvent<T>** — reflection overhead, GC pressure, dangling references — source: ADR-0002
- **Never use Singleton EventBus with string keys** — no type safety, violates ADR-0001 Singleton ban — source: ADR-0002
- **Never use legacy `Input.*` class** — deprecated in Unity 6.3; use `com.unity.inputsystem` — source: ADR-0003, deprecated-apis.md
- **Never use `Time.timeScale` to control strategy layer time** — FixedUpdate frequency changes, cockpit Rigidbody physics breaks — source: ADR-0012

### Performance Guardrails

- **CockpitScene cold load**: max 1.0s — source: ADR-0001
- **CockpitScene hot load**: max 0.4s — source: ADR-0001
- **Event channel Raise() CPU**: < 0.01ms — source: ADR-0002
- **SimClock CPU overhead**: < 0.01ms/frame — source: ADR-0012

---

## Core Layer Rules

*Applies to: core gameplay loop, main player systems, physics, collision*

### Required Patterns

- **Config ScriptableObjects are read-only at runtime** — Inspector-tunable, no SO mutations during play — source: ADR-0004
- **BFS pathfinding via `StarMapPathfinder.FindPath()`** — O(V+E), deterministic lexicographic tie-breaking — source: ADR-0004
- **Production tick uses `UniTask.WaitForSeconds(1f, ignoreTimeScale: true)`** — real-time independence from time manipulation — source: ADR-0004
- **All data owned by GameDataManager.Instance in MasterScene** — source: ADR-0004
- **Rigidbody movement: use `Rigidbody.AddForce()`, NEVER write `Rigidbody.velocity` directly** — source: ADR-0003, deprecated-apis.md

### Forbidden Approaches

- **Never use `Rigidbody.velocity` direct write** — causes physics anomalies; use `AddForce()` — source: ADR-0003
- **Never store runtime state in ScriptableObjects** — SO mutations persist to disk in Editor; breaks read-only contract — source: ADR-0004
- **Never use `Physics.RaycastAll()`** — use `Physics.RaycastNonAlloc()` — source: deprecated-apis.md

### Performance Guardrails

- **BFS (<= 20 nodes)**: < 0.1ms — source: ADR-0004
- **Production tick**: 1Hz, negligible — source: ADR-0004

---

## Feature Layer Rules

*Applies to: secondary mechanics, AI systems, secondary features*

### Required Patterns

- **Strategy layer systems (ColonySystem, FleetDispatch, ResourceSystem) must use `SimClock.Instance.DeltaTime`** — NOT `Time.deltaTime` — source: ADR-0012
- **SimRate values are {0, 1, 5, 20} only** — validate in `SetRate()` with Assert; illegal values silently ignored — source: ADR-0012
- **SimRate = 0 is NOT game pause** — DeltaTime returns 0 but Update() still runs; physics still runs; cockpit still controllable — source: ADR-0012
- **SimClock does NOT belong to ViewLayerManager** — independent system; ViewLayerManager does not modify SimRate — source: ADR-0012

### Forbidden Approaches

- **CockpitScene must NOT contain `SimClock.Instance`** — cockpit physics uses `Time.deltaTime` only — source: ADR-0012
- **Strategy layer files must NOT contain bare `Time.deltaTime`** — must use SimClock.DeltaTime — source: ADR-0012
- **Never use `UniTask.WaitForSeconds` (without ignoreTimeScale)** for strategy-layer timing — source: ADR-0004
- **Never use ComponentSystem or JobComponentSystem** — use `ISystem` / `IJobEntity` (Entities 1.0+) — source: deprecated-apis.md

### Performance Guardrails

- **SimClock memory**: < 1KB total — source: ADR-0012
- **Fleet dispatch GC**: 1 DispatchOrder + 1 List<string> one-time — source: ADR-0004

---

## Presentation Layer Rules

*Applies to: rendering, audio, UI, VFX, shaders, animations*

### Required Patterns

- **NO second Camera for star map overlay** — URP performs full Culling Pass per Camera; doubles draw calls beyond mobile budget — source: ADR-0007
- **PanelSettings are ScriptableObject assets** — create in Inspector; switch via `UIDocument.panelSettings` property at runtime — source: ADR-0007
- **Sort Order hierarchy (set in PanelSettings assets, NOT at runtime)**: Cockpit HUD = 10; StarMap overlay = 20; Full-screen transition mask = 100 — source: ADR-0007
- **Wait for slide-out animation before switching back to CameraSpace** — 200ms delay after slide-out completes before changing panelSettings — source: ADR-0007
- **Touch input routing: ShipControlSystem pauses touch input when overlay is open** — ViewLayerManager handles routing, NOT StarMapOverlayController — source: ADR-0007
- **`_isSwitching` unchanged during overlay operation** — overlay open/close does not set `_isSwitching = true` — source: ADR-0007

### Forbidden Approaches

- **Never use `VisualElement.transform.position/rotation/scale`** — deprecated Unity 6.1; use `element.style.translate`, `element.style.rotate`, `element.style.scale` — source: ADR-0007, deprecated-apis.md
- **Never use `ExecuteDefaultAction()` or `ExecuteDefaultActionAtTarget()`** — use `HandleEventTrickleDown()` or `StopPropagation()` — source: deprecated-apis.md
- **Never use UGUI Canvas for new UI** — use UIDocument (UI Toolkit); Canvas is deprecated for new projects — source: deprecated-apis.md
- **Never use Text component or Image component** — use TextMeshPro or UI Toolkit VisualElement — source: deprecated-apis.md

### Performance Guardrails

- **Overlay open CPU**: ~+0.5ms/frame (extra UI layout pass) — source: ADR-0007
- **Overlay open draw calls**: ~+5 (UI batch) — source: ADR-0007
- **CockpitScene render**: 3–5ms/frame — source: ADR-0001

---

## Global Rules (All Layers)

### Naming Conventions

| Element | Convention | Example |
|---------|-----------|---------|
| Classes | PascalCase | `PlayerShip`, `StarMapNode` |
| Public fields/properties | PascalCase | `MoveSpeed`, `HullPoints` |
| Private fields | `_camelCase` | `_currentHealth`, `_isWarping` |
| Methods | PascalCase | `TakeDamage()`, `GetResourceOutput()` |
| Files | PascalCase, match class | `PlayerShip.cs` |
| Scenes/Prefabs | PascalCase | `StarMapScene.unity`, `ShipPrefab.prefab` |
| Constants | PascalCase or UPPER_SNAKE_CASE | `MaxHull` or `MAX_HULL` |

### Performance Budgets

| Target | Value |
|--------|-------|
| Framerate | 60fps |
| Frame budget | 16.6ms |
| Draw calls | < 200 (mobile target) |
| Memory ceiling | TBD |

### Approved Libraries / Addons

| Library | Approved for |
|---------|--------------|
| `com.unity.inputsystem@1.11` | All touch and keyboard input |
| Addressables | All runtime asset loading |
| UniTask (≥2.5.3) | Async operations and timing |
| TextMeshPro | All text rendering |

### Forbidden APIs (Unity 6.3 LTS)

These APIs are deprecated or unverified for Unity 6.3 LTS:

| Forbidden API | Use Instead |
|--------------|-------------|
| Legacy `Input.*` (GetKey, GetAxis, mousePosition, etc.) | `com.unity.inputsystem` actions |
| `Resources.Load()` | Addressables `LoadAssetAsync()` |
| `Object.FindObjectsOfType<T>()` | `FindObjectsByType<T>(FindObjectsSortMode.None)` |
| `Object.FindObjectOfType<T>()` | `FindFirstObjectByType<T>()` / `FindAnyObjectByType<T>()` |
| `VisualElement.transform.position/rotation/scale` | `style.translate/rotate/scale` |
| `ExecuteDefaultAction()` / `ExecuteDefaultActionAtTarget()` | `HandleEventTrickleDown()` / `StopPropagation()` |
| UGUI `Canvas` (new UI) | UIDocument (UI Toolkit) |
| `Physics.RaycastAll()` | `Physics.RaycastNonAlloc()` |
| `ComponentSystem` / `JobComponentSystem` | `ISystem` / `IJobEntity` |
| `GameObjectEntity` | Pure ECS workflow |
| `Rigidbody.velocity` (direct write) | `Rigidbody.AddForce()` |
| `camera.gameObject.SetActive(false)` | `Camera.enabled = false` |

*Source: `docs/engine-reference/unity/deprecated-apis.md`*

### Cross-Cutting Constraints

- **SimRate must be archived** — `ShipDataModel.SaveData` includes SimRate field; archive writes current value; load restores (default 1) — source: ADR-0012
- **Single `.inputactions` asset** with two ActionMaps: `StarMapActions` and `CockpitActions` — source: ADR-0003
- **Input touch tracking: use `Finger` object reference, NOT `finger.index`** — index is unstable with touch count changes — source: ADR-0003
- **Virtual joystick dead zone formula**: `normalizedInput = Mathf.Clamp01((Mathf.Abs(offset) - DEAD_ZONE) / (1.0f - DEAD_ZONE))` with `DEAD_ZONE = 0.08f` — source: ADR-0003
