# Tag, publish, and aftercare

Detail for the [publish-release](SKILL.md) skill. These steps run **only after**
the approval checkpoint in the core has passed.

## 1. Create and push the tag

Use an annotated tag with a short message. Substitute the selected stream's
prefix (`v`, `analyzers-v`, or `ts-v`):

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
pushes with `--skip-duplicate`. `publish.yml` publishes only Touki for `v*`
and only the analyzer package for `analyzers-v*`. TestSupport's workflow fires
only on `ts-v*`.

If the workflow fails, treat it like any CI failure: do **not** delete and
re-push the tag without explicit user approval - that's destructive and a
nuget.org publish is irreversible. When every package already uploaded for the
tag is valid and the remaining failure is transient, rerun the same tag through
`workflow_dispatch` as described below. If NuGet rejected a package itself,
fix the package and publish the next version in the stream.

### Re-running the publish for an existing tag (`workflow_dispatch`)

If a transient failure (NuGet outage, OIDC blip) leaves a tag pushed but
not published, both workflows accept a `workflow_dispatch` with a
required `tag` input. Provide the **exact existing tag name** (e.g.
`v0.1.0-alpha.13`, `analyzers-v0.9.0`, or `ts-v0.1.0-alpha.9`); the workflow checks out that
ref, runs the same tag-format guard, and publishes. Do **not** dispatch
without a tag input - the workflow will fail validation rather than
publish a `0.0.0-alpha.0.<height>` MinVer fallback.

## 2. Create the GitHub release

Once the workflow has succeeded and the package is visible on
nuget.org, create the matching GitHub release. This is what users actually read.

Use `mcp_io_github_git_get_latest_release` first to find the prior release
on the same stream, so the new release notes can reference it. Then create
via the GitHub UI or `gh release create` (preferred when available):

```pwsh
# Drop the --prerelease flag for a stable release.
gh release create v0.1.0-alpha.13 `
  --title "v0.1.0-alpha.13" `
  --notes-file release-notes.md `
  --prerelease
```

If `gh` is not available, use the GitHub web UI (Releases -> Draft a new
release -> choose the existing tag).

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
    0.0.0.0 -> 1.0.0.0; consumers must rebuild. -->
- ...

## Compatibility

- Package: `<PackageId>`.
- Targets: `<target frameworks or analyzer host compatibility>`.
- AssemblyVersion: `<old>` -> `<new>` (note **changed** or **unchanged**).

## Install

```bash
dotnet add package <PackageId> --version <version>
```

**Full changelog:** <https://github.com/JeremyKuhne/touki/compare/v0.1.0-alpha.12...v0.1.0-alpha.13>
````

Notes on the template:

- Use the **same** `--prerelease` flag iff the SemVer has a prerelease label.
  GitHub displays prereleases differently (latest indicator stays on the
  most recent stable). Skipping `--prerelease` on an alpha is a real bug.
- Replace `<PackageId>` with the package selected for this stream; an analyzer
  release installs `KlutzyNinja.Touki.Analyzers`, not the runtime library.
- `compare/<prior>...<new>` works across all three streams (substitute
  the selected stream's prefix).
- For TestSupport releases, also call out the Touki version this build was
  produced against (look at the resolved `<dependency>` in the published
  `.nuspec`); that's what consumers will transitively pull.

## 3. Aftercare

- After publishing analyzers, update `ToukiAnalyzersPackageVersion` in
  [touki/touki.csproj](../../../touki/touki.csproj) only when the new version
  should become the default for future Touki releases. This is a separate source
  change and does not require an immediate Touki release.
- If you bumped `KlutzyNinja.Touki`, consider whether the sample's pinned
  version in [Directory.Packages.props](../../../Directory.Packages.props)
  should advance. The sample dog-foods the released package; leaving it
  stale defeats the purpose. (Open as a follow-up PR - not part of the
  release commit/tag.)
- If you bumped `Major` (binary break), TestSupport needs a release too -
  it cannot consume an incompatible Touki. Otherwise check whether
  `touki.testsupport/` code changed at all; if it did not, skip it. The
  criteria and the version-matching rule are in
  [versioning.md](versioning.md) "TestSupport releases: when and what
  version".
- Update [touki.testsupport/README.md](../../../touki.testsupport/README.md)
  if the supported targets or AOT story changed.
