---
name: perf-profile
description: "Structured performance profiling workflow. Identifies bottlenecks, measures against budgets, and generates optimization recommendations with priority rankings."
argument-hint: "[system-name or 'full']"
user-invocable: true
allowed-tools: Read, Glob, Grep, Bash
---
When this skill is invoked:

1. **Determine scope** from the argument:
   - If a system name: focus profiling on that specific system
   - If `full`: run a comprehensive profile across all systems

2. **Read performance budgets** — Check for existing performance targets in design docs or CLAUDE.md:
   - Target FPS (e.g., 60fps = 16.67ms frame budget)
   - Memory budget (total and per-system)
   - Load time targets
   - Draw call budgets
   - Network bandwidth limits (if multiplayer)

3. **Analyze the codebase** for common performance issues:

   **CPU Profiling Targets**:
   - `_process()` / `Update()` / `Tick()` functions — list all and estimate cost
   - Nested loops over large collections
   - String operations in hot paths
   - Allocation patterns in per-frame code
   - Unoptimized search/sort over game entities
   - Expensive physics queries (raycasts, overlaps) every frame

   **Memory Profiling Targets**:
   - Large data structures and their growth patterns
   - Texture/asset memory footprint estimates
   - Object pool vs instantiate/destroy patterns
   - Leaked references (objects that should be freed but aren't)
   - Cache sizes and eviction policies

   **Rendering Targets** (if applicable):
   - Draw call estimates
   - Overdraw from overlapping transparent objects
   - Shader complexity
   - Unoptimized particle systems
   - Missing LODs or occlusion culling

   **I/O Targets**:
   - Save/load performance
   - Asset loading patterns (sync vs async)
   - Network message frequency and size

4. **Generate the profiling report**:

   ```markdown
   ## Performance Profile: [System or Full]
   Generated: [Date]

   ### Performance Budgets
   | Metric | Budget | Estimated Current | Status |
   |--------|--------|-------------------|--------|
   | Frame time | [16.67ms] | [estimate] | [OK/WARNING/OVER] |
   | Memory | [target] | [estimate] | [OK/WARNING/OVER] |
   | Load time | [target] | [estimate] | [OK/WARNING/OVER] |
   | Draw calls | [target] | [estimate] | [OK/WARNING/OVER] |

   ### Hotspots Identified
   | # | Location | Issue | Estimated Impact | Fix Effort |
   |---|----------|-------|------------------|------------|
   | 1 | [file:line] | [description] | [High/Med/Low] | [S/M/L] |
   | 2 | [file:line] | [description] | [High/Med/Low] | [S/M/L] |

   ### Optimization Recommendations (Priority Order)
   1. **[Title]** — [Description of the optimization]
      - Location: [file:line]
      - Expected gain: [estimate]
      - Risk: [Low/Med/High]
      - Approach: [How to implement]

   ### Quick Wins (< 1 hour each)
   - [Simple optimization 1]
   - [Simple optimization 2]

   ### Requires Investigation
   - [Area that needs actual runtime profiling to determine impact]
   ```

5. **Output the report** with a summary: top 3 hotspots, estimated headroom vs budget, and recommended next action.

6. **Scope & Timeline Decision** — activate this phase only if any hotspot has Fix Effort rated M or L.

   Present a summary of the significant-effort items:

   > "The following optimizations require significant effort: [list titles and effort ratings from the Hotspots table]"

   For each M/L item, ask the user to choose one of:

   - **A) Implement the optimization** (estimated effort: [S/M/L] — proceed with fix now or schedule it)
   - **B) Reduce feature scope to avoid the bottleneck** (run `/scope-check [feature]` to analyze the trade-offs)
   - **C) Accept the performance hit and defer to Polish phase** (log it as a known issue)
   - **D) Escalate to technical-director for an architectural decision** (the bottleneck warrants an ADR)

   For choice B, remind the user:
   > "Run `/scope-check [feature]` to see what simplifications are available without sacrificing player experience."

   For choice D, note:
   > "A bottleneck requiring architectural change should become a new Architecture Decision Record. Run `/architecture-decision` to capture the decision and its trade-offs."

   If multiple items are deferred to Polish (choice C), record them in the report under a `### Deferred to Polish` section so they are not lost.

### Rules
- Never optimize without measuring first — gut feelings about performance are unreliable
- Recommendations must include estimated impact — "make it faster" is not actionable
- Profile on target hardware, not just development machines
- Distinguish between CPU-bound, GPU-bound, and I/O-bound bottlenecks
- Consider worst-case scenarios (maximum entities, lowest spec hardware, worst network conditions)
- Static analysis (this skill) identifies candidates; runtime profiling confirms
