#!/usr/bin/env bash
# MCP server (.mcp.json): logs, traces and metrics from Grafana LGTM for Claude Code.
# Needs Docker, the Development stack running and `op` signed in. The URL comes from
# grafana.env. Started by Claude Code, not by hand.
#
# --no-masking: op run masks secret values in the child's stdout by default, and MCP speaks
# JSON-RPC over stdout.
set -euo pipefail

dir=$(dirname "$(realpath "$0")")

exec op run --no-masking --env-file "$dir/grafana.env" -- \
  docker run -i --rm --init --network host -e GRAFANA_URL grafana/mcp-grafana:2.0.2 -t stdio
