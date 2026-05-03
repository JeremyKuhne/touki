---
name: create-pr
description: Create a pull request for the current changes. Use when asked to "make a PR", "open a pull request", "push and PR", or otherwise publish in-progress work for review. Ensures changes are on a non-`main` branch, commits are made and pushed, and the PR targets `upstream/main` when an `upstream` remote exists, otherwise `origin/main`.
---

# Create a pull request

Follow these steps in order. Stop and ask the user if any check is ambiguous;
do not force-push, rewrite history, or delete branches without explicit
confirmation.

## 1. Inspect repository state

Run these (read-only) checks first:

- `git remote -v` — determine whether an `upstream` remote exists.
- `git rev-parse --abbrev-ref HEAD` — current branch name.
- `git status --porcelain` — uncommitted changes.
- `git log --oneline @{u}.. 2>$null` (if upstream is set) — unpushed commits.

Decide the PR base:

- If a remote literally named `upstream` exists, the PR base is
  `upstream/main` (i.e. `--repo` points at the upstream repo, base branch
  `main`).
- Otherwise the PR base is `origin/main` (the fork itself, base `main`).

## 2. Ensure work is on a feature branch

- If the current branch is `main`, **do not commit on `main`**. Create a new
  branch from the current `HEAD` with a short, descriptive, kebab-case name
  derived from the change (e.g. `fix-span-enumerate-lines`,
  `add-create-pr-skill`). Confirm the name with the user if it is not
  obvious.
- Use `git switch -c <branch>` to move uncommitted changes onto the new
  branch.
- If already on a non-`main` branch, keep using it.

## 3. Commit changes

- If `git status` shows uncommitted changes that belong in the PR, stage them
  with `git add` (prefer explicit paths over `git add -A` unless the user
  asked for everything).
- Write a concise commit message: short imperative subject (≤ 72 chars), and
  a body only if the change needs explanation. Match the style of recent
  commits (`git log --oneline -20`).
- Do not amend or rebase published commits without explicit user approval.
- Do not pass `--no-verify`; let hooks run.

## 4. Push the branch

- Push to `origin` (the user's fork / working remote), setting upstream on
  first push:

  ```pwsh
  git push -u origin <branch>
  ```

- Never `git push --force` or `--force-with-lease` without explicit user
  confirmation.

## 5. Open the PR

Prefer the GitHub CLI (`gh`) when available. Determine the target with the
rule from step 1:

- **Upstream exists** — PR against the upstream repo. Resolve the upstream
  `owner/repo` from `git remote get-url upstream`, then:

  ```pwsh
  gh pr create --repo <upstreamOwner>/<repo> --base main --head <forkOwner>:<branch> --title "<title>" --body "<body>"
  ```

- **No upstream** — PR within `origin`:

  ```pwsh
  gh pr create --base main --head <branch> --title "<title>" --body "<body>"
  ```

PR title and body:

- Title: same style as the commit subject; reference the area touched.
- Body: brief summary of what changed and why, bullet list of notable
  changes, and any test/validation notes (e.g. "ran `dotnet test`"). Link
  related issues with `Fixes #N` when appropriate.
- If the user has not supplied a title/body, propose one and confirm before
  creating.

If `gh` is unavailable, print the compare URL the user can open in a
browser:

- Upstream: `https://github.com/<upstreamOwner>/<repo>/compare/main...<forkOwner>:<branch>?expand=1`
- Origin only: `https://github.com/<originOwner>/<repo>/compare/main...<branch>?expand=1`

## 6. Report back

Tell the user:

- The branch name and where it was pushed.
- The base the PR targets (`upstream/main` or `origin/main`).
- The PR URL (from `gh pr create` output) or the compare URL fallback.

## Guardrails

- Never commit directly to `main`, even if the user is currently on it —
  always branch first.
- Never delete branches, force-push, or rewrite history as part of this
  workflow.
- If the working tree has unrelated changes, ask the user which files belong
  in the PR before staging.
