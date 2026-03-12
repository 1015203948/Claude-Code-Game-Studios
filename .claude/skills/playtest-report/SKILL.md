---
name: playtest-report
description: "Generates a structured playtest report template or analyzes existing playtest notes into a structured format. Use this to standardize playtest feedback collection and analysis."
argument-hint: "[new|analyze path-to-notes]"
user-invocable: true
allowed-tools: Read, Glob, Grep, Write
---

When invoked with `new`, generate this template:

```markdown
# Playtest Report

## Session Info
- **Date**: [Date]
- **Build**: [Version/Commit]
- **Duration**: [Time played]
- **Tester**: [Name/ID]
- **Platform**: [PC/Console/Mobile]
- **Input Method**: [KB+M / Gamepad / Touch]
- **Session Type**: [First time / Returning / Targeted test]

## Test Focus
[What specific features or flows were being tested]

## First Impressions (First 5 minutes)
- **Understood the goal?** [Yes/No/Partially]
- **Understood the controls?** [Yes/No/Partially]
- **Emotional response**: [Engaged/Confused/Bored/Frustrated/Excited]
- **Notes**: [Observations]

## Gameplay Flow
### What worked well
- [Observation 1]
- [Observation 2]

### Pain points
- [Issue 1 -- Severity: High/Medium/Low]
- [Issue 2 -- Severity: High/Medium/Low]

### Confusion points
- [Where the player was confused and why]

### Moments of delight
- [What surprised or pleased the player]

## Bugs Encountered
| # | Description | Severity | Reproducible |
|---|-------------|----------|-------------|

## Feature-Specific Feedback
### [Feature 1]
- **Understood purpose?** [Yes/No]
- **Found engaging?** [Yes/No]
- **Suggestions**: [Tester suggestions]

## Quantitative Data (if available)
- **Deaths**: [Count and locations]
- **Time per area**: [Breakdown]
- **Items used**: [What and when]
- **Features discovered vs missed**: [List]

## Overall Assessment
- **Would play again?** [Yes/No/Maybe]
- **Difficulty**: [Too Easy / Just Right / Too Hard]
- **Pacing**: [Too Slow / Good / Too Fast]
- **Session length preference**: [Shorter / Good / Longer]

## Top 3 Priorities from this session
1. [Most important finding]
2. [Second priority]
3. [Third priority]
```

When invoked with `analyze`, read the raw notes, cross-reference with existing
design documents, and fill in the template above with structured findings.
Flag any playtest observations that conflict with design intent.

After generating or analyzing a report, run the **Action Routing** phase:

**Action Routing**

Categorize all findings from the report into the four buckets below (a single
finding may appear in more than one bucket if appropriate):

- **Design changes needed** — fun issues, player confusion, broken mechanics,
  observations that conflict with the GDD's intended experience
- **Balance adjustments** — numbers feel wrong, difficulty too spiked or too
  flat, economy or progression feedback
- **Bug reports** — clear implementation defects that are reproducible
- **Polish items** — not blocking progress, but friction or feel issues noted
  for later

Present the categorized list, then provide the routing guidance for each
non-empty bucket:

- **Design changes:** "These findings suggest GDD revisions. Run
  `/propagate-design-change [path]` on the affected design document to find
  downstream impacts before making changes."
- **Balance adjustments:** "Run `/balance-check [system]` to verify the full
  balance picture before tuning individual values."
- **Bugs:** "Use `/bug-report` to formally track these so they are not lost
  between sessions."
- **Polish items:** "No immediate action required. Consider adding these to the
  polish backlog in `production/` when the team reaches that phase."

Finally, ask:

> "Which category would you like to act on first?"
