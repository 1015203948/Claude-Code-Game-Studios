---
name: balance-check
description: "Analyzes game balance data files, formulas, and configuration to identify outliers, broken progressions, degenerate strategies, and economy imbalances. Use after modifying any balance-related data or design."
argument-hint: "[system-name|path-to-data-file]"
user-invocable: true
allowed-tools: Read, Glob, Grep
context: fork
agent: economy-designer
---

When this skill is invoked:

1. **Identify the balance domain** from the argument.

2. **Read relevant data files** from `assets/data/` and `design/balance/`.

3. **Read the design document** for the system being checked from `design/gdd/`.

4. **Perform analysis**:

   For **combat balance**:
   - Calculate DPS for all weapons/abilities at each power tier
   - Check time-to-kill at each tier
   - Identify any options that dominate all others (strictly better)
   - Check if defensive options can create unkillable states
   - Verify damage type/resistance interactions are balanced

   For **economy balance**:
   - Map all resource faucets and sinks with flow rates
   - Project resource accumulation over time
   - Check for infinite resource loops
   - Verify gold sinks scale with gold generation
   - Check if any items are never worth purchasing

   For **progression balance**:
   - Plot the XP curve and power curve
   - Check for dead zones (no meaningful progression for too long)
   - Check for power spikes (sudden jumps in capability)
   - Verify content gates align with expected player power
   - Check if skip/grind strategies break intended pacing

   For **loot balance**:
   - Calculate expected time to acquire each rarity tier
   - Check pity timer math
   - Verify no loot is strictly useless at any stage
   - Check inventory pressure vs acquisition rate

5. **Output the analysis**:

```
## Balance Check: [System Name]

### Data Sources Analyzed
- [List of files read]

### Health Summary: [HEALTHY / CONCERNS / CRITICAL ISSUES]

### Outliers Detected
| Item/Value | Expected Range | Actual | Issue |
|-----------|---------------|--------|-------|

### Degenerate Strategies Found
- [Strategy description and why it is problematic]

### Progression Analysis
[Graph description or table showing progression curve health]

### Recommendations
| Priority | Issue | Suggested Fix | Impact |
|----------|-------|--------------|--------|

### Values That Need Attention
[Specific values with suggested adjustments and rationale]
```

6. **Fix & Verify Cycle**

   After presenting the report, ask:

   > "Would you like to fix any of these balance issues now?"

   If yes:
   - Ask which issue to address first (refer to the Recommendations table by priority row)
   - Guide the user to update the relevant data file in `assets/data/` or formula in `design/balance/`
   - After each fix, offer to re-run the relevant balance checks for that system to verify the fix did not introduce new outliers or degenerate interactions
   - If the fix changes a tuning knob that is defined in a GDD or referenced by an ADR, remind the user:
     > "This value is defined in a design document. Run `/propagate-design-change [path]` on the affected GDD to find downstream impacts before committing."

   If no:
   - Summarize the open issues and suggest saving the report to `design/balance/balance-check-[system]-[date].md` for later.

   End with:
   > "Re-run `/balance-check` after fixes to verify."
