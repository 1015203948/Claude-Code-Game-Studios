# Test Infrastructure

**Engine**: Unity 6.3 LTS
**Test Framework**: Unity Test Framework (NUnit — built-in, EditMode + PlayMode)
**CI**: `.github/workflows/tests.yml`
**Setup date**: 2026-04-14

## Directory Layout

```
tests/
  EditMode/       # Isolated unit tests — no scene loading required
  PlayMode/       # Integration tests — runs in real scene with frame loop
  smoke/          # Critical path test list for /smoke-check gate
  evidence/       # Screenshot logs and manual test sign-off records
```

## Running Tests

**In Editor**: Window → General → Test Runner → Run All

**Headless (CI)**:
```
# EditMode
unity-test-runner testMode=editmode

# PlayMode
unity-test-runner testMode=playmode
```

## Test Naming

- **Files**: `[System][Feature]Test.cs`
- **Classes**: `[System][Feature]Tests`
- **Methods**: `[Scenario]_[Expected]`
- **Example**: `ResourceSystemOutputTest.cs` → `ColonyProducing_ReturnsExpectedOrePerSecond()`

## Story Type → Test Evidence

| Story Type | Required Evidence | Location |
|---|---|---|
| Logic | Automated unit test — must pass | `tests/EditMode/[System]/` |
| Integration | Integration test OR playtest doc | `tests/PlayMode/[System]/` |
| Visual/Feel | Screenshot + lead sign-off | `tests/evidence/` |
| UI | Manual walkthrough OR interaction test | `tests/evidence/` |
| Config/Data | Smoke check pass | `production/qa/smoke-*.md` |

## Assembly Definitions Required

Before writing tests, create assembly definition files:

- `tests/EditMode/EditModeTests.asmdef`
  - References: `UnityEngine.TestRunner`, `UnityEditor.TestRunner`
  - Platforms: Editor only

- `tests/PlayMode/PlayModeTests.asmdef`
  - References: `UnityEngine.TestRunner`
  - Include platforms: all

Create via: Assets → Create → Testing → Assembly Definition

## CI

Tests run automatically on every push to `main` and every pull request.
A failed test suite blocks merging.

> **Note**: Unity CI requires a `UNITY_LICENSE` secret in GitHub repository settings.
> Add before the first CI run: Settings → Secrets and variables → Actions → New repository secret.
