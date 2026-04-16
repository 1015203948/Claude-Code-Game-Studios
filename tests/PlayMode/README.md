# Play Mode Tests

Integration tests that run in a real game scene with the full Unity frame loop.
Use for cross-system interactions, physics, coroutines, and async UniTask flows.

## Assembly Definition

Create `PlayModeTests.asmdef` in this directory:
- References: `UnityEngine.TestRunner`
- Include platforms: all (not Editor-only)
- Via: Assets → Create → Testing → Assembly Definition

## Directory Layout

```
PlayMode/
  PlayModeTests.asmdef          # Assembly definition (create in Unity Editor)
  SceneManagement/
    ViewLayerSwitchTests.cs     # Tests for MasterScene ↔ CockpitScene switching
  InputSystem/
    TouchInputTests.cs          # Tests for dual ActionMap context switching
  DataPersistence/
    SaveLoadRoundTripTests.cs   # Tests for save/load round-trips
```

## Example Test

```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

public class ViewLayerSwitchTests
{
    [UnityTest]
    public IEnumerator SwitchToCockpit_SetsIsSwitchingTrue_ThenFalse()
    {
        // Arrange
        var manager = Object.FindFirstObjectByType<ViewLayerManager>();

        // Act
        var task = manager.SwitchToCockpitAsync();
        Assert.IsTrue(manager.IsSwitching);  // should be true mid-switch

        yield return new WaitUntil(() => task.IsCompleted);

        // Assert
        Assert.IsFalse(manager.IsSwitching);
    }
}
```

## What to Test Here

- ViewLayerManager: `_isSwitching` state management, `_preEnterState` snapshot/restore
- SO Channel event delivery across scene boundaries
- ActionMap context switching (StarMapActions ↔ CockpitActions)
- Scene load progress check (`progress >= 0.9f` gating)
- CancellationToken propagation on scene unload
- Save/load round-trip: state written → loaded → matches original
