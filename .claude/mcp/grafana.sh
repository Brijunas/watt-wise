#!/usr/bin/env bash
# MCP server (.mcp.json): logs, traces and metrics from Grafana LGTM for Claude Code.
# Needs Docker and the Development stack running. The URL is not a secret, so it is read
# from the rendered deploy/.env (GRAFANA_URL, see deploy/development/compose.settings.tpl)
# and this server never calls `op`. Started by Claude Code, not by hand.
set -euo pipefail

# deploy/.env is git-ignored, so a worktree has none: read the main checkout's.
main=$(dirname "$(realpath "$(git -C "$(dirname "$(realpath "$0")")" rev-parse --git-common-dir)")")
env_file="$main/deploy/.env"

GRAFANA_URL=$(sed -n 's/^GRAFANA_URL="\{0,1\}\([^"]*\)"\{0,1\}$/\1/p' "$env_file" 2>/dev/null || true)
if [[ -z "$GRAFANA_URL" ]]; then
  echo "grafana MCP: no GRAFANA_URL in $env_file; render it again, see docs/development.md" >&2
  exit 1
fi
export GRAFANA_URL

exec docker run -i --rm --init --network host -e GRAFANA_URL grafana/mcp-grafana:2.0.2 -t stdio
