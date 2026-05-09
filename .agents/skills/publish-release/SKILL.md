---
name: publish-release
description: Publish a new version of `KlutzyNinja.Touki` or `KlutzyNinja.Touki.TestSupport` to NuGet by cutting a release tag. Use when asked to "publish a new version", "release alpha.N", "ship a beta", "cut a release", "promote alpha to beta", or "tag and publish". Walks the user through choosing the right `Major.Minor.Patch` bump, deciding whether to stay in `alpha` / `beta` / `rc` / stable, picking the correct tag stream (`v*` vs `ts-v*`), pushing the tag, and creating the matching GitHub release. Vets when an `AssemblyVersion`-changing bump is required (binary breaking changes vs additive/bugfix work).
---

# Publish a release

This repo ships two NuGet packages from independent tag streams:

| Package | Tag prefix | Workflow |
| ------- | ---------- | -------- |
| `KlutzyNinja.Touki` | `v` (e.g. `v0.1.0-alpha.13`) | [.github/workflows/publish.yml](../../../.github/workflows/publish.yml) |
| `KlutzyNinja.Touki.TestSupport` | `ts-v` (e.g. `ts-v0.1.0-alpha.9`) | [.github/workflows/publishtestsupport.yml](../../../.github/workflows/publishtestsupport.yml) |

[MinVer](https://github.com/adamralph/minver) derives every version artifact
(`Version`, `PackageVersion`, `AssemblyVersion`, `FileVersion`,
`InformationalVersion`) from the tag at HEAD. **The tag *is* the version.**
`MinVer` is wired in [Directory.Build.targets](../../../Directory.Build.targets)
gated on `IsPackable != 'false'`; per-project `<MinVerTagPrefix>` overrides
live in [touki.testsupport/touki.testsupport.csproj](../../../touki.testsupport/touki.testsupport.csproj).

The publish workflow only fires when the tag is pushed. Both workflows have a
regex guard that rejects malformed tags (e.g. the historical `v.0.1.0-alpha.11`
typo) before any pack/push runs.

**Approval scope.** "Publish a release" authorizes preparing the tag and
release notes. It does **not** authorize the tag push. The
**Approval checkpoint** in step 6 is the gate. See AGENTS.md
§ "Working with the user on changes" for the canonical rule.

## 1. Inspect repo state and confirm the package

Read-only checks:

- `git remote -v` — confirm `origin` points at `JeremyKuhne/touki` (push
  target).
- `git rev-parse --abbrev-ref HEAD` — must be `main` or a release branch.
  Refuse to tag from an arbitrary feature branch unless the user explicitly
  says so.
- `git status --porcelain` — must be clean. If dirty, stop and ask.
- `git log origin/main..HEAD` and `git log HEAD..origin/main` — must both be
  empty. Tag must point at the published `origin/main` tip (or whatever the
  user confirmed in the previous bullet).

Ask the user **which package** if not already obvious from the request:

- `KlutzyNinja.Touki` — the main library.
- `KlutzyNinja.Touki.TestSupport` — test helpers (rarely shipped; see
  [docs/release-strategy](../../../touki.testsupport/README.md)).

Use `vscode_askQuestions` only if ambiguous; if the user said "publish
TestSupport" or similar, skip the prompt.

## 2. Establish the prior version on this stream

Different streams have different prior tags. List the most recent five tags
on the relevant prefix:

```pwsh
# Main package
git tag --list 'v*' --sort=-creatordate | Select-Object -First 5

# TestSupport
git tag --list 'ts-v*' --sort=-creatordate | Select-Object -First 5
```

Cross-check against nuget.org so you don't accidentally re-publish a version
that's already live (publishing is idempotent thanks to `--skip-duplicate`,
but choosing a duplicate is almost always a mistake):

- <https://www.nuget.org/packages/KlutzyNinja.Touki>
- <https://www.nuget.org/packages/KlutzyNinja.Touki.TestSupport>

Record the prior version (e.g. `0.1.0-alpha.12` for the main package).

If TestSupport has **no** `ts-v*` tags yet, the next published version becomes
the bootstrap of that stream. The previously-published version on nuget.org
under the old scheme was `0.1.0-alpha.8.11`; pick a `ts-v` tag that sorts
strictly higher (e.g. `ts-v0.1.0-alpha.9` or higher).

## 3. Decide the prerelease channel (alpha / beta / rc / stable)

Before picking numbers, decide what *kind* of release this is. **Always
prompt** the user with the current state explicit:

> "The last release was an **alpha** (`v0.1.0-alpha.12`). Should this also
> be an alpha release, or are you promoting to beta / rc / stable?"

Use `vscode_askQuestions` with these options (mark the matching-prior option
as `recommended: true`):

- `Stay in alpha` — bug fixes / additive work during early development.
- `Promote to beta` — feature-complete for the upcoming Minor; only
  stabilization work expected.
