#!/usr/bin/env bash
# MCP server (.mcp.json): GitHub for Claude Code. Needs Docker and `gh auth login`.
# The token is the active gh account's, read at start; it lives only in this process's
# environment and is never written to disk, printed or switched.
# Started by Claude Code, not by hand.
set -euo pipefail

if ! token=$(gh auth token 2>/dev/null) || [[ -z "$token" ]]; then
  echo 'github MCP: run `gh auth login` first' >&2
  exit 1
fi

GITHUB_PERSONAL_ACCESS_TOKEN="$token" exec docker run -i --rm --init -e GITHUB_PERSONAL_ACCESS_TOKEN ghcr.io/github/github-mcp-server:v2.0.2 stdio
