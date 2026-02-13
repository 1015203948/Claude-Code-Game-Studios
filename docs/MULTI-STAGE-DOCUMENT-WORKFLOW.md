# Multi-Stage Document Workflow

**Problem:** Some design documents are too large or complex to complete in a single session. An inventory system GDD might need multiple rounds of work across several days.

**Solution:** Use incremental document creation with clear WIP markers.

---

## 📋 **When to Use Multi-Stage Workflow**

Use this approach when:
- **Document is large** (inventory + items + crafting might be one big GDD)
- **Design needs time to evolve** (you want to think on it overnight)
- **Multiple stakeholders need input** (waiting for feedback before continuing)
- **Context is getting full** (~70%) before doc is complete
- **User explicitly wants to work incrementally** ("Let's do sections 1-4 today")

---

## ✅ **How It Works: Incremental Document Creation**

### **Session 1: Start the Document**

```
User: "Let's design the inventory system, but I only have 30 minutes today.
      Can we do the first few sections and continue tomorrow?"

Agent: "Absolutely! For an inventory GDD, here's my proposed session breakdown:

       **Session 1 (today, ~30 min):**
       - Overview
       - Player Fantasy
       - Core Rules (basic structure)

       **Session 2 (tomorrow):**
       - Formulas
       - Edge Cases
       - Dependencies

       **Session 3 (when ready):**
       - Tuning Knobs
       - Acceptance Criteria

       Does this work?"

User: "Yes, let's do Session 1 sections today"

Agent: [Works through Overview, Player Fantasy, Core Rules with Q&A]

Agent: "Session 1 sections complete. Should I:
       A) Save this as a WIP document with sections 4-8 marked as TODO
       B) Wait and only write the file when all sections are done
       C) Something else?"

User: "A - save it as WIP so I can review tonight"

Agent: "I'll create design/gdd/inventory-system.md with WIP markers:

       [Shows draft with this structure]

       ---
       # Inventory System (WIP)

       **Status:** INCOMPLETE - Sections 1-3 complete, 4-8 pending
       **Last Updated:** 2026-02-13
       **Next Session:** Complete Formulas and Edge Cases

       ## 1. Overview
       [Complete content]

       ## 2. Player Fantasy
       [Complete content]

       ## 3. Detailed Rules
       [Complete content]

       ## 4. Formulas
       **TODO:** Define capacity formulas, stack size calculations, weight calculations

       ## 5. Edge Cases
       **TODO:** Full inventory, item pickup while full, stack splitting behavior

       ## 6. Dependencies
       **TODO:** List integration points

       ## 7. Tuning Knobs
       **TODO:** List designer-facing values

       ## 8. Acceptance Criteria
       **TODO:** How to validate this works

       ---

       May I write this WIP document to design/gdd/inventory-system.md?"

User: "Yes"

Agent: [Writes file with WIP status clearly marked]
```

---

### **Session 2: Resume Work**

```
User: "Let's continue the inventory GDD from yesterday"

Agent: "I'll read the current state of design/gdd/inventory-system.md...

       [Reads file]

       Current status:
       ✓ Section 1-3: Complete
       ⏸ Section 4-8: Pending

       Today we're tackling Formulas and Edge Cases. Before we start, any
       changes to sections 1-3 based on your review last night?"

User: "Actually, change the grid size from 8x6 to 10x8 in section 3"

Agent: [Updates section 3]

       "Updated. Now let's continue with Formulas. For capacity calculation,
       I need to know: Should it be based on weight, slot count, or both?"

[Agent works through sections 4-5 with same collaborative process]

Agent: "Sections 4-5 complete. Should I:
       A) Update the WIP doc with 1-5 complete, 6-8 pending
       B) Continue to sections 6-8 now
       C) Stop here and finish later?"

User: "A - update the WIP. I'll finish 6-8 tomorrow."

Agent: [Updates doc, changes status to "Sections 1-5 complete, 6-8 pending"]
```

---

### **Session 3: Finalize Document**

```
User: "Let's finish the inventory GDD"

Agent: [Reads current WIP state]

       "Current status:
       ✓ Sections 1-5: Complete
       ⏸ Sections 6-8: Pending

       Finishing up with Dependencies, Tuning Knobs, Acceptance Criteria..."

[Agent completes final sections]

Agent: "All 8 sections complete! Should I:
       A) Remove WIP markers and mark as FINAL
       B) Run /design-review first to validate
       C) Keep as WIP for one more review cycle?"

User: "B - review it first"

Agent: [Runs /design-review design/gdd/inventory-system.md]

       "Design review passed! All 8 sections present and complete.
       Should I remove WIP markers now?"

User: "Yes"

Agent: [Updates doc, removes WIP status, marks as complete with final date]

       "Document finalized: design/gdd/inventory-system.md
       Status: COMPLETE
       Completed: 2026-02-15"
```

---

## 🏗️ **WIP Document Structure**

When saving incomplete documents, ALWAYS include:

```markdown
# [System Name] (WIP)

**Status:** INCOMPLETE - Sections [X-Y] complete, [Z-W] pending
**Last Updated:** [Date]
**Last Session Summary:** [What was accomplished]
**Next Session Plan:** [What to work on next]

---

## Completed Sections

[Sections with full content]

---

## Pending Sections

[Sections with TODO markers explaining what's needed]

## 4. Formulas
**TODO:** [Specific guidance on what to address]
- Capacity calculation formula
- Stack size limits
- Weight-based restrictions

## 5. Edge Cases
**TODO:** Address these scenarios:
- Full inventory behavior
- Partial stack pickup
- Item swapping logic
```

