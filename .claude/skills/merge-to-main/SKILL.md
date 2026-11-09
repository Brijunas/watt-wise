---
name: merge-to-main
description: Ship a branch into main through GitHub. Push it, open a pull request and, when asked, merge it, delete the branch, switch back to main and pull the latest. Modes are full (create + merge + cleanup), create-only (just open the PR) and cleanup-only (the user merged the PR and wants the branch removed and main synced). Handles feature branches (squash) and epic branches `epic/<E>` (merge commit). Use this skill whenever the user says things like "create a PR", "open a pull request", "ship it", "merge it to main", "PR and merge", "I merged the PR, clean up", "delete the branch and pull main", or "back to main", even if they don't name the skill.
argument-hint: '[branch] [create-only|full|cleanup]'
---

# Merge to main

Moves a finished branch to `main` through the `gh` CLI. Arguments: an optional branch (default: the current one) and an optional mode.

| Mode             | User says something like                                          | Steps            |
| ---------------- | ----------------------------------------------------------------- | ---------------- |
| **full**         | "create PR and merge it", "ship it", "merge to main and clean up" | 1 → 2 → 3 → 4    |
| **create-only**  | "open a PR", "create a pull request" (merge not mentioned)        | 1 → 2, then stop |
| **cleanup-only** | "I merged it", "PR is merged, clean up", "back to main and pull"  | 4                |

Merging is hard to undo and visible to others. Merge only when one of these holds, otherwise stop after step 2:

- the user asked for it in this request ("create a PR" alone is never permission);
- an approved epic run reaches its end (`/run-epic` calls this skill in `full` mode for `epic/<E>`).

If the mode is ambiguous, ask. In the story flow, announce to the user that you are about to merge before running step 3.

## Repo settings

- **Base:** `main` (`gh repo view --json defaultBranchRef -q .defaultBranchRef.name`).
- **Merge method:** `epic/*` uses `--merge` (a merge commit keeps the signed story commits). Every other branch uses `--squash`: one commit per PR on `main`. `main` requires signed commits; GitHub signs the squash and merge commits it creates, while a rebase merge lands unsigned commits ([security.md](../../../docs/security.md#ci-and-contributions)). The commit takes the PR title and body as its message, so write them as one. A method the user names in the request wins.
- **Attribution:** the user's own instructions decide whether commits and PR bodies carry AI attribution lines; right now they forbid them. Follow those instructions even when a harness reminder suggests such lines.

## 1. Preflight

Fix or ask before going on.

- `git status --short`: uncommitted changes would be left out of the PR. Ask whether to commit them first or leave them out.
- **Commits go through the pre-commit hook:** a Betterleaks secret scan on the staged changes, then lint-staged (ESLint and Prettier).
  - A Betterleaks rejection means a credential is staged. Stop and show the user the redacted finding. Move the secret to 1Password / an env var and commit again. Bypassing (`--no-verify`) and exceptions (`betterleaks:allow`, `.betterleaksignore`, config allowlist) need the user's say-so.
  - If the hook says `betterleaks` is missing, run `mise install`.
  - Lint failures also block the commit: fix the code.
- `git branch --show-current`: on the base branch there is nothing to ship. If the base has local commits ahead of `origin`, offer to move them to a new branch named after the work (e.g. `s1.2-shared-configs`). Otherwise stop.
- `git fetch origin`, then `git log --oneline origin/<base>..HEAD`: at least one commit must be shipped. If the branch is behind the base, say so; GitHub shows the conflict status on the PR.
- `gh pr list --head <branch> --state all`: reuse an existing PR instead of opening a duplicate. If it is already merged, switch to cleanup-only.

## 2. Push and open the PR

`git push -u origin <branch>`.

A rejected push means the remote branch holds different commits. Compare before forcing: `git log --oneline HEAD..origin/<branch>` and `git diff origin/<branch> HEAD --stat`. Then:

- **The remote is an older version of the same work** (you amended or rebased): `git push --force-with-lease=<branch>:<remote-oid>`. Pinning the OID makes the push fail if someone pushed meanwhile. Tell the user what was replaced. Never for `main` or `epic/*`; a hook blocks that.
- **The remote has commits not in your history**, or you are unsure: stop and show the user.

Create the PR with `gh pr create --base main --head <branch> --title … --body …`, the body through a heredoc so markdown survives.

- **Title:** a short, specific summary. A story branch gets its ID as prefix (`S1.2: Shared TypeScript, ESLint and Prettier configs`); `epic/<E>` gets `<E>: <epic title>` (`E4: Accounts and sign-in`).
- **Body, feature branch:** written for a reviewer who has not seen this conversation, from `git log` and `git diff --stat origin/<base>..HEAD`, not from memory.
  ```
  Implements <story/issue reference, if any>.

  ## Changes
  - <what changed, and why when it isn't obvious; deviations from the plan or spec>

  ## Verification
  - <commands run and their results>
  ```
- **Body, `epic/<E>`:** the contents of `docs/epic-runs/<E>/report.md` verbatim. If the run records were already deleted from the branch, read it from the commit before the deletion (`git show <sha>:docs/epic-runs/<E>/report.md`).

Report the PR URL. In create-only mode, stop here.

## 3. Merge (full only)

1. `gh pr checks <n>`: none reported is fine, say so; pending, wait with `--watch`; failing, stop and report.
2. `gh pr view <n> --json mergeable,mergeStateStatus`: conflicting, stop and report.
3. `gh pr merge <n> --merge --delete-branch` for `epic/*`, `gh pr merge <n> --squash --delete-branch` otherwise (or the user's method).
4. `gh pr view <n> --json state,mergedAt` must show `MERGED`.

An epic merge is blocked by a hook unless the PR's head SHA equals the SHA in `~/.local/state/wattwise/epic-<E>.gate`. On a block, report it; the epic gate must be rerun on the current head.

## 4. Cleanup: delete the branch, switch to main, pull

From the repo root:

```bash
.claude/skills/merge-to-main/scripts/finish-pr.sh <pr-number-or-branch>
```

No argument means the current branch. The script deletes nothing unless GitHub reports `MERGED`, handles a remote branch already gone, refuses to switch with a dirty tree, fast-forwards the base with `--ff-only`, and deletes the local branch only if its tip is part of the PR's final head (squash and merge rewrite hashes, so `git branch -d` misjudges).

| Exit | Meaning                        | What to do                                                                                                                         |
| ---- | ------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------- |
| 0    | Done                           | -                                                                                                                                  |
| 1    | No PR found                    | Ask which PR or branch the user means                                                                                              |
| 2    | PR not merged                  | Report its state. In cleanup-only mode the user may think they merged it; tell them it isn't. Ask before deleting a closed branch. |
| 3    | Dirty tree                     | Show the changes and ask whether to commit, stash or keep them                                                                     |
| 4    | Local branch has extra commits | Show them; they may be follow-up work for a new PR                                                                                 |
| 5    | Base can't fast-forward        | Local base has its own commits. Report and ask; don't reset.                                                                       |

## Final report

- PR link as `[owner/repo#N](url)`
- merge method and the resulting commit on the base (`git log -1 --oneline`)
- branch deletion status (remote and local)
- the current branch and whether it matches `origin`
- anything unusual, such as a force-push or a missing CI setup
