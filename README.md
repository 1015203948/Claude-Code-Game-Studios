# Claude Code Game Studios

A production-ready [Claude Code](https://docs.anthropic.com/en/docs/claude-code) project template that turns a single AI session into a full game development studio. 48 specialized agents, 34 workflow skills, and a complete coordination system — all wired into Claude Code's native agent architecture.

Clone the repo, open Claude Code, and start building your game with a team of AI specialists that mirrors a real studio hierarchy: directors, department leads, and domain experts working together with defined roles, delegation rules, and quality gates.

## What's Included

| Category | Count | Description |
|----------|-------|-------------|
| **Agents** | 48 | Specialized subagents across design, programming, art, audio, narrative, QA, and production |
| **Skills** | 34 | Slash commands for common workflows (`/sprint-plan`, `/code-review`, `/brainstorm`, etc.) |
| **Hooks** | 8 | Automated validation on commits, pushes, asset changes, session lifecycle, agent audit, and gap detection |
| **Rules** | 11 | Path-scoped coding standards enforced when editing gameplay, engine, AI, UI, network code, and more |
| **Templates** | 28 | Document templates for GDDs, ADRs, sprint plans, economy models, faction design, and more |

## Studio Hierarchy

Agents are organized into three tiers, matching how real studios operate:

```
Tier 1 — Directors (Opus)
  creative-director    technical-director    producer

Tier 2 — Department Leads (Sonnet)
  game-designer        lead-programmer       art-director
  audio-director       narrative-director    qa-lead
  release-manager      localization-lead

Tier 3 — Specialists (Sonnet/Haiku)
  gameplay-programmer  engine-programmer     ai-programmer
  network-programmer   tools-programmer      ui-programmer
  systems-designer     level-designer        economy-designer
  technical-artist     sound-designer        writer
  world-builder        ux-designer           prototyper
  performance-analyst  devops-engineer       analytics-engineer
  security-engineer    qa-tester             accessibility-specialist
  live-ops-designer    community-manager
```

### Engine Specialists

The template includes agent sets for all three major engines. Use the set that matches your project:

| Engine | Lead Agent | Sub-Specialists |
|--------|-----------|-----------------|
| **Godot 4** | `godot-specialist` | GDScript, Shaders, GDExtension |
| **Unity** | `unity-specialist` | DOTS/ECS, Shaders/VFX, Addressables, UI Toolkit |
| **Unreal Engine 5** | `unreal-specialist` | GAS, Blueprints, Replication, UMG/CommonUI |

## Slash Commands

Type `/` in Claude Code to access all 34 skills:

**Reviews & Analysis**
`/design-review` `/code-review` `/balance-check` `/asset-audit` `/scope-check` `/perf-profile` `/tech-debt`

**Production**
`/sprint-plan` `/milestone-review` `/estimate` `/retrospective` `/bug-report`

**Project Management**
`/project-stage-detect` `/reverse-document` `/gate-check`

**Release**
`/release-checklist` `/launch-checklist` `/changelog` `/patch-notes` `/hotfix`

**Creative**
`/brainstorm` `/playtest-report` `/prototype` `/onboard` `/localize`

**Team Orchestration** (coordinate multiple agents on a single feature)
`/team-combat` `/team-narrative` `/team-ui` `/team-release` `/team-polish` `/team-audio` `/team-level`

## Getting Started

### Prerequisites

- [Git](https://git-scm.com/)
- [Claude Code](https://docs.anthropic.com/en/docs/claude-code) (`npm install -g @anthropic-ai/claude-code`)
- **Recommended**: [jq](https://jqlang.github.io/jq/) (for hook validation) and Python 3 (for JSON validation)

All hooks fail gracefully if optional tools are missing — nothing breaks, you just lose validation.

### Setup

1. **Clone or use as template**:
   ```bash
   git clone https://github.com/Donchitos/Claude-Code-Game-Studios.git my-game
   cd my-game
   ```

2. **Configure your engine** — run `/setup-engine` in Claude Code (or manually edit `CLAUDE.md`):

   ```bash
   /setup-engine godot 4.6
   ```

   This pins the engine, populates reference docs, and creates your technical preferences file.

3. **Open Claude Code** and start working:
   ```bash
   claude
   ```

4. **Try a slash command** to see the system in action:
   ```
   /brainstorm roguelike
   ```

### First Steps for a New Game

1. Run `/setup-engine` to configure your engine and technical preferences
2. Run `/brainstorm` to develop your game concept
3. Create game pillars with the `creative-director` agent
4. Record your architecture decisions with `/architecture-decision`
5. Plan your first sprint with `/sprint-plan new`
6. Prototype the core loop with `/prototype`
7. Start building

## Project Structure

```
CLAUDE.md                           # Master configuration
.claude/
  settings.json                     # Hooks, permissions, safety rules
  agents/                           # 48 agent definitions (markdown + YAML frontmatter)
  skills/                           # 34 slash commands (subdirectory per skill)
  hooks/                            # 8 hook scripts (bash, cross-platform)
  rules/                            # 11 path-scoped coding standards
  docs/
    quick-start.md                  # Detailed usage guide
    agent-roster.md                 # Full agent table with domains
    agent-coordination-map.md       # Delegation and escalation paths
    setup-requirements.md           # Prerequisites and platform notes
    templates/                      # 28 document templates
src/                                # Game source code
assets/                             # Art, audio, VFX, shaders, data files
design/                             # GDDs, narrative docs, level designs
docs/                               # Technical documentation and ADRs
tests/                              # Test suites
tools/                              # Build and pipeline tools
prototypes/                         # Throwaway prototypes (isolated from src/)
production/                         # Sprint plans, milestones, release tracking
```

## How It Works

### Agent Coordination

Agents follow a structured delegation model:

1. **Vertical delegation** — directors delegate to leads, leads delegate to specialists
2. **Horizontal consultation** — same-tier agents can consult each other but can't make binding cross-domain decisions
3. **Conflict resolution** — disagreements escalate up to the shared parent (`creative-director` for design, `technical-director` for technical)
4. **Change propagation** — cross-department changes are coordinated by `producer`
5. **Domain boundaries** — agents don't modify files outside their domain without explicit delegation

### Automated Safety

**Hooks** run automatically on every session:

| Hook | Trigger | What It Does |
|------|---------|--------------|
| `validate-commit.sh` | `git commit` | Checks for hardcoded values, TODO format, JSON validity, design doc sections |
| `validate-push.sh` | `git push` | Warns on pushes to protected branches |
| `validate-assets.sh` | File writes in `assets/` | Validates naming conventions and JSON structure |
| `session-start.sh` | Session open | Loads sprint context and recent git activity |
| `detect-gaps.sh` | Session open | Detects missing documentation when code/prototypes exist, suggests `/reverse-document` or `/project-stage-detect` |
| `pre-compact.sh` | Context compression | Preserves session progress notes |
| `session-stop.sh` | Session close | Logs accomplishments |
| `log-agent.sh` | Agent spawned | Audit trail of all subagent invocations |

**Permission rules** in `settings.json` auto-allow safe operations (git status, test runs) and block dangerous ones (force push, `rm -rf`, reading `.env` files).

### Path-Scoped Rules

Coding standards are automatically enforced based on file location:

| Path | Enforces |
|------|----------|
| `src/gameplay/**` | Data-driven values, delta time usage, no UI references |
| `src/core/**` | Zero allocations in hot paths, thread safety, API stability |
| `src/ai/**` | Performance budgets, debuggability, data-driven parameters |
| `src/networking/**` | Server-authoritative, versioned messages, security |
| `src/ui/**` | No game state ownership, localization-ready, accessibility |
| `design/gdd/**` | Required 8 sections, formula format, edge cases |
| `tests/**` | Test naming, coverage requirements, fixture patterns |
| `prototypes/**` | Relaxed standards, README required, hypothesis documented |

## Design Philosophy

This template is grounded in professional game development practices:

- **MDA Framework** — Mechanics, Dynamics, Aesthetics analysis for game design
- **Self-Determination Theory** — Autonomy, Competence, Relatedness for player motivation
- **Flow State Design** — Challenge-skill balance for player engagement
- **Bartle Player Types** — Audience targeting and validation
- **Verification-Driven Development** — Tests first, then implementation

## Customization

This is a **template**, not a locked framework. Everything is meant to be customized:

- **Add/remove agents** — delete agent files you don't need, add new ones for your domains
- **Edit agent prompts** — tune agent behavior, add project-specific knowledge
- **Modify skills** — adjust workflows to match your team's process
- **Add rules** — create new path-scoped rules for your project's directory structure
- **Tune hooks** — adjust validation strictness, add new checks
- **Pick your engine** — use the Godot, Unity, or Unreal agent set (or none)

## Platform Support

Tested on **Windows 10** with Git Bash. All hooks use POSIX-compatible patterns (`grep -E`, not `grep -P`) and include fallbacks for missing tools. Works on macOS and Linux without modification.

## License

MIT License. See [LICENSE](LICENSE) for details.
