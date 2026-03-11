---
name: architecture-decision
description: "Creates an Architecture Decision Record (ADR) documenting a significant technical decision, its context, alternatives considered, and consequences. Every major technical choice should have an ADR."
argument-hint: "[title]"
user-invocable: true
allowed-tools: Read, Glob, Grep, Write
---

When this skill is invoked:

## 0. Parse Arguments — Detect Retrofit Mode

**If the argument starts with `retrofit` followed by a file path**
(e.g., `/architecture-decision retrofit docs/architecture/adr-0001-event-system.md`):

Enter **retrofit mode**:

1. Read the existing ADR file completely.
2. Identify which template sections are present by scanning headings:
   - `## Status` — **BLOCKING if missing**: `/story-readiness` cannot check ADR acceptance
   - `## ADR Dependencies` — HIGH if missing: dependency ordering breaks
   - `## Engine Compatibility` — HIGH if missing: post-cutoff risk unknown
   - `## GDD Requirements Addressed` — MEDIUM if missing: traceability lost
3. Present to the user:
   ```
   ## Retrofit: [ADR title]
   File: [path]

   Sections already present (will not be touched):
   ✓ Status: [current value, or "MISSING — will add"]
   ✓ [section]

   Missing sections to add:
   ✗ Status — BLOCKING (stories cannot validate ADR acceptance without this)
   ✗ ADR Dependencies — HIGH
   ✗ Engine Compatibility — HIGH
   ```
4. Ask: "Shall I add the [N] missing sections? I will not modify any existing content."
5. If yes:
   - For **Status**: ask the user — "What is the current status of this decision?"
     Options: "Proposed", "Accepted", "Deprecated", "Superseded by ADR-XXXX"
   - For **ADR Dependencies**: ask — "Does this decision depend on any other ADR?
     Does it enable or block any other ADR or epic?" Accept "None" for each field.
   - For **Engine Compatibility**: read the engine reference docs (same as Step 0 below)
     and ask the user to confirm the domain. Then generate the table with verified data.
   - For **GDD Requirements Addressed**: ask — "Which GDD systems motivated this decision?
     What specific requirement in each GDD does this ADR address?"
   - Append each missing section to the ADR file using the Edit tool.
   - **Never modify any existing section.** Only append or fill absent sections.
6. After adding all missing sections, update the ADR's `## Date` field if it is absent.
7. Suggest: "Run `/architecture-review` to re-validate coverage now that this ADR
   has its Status and Dependencies fields."

If NOT in retrofit mode, proceed to Step 0 below (normal ADR authoring).

---

## 0. Load Engine Context (ALWAYS FIRST)

Before doing anything else, establish the engine environment:

1. Read `docs/engine-reference/[engine]/VERSION.md` to get:
   - Engine name and version
   - LLM knowledge cutoff date
   - Post-cutoff version risk levels (LOW / MEDIUM / HIGH)

2. Identify the **domain** of this architecture decision from the title or
   user description. Common domains: Physics, Rendering, UI, Audio, Navigation,
   Animation, Networking, Core, Input, Scripting.

3. Read the corresponding module reference if it exists:
   `docs/engine-reference/[engine]/modules/[domain].md`

4. Read `docs/engine-reference/[engine]/breaking-changes.md` — flag any
   changes in the relevant domain that post-date the LLM's training cutoff.

5. Read `docs/engine-reference/[engine]/deprecated-apis.md` — flag any APIs
   in the relevant domain that should not be used.

6. **Display a knowledge gap warning** before proceeding if the domain carries
   MEDIUM or HIGH risk:

   ```
   ⚠️  ENGINE KNOWLEDGE GAP WARNING
   Engine: [name + version]
   Domain: [domain]
   Risk Level: HIGH — This version is post-LLM-cutoff.

   Key changes verified from engine-reference docs:
   - [Change 1 relevant to this domain]
   - [Change 2]

   This ADR will be cross-referenced against the engine reference library.
   Proceed with verified information only — do NOT rely solely on training data.
   ```

   If no engine has been configured yet, prompt: "No engine is configured.
   Run `/setup-engine` first, or tell me which engine you are using."

---

## 1. Determine the next ADR number