---

## 📌 **WIP Markers Reference**

Use these status markers in documents:

```markdown
**Status:** INCOMPLETE - Section 3 partial, 4-8 pending
**Status:** IN REVIEW - All sections complete, awaiting feedback
**Status:** COMPLETE - All sections finalized and reviewed
**Status:** DEPRECATED - Replaced by [new doc]
```

For sections:
```markdown
## 4. Formulas
**STATUS: COMPLETE** - Reviewed 2026-02-13

## 5. Edge Cases
**STATUS: DRAFT** - Needs review

## 6. Dependencies
**STATUS: TODO** - Not started

## 7. Tuning Knobs
**STATUS: BLOCKED** - Waiting for systems-designer input on balance values
```

---

## 🔄 **Git Workflow for WIP Docs**

### **Committing WIP Work:**

```bash
git add design/gdd/inventory-system.md
git commit -m "WIP: Inventory GDD sections 1-3 complete

Completed:
- Overview: Grid-based inventory (10x8)
- Player Fantasy: Spatial puzzle + organization
- Detailed Rules: Core pickup, drop, stack, swap logic

Pending:
- Formulas (session 2)
- Edge cases (session 2)
- Dependencies, Tuning, Acceptance (session 3)

per design/pillars.md Pillar 3"
```

**Why commit WIP work?**
- Preserves progress if you need to /clear context
- Allows review by others
- You can revert if you change direction

---

## 🎯 **Best Practices**

### ✅ **DO:**
- **Mark WIP status clearly** at the top of the doc
- **Commit WIP versions** so progress isn't lost
- **Document what's done and what's pending** explicitly
- **Provide context** for the next session (what to address)
- **Update status** as you progress (INCOMPLETE → IN REVIEW → COMPLETE)
- **Use TODO with specifics** ("TODO: Define edge case for X" not just "TODO")

### ❌ **DON'T:**
- **Don't leave TODO sections blank** - always explain what needs to be addressed
- **Don't assume incomplete = bad** - WIP is normal for complex systems
- **Don't skip WIP markers** - future-you needs to know what's finished
- **Don't mix WIP and COMPLETE** status without clear section markers
- **Don't run `/design-review` on WIP docs** - it will fail (intentionally, not all 8 sections complete)

---

## 🧩 **Multi-Document Systems**

Sometimes one "system" spans multiple documents:

```
Inventory System (umbrella concept):
├── design/gdd/inventory-grid.md (COMPLETE)
├── design/gdd/item-types.md (WIP - 60% done)
├── design/gdd/equipment-slots.md (TODO - not started)
└── design/gdd/inventory-ui.md (BLOCKED - waiting for ux-designer)
```

**How to manage:**
1. Create a **tracker doc** in `design/gdd/inventory-system-TRACKER.md`
2. List all sub-documents with status
3. Update tracker as each doc completes
4. When all complete, the "system" is complete

**Example Tracker:**

```markdown
# Inventory System - Document Tracker

**Overall Status:** 60% Complete

| Document | Status | Owner | Blocker |
|----------|--------|-------|---------|
| inventory-grid.md | ✅ COMPLETE | game-designer | None |
| item-types.md | 🔄 WIP (60%) | systems-designer | Needs formula review |
| equipment-slots.md | ⏸ TODO | game-designer | Waiting for inventory-grid finalization |
| inventory-ui.md | 🚫 BLOCKED | ux-designer | Needs wireframes |

**Next Steps:**
1. Complete item-types formulas (systems-designer consultation)
2. Get ux-designer wireframes for UI doc
3. Start equipment-slots once grid is implemented
```

---

## 💡 **Example: Large Inventory System Across 3 Sessions**

### **Session 1 (30 min):**
```
User: "Start inventory GDD, do first 3 sections"
Agent: [Creates sections 1-3]
Agent: "May I save as WIP with sections 4-8 pending?"
User: "Yes"
Result: design/gdd/inventory-system.md (WIP, 3/8 sections)
```

### **Session 2 (45 min):**
```
User: "Continue inventory GDD"
Agent: [Reads WIP, completes sections 4-6]
Agent: "Sections 1-6 done. Continue to 7-8 or stop?"
User: "Stop here, I need to think about acceptance criteria"
Agent: "Updating WIP doc..."
Result: design/gdd/inventory-system.md (WIP, 6/8 sections)
```

### **Session 3 (20 min):**
```
User: "Finish the inventory GDD"
Agent: [Reads WIP, completes sections 7-8]
Agent: "All sections complete. Run /design-review?"
User: "Yes"
Agent: [Review passes]
Agent: "Removing WIP markers, marking as COMPLETE"
Result: design/gdd/inventory-system.md (COMPLETE, 8/8 sections)
```

**Total time:** ~95 minutes across 3 days
**Result:** High-quality, reviewed GDD without rushing

---

## 🚀 **Key Takeaway**

**You DON'T have to finish documents in one session!**

- Save WIP versions with clear markers
- Resume across multiple sessions
- Keep progress tracked and visible
- Commit WIP work to preserve it
- Agents support incremental workflows

The collaborative protocol works **within** sessions AND **across** sessions.
