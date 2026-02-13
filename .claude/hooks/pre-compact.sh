#!/bin/bash
# Claude Code PreCompact hook: Save session state before context compression
# Ensures progress notes survive context window compression

TIMESTAMP=$(date +%Y%m%d_%H%M%S)
SESSION_LOG_DIR="production/session-logs"

# Create session log directory if it doesn't exist
mkdir -p "$SESSION_LOG_DIR" 2>/dev/null

# Save a marker file noting that compaction occurred
echo "Context compaction occurred at $(date). Session state may have been compressed." \
    >> "$SESSION_LOG_DIR/compaction-log.txt" 2>/dev/null

exit 0
