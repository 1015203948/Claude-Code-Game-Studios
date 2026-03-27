---
name: start
description: "First-time onboarding — asks where you are, then guides you to the right workflow. No assumptions."
argument-hint: "[no arguments]"
user-invocable: true
allowed-tools: Read, Glob, Grep, AskUserQuestion
---

# Guided Onboarding

This skill is read-only — it reports findings but writes no files.

This skill is the entry point for new users. It does NOT assume you have a game idea, an engine preference, or any prior experience. It asks first, then routes you to the right workflow.

---

## Phase 1: Detect Project State

Before asking anything, silently gather context so you can tailor your guidance. Do NOT show these results unprompted — they inform your recommendations, not the conversation opener.

Check:
- **Engine configured?** Read `.claude/docs/technical-preferences.md`. If the Engine field contains `[TO BE CONFIGURED]`, the engine is not set.
- **Game concept exists?** Check for `design/gdd/game-concept.md`.
- **Source code exists?** Glob for source files in `src/` (`*.gd`, `*.cs`, `*.cpp`, `*.h`, `*.rs`, `*.py`, `*.js`, `*.ts`).
- **Prototypes exist?** Check for subdirectories in `prototypes/`.
- **Design docs exist?** Count markdown files in `design/gdd/`.
- **Production artifacts?** Check for files in `production/sprints/` or `production/milestones/`.

Store these findings internally to validate the user's self-assessment and tailor recommendations.

---

## Phase 2: Ask Where the User Is

This is the first thing the user sees. Present these 4 options clearly:

> **Welcome to Claude Code Game Studios!**
>
> Before I suggest anything, I'd like to understand where you're starting from.
> Where are you at with your game idea right now?
>
> **A) No idea yet** — I don't have a game concept at all. I want to explore and figure out what to make.
>
> **B) Vague idea** — I have a rough theme, feeling, or genre in mind (e.g., "something with space" or "a cozy farming game") but nothing concrete.
>
> **C) Clear concept** — I know the core idea — genre, basic mechanics, maybe a pitch sentence — but haven't formalized it into documents yet.
>
> **D) Existing work** — I already have design docs, prototypes, code, or significant planning done. I want to organize or continue the work.

Wait for the user's answer. Do not proceed until they respond.

---

## Phase 3: Route Based on Answer

#### If A: No idea yet

The user needs creative exploration before anything else.

1. Acknowledge that starting from zero is completely fine
2. Briefly explain what `/brainstorm` does (guided ideation using professional frameworks — MDA, player psychology, verb-first design)
3. Recommend running `/brainstorm open` as the next step
4. Show the recommended path:
   - `/brainstorm` — discover your game concept
   - `/setup-engine` — configure the engine (brainstorm will recommend one)
   - `/map-systems` — decompose the concept into systems and plan GDD writing order
   - `/prototype` — test the core mechanic
   - `/sprint-plan` — plan the first sprint

#### If B: Vague idea

1. Ask them to share their vague idea — even a few words is enough
2. Validate the idea as a starting point (don't judge or redirect)
3. Recommend running `/brainstorm [their hint]` to develop it
4. Show the recommended path:
   - `/brainstorm [hint]` — develop the idea into a full concept
   - `/setup-engine` — configure the engine
   - `/map-systems` — decompose the concept into systems
   - `/prototype` — test the core mechanic
   - `/sprint-plan` — plan the first sprint

#### If C: Clear concept

1. Ask 2-3 follow-up questions:
   - What's the genre and core mechanic? (one sentence)
   - Do they have an engine preference, or need help choosing?
   - What's the rough scope? (jam game, small project, large project)
2. Offer two paths:
   - **Formalize first**: Run `/brainstorm` to structure the concept into a proper game concept document
   - **Jump to engine setup**: Go straight to `/setup-engine` and write the GDD manually afterward
3. Show the recommended path:
   - `/brainstorm` or `/setup-engine` (their pick)
   - `/design-review` — validate the concept doc
   - `/map-systems` — decompose the concept into individual systems
   - `/design-system` — author per-system GDDs
   - `/architecture-decision` — make first technical decisions
   - `/sprint-plan` — plan the first sprint

#### If D: Existing work

1. Share what you found in Phase 1:
   - "I can see you have [X source files / Y design docs / Z prototypes]..."
   - "Your engine is [configured as X / not yet configured]..."

2. **Sub-case D1 — Early stage** (engine not configured or only a game concept exists):
   - Recommend `/setup-engine` first if engine not configured
   - Then `/project-stage-detect` for a gap inventory

   **Sub-case D2 — GDDs, ADRs, or stories already exist:**
   - Explain: "Having files isn't the same as the template's skills being able to use them. GDDs might be missing required sections. `/adopt` checks this specifically."
   - Recommend:
     1. `/project-stage-detect` — understand what phase and what's missing entirely
     2. `/adopt` — audit whether existing artifacts are in the right internal format

3. Show the recommended path for D2:
   - `/project-stage-detect` — phase detection + existence gaps
   - `/adopt` — format compliance audit + migration plan
   - `/setup-engine` — if engine not configured
   - `/design-system retrofit [path]` — fill missing GDD sections
   - `/architecture-decision retrofit [path]` — add missing ADR sections
   - `/architecture-review` — bootstrap the TR requirement registry
   - `/gate-check` — validate readiness for next phase

---

## Phase 4: Confirm Before Proceeding

After presenting the recommended path, ask the user which step they'd like to take first. Never auto-run the next skill.

> "Would you like to start with [recommended first step], or would you prefer to do something else first?"

---

## Phase 5: Hand Off

When the user chooses their next step, let them invoke the skill themselves or offer to run it for them. The `/start` skill's job is done once the user has a clear next action.

Verdict: **COMPLETE** — user oriented and handed off to next step.

---

## Edge Cases

- **User picks D but project is empty**: Gently redirect — "It looks like the project is a fresh template with no artifacts yet. Would Path A or B be a better fit?"
- **User picks A but project has code**: Mention what you found — "I noticed there's already code in `src/`. Did you mean to pick D (existing work)?"
- **User is returning (engine configured, concept exists)**: Skip onboarding entirely — "It looks like you're already set up! Your engine is [X] and you have a game concept at `design/gdd/game-concept.md`. Want to pick up where you left off? Try `/sprint-plan` or just tell me what you'd like to work on."
- **User doesn't fit any option**: Let them describe their situation in their own words and adapt.

---

## Collaborative Protocol

1. **Ask first** — never assume the user's state or intent
2. **Present options** — give clear paths, not mandates
3. **User decides** — they pick the direction
4. **No auto-execution** — recommend the next skill, don't run it without asking
5. **Adapt** — if the user's situation doesn't fit a template, listen and adjust
