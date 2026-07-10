---
name: run-tests-on-wsl
description: Run touki tests - especially the Unix-only oracle suites under `touki.tests/Touki/Io/Globbing/` - inside WSL Ubuntu on Windows. Use when the user asks to "run tests on Linux", "run the Posix/PosixPath/Bash oracles", or "iterate Unix tests locally", and for any fix that needs Linux verification before pushing.
metadata:
  applicability: repo-local
  binding: none
  maturity: stable
  portability: repo-specific
  related: performance-testing, pre-pr-self-review
  requires: none
  risk: local-write
---

# Running tests inside WSL Ubuntu

**Headline rule:** keep a Linux-native checkout (e.g. `~/repos/touki`) and
sync from the Windows-mounted checkout before each run. Building directly
from the `/mnt/<drive>/...` mount **does not work** - see the trap
below.

The agent drives WSL from the existing PowerShell terminal via `wsl --`;
no need to open a Linux shell. Per-run cost after the one-time bootstrap is
a fast `rsync` + the normal `dotnet test`. Substitute the user's actual
paths for `<WIN_CHECKOUT>` (e.g. `/mnt/d/src/touki`) and `<LINUX_CHECKOUT>`
(defaults to `~/repos/touki`).

## The `/mnt/` DrvFs NuGet trap

`dotnet restore` run from a Windows DrvFs mount discovers
`/mnt/c/Users/<user>/AppData/Roaming/NuGet/NuGet.Config` and
`/mnt/c/Program Files (x86)/NuGet/Config/Microsoft.VisualStudio.*.config`,
which reference
`C:\Program Files (x86)\Microsoft Visual Studio\Shared\NuGetPackages` -
unreachable as a Linux path. NuGet writes the Windows fallback verbatim into
`project.assets.json` / `*.nuget.dgspec.json`, and the next build fails with:

```text
error MSB4018: NuGet.Packaging.Core.PackagingException: Unable to find
fallback package folder
'C:\Program Files (x86)\Microsoft Visual Studio\Shared\NuGetPackages'.
```

Clearing the NuGet cache, deleting `artifacts/obj`, setting
`NUGET_PACKAGES`, or passing absolute Linux paths **does not fix it**. Only
moving the checkout off the DrvFs mount does.

## Prerequisites

- WSL2 with a default Ubuntu distro. If `wsl -l -v` reports "Windows
  Subsystem for Linux is not installed", the user must run `wsl --install`
  from an elevated PowerShell and reboot - the agent cannot drive the
  elevation prompt.
- `libicu-dev` installed (`sudo apt-get install -y libicu-dev`). Without
  it, `dotnet --version` throws on `Console.OutputEncoding`. Tell the user
  to run `sudo` - do not collect the password via chat.
- .NET SDK 10 under `$HOME/.dotnet` (the repo's
  [setup.sh](../../../setup.sh) installs latest 10.x). If
  [global.json](../../../global.json) pins a specific feature-band patch
  with `rollForward: latestFeature`, **also** install that exact patch
  inside WSL:

  ```bash
  <LINUX_CHECKOUT>/dotnet-install.sh --version <pinned> --install-dir $HOME/.dotnet
  ```

  Without it, MSBuild's separate SDK lookup picks up the Windows-side SDK
  via the inherited Windows PATH (`/mnt/c/Program Files/dotnet/sdk/...`)
  and stamps Windows paths into `runtimeIdentifierGraphPath`.
- `DOTNET_ROOT` exported. The test apphost requires it; `dotnet` on `PATH`
  alone is not enough. Persist it once:

  ```bash
  echo 'export DOTNET_ROOT=$HOME/.dotnet' >> ~/.bashrc
  ```

## Per-run recipe

```pwsh
# 1. Sync the Windows checkout into the Linux-native mirror. Exclude
#    artifacts/ so Windows-side restore output never poisons the Linux cache.
wsl -- bash -ic 'rsync -a --delete `
    --exclude artifacts/ --exclude BenchmarkDotNet.Artifacts/ `
    --exclude TestResults/ --exclude .vs/ --exclude "*.lscache" `
    <WIN_CHECKOUT>/ <LINUX_CHECKOUT>/'

# 2. Build inside Linux (first time per sync).
wsl --cd <LINUX_CHECKOUT> -- bash -ic `
    'dotnet build touki.tests -c Release -f net10.0 --nologo'

# 3. Run a filtered subset. `--filter-method` only allows wildcards at the
#    start and/or end of the pattern and does not support `|` OR -
#    run each suite separately.
wsl --cd <LINUX_CHECKOUT> -- bash -ic `
    'dotnet test touki.tests -c Release -f net10.0 --no-build --nologo -- --filter-method "*Posix*"'

# 4. Read the test log (little-endian UTF-16; plain grep returns nothing).
wsl --cd <LINUX_CHECKOUT> -- bash -ic `
    'iconv -f UTF-16LE -t UTF-8 artifacts/x64/Release/touki.tests/net10.0/TestResults/touki.tests_net10.0_x64.log | grep -E "^failed|but found"'
```

`bash -ic` is **interactive + login** so `~/.bashrc` is sourced and
`DOTNET_ROOT` is in scope; `bash -lc` alone is not enough.

## Coverage delta vs Windows

Windows runs skip `Posix`, `PosixPath`, and (without Git for Windows
installed) `Bash` oracle suites. The Linux run adds them and validates
`GlobMatcher`'s multiple-asterisk and sequential-separator normalization
against `fnmatch(3)` and native bash 5 with `extglob`/`globstar`. Pending
engine-level gaps are tracked under "Multiple-asterisk-run behavior"
findings in [docs/globbing-feature-plan.md](../../../docs/globbing-feature-plan.md).

The one OS-conditional row to expect is
`SequentialSeparatorMSBuildOracleTests` `//a` vs `/a`: `MSBuildGlob` on
Linux treats `//a` as equivalent to `/a`; on Windows it treats `//a` as a UNC root.
Touki is OS-stable, matching the Windows verdict - this is a
documented platform divergence in the oracle, not in touki.

## Related skills

- [`performance-testing`](../performance-testing/SKILL.md) - same
  recipe runs `touki.perf` benchmarks on Linux; swap `touki.tests` for
  `touki.perf`.
- [`pre-pr-self-review`](../pre-pr-self-review/SKILL.md) - for
  changes touching `touki/Touki/Io/Globbing/`, run the Linux oracle suites
  before pushing so Unix-only regressions are caught locally instead of in CI.
