---
core: github-actions-cost-optimization
core-pin: v0.15.0
---

# Touki overlay - github-actions-cost-optimization

Repo-specific companion to the vendored
[github-actions-cost-optimization](SKILL.md) skill. The `SKILL.md` and its three
sibling pages (`audit.md`, `cost-model.md`, `optimizations.md`) are a **pinned
copy of the portable core** from
[JeremyKuhne/agent-skills](https://github.com/JeremyKuhne/agent-skills) (see the
`metadata.github-*` provenance in `SKILL.md`). Do not hand-edit the core -
`gh skill update` would flag the drift. Everything touki-specific lives here.

> **Pinned to a release.** The core is pinned to the commons **v0.15.0** tag. Pull
> later upstream changes with `gh skill update github-actions-cost-optimization`
> (review the diff, re-pin to the new tag).

## Workflow inventory (the core's step 1)

Everything lives under [.github/workflows/](../../../.github/workflows/):

| Workflow | Trigger | Runners |
| --- | --- | --- |
| [dotnet.yml](../../../.github/workflows/dotnet.yml) | PR and push to `main` | `ubuntu-24.04-arm` matrix, one `windows-latest` and one `windows-11-arm` smoke leg, `ubuntu-slim` aggregator |
| [agent-files.yml](../../../.github/workflows/agent-files.yml) | PR and push to `main` | `ubuntu-latest`, gated in-job by `dorny/paths-filter` |
| [platform-tests.yml](../../../.github/workflows/platform-tests.yml) | `workflow_call` only | caller-supplied `inputs.runner` |
| [manual-linux.yml](../../../.github/workflows/manual-linux.yml), [manual-macos.yml](../../../.github/workflows/manual-macos.yml), [manual-windows.yml](../../../.github/workflows/manual-windows.yml) | `workflow_dispatch` | the full per-platform matrices moved off the automatic path |
| [publish.yml](../../../.github/workflows/publish.yml) (`v*.*.*`), [publishtestsupport.yml](../../../.github/workflows/publishtestsupport.yml) (`ts-v*.*.*`) | tag push, `workflow_dispatch` | `windows-latest` |

`platform-tests.yml` is the single reusable body; measure a change there, not in
each caller. Its `full-validation`, `collect-coverage`, `pack`, `anycpu`,
`linux-desktop`, and `native-aot-rid` inputs are the existing cost levers.

## Validation invariants (the core's step 2)

State these before proposing any saving:

- **`.NET / build` is the required status check name.** It is a deliberately
  cheap `ubuntu-slim` aggregation job that fails when any upstream leg fails.
  Renaming it or changing its `needs` breaks branch protection on `main` -
  a governance change, not an accounting one.
- **Both target frameworks must stay covered on the automatic path.** `net481`
  is validated by the `windows-latest` leg; the Linux ARM64 matrix covers
  `net10.0`/`net11.0` in Debug and Release. A cross-TFM library cannot drop
  either leg.
- **Windows-only behavior needs a real Windows runner** (clipboard providers,
  the CsWin32 projection - see
  [`cswin32-interop`](../cswin32-interop/SKILL.md)), and the Unix path oracles
  need a real Linux runner (see
  [`run-tests-on-wsl`](../run-tests-on-wsl/SKILL.md)).
- Coverage upload, packaging checks, and Native AOT publish already run on
  exactly one leg each. Do not fan them out to save wall-clock.

The existing topology is already the result of one cost pass: Linux ARM64 owns
the full matrix because it is the cheapest runner, Windows is reduced to smoke
legs, the full per-platform matrices are `workflow_dispatch`-only, and
`concurrency` cancels superseded PR runs. Re-measure before assuming further
savings remain.

## Remote boundary

The core's remote boundary binds to touki's stricter rule: branch protection,
required checks, and Actions settings are remote changes that need explicit
approval, and committing, pushing, and opening a PR are three separate approval
boundaries - see
[AGENTS.md](../../../AGENTS.md#working-with-the-user-on-changes) and
[`create-pr`](../create-pr/SKILL.md).

## Cross-references (the core's "Related skills")

- [`security-review`](../security-review/SKILL.md) - before changing CodeQL
  cadence, workflow permissions, or anything that runs untrusted PR code.
- [`publish-release`](../publish-release/SKILL.md) - owns the two tag-triggered
  publish workflows; do not re-time them for cost.
- [`agent-files-review`](../agent-files-review/SKILL.md) - owns `agent-files.yml`
  and its path filters.

The core's related `engineering-baseline` skill is **not vendored here**: touki
is an established repository already wired for build, test, publish, and
governance, so the scaffold/audit workflow has no binding to make.

## Updating

Pull upstream changes to the core with
`gh skill update github-actions-cost-optimization` (review the diff, re-pin).
Keep touki-specific additions in this file, not in the core.
