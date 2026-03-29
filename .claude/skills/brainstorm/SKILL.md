---
name: brainstorm
description: "Guided game concept ideation — from zero idea to a structured game concept document. Uses professional studio ideation techniques, player psychology frameworks, and structured creative exploration."
argument-hint: "[genre or theme hint, or 'open'] [--review full|lean|solo]"
user-invocable: true
allowed-tools: Read, Glob, Grep, Write, WebSearch, AskUserQuestion
---

When this skill is invoked:

1. **Parse the argument** for an optional genre/theme hint (e.g., `roguelike`,
   `space survival`, `cozy farming`). If `open` or no argument, start from
   scratch. Also extract `--review [full|lean|solo]` if present and store as
   the review mode override for this run (see `.claude/docs/director-gates.md`).

2. **Check for existing concept work**:
   - Read `design/gdd/game-concept.md` if it exists (resume, don't restart)
   - Read `design/gdd/game-pillars.md` if it exists (build on established pillars)

3. **Run through ideation phases** interactively, asking the user questions at
   each phase. Do NOT generate everything silently — the goal is **collaborative
   exploration** where the AI acts as a creative facilitator, not a replacement
   for the human's vision.

   **Use `AskUserQuestion`** at key decision points throughout brainstorming:
   - Constrained taste questions (genre preferences, scope, team size)
   - Concept selection ("Which 2-3 concepts resonate?") after presenting options
   - Direction choices ("Develop further, explore more, or prototype?")
   - Pillar ranking after concepts are refined
   Write full creative analysis in conversation text first, then use
   `AskUserQuestion` to capture the decision with concise labels.

   Professional studio brainstorming principles to follow:
   - Withhold judgment — no idea is bad during exploration
   - Encourage unusual ideas — outside-the-box thinking sparks better concepts
   - Build on each other — "yes, and..." responses, not "but..."
   - Use constraints as creative fuel — limitations often produce the best ideas
   - Time-box each phase — keep momentum, don't over-deliberate early

---

### Phase 1: Creative Discovery

Start by understanding the person, not the game. Ask these questions
conversationally (not as a checklist):

**Emotional anchors**:
- What's a moment in a game that genuinely moved you, thrilled you, or made
  you lose track of time? What specifically created that feeling?
- Is there a fantasy or power trip you've always wanted in a game but never
  quite found?

**Taste profile**:
- What 3 games have you spent the most time with? What kept you coming back?
  *(Ask this as plain text — the user must be able to type specific game names freely.
  Do NOT put this in an AskUserQuestion with preset options.)*
- Are there genres you love? Genres you avoid? Why?
- Do you prefer games that challenge you, relax you, tell you stories,
  or let you express yourself? *(Use `AskUserQuestion` for this — constrained choice.)*

**Practical constraints** (shape the sandbox before brainstorming).
Bundle these into a single multi-tab `AskUserQuestion` with these exact tab labels:
- Tab "Experience" — "What kind of experience do you most want players to have?" (Challenge & Mastery / Story & Discovery / Expression & Creativity / Relaxation & Flow)
- Tab "Timeline" — "What's your realistic development timeline?" (Weeks / Months / 1-2 years / Multi-year)
- Tab "Dev level" — "Where are you in your dev journey?" (First game / Shipped before / Professional background)

Use exactly these tab names — do not rename or duplicate them.

**Synthesize** the answers into a **Creative Brief** — a 3-5 sentence
summary of the person's emotional goals, taste profile, and constraints.
Read the brief back and confirm it captures their intent.

---

### Phase 2: Concept Generation

Using the creative brief as a foundation, generate **3 distinct concepts**
that each take a different creative direction. Use these ideation techniques:

**Technique 1: Verb-First Design**
Start with the core player verb (build, fight, explore, solve, survive,
create, manage, discover) and build outward from there. The verb IS the game.

**Technique 2: Mashup Method**
Combine two unexpected elements: [Genre A] + [Theme B]. The tension between
the two creates the unique hook. (e.g., "farming sim + cosmic horror",
"roguelike + dating sim", "city builder + real-time combat")

**Technique 3: Experience-First Design (MDA Backward)**
Start from the desired player emotion (aesthetic goal from MDA framework:
sensation, fantasy, narrative, challenge, fellowship, discovery, expression,
submission) and work backward to the dynamics and mechanics that produce it.

For each concept, present:
- **Working Title**
- **Elevator Pitch** (1-2 sentences — must pass the "10-second test")
- **Core Verb** (the single most common player action)
- **Core Fantasy** (the emotional promise)
- **Unique Hook** (passes the "and also" test: "Like X, AND ALSO Y")
- **Primary MDA Aesthetic** (which emotion dominates?)
- **Estimated Scope** (small / medium / large)
- **Why It Could Work** (1 sentence on market/audience fit)
- **Biggest Risk** (1 sentence on the hardest unanswered question)

Present all three. Then use `AskUserQuestion` to capture the selection:
- **Use a single-list call — NO tabs, just `prompt` and `options`. Do not use a tabbed form here.**
- **Prompt**: "Which concept resonates with you? You can pick one, combine elements, or ask for fresh directions."
- **Options**: one option per concept (e.g., `Concept 1 — SCAR`), plus `Combine elements across concepts` and `Generate fresh directions`

Never pressure toward a choice — let them sit with it.

---

### Phase 3: Core Loop Design

For the chosen concept, use structured questioning to build the core loop.
The core loop is the beating heart of the game — if it isn't fun in
isolation, no amount of content or polish will save the game.

**30-Second Loop** (moment-to-moment):

Ask these as `AskUserQuestion` calls — derive the options from the chosen concept, don't hardcode them:

1. **Core action feel** — prompt: "What's the primary feel of the core action?" Generate 3-4 options that fit the concept's genre and tone, plus a free-text escape (`I'll describe it`).

2. **Key design dimension** — identify the most important design variable for this specific concept (e.g., world reactivity, pacing, player agency) and ask about it. Generate options that match the concept. Always include a free-text escape.

After capturing answers, analyze: Is this action intrinsically satisfying? What makes it feel good? (Audio feedback, visual juice, timing satisfaction, tactical depth?)

**5-Minute Loop** (short-term goals):
- What structures the moment-to-moment play into cycles?
- Where does "one more turn" / "one more run" psychology kick in?
- What choices does the player make at this level?

**Session Loop** (30-120 minutes):
- What does a complete session look like?
- Where are the natural stopping points?
- What's the "hook" that makes them think about the game when not playing?

**Progression Loop** (days/weeks):
- How does the player grow? (Power? Knowledge? Options? Story?)
- What's the long-term goal? When is the game "done"?

**Player Motivation Analysis** (based on Self-Determination Theory):
- **Autonomy**: How much meaningful choice does the player have?
- **Competence**: How does the player feel their skill growing?
- **Relatedness**: How does the player feel connected (to characters,
  other players, or the world)?

---

### Phase 4: Pillars and Boundaries

Game pillars are used by real AAA studios (God of War, Hades, The Last of
Us) to keep hundreds of team members making decisions that all point the
same direction. Even for solo developers, pillars prevent scope creep and
keep the vision sharp.

Collaboratively define **3-5 pillars**:
- Each pillar has a **name** and **one-sentence definition**
- Each pillar has a **design test**: "If we're debating between X and Y,
  this pillar says we choose __"
- Pillars should feel like they create tension with each other — if all
  pillars point the same way, they're not doing enough work

Then define **3+ anti-pillars** (what this game is NOT):
- Anti-pillars prevent the most common form of scope creep: "wouldn't it
  be cool if..." features that don't serve the core vision
- Frame as: "We will NOT do [thing] because it would compromise [pillar]"

**After pillars and anti-pillars are agreed, spawn `creative-director` via Task using gate CD-PILLARS (`.claude/docs/director-gates.md`) before moving to Phase 5.**

Pass: full pillar set with design tests, anti-pillars, core fantasy, unique hook.

Present the feedback to the user. If CONCERNS or REJECT, offer to revise specific pillars before moving on. If APPROVE, note the approval and continue.

---

### Phase 5: Player Type Validation

Using the Bartle taxonomy and Quantic Foundry motivation model, validate
who this game is actually for:

- **Primary player type**: Who will LOVE this game? (Achievers, Explorers,
  Socializers, Competitors, Creators, Storytellers)
- **Secondary appeal**: Who else might enjoy it?
- **Who is this NOT for**: Being clear about who won't like this game is as
  important as knowing who will
- **Market validation**: Are there successful games that serve a similar
  player type? What can we learn from their audience size?

---

### Phase 6: Scope and Feasibility

Ground the concept in reality:

- **Target platform**: Use `AskUserQuestion` — "What platforms are you targeting for this game?"
  Options: `PC (Steam / Epic)` / `Mobile (iOS / Android)` / `Console` / `Web / Browser` / `Multiple platforms`
  Record the answer — it directly shapes the engine recommendation and will be passed to `/setup-engine`.
  Note platform implications if relevant (e.g., mobile means Unity is strongly preferred; console means Godot has limitations; web means Godot exports cleanly).

- **Engine experience**: Use `AskUserQuestion` — "Do you already have an engine you work in?"
  Options: `Godot` / `Unity` / `Unreal Engine 5` / `No preference — help me decide`
  - If they pick an engine → record it as their preference and move on. Do NOT second-guess it.
  - If "No preference" → tell them: "Run `/setup-engine` after this session — it will walk you through the full decision based on your concept and platform target." Do not make a recommendation here.
- **Art pipeline**: What's the art style and how labor-intensive is it?
- **Content scope**: Estimate level/area count, item count, gameplay hours
- **MVP definition**: What's the absolute minimum build that tests "is the
  core loop fun?"
- **Biggest risks**: Technical risks, design risks, market risks
- **Scope tiers**: What's the full vision vs. what ships if time runs out?

**After identifying biggest technical risks, spawn `technical-director` via Task using gate TD-FEASIBILITY (`.claude/docs/director-gates.md`) before scope tiers are defined.**

Pass: core loop description, platform target, engine choice (or "undecided"), list of identified technical risks.

Present the assessment to the user. If HIGH RISK, offer to revisit scope before finalising. If CONCERNS, note them and continue.

**After scope tiers are defined, spawn `producer` via Task using gate PR-SCOPE (`.claude/docs/director-gates.md`).**

Pass: full vision scope, MVP definition, timeline estimate, team size.

Present the assessment to the user. If UNREALISTIC, offer to adjust the MVP definition or scope tiers before writing the document.

---

4. **Generate the game concept document** using the template at
   `.claude/docs/templates/game-concept.md`. Fill in ALL sections from the
   brainstorm conversation, including the MDA analysis, player motivation
   profile, and flow state design sections.

5. Ask: "May I write the game concept document to `design/gdd/game-concept.md`?"

If yes, generate the document using the template at `.claude/docs/templates/game-concept.md`, fill in ALL sections from the brainstorm conversation, and write the file, creating directories as needed.

If no:
- If the user already named a section to change, revise it directly — do not ask again which section.
- If the user said no without specifying what to change, use `AskUserQuestion` — "Which section would you like to revise?"
  Options: `Elevator Pitch` / `Core Fantasy & Unique Hook` / `Pillars` / `Core Loop` / `MVP Definition` / `Scope Tiers` / `Risks` / `Something else — I'll describe`

After revising, show the updated section as a diff or clear before/after, then use `AskUserQuestion` — "Ready to write the updated concept document?"
Options: `Yes — write it` / `Revise another section`
Repeat until the user approves the write.

**Scope consistency rule**: The "Estimated Scope" field in the Core Identity table must match the full-vision timeline from the Scope Tiers section — not just say "Large (9+ months)". Write it as "Large (X–Y months, solo)" or "Large (X–Y months, team of N)" so the summary table is accurate.

6. **Suggest next steps** (in this order — this is the professional studio
   pre-production pipeline). List ALL steps — do not abbreviate or truncate:
   1. "Run `/setup-engine` to configure the engine and populate version-aware reference docs"
   2. "Use `/design-review design/gdd/game-concept.md` to validate concept completeness before going downstream"
   3. "Discuss vision with the `creative-director` agent for pillar refinement"
   4. "Decompose the concept into individual systems with `/map-systems` — maps dependencies, assigns priorities, and creates the systems index"
   5. "Author per-system GDDs with `/design-system` — guided, section-by-section GDD writing for each system identified in step 4"
   6. "Plan the technical architecture with `/create-architecture` — defines how all systems fit together and connect"
   7. "Validate readiness to advance with `/gate-check` — phase gate before committing to production"
   8. "Prototype the riskiest system with `/prototype [core-mechanic]` — validate the core loop before full implementation"
   9. "Run `/playtest-report` after the prototype to validate the core hypothesis"
   10. "If validated, plan the first sprint with `/sprint-plan new`"

7. **Output a summary** with the chosen concept's elevator pitch, pillars,
   primary player type, engine recommendation, biggest risk, and file path.

Verdict: **COMPLETE** — game concept created and handed off for next steps.
