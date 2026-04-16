# Agent Coordination Rules

1. **Vertical Delegation**: Leadership agents delegate to department leads, who
   delegate to specialists. Never skip a tier for complex decisions.
2. **Horizontal Consultation**: Agents at the same tier may consult each other
   but must not make binding decisions outside their domain.
3. **Conflict Resolution**: When two agents disagree, escalate to the shared
   parent. If no shared parent, escalate to `creative-director` for design
   conflicts or `technical-director` for technical conflicts.
4. **Change Propagation**: When a design change affects multiple domains, the
   `producer` agent coordinates the propagation.
5. **No Unilateral Cross-Domain Changes**: An agent must never modify files
   outside its designated directories without explicit delegation.

## Model Tier Assignment

Skills and agents are assigned to model tiers based on task complexity:

| Tier | Model | When to use |
|------|-------|-------------|
| **Haiku** | `claude-haiku-4-5-20251001` | Read-only status checks, formatting, simple lookups — no creative judgment needed |
| **Sonnet** | `claude-sonnet-4-6` | Implementation, design authoring, analysis of individual systems — default for most work |
| **Opus** | `claude-opus-4-6` | Multi-document synthesis, high-stakes phase gate verdicts, cross-system holistic review |

Skills with `model: haiku`: `/help`, `/sprint-status`, `/story-readiness`, `/scope-check`,
`/project-stage-detect`, `/changelog`, `/patch-notes`, `/onboard`

Skills with `model: opus`: `/review-all-gdds`, `/architecture-review`, `/gate-check`

All other skills default to Sonnet. When creating new skills, assign Haiku if the
skill only reads and formats; assign Opus if it must synthesize 5+ documents with
high-stakes output; otherwise leave unset (Sonnet).

## Subagents vs Agent Teams

This project uses two distinct multi-agent patterns:

### Subagents (current, always active)
Spawned via `Task` within a single Claude Code session. Used by all `team-*` skills
and orchestration skills. Subagents share the session's permission context, run
sequentially or in parallel within the session, and return results to the parent.

**When to spawn in parallel**: If two subagents' inputs are independent (neither
needs the other's output to begin), spawn both Task calls simultaneously rather
than waiting. Example: `/review-all-gdds` Phase 1 (consistency) and Phase 2
(design theory) are independent — spawn both at the same time.

### Agent Teams (experimental — opt-in)
Multiple independent Claude Code *sessions* running simultaneously, coordinated
via a shared task list. Each session has its own context window and token budget.
Requires `CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS=1` environment variable.

**Use agent teams when**:
- Work spans multiple subsystems that will not touch the same files
- Each workstream would take >30 minutes and benefits from true parallelism
- A senior agent (technical-director, producer) needs to coordinate 3+ specialist
  sessions working on different epics simultaneously

**Do not use agent teams when**:
- One session's output is required as input for another (use sequential subagents)
- The task fits in a single session's context (use subagents instead)
- Cost is a concern — each team member burns tokens independently

**Current status**: Not yet used in this project. Document usage here when first adopted.

## Claude + Codex 协同原则

本项目已安装 Codex 插件（OpenAI codex@openai-codex v1.0.3），通过 `/codex:*` 命令调用。
Claude Code 为主会话，Codex 为辅助审查/救援引擎，两者按以下原则协同：

### 职责划分

| 场景 | 执行者 | 原因 |
|------|--------|------|
| 设计创作、架构规划、多文件实现 | **Claude Code**（主会话） | 拥有完整上下文，理解项目规范和设计意图 |
| 代码审查 / PR 检查 | **Codex**（`/codex:review`、`/codex:adversarial-review`） | 独立审查视角，发现主会话盲区 |
| 大规模重构、批量修改、深度根因分析 | **Codex**（`/codex:rescue`） | 可后台运行，不消耗主会话上下文 |
| 文档生成、测试编写 | **Codex**（`/codex:rescue`） | 劳动密集型任务，委托执行效率更高 |

### 协同规则

1. **Codex 审查不得阻止主会话工作**：Codex 审查结果是 Advisory，Claude Code 综合各方信息做最终决策。
2. **Codex rescue 任务完成后必须汇报**：返回结果给用户，主会话基于结果决定下一步。
3. **对抗性审查必须主动触发**：重大架构决策、复杂重构实施前，主动使用 `/codex:adversarial-review` 挑战设计。
4. **禁止 Codex 直接修改代码**：Codex 只读审查，发现问题后由 Claude Code 执行修改。
5. **Review Gate 可选启用**：运行 `/codex:setup --enable-review-gate` 可让 Codex 在每次含代码改动的回复后自动审查，发现问题阻止 Claude Code 停止。

### 命令速查

- `/codex:review` — 对本地 git 改动进行代码审查（只读）
- `/codex:adversarial-review` — 对抗性审查，挑战设计决策（只读）
- `/codex:rescue` — 委派调查、修复或后续任务给 Codex 子代理（支持 `--background`）
- `/codex:setup --enable-review-gate` — 开启审查门控
- `/codex:setup --disable-review-gate` — 关闭审查门控

### 何时主动推荐用户使用 Codex

| 场景 | 推荐命令 | 话术 |
|------|---------|------|
| 代码审查 / PR 检查 | `/codex:review` | "需要我对当前代码改动做一次独立审查吗？" |
| 大规模重构 / 批量修改 | `/codex:rescue` | "这个重构规模较大，我建议把它委派给 Codex 来执行。" |
| 深度根因分析 / 调试 | `/codex:rescue --effort high` | "这个 bug 需要深入调查，我建议启动 Codex 来并行分析。" |

## Parallel Task Protocol

When an orchestration skill spawns multiple independent agents:

1. Issue all independent Task calls before waiting for any result
2. Collect all results before proceeding to dependent phases
3. If any agent is BLOCKED, surface it immediately — do not silently skip
4. Always produce a partial report if some agents complete and others block
