# Unity 6.3 LTS — Deprecated APIs

**Last verified:** 2026-04-11

Quick lookup table for deprecated APIs and their replacements.
Format: **Don't use X** → **Use Y instead**

---

## Input

| Deprecated | Replacement | Notes |
|------------|-------------|-------|
| `Input.GetKey()` | `Keyboard.current[Key.X].isPressed` | New Input System |
| `Input.GetKeyDown()` | `Keyboard.current[Key.X].wasPressedThisFrame` | New Input System |
| `Input.GetMouseButton()` | `Mouse.current.leftButton.isPressed` | New Input System |
| `Input.GetAxis()` | `InputAction` callbacks | New Input System |
| `Input.mousePosition` | `Mouse.current.position.ReadValue()` | New Input System |

**Migration:** Install `com.unity.inputsystem` package.

---

## UI

| Deprecated | Replacement | Notes |
|------------|-------------|-------|
| `Canvas` (UGUI) | `UIDocument` (UI Toolkit) | UI Toolkit is now production-ready |
| `Text` component | `TextMeshPro` or UI Toolkit `Label` | Better rendering, fewer draw calls |
| `Image` component | UI Toolkit `VisualElement` with background | More flexible styling |

**Migration:** UGUI still works, but UI Toolkit is recommended for new projects.

---

## DOTS/Entities

| Deprecated | Replacement | Notes |
|------------|-------------|-------|
| `ComponentSystem` | `ISystem` (unmanaged) | Entities 1.0+ complete rewrite |
| `JobComponentSystem` | `ISystem` with `IJobEntity` | Burst-compatible |
| `GameObjectEntity` | Pure ECS workflow | No GameObject conversion |
| `EntityManager.CreateEntity()` (old signature) | `EntityManager.CreateEntity(EntityArchetype)` | Explicit archetype |
| `ComponentDataFromEntity<T>` | `ComponentLookup<T>` | Entities 1.0+ rename |

**Migration:** See Entities package migration guide. Major refactor required.

---

## Rendering

| Deprecated | Replacement | Notes |
|------------|-------------|-------|
| `CommandBuffer.DrawMesh()` | RenderGraph API | URP/HDRP render passes |
| `OnPreRender()` / `OnPostRender()` | `RenderPipelineManager` callbacks | SRP compatibility |
| `Camera.SetReplacementShader()` | Custom render pass | Not supported in SRP |

---

## Physics

| Deprecated | Replacement | Notes |
|------------|-------------|-------|
| `Physics.RaycastAll()` | `Physics.RaycastNonAlloc()` | Avoid GC allocations |
| `Rigidbody.velocity` (direct write) | `Rigidbody.AddForce()` | Better physics stability |
| `Rigidbody.drag` | `Rigidbody.linearDamping` | Unity 6+ 重命名；`drag` 在 Unity 6.3 中已移除 |
| `Rigidbody.angularDrag` | `Rigidbody.angularDamping` | Unity 6+ 重命名 |

---

## Asset Loading

| Deprecated | Replacement | Notes |
|------------|-------------|-------|
| `Resources.Load()` | Addressables | Better memory control, async loading |
| Synchronous asset loading | `Addressables.LoadAssetAsync()` | Non-blocking |

---

## Animation

| Deprecated | Replacement | Notes |
|------------|-------------|-------|
| Legacy Animation component | Animator Controller | Mecanim system |
| `Animation.Play()` | `Animator.Play()` | State machine control |

---

## Particles

| Deprecated | Replacement | Notes |
|------------|-------------|-------|
| Legacy Particle System | Visual Effect Graph | GPU-accelerated, more performant |

---

## Scripting

| Deprecated | Replacement | Notes |
|------------|-------------|-------|
| `WWW` class | `UnityWebRequest` | Modern async networking |
| `Application.LoadLevel()` | `SceneManager.LoadScene()` | Scene management |

---

## Platform-Specific

### WebGL
| Deprecated | Replacement | Notes |
|------------|-------------|-------|
| WebGL 1.0 | WebGL 2.0 or WebGPU | Unity 6+ defaults to WebGPU |

---

## Quick Migration Patterns

### Input Example
```csharp
// ❌ Deprecated
if (Input.GetKeyDown(KeyCode.Space)) {
    Jump();
}

// ✅ New Input System
using UnityEngine.InputSystem;
if (Keyboard.current.spaceKey.wasPressedThisFrame) {
    Jump();
}
```

