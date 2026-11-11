#!/usr/bin/env bash
# Post-merge cleanup for one pull request.
# Usage: finish-pr.sh <pr-number | branch-name>
#
# Deletes the PR's head branch (remote and local), switches to the base
# branch and fast-forwards it. Refuses to delete anything unless GitHub
# reports the PR as MERGED, and refuses to delete a local branch that holds
# commits the merged PR did not contain.
#
# Exit codes: 0 done, 1 usage/lookup error, 2 PR not merged,
#             3 dirty working tree, 4 local branch has unmerged work,
#             5 base branch cannot fast-forward.
set -euo pipefail

target="${1:-}"
if [[ -z "$target" ]]; then
  target="$(git branch --show-current)"
fi

if ! pr_json="$(gh pr view "$target" --json number,state,headRefName,headRefOid,baseRefName,url 2>/dev/null)"; then
  echo "No pull request found for '$target'." >&2
  exit 1
fi

field() { jq -r ".$1" <<<"$pr_json"; }
number="$(field number)"
state="$(field state)"
head="$(field headRefName)"
head_oid="$(field headRefOid)"
base="$(field baseRefName)"
url="$(field url)"

echo "PR #$number ($url): $state, $head -> $base"

if [[ "$state" != "MERGED" ]]; then
  echo "PR is $state, not MERGED. Nothing deleted." >&2
  exit 2
fi

git fetch --prune --quiet origin

# Remote branch: delete it if the merge didn't already.
if git ls-remote --exit-code --heads origin "$head" >/dev/null 2>&1; then
  git push --quiet origin --delete "$head"
  echo "Deleted remote branch origin/$head."
else
  echo "Remote branch origin/$head already deleted."
fi

# Switch to base. A dirty tree would either block the switch or travel along.
current="$(git branch --show-current)"
if [[ "$current" != "$base" ]]; then
  if [[ -n "$(git status --porcelain)" ]]; then
    echo "Working tree has uncommitted changes; not switching to $base." >&2
    git status --short >&2
    exit 3
  fi
  git switch --quiet "$base"
fi

if ! git pull --ff-only --quiet origin "$base"; then
  echo "$base cannot fast-forward to origin/$base (local commits on $base?)." >&2
  exit 5
fi
echo "On $base at $(git log -1 --format='%h %s')."

# Local branch: rebase/squash merges change commit hashes, so `git branch -d`
# can't tell it was merged. Compare against the PR's final head commit instead.
if git show-ref --verify --quiet "refs/heads/$head"; then
  local_oid="$(git rev-parse "refs/heads/$head")"
  if [[ "$local_oid" == "$head_oid" ]] || git merge-base --is-ancestor "$local_oid" "$head_oid" 2>/dev/null; then
    git branch --quiet -D "$head"
    echo "Deleted local branch $head."
  else
    echo "Local branch $head has commits that were not in the merged PR; kept it." >&2
    git log --oneline "$head_oid..$head" >&2 || true
    exit 4
  fi
else
  echo "Local branch $head already deleted."
fi
