# Context Management

Context is the most critical resource in a Claude Code session. Manage it actively:

- **Compact proactively** at ~65-70% context usage, not reactively when at the limit
- **Use `/clear`** between unrelated tasks, or after 2+ failed correction attempts
- **Context budgets by task type**:
  - Light (read/review): ~3k tokens startup
  - Medium (implement feature): ~8k tokens
  - Heavy (multi-system refactor): ~15k tokens
- **Preserve during compaction**: modified file list, active sprint tasks, architectural
  decisions made this session, agent invocation outcomes, test results, unresolved blockers
- When delegating to subagents, provide full context in the prompt -- subagents do not
  inherit conversation history unless explicitly given it

# Compaction Instructions

When context is compacted, preserve the following in the summary:

- List of files modified in this session and their purpose
- Any architectural decisions made and their rationale
- Active sprint tasks and their current status
- Agent invocations and their outcomes (success/failure/blocked)
- Test results (pass/fail counts, specific failures)
- Unresolved blockers or questions awaiting user input
- The current task and what step we are on