### Asset Loading Example
```csharp
// ❌ Deprecated
var prefab = Resources.Load<GameObject>("Enemies/Goblin");

// ✅ Addressables
var handle = Addressables.LoadAssetAsync<GameObject>("Enemies/Goblin");
await handle.Task;
var prefab = handle.Result;
```

### UI Example
```csharp
// ❌ Deprecated (UGUI)
GetComponent<Text>().text = "Score: 100";

// ✅ TextMeshPro
GetComponent<TextMeshProUGUI>().text = "Score: 100";

// ✅ UI Toolkit
rootVisualElement.Q<Label>("score-label").text = "Score: 100";
```

---

## Object Finding (Unity 6.0+)

| Deprecated | Replacement | Notes |
|------------|-------------|-------|
| `Object.FindObjectsOfType<T>()` | `Object.FindObjectsByType<T>(FindObjectsSortMode.None)` | Use `None` for better perf vs `InstanceID` |
| `Object.FindObjectOfType<T>()` | `Object.FindFirstObjectByType<T>()` or `FindAnyObjectByType<T>()` | `FindAny` is fastest |

---

## URP Render Passes (Unity 6.2+)

| Deprecated | Replacement | Notes |
|------------|-------------|-------|
| `SetupRenderPasses()` in ScriptableRendererFeature | `AddRenderPasses()` + render graph | Rewrite using `RecordRenderGraph` |
| `AfterRendering` injection point (same behavior) | `AfterRenderingPostProcessing` | Execution order changed in 6.2 |

---

## UI Toolkit Transform (Unity 6.2+)

| Deprecated | Replacement | Notes |
|------------|-------------|-------|
| `VisualElement.transform.position` | `element.style.translate` | CSS-style transform |
| `VisualElement.transform.rotation` | `element.style.rotate` | CSS-style transform |
| `VisualElement.transform.scale` | `element.style.scale` | CSS-style transform |
| `ExecuteDefaultAction()` | `HandleEventTrickleDown()` | New event dispatch model |
| `ExecuteDefaultActionAtTarget()` | `HandleEventBubbleUp()` | New event dispatch model |
| `PreventDefault()` | `StopPropagation()` | Renamed for clarity |

---

## Graphics Formats (Unity 6.0+ — compile errors)

| Deprecated | Replacement | Notes |
|------------|-------------|-------|
| `GraphicsFormat.DepthAuto` | `GraphicsFormat.None` (depth-only) | Produces compile error in 6.0+ |
| `GraphicsFormat.ShadowAuto` | `GraphicsFormat.None` | Produces compile error in 6.0+ |
| `GraphicsFormat.VideoAuto` | Explicit format | Produces compile error in 6.0+ |

---

## Android (Unity 6.3+)

| Deprecated | Replacement | Notes |
|------------|-------------|-------|
| `PlayerSettings.Android.androidIsGame` | App Category setting in Player Settings | For Android 16+ large-screen behavior |

---

## SRP Attributes (Unity 6.0+)

| Deprecated | Replacement | Notes |
|------------|-------------|-------|
| `CustomEditorForRenderPipelineAttribute` | `CustomEditor` + `SupportedOnRenderPipelineAttribute` | |
| `VolumeComponentMenuForRenderPipelineAttribute` | `VolumeComponentMenu` + `SupportedOnRenderPipelineAttribute` | |
| `RenderPipelineEditorUtility.FetchFirstCompatibleType...` | `GetDerivedTypesSupportedOnCurrentPipeline` | Returns all types now |

---

## Accessibility (Unity 6.3+)

| Deprecated | Replacement | Notes |
|------------|-------------|-------|
| `AccessibilityNode.selected` | `AccessibilityNode.invoked` | Semantic rename |

---

**Sources (Updated):**
- https://docs.unity3d.com/6000.0/Documentation/Manual/deprecated-features.html
- https://docs.unity3d.com/6000.1/Documentation/Manual/UpgradeGuideUnity6.html
- https://docs.unity3d.com/6000.2/Documentation/Manual/UpgradeGuideUnity62.html
- https://docs.unity3d.com/Packages/com.unity.inputsystem@1.11/manual/Migration.html
