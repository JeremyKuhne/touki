# Tag, publish, and aftercare

Detail for the [publish-release](SKILL.md) skill. These steps run **only after**
the approval checkpoint in the core has passed.

## 1. Create and push the tag

Use an annotated tag with a short message:

```pwsh
git tag -a v0.1.0-alpha.13 -m "v0.1.0-alpha.13"
git push origin v0.1.0-alpha.13
```

Do **not** use lightweight tags - annotated tags carry the tagger and date
that show up in GitHub releases.

Pushing the tag triggers the publish workflow. Watch the run:

- <https://github.com/JeremyKuhne/touki/actions/workflows/publish.yml>
- <https://github.com/JeremyKuhne/touki/actions/workflows/publishtestsupport.yml>

The workflow validates the tag format, packs, OIDC-logs into NuGet, and
pushes with `--skip-duplicate`. The main publish workflow filters out
`KlutzyNinja.Touki.TestSupport.*` from its glob, and TestSupport's workflow
fires only on `ts-v*` - they will not stomp each other.

If the workflow fails, treat it like any CI failure: do **not** delete and
re-push the tag without explicit user approval - that's destructive and the
nuget.org publish is irreversible. Fix forward with the next tag in the
stream.

### Re-running the publish for an existing tag (`workflow_dispatch`)

If a transient failure (NuGet outage, OIDC blip) leaves a tag pushed but
not published, both workflows accept a `workflow_dispatch` with a
required `tag` input. Provide the **exact existing tag name** (e.g.
`v0.1.0-alpha.13` or `ts-v0.1.0-alpha.9`); the workflow checks out that
ref, runs the same tag-format guard, and publishes. Do **not** dispatch
without a tag input - the workflow will fail validation rather than
publish a `0.0.0-alpha.0.<height>` MinVer fallback.

## 2. Create the GitHub release

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

````markdown
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

**Full changelog:** <https://github.com/JeremyKuhne/touki/compare/v0.1.0-alpha.12...v0.1.0-alpha.13>
````

Notes on the template:

- Use the **same** `--prerelease` flag iff the SemVer has a prerelease label.
  GitHub displays prereleases differently (latest indicator stays on the
  most recent stable). Skipping `--prerelease` on an alpha is a real bug.
- `compare/<prior>...<new>` works across both streams (just substitute
  `ts-v...` for TestSupport).
- For TestSupport releases, also call out the Touki version this build was
  produced against (look at the resolved `<dependency>` in the published
  `.nuspec`); that's what consumers will transitively pull.

## 3. Aftercare

- If you bumped `KlutzyNinja.Touki`, consider whether the sample's pinned
  version in [Directory.Packages.props](../../../Directory.Packages.props)
  should advance. The sample dog-foods the released package; leaving it
  stale defeats the purpose. (Open as a follow-up PR - not part of the
  release commit/tag.)
- If you bumped `Major` (binary break), also bump `Major` of TestSupport
  on its next release. They don't have to march in lockstep but TestSupport
  cannot consume an incompatible Touki.
- Update [touki.testsupport/README.md](../../../touki.testsupport/README.md)
  if the supported targets or AOT story changed.
