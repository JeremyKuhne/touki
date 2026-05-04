---
name: create-pr
description: Create a pull request for the current changes. Use when asked to "make a PR", "open a pull request", "push and PR", or otherwise publish in-progress work for review. Ensures changes are on a non-`main` branch, commits are made and pushed, and the PR targets `upstream/main` when an `upstream` remote exists, otherwise `origin/main`.
---

# Create a pull request

Follow these steps in order. Stop and ask the user if any check is ambiguous;
do not force-push, rewrite history, or delete branches without explicit
confirmation.

Before running this workflow, walk through the
[`pre-pr-self-review`](../pre-pr-self-review/SKILL.md) checklist &mdash; it
catches the test, allocation, overflow-arithmetic, and TFM-phrasing mistakes
that have repeatedly cost a review round-trip on this repo. If the change
polyfills a .NET API for .NET Framework, the
[`polyfill-dotnet-api`](../polyfill-dotnet-api/SKILL.md) skill defines the
design rules the self-review then validates against.

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
  `add-create-pr-skill`).
- When asking the user to confirm the branch name, use `vscode_askQuestions`
  with the suggested name as a selectable option **and** allow free-form
  input so the user can either accept the suggestion with one click or type
  a different name. Example: a single question whose `options` contains the
  suggested kebab-case name and whose `allowFreeformInput` is left at the
  default (`true`). Note that `vscode_askQuestions` requires at least two
  options or none — when offering a suggestion, pair it with a second
  option such as `Use a different name` (which the user can ignore in favor
  of free-form text), or omit `options` entirely and rely on free text.
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

**Prefer the VS Code GitHub Pull Requests tool**
(`github-pull-request_create_pull_request`) when it is available in the
current tool set — it works without requiring `gh` to be installed and
returns the PR number/URL directly. Fall back to the GitHub CLI (`gh`) if
the VS Code tool is not available, and only fall back to a browser compare
URL if neither is available.

Determine the target with the rule from step 1.

### Option A — VS Code GitHub Pull Requests tool (preferred)

Call `github-pull-request_create_pull_request` with:

- `title`, `body` (see PR title/body guidance below),
- `head` = the feature branch name (no owner prefix),
- `base` = `main`,
- For an upstream-targeted PR, set `repo` to `{ owner: <upstreamOwner>, name: <repo> }` and `headOwner` to the fork owner.
- For an origin-only PR, omit `repo` and `headOwner` (they default correctly).

### Option B — GitHub CLI fallback

- **Upstream exists**:

  ```pwsh
  gh pr create --repo <upstreamOwner>/<repo> --base main --head <forkOwner>:<branch> --title "<title>" --body "<body>"
  ```

- **No upstream**:

  ```pwsh
  gh pr create --base main --head <branch> --title "<title>" --body "<body>"
  ```

### PR title and body

- Title: same style as the commit subject; reference the area touched.
- Body: brief summary of what changed and why, bullet list of notable
  changes, and any test/validation notes (e.g. "ran `dotnet test`"). Link
  related issues with `Fixes #N` when appropriate.
- If the user has not supplied a title/body, propose one and confirm before
  creating.

### Option C — Browser fallback

If neither the VS Code tool nor `gh` is available, print the compare URL:

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
