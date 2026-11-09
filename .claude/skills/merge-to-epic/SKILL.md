---
name: merge-to-epic
description: Squash-merge a finished story branch `story/<E>/<id>` into its epic branch `epic/<E>` locally, with no PR, and clean the story up. Used by the merge-runner agent during an epic run; not for the story flow (use merge-to-main there).
argument-hint: <E> <id>
disable-model-invocation: true
---

# Merge a story into its epic

Arguments: `$0` is the epic (`E4`), `$1` the story ID (`S4.3`, or `S4.3-backend` for a part). There is no PR: epic branches take direct signed pushes ([agentic-delivery.md](../../../docs/agentic-delivery.md)). Run it from the epic checkout, the worktree on `epic/<E>`.

The caller gives you the gate result and the review dispositions for the commit body. Without them, stop and ask for them.

## 1. Preflight

Any failure stops the run; report which one.

- `git branch --show-current` is `epic/$0`, and `git status --short` is empty.
- `git fetch origin --prune`, then `git merge --ff-only origin/epic/$0`: the epic branch is up to date with origin.
- `git rev-parse origin/story/$0/$1` exists, and the local `story/$0/$1` (if any) has the same SHA: the story branch is pushed.
- `git log --oneline epic/$0..origin/story/$0/$1` lists at least one commit.

## 2. Squash and commit

1. `git merge --squash origin/story/$0/$1`. A conflict stops the run: `git merge --abort` is not available for squash, so `git reset --hard HEAD` and report the files.
2. One signed commit through the pre-commit hook (no `--no-verify`), title from the story plan's heading:

   ```bash
   git commit -S -m "$1: <title>" -m "<body>"
   ```

   Body: the gate result (checks passed, mutation scores, suppressions: 0) and one line per review finding with its disposition (fixed in round N, or declined with the reason). No AI attribution lines.

3. `git log -1 --show-signature` must show a good signature. If not, stop.

## 3. Push

`git push origin epic/$0`. No force, ever. A rejected push means the epic moved: `git reset --hard origin/epic/$0` is wrong here (it drops the commit); stop and report so the caller reruns the merge.

## 4. Clean up the story

Only after the push succeeded:

1. `git push origin --delete story/$0/$1`
2. `git worktree list`: remove the story's worktree with `git worktree remove --force <path>`, then `git branch -D story/$0/$1` (squash rewrites hashes, so `-d` can't tell it was merged).

## Report

The new epic head (`git log -1 --oneline`), the signature status, and what was deleted.
