---
name: create-pr
description: Create a pull request for the current changes. Use when asked to "make a PR", "open a pull request", "push and PR", or otherwise publish in-progress work for review. Ensures changes are on a non-`main` branch, commits are made and pushed, and the PR targets `upstream/main` when an `upstream` remote exists, otherwise `origin/main`.
metadata:
  portability: semi-portable
---

# Create a pull request

Follow these steps in order. Stop and ask the user if any check is ambiguous;
do not force-push, rewrite history, or delete branches without explicit
confirmation.

**Approval scope.** A request to "open / make / create a PR" authorizes the
*work* of preparing one. It does **not** authorize the commit or the push.
The **Approval checkpoint** inside step 3 (Commit changes) is the gate:
staging happens before it, `git commit` and everything after happen only
once the user supplies an explicit publishing verb - `commit`, `push`,
`ship it`, or equivalent. See AGENTS.md § "Working with the user on
changes" for the canonical rule and the recurring not-approval phrasings.

Before running this workflow, walk through the
[`pre-pr-self-review`](../pre-pr-self-review/SKILL.md) checklist - it
catches the test, allocation, overflow-arithmetic, and TFM-phrasing mistakes
that have repeatedly cost a review round-trip on this repo. If the change
polyfills a .NET API for .NET Framework, the
[`polyfill-dotnet-api`](../polyfill-dotnet-api/SKILL.md) skill defines the
design rules the self-review then validates against.

## 1. Inspect repository state

Run these (read-only) checks first:

- `git remote -v` - determine whether an `upstream` remote exists.
- `git rev-parse --abbrev-ref HEAD` - current branch name.
- `git status --porcelain` - uncommitted changes.
- `git log --oneline @{u}.. 2>$null` (if upstream is set) - unpushed commits.

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
- Confirm the branch name with `vscode_askQuestions` before creating it.
  Pass two `options`: the suggested kebab-case name and `Use a different
  name`. Free-form text is allowed by default, so the user can either
  click the suggestion or type an override.
  (`vscode_askQuestions` requires either zero options or two-plus.)
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

### Approval checkpoint

**Stop here.** Show the user the staged diff (or summarize it) and the
proposed commit message. Wait for the user to explicitly say `commit`,
`push`, or `ship it` (or one of the other verbs listed in AGENTS.md
§ "Working with the user on changes") before running `git commit`. If
the user already used one of those verbs in the message that triggered
this skill, proceed without asking again. Do not infer approval from the
original "open a PR" request.

## 4. Push the branch

### Pre-push conflict check

Before pushing (or re-pushing), confirm the branch still merges cleanly into
its base (`<base>` = `origin/main`, or `upstream/main` when that remote exists)
so conflicts surface locally instead of on the PR:

- `git fetch <remote> main`, then `git rev-list --left-right --count <base>...HEAD`.
  Left count `0` means up to date - skip to push.
- If behind, dry-run the merge without touching the tree:

  ```pwsh
  git merge-tree $(git merge-base HEAD <base>) HEAD <base> | Select-String 'CONFLICT|<<<<<<<'
  ```

- **Clean:** prefer rebasing onto `<base>` so the PR diff is current.
- **Conflicts:** rebase (`git rebase <base>`, `$env:GIT_EDITOR='true'`), resolve,
  `git add` by path, `git rebase --continue`, then re-run `dotnet build` and
  `dotnet test -c Release` (base may have moved/renamed code you depend on).
- A rebase rewrites commits, so the next push needs `--force-with-lease` - a
  force-push that **requires explicit user approval** per the publish-boundary rule.

### Push

- Fresh branch (no rebase): `git push -u origin <branch>`.
- After rebasing an already-pushed branch (explicit approval only):
  `git push --force-with-lease origin <branch>`.
- Never `git push --force` or `--force-with-lease` without explicit user
  confirmation.

## 5. Open the PR

**Prefer the VS Code GitHub Pull Requests tool**
(`github-pull-request_create_pull_request`) when it is available in the
current tool set - it works without requiring `gh` to be installed and
returns the PR number/URL directly. Fall back to the GitHub CLI (`gh`) if
the VS Code tool is not available, and only fall back to a browser compare
URL if neither is available.

Determine the target with the rule from step 1.

### Option A - VS Code GitHub Pull Requests tool (preferred)

Call `github-pull-request_create_pull_request` with:

- `title`, `body` (see PR title/body guidance below),
- `head` = the feature branch name (no owner prefix),
- `base` = `main`,
- For an upstream-targeted PR, set `repo` to `{ owner: <upstreamOwner>, name: <repo> }` and `headOwner` to the fork owner.
- For an origin-only PR, omit `repo` and `headOwner` (they default correctly).

### Option B - GitHub CLI fallback

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

### Option C - Browser fallback

If neither the VS Code tool nor `gh` is available, print the compare URL:

- Upstream: `https://github.com/<upstreamOwner>/<repo>/compare/main...<forkOwner>:<branch>?expand=1`
- Origin only: `https://github.com/<originOwner>/<repo>/compare/main...<branch>?expand=1`

## 6. Report back

Tell the user:

- The branch name and where it was pushed.
- The base the PR targets (`upstream/main` or `origin/main`).
- The PR URL (from `gh pr create` output) or the compare URL fallback.

Once the PR exists, this skill is done. Subsequent rounds of edits in
response to review comments, requested changes, or CI failures go through
the [`address-pr-feedback`](../address-pr-feedback/SKILL.md) skill, which
has different commit/push approval semantics.

## Guardrails

- Never commit directly to `main`, even if the user is currently on it -
  always branch first.
- Never delete branches, force-push, or rewrite history as part of this
  workflow.
- If the working tree has unrelated changes, ask the user which files belong
  in the PR before staging.