Scan `docs/architecture/` for existing ADRs to find the next number.

---

## 2. Gather context

Read related code, existing ADRs, and relevant GDDs from `design/gdd/`.

---

## 3. Guide the decision collaboratively

Ask clarifying questions if the title alone is not sufficient. For each major
section, present 2-4 options with pros/cons before drafting. Do not generate
the ADR until the key decision is confirmed by the user.

Key questions to ask:
- What problem are we solving? What breaks if we don't decide this now?
- What constraints apply (engine version, platform, performance budget)?
- What alternatives have you already considered?
- Which post-cutoff engine features (if any) does this decision depend on?
- **Which GDD systems motivated this decision?** For each, what specific
  requirement (rule, formula, performance constraint, integration point) in
  that GDD cannot be satisfied without this architectural decision?

If the decision is foundational (no GDD drives it directly), ask:
- Which GDD systems will this decision constrain or enable?

This GDD linkage becomes a mandatory "GDD Requirements Addressed" section
in the ADR. Do not skip it.

**Does this ADR have ordering constraints?** Ask:
- Does this decision depend on any other ADR that isn't yet Accepted? (If
  so, this ADR cannot be safely implemented until that one is resolved.)
- Does accepting this ADR unlock or unblock any other pending decisions?
- Does this ADR block any specific epic or story from starting?

Record the answers in the **ADR Dependencies** section. If no ordering
constraints exist, write "None" in each field.

---

## 4. Generate the ADR

Following this format:

```markdown
# ADR-[NNNN]: [Title]

## Status
[Proposed | Accepted | Deprecated | Superseded by ADR-XXXX]

## Date
[Date of decision]

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | [e.g. Godot 4.6] |
| **Domain** | [Physics / Rendering / UI / Audio / Navigation / Animation / Networking / Core / Input] |
| **Knowledge Risk** | [LOW / MEDIUM / HIGH — from VERSION.md] |
| **References Consulted** | [List engine-reference docs read, e.g. `docs/engine-reference/godot/modules/physics.md`] |
| **Post-Cutoff APIs Used** | [Any APIs from post-LLM-cutoff versions this decision depends on, or "None"] |
| **Verification Required** | [Specific behaviours to test before shipping, or "None"] |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | [ADR-NNNN (must be Accepted before this can be implemented), or "None"] |
| **Enables** | [ADR-NNNN (this ADR unlocks that decision), or "None"] |
| **Blocks** | [Epic/Story name — cannot start until this ADR is Accepted, or "None"] |
| **Ordering Note** | [Any sequencing constraint that isn't captured above] |

## Context

### Problem Statement
[What problem are we solving? Why does this decision need to be made now?]

### Constraints
- [Technical constraints]
- [Timeline constraints]
- [Resource constraints]
- [Compatibility requirements]

### Requirements
- [Must support X]
- [Must perform within Y budget]
- [Must integrate with Z]

## Decision

[The specific technical decision made, described in enough detail for someone
to implement it.]

### Architecture Diagram
[ASCII diagram or description of the system architecture this creates]

### Key Interfaces
[API contracts or interface definitions this decision creates]

## Alternatives Considered

### Alternative 1: [Name]
- **Description**: [How this would work]
- **Pros**: [Advantages]
- **Cons**: [Disadvantages]
- **Rejection Reason**: [Why this was not chosen]

### Alternative 2: [Name]
- **Description**: [How this would work]
- **Pros**: [Advantages]
- **Cons**: [Disadvantages]
- **Rejection Reason**: [Why this was not chosen]

## Consequences

### Positive
- [Good outcomes of this decision]

### Negative
- [Trade-offs and costs accepted]

### Risks
- [Things that could go wrong]
- [Mitigation for each risk]

## Performance Implications
- **CPU**: [Expected impact]
- **Memory**: [Expected impact]
- **Load Time**: [Expected impact]
- **Network**: [Expected impact, if applicable]

## Migration Plan
[If this changes existing code, how do we get from here to there?]

## Validation Criteria
[How will we know this decision was correct? What metrics or tests?]

## Related Decisions
- [Links to related ADRs]
- [Links to related design documents]
```

5. **Save the ADR** to `docs/architecture/adr-[NNNN]-[slug].md`.
