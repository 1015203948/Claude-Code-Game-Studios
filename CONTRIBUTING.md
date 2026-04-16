# Contributing to 星链霸权 (Starchain Hegemony)

Thank you for your interest in contributing.

---

## Development Workflow

This project uses Claude Code with a structured agent system. Before making changes, read [CLAUDE.md](CLAUDE.md) for:

- Agent coordination rules (who owns what)
- Collaboration protocol (ask → options → you decide → draft → approve)
- Verification-driven development (tests first for gameplay logic)
- Branch strategy and commit conventions

---

## Code Standards

### Required

- All gameplay values from external config/data files — **never hardcode**
- `Time.deltaTime` for frame-rate dependent logic
- `SimClock.DeltaTime` for strategy-layer time (fleet transit, colony ticks)
- `Physics.RaycastNonAlloc` / `Physics.OverlapSphereNonAlloc` — **zero GC in hot paths**
- Unit tests for all gameplay formulas and state machines
- Doc comments on all public APIs

### Forbidden

- `new Collider[]` / `new RaycastHit[]` in combat loops (use pre-allocated buffers)
- `Time.time` for gameplay timing (use `Time.deltaTime` or `SimClock.DeltaTime`)
- Static singleton GameObject.Find / GetComponent in system code

### File Routing

| File Type | Specialist |
|-----------|------------|
| `.cs` gameplay code | `unity-specialist` |
| `.shader`, `.shadergraph` | `unity-shader-specialist` |
| `.uxml`, `.uss`, Canvas prefabs | `unity-ui-specialist` |
| `.dll`, native plugins | `unity-specialist` |
| Architecture review | `unity-specialist` |

---

## Commit Convention

Format: `<type>: <short description>`

Types: `feat`, `fix`, `chore`, `docs`, `test`, `refactor`

Examples:
```
feat: add FleetDispatch CancelDispatch return journey
fix: CombatSystem FireRequested null guard
docs: update ADR-0017 U-4 path documentation
test: add EnemyAIController AI state transition tests
```

---

## Story Workflow

1. Read story manifest in `production/epics/[epic]/story-XXX-*.md`
2. Implement in `src/`
3. Write tests in `tests/`
4. Update session state in `production/session-state/active.md`
5. Mark story complete only when all ACs pass

---

## Testing

- Unit tests for logic: `tests/unit/`
- Integration tests for multi-system: `tests/integration/`
- All tests must pass before merging to main
- Tests must be deterministic (no random seeds, no time-dependent assertions)

---

## Pull Requests

1. Create feature branch from `main`
2. Ensure all tests pass
3. Update relevant ADRs if architecture changed
4. Fill out PR description with: what changed, why, test evidence

---

## Reporting Issues

Bug reports should include:
- Steps to reproduce
- Expected vs actual behavior
- Relevant log output
- Unity version
- Platform (Android in this project)

Use GitHub Issues with the bug report template.