- `Promote to rc` — release candidate; only blocker bug fixes.
- `Promote to stable` — drop the prerelease label entirely.
- `Use a different label` — free-form.

Channel rules of thumb:

- Don't *skip* channels casually. `alpha → beta → rc → stable` is the
  normal path. Going `alpha → stable` is allowed but should be deliberate.
- Once you ship a stable `Major.Minor.Patch`, the next prerelease must
  bump *something* (`0.1.0` → `0.1.1-alpha.1` or `0.2.0-alpha.1`); you
  cannot ship `0.1.0-alpha.2` after `0.1.0` stable.
- Do **not** mix channels backwards (no going from `beta` back to `alpha`
  for the same Major.Minor.Patch). If a beta turned out to need more
  churn, bump the underlying version: `0.2.0-beta.3` → `0.3.0-alpha.1`.

## 4. Decide `Major.Minor.Patch`

Apply [SemVer 2.0.0](https://semver.org) rules. The package is currently
pre-1.0, so the rules below describe the **target stable** semantics; while
the prior tag is itself a prerelease, the same bump table applies to the
underlying `Major.Minor.Patch` portion.

| Change shipped since the last tag | Bump |
| --- | --- |
| Binary breaking change to public API of `touki.dll` (removed/renamed type or member, signature change, return-type change, base-type change, broken inheritance, removed `[Obsolete]`'d API) | **Major** |
| Behavioral break that compiles but changes observable runtime contract (different exception type, different default, different ordering, different threading guarantee) | **Major** unless the user explicitly accepts shipping it as Minor with a release-note callout |
| Net-new public API, new overload, new optional parameter, new public type, new TFM | **Minor** |
| Bug fix only, no new public surface, no observable contract change for non-buggy callers | **Patch** |
| Internal-only refactor, perf, doc, comment, build, CI | **Patch** (or no release at all) |

For pre-1.0, the user may treat **Major** and **Minor** as both "Minor"
during alpha/beta. That is fine; just confirm the call out loud:

> "This change adds a public type but you're still in 0.1.x alpha — bump
> to 0.2.0-alpha.1 (treating it as Minor) or stay at 0.1.0-alpha.13?"

### When `AssemblyVersion` changes — and why it matters

MinVer's defaults (used as-is in this repo, no overrides) produce:

- `Version` / `PackageVersion` / `InformationalVersion` = full SemVer
  (e.g. `0.1.0-alpha.13`).
- `FileVersion` = `Major.Minor.Patch.0` (e.g. `0.1.0.0`).
- **`AssemblyVersion` = `Major.0.0.0`** (e.g. `0.0.0.0` for any `0.x.y`).

Only a **Major** bump changes `AssemblyVersion`. That has real consequences:

- A change in `AssemblyVersion` forces every consumer that has the assembly
  in their build graph to either rebuild against the new identity or use
  binding redirects (on .NET Framework). Strong-named assemblies make this
  stricter, and `touki.dll` is strong-named.
- A change in `Version`/`FileVersion` *without* `AssemblyVersion` changing
  is binary-compatible — consumers can drop the new DLL into an existing
  bin folder and it just works.

**Therefore:** any binary breaking change *must* bump `Major`, even during
0.x. Refusing to bump `Major` on a binary break — to "stay at 0.1.x" —
silently keeps `AssemblyVersion = 0.0.0.0` across an incompatible
boundary, which is a real foot-gun for downstream binders.

When `Major` does bump, also note in the release that `AssemblyVersion`
moved (`0.0.0.0` → `1.0.0.0`).

## 5. Compose the tag

Format (enforced by the regex guard in each workflow):

```text
v<Major>.<Minor>.<Patch>[-<prerelease>]          # main package
ts-v<Major>.<Minor>.<Patch>[-<prerelease>]       # TestSupport
```

Prerelease segment uses dot-separated identifiers, e.g. `alpha.13`,
`beta.2`, `rc.1`. Numeric identifiers are SemVer-sorted as numbers, so
`alpha.10` correctly sorts above `alpha.9` (no leading zeros).

Examples (good):

- `v0.1.0-alpha.13`
- `v0.2.0-beta.1`
- `v0.2.0-rc.1`
- `v0.2.0`
- `v1.0.0-rc.1`
- `ts-v0.1.0-alpha.9`

Examples (rejected by guard):

- `v.0.1.0-alpha.11` — stray `.` after `v`. The historical 9/10/11 tags
  hit this; do not recreate it.
- `0.1.0-alpha.13` — missing `v` prefix.
- `v0.1.0.alpha.13` — `.` instead of `-` before prerelease.
- `v0.1` — missing patch component.
- `v01.02.03` — leading zeros in numeric identifiers.

The regex used by the workflow guards (must match):

```text
^v\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$
^ts-v\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$
```

## 6. Approval checkpoint

**Stop here.** Show the user:

- The chosen tag (e.g. `v0.1.0-alpha.13`).
- The prior tag and what bumped (e.g. "alpha.12 → alpha.13, no Major/Minor
  change").
- The commit the tag will point at (`git rev-parse HEAD`, short SHA + subject).
- A short summary of the changes since the prior tag (`git log --oneline
  <prior-tag>..HEAD`).
- The expected published `AssemblyVersion` before/after.

Wait for an explicit publishing verb (`tag`, `push the tag`, `ship it`,
`publish`). Do **not** infer approval from the original "publish a release"
request — that authorized the *preparation*, not the push. See AGENTS.md.

## 7. Create and push the tag

Use an annotated tag with a short message:

```pwsh
git tag -a v0.1.0-alpha.13 -m "v0.1.0-alpha.13"
git push origin v0.1.0-alpha.13
```

Do **not** use lightweight tags — annotated tags carry the tagger and date
that show up in GitHub releases.

Pushing the tag triggers the publish workflow. Watch the run:

- <https://github.com/JeremyKuhne/touki/actions/workflows/publish.yml>
- <https://github.com/JeremyKuhne/touki/actions/workflows/publishtestsupport.yml>

The workflow validates the tag format, packs, OIDC-logs into NuGet, and
pushes with `--skip-duplicate`. The main publish workflow filters out
`KlutzyNinja.Touki.TestSupport.*` from its glob, and TestSupport's workflow
fires only on `ts-v*` — they will not stomp each other.

If the workflow fails, treat it like any CI failure: do **not** delete and
re-push the tag without explicit user approval — that's destructive and the
nuget.org publish is irreversible. Fix forward with the next tag in the
stream.

## 8. Create the GitHub release

Once the workflow has succeeded and the package is visible on nuget.org,
create the matching GitHub release. This is what users actually read.

Use `mcp_io_github_git_get_latest_release` first to find the prior release
on the same stream, so the new release notes can reference it. Then create
via the GitHub UI or `gh release create` (preferred when available):

```pwsh
gh release create v0.1.0-alpha.13 `
  --title "v0.1.0-alpha.13" `
  --notes-file release-notes.md `
  --prerelease   # omit for a stable release
```

If `gh` is not available, use the GitHub web UI (Releases → Draft a new
release → choose the existing tag).

### Release notes template

```markdown
## Changes

<!-- One-sentence headline of the most important change. -->

### Added
- ...

### Changed
- ...

### Fixed
- ...

### Breaking changes
<!-- Only present on Major bumps. AssemblyVersion changed from
     0.0.0.0 → 1.0.0.0; consumers must rebuild. -->
- ...

## Compatibility

- Targets: `net10.0`, `net472`.
- AssemblyVersion: `<old>` → `<new>` (note **changed** or **unchanged**).

## Install

```bash
dotnet add package KlutzyNinja.Touki --version 0.1.0-alpha.13
```

**Full changelog:** https://github.com/JeremyKuhne/touki/compare/v0.1.0-alpha.12...v0.1.0-alpha.13
```

Notes on the template:

- Use the **same** `--prerelease` flag iff the SemVer has a prerelease label.
  GitHub displays prereleases differently (latest indicator stays on the
  most recent stable). Skipping `--prerelease` on an alpha is a real bug.
- `compare/<prior>...<new>` works across both streams (just substitute
  `ts-v...` for TestSupport).
- For TestSupport releases, also call out the Touki version this build was
  produced against (look at the resolved `<dependency>` in the published
  `.nuspec`); that's what consumers will transitively pull.

## 9. Aftercare

- If you bumped `KlutzyNinja.Touki`, consider whether the sample's pinned
  version in [Directory.Packages.props](../../../Directory.Packages.props)
  should advance. The sample dog-foods the released package; leaving it
  stale defeats the purpose. (Open as a follow-up PR — not part of the
  release commit/tag.)
- If you bumped `Major` (binary break), also bump `Major` of TestSupport
  on its next release. They don't have to march in lockstep but TestSupport
  cannot consume an incompatible Touki.
- Update [touki.testsupport/README.md](../../../touki.testsupport/README.md)
  if the supported targets or AOT story changed.

## Cross-references

- [`pre-pr-self-review`](../pre-pr-self-review/SKILL.md) — run before
  shipping any change that lands in a release.
- [`address-pr-feedback`](../address-pr-feedback/SKILL.md) — used to land
  the changes that this skill then ships.
- AGENTS.md § "Working with the user on changes" — publish-boundary rule
  governing the approval checkpoint in step 6.
- [Directory.Build.targets](../../../Directory.Build.targets) — central
  MinVer wiring.
- [.github/workflows/publish.yml](../../../.github/workflows/publish.yml),
  [.github/workflows/publishtestsupport.yml](../../../.github/workflows/publishtestsupport.yml)
  — the publish pipelines themselves.
