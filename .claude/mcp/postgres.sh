#!/usr/bin/env bash
# MCP server (.mcp.json): read-only PostgreSQL access for Claude Code, as the `reader` role.
# Needs Docker, the Development stack running and `op` signed in. Settings come from
# postgres.env. Started by Claude Code, not by hand.
#
# op run executes this same script again with --inner, so the second run sees the resolved
# PGMCP_* variables and can build DATABASE_URI from them.
#
# --no-masking: op run masks secret values in the child's stdout by default, and MCP speaks
# JSON-RPC over stdout, so a masked password in any reply would corrupt it.
set -euo pipefail

dir=$(dirname "$(realpath "$0")")

if [[ "${1:-}" != "--inner" ]]; then
  exec op run --no-masking --env-file "$dir/postgres.env" -- "$dir/postgres.sh" --inner
fi

password=$(python3 -c 'import urllib.parse,sys;print(urllib.parse.quote(sys.argv[1],safe=""))' "$PGMCP_PASSWORD")
export DATABASE_URI="postgresql://${PGMCP_USER}:${password}@${PGMCP_HOST}:${PGMCP_PORT}/${PGMCP_DB}"

exec docker run -i --rm --init --network host -e DATABASE_URI crystaldba/postgres-mcp:0.3.0 --access-mode=restricted
