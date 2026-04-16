# Edit Mode Tests

Unit tests that run without entering Play Mode. No scene loading, no frame loop.

**Use for**: pure logic — formulas, state machines, data validation, SO config checks.

## Assembly Definition

Create `EditModeTests.asmdef` in this directory:
- References: `UnityEngine.TestRunner`, `UnityEditor.TestRunner`
- Platforms: Editor only
- Via: Assets → Create → Testing → Assembly Definition

## Directory Layout

```
EditMode/
  EditModeTests.asmdef          # Assembly definition (create in Unity Editor)
  ResourceSystem/               # One subdirectory per system
    ResourceOutputTests.cs      # Tests for resource production formulas
  ShipSystem/
    ShipBlueprintTests.cs       # Tests for blueprint data validation
  HealthSystem/
    DamageGatingTests.cs        # Tests for state-gated damage logic
```

## Example Test

```csharp
using NUnit.Framework;
using UnityEngine;

public class ResourceOutputTests
{
    [Test]
    public void ColonyOutput_WithBaseRate_ReturnsExpectedValue()
    {
        // Arrange
        var config = ScriptableObject.CreateInstance<ResourceConfig>();
        config.BaseColonyOreOutput = 10f;

        // Act
        float output = ResourceSystem.CalculateOutput(config, deltaSeconds: 1f);

        // Assert
        Assert.AreEqual(10f, output, 0.001f);
    }
}
```

## What to Test Here

- Resource production formulas (BaseColonyOreOutput, OreCap enforcement)
- Ship blueprint data validation (HangarCapacity, RequiredShipyardTier)
- Health system state gating (ApplyDamage only in IN_COCKPIT / IN_COMBAT)
- Dead zone formula: `Clamp01((Abs(offset) - 0.08f) / 0.92f)`
- SO Config OnValidate invariants (OreCap > 0, etc.)
