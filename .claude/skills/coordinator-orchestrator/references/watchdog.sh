#!/usr/bin/env bash
# Watchdog precheck for the coordinator orchestrator. Uses no tokens.
# Usage: watchdog.sh <progress file> <coordinator transcript folder> <run name>
# Exit 0 (fire the automation) only when the run is neither DONE nor STOPPED and the coordinator has stopped:
#   - its newest transcript's last user/assistant entry is a usage-limit error
#     (an assistant entry with "error": "rate_limit"), or
#   - no running process has its session id (the transcript's file name) on its command line
#     and the transcript has not been written for 20 minutes.
# Any other exit skips the run.
set -u

progress=${1:?progress file}
folder=${2:?transcript folder}
run=${3:?run name}

# Run finished or stopped for the user: nothing to resume.
if grep -qF -e "$run DONE" -e "$run STOPPED" "$progress" 2>/dev/null; then
  exit 1
fi

# Newest top-level transcript (subagent transcripts live in subfolders).
newest=$(ls -t "$folder"/*.jsonl 2>/dev/null | head -n 1)
[ -n "$newest" ] || exit 1

# Last conversation entry; the file ends with metadata entries (pr-link, cost-state, ...).
last_error=$(tail -n 200 "$newest" | jq -rs '[.[] | select(.type == "user" or .type == "assistant")] | last | .error // empty' 2>/dev/null)
if [ "$last_error" = "rate_limit" ]; then
  exit 0
fi

# Session gone: no process carries the session id, and the transcript has been quiet.
session=$(basename "$newest" .jsonl)
if ! pgrep -f -- "$session" >/dev/null 2>&1; then
  if [ -z "$(find "$newest" -mmin -20 2>/dev/null)" ]; then
    exit 0
  fi
fi

exit 1
