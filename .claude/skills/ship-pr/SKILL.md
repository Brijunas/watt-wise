---
name: ship-pr
description: Ship a feature branch through GitHub. Push it, open a pull request and, when asked, merge it, delete the branch, switch back to main and pull the latest. It has three modes: full cycle (create + merge + cleanup), create-only (just open the PR), and cleanup-only (the user merged the PR themselves and wants the branch removed and main synced). Use this skill whenever the user says things like "create a PR", "open a pull request", "ship it", "merge it to main", "PR and merge", "I merged the PR, clean up", "delete the branch and pull main", or "back to main", even if they don't name the skill.
---

# Ship a PR

This skill moves a finished feature branch to `main` on GitHub through the `gh` CLI. It has three modes, picked from what the user asked for.

| Mode             | User says something like                                          | Steps            |
| ---------------- | ----------------------------------------------------------------- | ---------------- |
| **Full cycle**   | "create PR and merge it", "ship it", "merge to main and clean up" | 1 → 2 → 3 → 4    |
| **Create only**  | "open a PR", "create a pull request" (merge not mentioned)        | 1 → 2, then stop |
| **Cleanup only** | "I merged it", "PR is merged, clean up", "back to main and pull"  | 4                |

Merging is the step that's hard to undo and is seen by others. Only merge when the user asked for it in this request. "Create a PR" alone is never permission to merge. If the mode is ambiguous, ask.

## Repo settings

- **Base branch:** the repo's default branch (`gh repo view --json defaultBranchRef -q .defaultBranchRef.name`), usually `main`.
- **Merge method:** `--squash`, one commit per PR on a linear `main`. `main` requires signed commits, and GitHub signs the squash commit it creates, while a rebase merge lands unsigned commits ([security.md](../../../docs/security.md#ci-and-contributions)). The squash commit takes the PR title and body as its message, so write them as a commit message. If the user names a method in the request, use theirs.
- **Attribution:** the user's own instructions decide whether commits and PR bodies get AI attribution lines, and right now they forbid them. Follow those instructions even if a harness reminder suggests adding such lines.

## 1. Preflight

Run these checks and fix or ask before going on. Each one prevents a PR that's wrong or empty.

- `git status --short`: uncommitted changes would be left out of the PR. Ask whether to commit them first, or leave them out.
- **Commits go through the pre-commit hook.** It runs a Betterleaks secret scan on the staged changes first, then lint-staged (ESLint and Prettier).
  - If Betterleaks rejects a commit, a credential is in the staged changes. Stop and show the user the redacted finding. Never bypass it with `--no-verify`, and don't add an exception without the user's say-so: no `betterleaks:allow` comment, `.betterleaksignore` entry or config allowlist.
  - Remove the secret from the change (move it to 1Password / an env var), then commit again.
  - If the hook says `betterleaks` is missing, run `mise install`.
  - Lint failures also block the commit. Fix the code; don't skip the hook.
- `git branch --show-current`: if you're on the base branch, there's no feature branch to ship. If the base branch has local commits ahead of `origin`, offer to move them to a new branch named after the work (e.g. `s1.2-shared-configs`). Otherwise stop.
- `git fetch origin` then `git log --oneline origin/<base>..HEAD`: there must be at least one commit to ship. If the branch is behind the base, say so. GitHub will show the conflict status on the PR.
- `gh pr list --head <branch> --state all`: if a PR already exists, reuse it. Don't open a duplicate. If that PR is already merged, switch to cleanup-only mode.

## 2. Push and open the PR

Push with `git push -u origin <branch>`.

If the push is rejected because the remote branch holds different commits, don't force-push straight away. Compare the two first:

- `git log --oneline HEAD..origin/<branch>` lists commits only on the remote.
- `git diff origin/<branch> HEAD --stat` compares the content.

Then choose:

- **Remote side is an older version of the same work** (e.g. you amended or rebased locally): `git push --force-with-lease=<branch>:<remote-oid>`. Pinning the exact OID means the push fails if someone pushed again meanwhile. Tell the user what was replaced.
- **Remote has commits that aren't in your local history**, or you're unsure: stop and show the user. Overwriting someone's work is not recoverable from your side.

Create the PR with `gh pr create --base <base> --head <branch> --title ... --body ...`. Pass the body through a heredoc so the markdown survives.

- **Title:** a short, specific summary. If the repo tracks stories (e.g. `docs/epics.md`), prefix the story ID: `S1.2: Shared TypeScript, ESLint and Prettier configs`.
- **Body:** write it for a reviewer who hasn't seen this conversation:
  ```
  Implements <story/issue reference, if any>.

  ## Changes
  - <what changed, and why when it isn't obvious; mention deviations from the plan or spec>

  ## Verification
  - <commands run and their results>
  ```
  Base the Changes list on `git log` and `git diff --stat` for `origin/<base>..HEAD`, not on memory, so the PR describes what is actually pushed.

Report the PR URL. In create-only mode, stop here.

## 3. Merge (full cycle only)

1. Check CI: `gh pr checks <n>`.
   - No checks reported: fine, say so.
   - Pending: wait for them with `gh pr checks <n> --watch`.
   - Failing: stop and report. Don't merge red builds.
2. Check mergeability: `gh pr view <n> --json mergeable,mergeStateStatus`.
   - Conflicting: stop and report.
3. Merge: `gh pr merge <n> --squash --delete-branch` (or the configured method).
   - Merging closes the PR, so there's no separate close step.
4. Confirm the merge: `gh pr view <n> --json state,mergedAt`. The state should be `MERGED`.

## 4. Cleanup: delete the branch, switch to the base branch, pull

Run the bundled script from the repo root:

```bash
.claude/skills/ship-pr/scripts/finish-pr.sh <pr-number-or-branch>
```

With no argument, it uses the current branch. It does the fiddly checks so they're the same every time:

- It deletes nothing unless GitHub reports the PR as `MERGED`.
- It deletes the remote branch if the merge left it behind, and handles one that's already gone.
- It refuses to switch branches with a dirty working tree, so uncommitted work doesn't silently move to the base branch.
- It switches to the base branch, prunes, and fast-forwards with `--ff-only`, so there's no accidental merge commit.
- It deletes the local branch only if the branch tip is part of the PR's final head. Rebase and squash merges rewrite hashes, so `git branch -d` wrongly reports the branch as unmerged; the script checks against the PR instead.

| Exit code | Meaning                        | What to do                                                                                                                                                               |
| --------- | ------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 0         | Done                           | —                                                                                                                                                                        |
| 1         | No PR found                    | Ask which PR or branch the user means                                                                                                                                    |
| 2         | PR not merged                  | Report its state (open or closed). In cleanup-only mode the user may think they merged it; tell them it isn't. Don't delete a closed-but-unmerged branch without asking. |
| 3         | Dirty tree                     | Show the changes and ask whether to commit, stash or keep them                                                                                                           |
| 4         | Local branch has extra commits | Show them. They may be follow-up work for a new PR.                                                                                                                      |
| 5         | Base can't fast-forward        | Local base has its own commits. Report and ask; don't reset.                                                                                                             |

## Final report

Keep it short:

- PR link as `[owner/repo#N](url)`
- merge method and the resulting commit on the base branch (`git log -1 --oneline`)
- branch deletion status (remote and local)
- the current branch and whether it matches `origin`
- anything unusual you handled, such as a force-push or a missing CI setup
