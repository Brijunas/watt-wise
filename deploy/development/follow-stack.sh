#!/bin/sh
# Called by the husky hooks post-merge, post-checkout and post-rewrite: rebuild the Development stack
# when it runs from the checkout where the git command ran (deploy/docs/development-stack.md, "Following git").
# Never blocks git: it always exits 0. With --skip-mid-rebase (post-checkout) it also stays silent while
# a rebase, cherry-pick, revert or bisect is in progress; post-merge and post-rewrite fire with that state still set.
container=wattwise-development-api-1
hint='op run --env-file deploy/development/compose.env -- docker compose -f deploy/docker-compose.development.yml up -d --wait'

command -v docker > /dev/null 2>&1 || exit 0
top=$(git rev-parse --show-toplevel 2> /dev/null) || exit 0
gitdir=$(git rev-parse --absolute-git-dir 2> /dev/null) || exit 0

if [ "${1-}" = "--skip-mid-rebase" ]; then
  for state in rebase-merge rebase-apply CHERRY_PICK_HEAD REVERT_HEAD sequencer BISECT_LOG; do
    [ -e "$gitdir/$state" ] && exit 0
  done
fi

# The bind source of /src; empty or an error means no stack to follow.
source=$(docker inspect --format '{{range .Mounts}}{{if eq .Destination "/src"}}{{.Source}}{{end}}{{end}}' "$container" 2> /dev/null) || exit 0
[ -n "$source" ] || exit 0

source_real=$(cd "$source" 2> /dev/null && pwd -P) || exit 0
top_real=$(cd "$top" 2> /dev/null && pwd -P) || exit 0
[ "$source_real" = "$top_real" ] || exit 0

# An agent session (Claude Code sets CLAUDECODE=1, Codex sets CODEX_THREAD_ID in its commands) must not
# run `op`: it would put a 1Password prompt in front of the user. It prints the command instead.
if [ "${CLAUDECODE-}" = 1 ] || [ -n "${CODEX_THREAD_ID-}" ]; then
  echo 'follow-stack: the Development stack runs from this checkout; rebuild it with:' >&2
  echo "  $hint" >&2
  exit 0
fi

echo 'follow-stack: the Development stack runs from this checkout; rebuilding (Ctrl-C to skip)' >&2
if ! (cd "$top" && op run --env-file deploy/development/compose.env -- docker compose -f deploy/docker-compose.development.yml up -d --wait < /dev/null); then
  echo 'follow-stack: rebuild failed; run it yourself:' >&2
  echo "  $hint" >&2
fi
exit 0
