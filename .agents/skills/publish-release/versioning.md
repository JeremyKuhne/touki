# Choosing the version and composing the tag

Detail for the [publish-release](SKILL.md) skill. Covers establishing the prior
version, the prerelease channel decision, the `Major.Minor.Patch` bump, the
`AssemblyVersion` gotcha, and the exact tag format.

## 1. Establish the prior version on this stream

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

## 2. Decide the prerelease channel (alpha / beta / rc / stable)

Before picking numbers, decide what *kind* of release this is. **Always
prompt** the user with the current state explicit:

> "The last release was an **alpha** (`v0.1.0-alpha.12`). Should this also
> be an alpha release, or are you promoting to beta / rc / stable?"

Use `vscode_askQuestions` with these options (mark the matching-prior option
as `recommended: true`):

- `Stay in alpha` - bug fixes / additive work during early development.
- `Promote to beta` - feature-complete for the upcoming Minor; only
  stabilization work expected.
- `Promote to rc` - release candidate; only blocker bug fixes.
- `Promote to stable` - drop the prerelease label entirely.
- `Use a different label` - free-form.

Channel rules of thumb:

- Don't *skip* channels casually. `alpha → beta → rc → stable` is the
  normal path. Going `alpha → stable` is allowed but should be deliberate.
- Once you ship a stable `Major.Minor.Patch`, the next prerelease must
  bump *something* (`0.1.0` → `0.1.1-alpha.1` or `0.2.0-alpha.1`); you
  cannot ship `0.1.0-alpha.2` after `0.1.0` stable.
- Do **not** mix channels backwards (no going from `beta` back to `alpha`
  for the same Major.Minor.Patch). If a beta turned out to need more
  churn, bump the underlying version: `0.2.0-beta.3` → `0.3.0-alpha.1`.

## 3. Decide `Major.Minor.Patch`

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

> "This change adds a public type but you're still in 0.1.x alpha - bump
> to 0.2.0-alpha.1 (treating it as Minor) or stay at 0.1.0-alpha.13?"

### When `AssemblyVersion` changes - and why it matters

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
  is binary-compatible - consumers can drop the new DLL into an existing
  bin folder and it just works.

**Therefore:** any binary breaking change *must* bump `Major`, even during
0.x. Refusing to bump `Major` on a binary break - to "stay at 0.1.x" -
silently keeps `AssemblyVersion = 0.0.0.0` across an incompatible
boundary, which is a real foot-gun for downstream binders.

When `Major` does bump, also note in the release that `AssemblyVersion`
moved (`0.0.0.0` → `1.0.0.0`).

## 4. Compose the tag

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

- `v.0.1.0-alpha.11` - stray `.` after `v`. The historical 9/10/11 tags
  hit this; do not recreate it.
- `0.1.0-alpha.13` - missing `v` prefix.
- `v0.1.0.alpha.13` - `.` instead of `-` before prerelease.
- `v0.1` - missing patch component.
- `v01.02.03` - leading zeros in numeric identifiers.

The regex used by the workflow guards (must match):

```text
^v(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(-[0-9A-Za-z.-]+)?$
^ts-v(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(-[0-9A-Za-z.-]+)?$
```
