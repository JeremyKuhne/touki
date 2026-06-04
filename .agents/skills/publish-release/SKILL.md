---
name: publish-release
description: Publish a new version of `KlutzyNinja.Touki` or `KlutzyNinja.Touki.TestSupport` to NuGet by cutting a release tag. Use when asked to "publish a new version", "release alpha.N", "ship a beta", "cut a release", "promote alpha to beta", or "tag and publish". Walks the user through choosing the right `Major.Minor.Patch` bump, deciding whether to stay in `alpha` / `beta` / `rc` / stable, picking the correct tag stream (`v*` vs `ts-v*`), pushing the tag, and creating the matching GitHub release. Vets when an `AssemblyVersion`-changing bump is required (binary breaking changes vs additive/bugfix work).
metadata:
  portability: repo-specific
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
**Approval checkpoint** below is the gate. See AGENTS.md
§ "Working with the user on changes" for the canonical rule.

## Steps overview

1. **Inspect repo state and confirm the package** (below).
2. Establish the prior version on the stream - see [versioning.md](versioning.md).
3. Decide the prerelease channel (alpha / beta / rc / stable) - [versioning.md](versioning.md).
4. Decide `Major.Minor.Patch` and check the `AssemblyVersion` gotcha - [versioning.md](versioning.md).
5. Compose and validate the tag against the workflow regex - [versioning.md](versioning.md).
6. **Approval checkpoint** (below) - stop and wait for an explicit publish verb.
7. Create and push the tag, then watch the workflow - see [release-steps.md](release-steps.md).
8. Create the GitHub release from the notes template - [release-steps.md](release-steps.md).
9. Aftercare (sample version bump, TestSupport lockstep) - [release-steps.md](release-steps.md).

## 1. Inspect repo state and confirm the package

Read-only checks:

- `git remote -v` - confirm `origin` points at `JeremyKuhne/touki` (push
  target).
- `git rev-parse --abbrev-ref HEAD` - must be `main` or a release branch.
  Refuse to tag from an arbitrary feature branch unless the user explicitly
  says so.
- `git status --porcelain` - must be clean. If dirty, stop and ask.
- `git log origin/main..HEAD` and `git log HEAD..origin/main` - must both be
  empty. Tag must point at the published `origin/main` tip (or whatever the
  user confirmed in the previous bullet).

Ask the user **which package** if not already obvious from the request:

- `KlutzyNinja.Touki` - the main library.
- `KlutzyNinja.Touki.TestSupport` - test helpers (rarely shipped; see
  [docs/release-strategy](../../../touki.testsupport/README.md)).

Use `vscode_askQuestions` only if ambiguous; if the user said "publish
TestSupport" or similar, skip the prompt.

Then work through steps 2-5 in [versioning.md](versioning.md) to land on a
validated tag.

## Approval checkpoint

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
request - that authorized the *preparation*, not the push. See AGENTS.md.

After approval, follow [release-steps.md](release-steps.md) to push the tag,
watch the workflow, and create the GitHub release.

## Sub-pages

- [versioning.md](versioning.md) - establishing the prior version, the
  prerelease channel decision, the `Major.Minor.Patch` bump table, the
  `AssemblyVersion` gotcha, and the exact tag format with its regex guard.
- [release-steps.md](release-steps.md) - creating and pushing the annotated
  tag, `workflow_dispatch` recovery, the GitHub release notes template, and
  aftercare.

## Cross-references

- [`pre-pr-self-review`](../pre-pr-self-review/SKILL.md) - run before
  shipping any change that lands in a release.
- [`address-pr-feedback`](../address-pr-feedback/SKILL.md) - used to land
  the changes that this skill then ships.
- AGENTS.md § "Working with the user on changes" - publish-boundary rule
  governing the approval checkpoint.
- [Directory.Build.targets](../../../Directory.Build.targets) - central
  MinVer wiring.
- [.github/workflows/publish.yml](../../../.github/workflows/publish.yml),
  [.github/workflows/publishtestsupport.yml](../../../.github/workflows/publishtestsupport.yml)
  - the publish pipelines themselves.
