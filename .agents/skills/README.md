# Skills catalog (`.agents/skills/`)

Inventory of skills available in this repo, what triggers each, and how to
disambiguate overlapping ones. For the file format and discovery rules, see
[FORMAT.md](./FORMAT.md).

Skill auto-invocation matches against the `description` field. When two skills
have overlapping verbs in their descriptions, the wrong one can fire silently.
The "Disambiguation" section below records every known overlap.

## Inventory

| Skill | Trigger phrasing | Portability | Cross-references |
| ----- | ---------------- | ----------- | ---------------- |
| [polyfill-dotnet-api](./polyfill-dotnet-api/SKILL.md) | "polyfill", "backport", "add a span overload for net472/net481", "make API X available downlevel", missing-on-net472-present-on-net10 | repo-specific | `pre-pr-self-review`, `performance-testing`, `framework-jit-optimization`, `create-pr` |
| [framework-jit-optimization](./framework-jit-optimization/SKILL.md) | hot-path tuning for `net481` in `touki/Framework/`, generic specialization, scalar/unrolled vs BCL delegation, net481 RyuJIT regressions | semi-portable | `performance-testing` |
| [performance-testing](./performance-testing/SKILL.md) | authoring/running BenchmarkDotNet benchmarks in `touki.perf`, comparing implementations, evaluating allocations | semi-portable | `framework-jit-optimization`, `scratch-buffer-strategy` |
| [scratch-buffer-strategy](./scratch-buffer-strategy/SKILL.md) | choosing a scratch-buffer strategy (zeroed `stackalloc` vs `[SkipLocalsInit]` vs `BufferScope<T>` vs `ArrayPool` rental), "should I rent or stackalloc?", net481/net10 size crossovers, weighing `[SkipLocalsInit]` | semi-portable | `performance-testing`, `framework-jit-optimization` |
| [pre-pr-self-review](./pre-pr-self-review/SKILL.md) | self-review checklist before opening or pushing a PR; missing tests, unchecked length sums, empty-span foot-guns, TFM phrasing | semi-portable | `create-pr`, `polyfill-dotnet-api` |
| [security-review](./security-review/SKILL.md) | "assess for security vulnerabilities", "do a security review", "check for ReDoS / DoS", "audit untrusted input handling"; any member accepting caller-supplied data, any use of `unsafe` / `Unsafe.*` / `MemoryMarshal.*` / `Marshal.*`, or any BCL API whose name or doc says "unsafe" / "caller must" - abusive-input handling, length / integer overflow, allocation and algorithmic DoS, caller-validated preconditions, argument validation | semi-portable | `pre-pr-self-review`, `performance-testing` |
| [create-pr](./create-pr/SKILL.md) | "make a PR", "open a pull request", "push and PR", "publish for review" - **initial** publish | semi-portable | `pre-pr-self-review` |
| [address-pr-feedback](./address-pr-feedback/SKILL.md) | "address the review", "fix the comments", "address Copilot's feedback", "fix the CI failure" - **post-PR** review-cycle work | repo-specific | `pre-pr-self-review` |
| [agent-files-review](./agent-files-review/SKILL.md) | reviewing/validating agent-customization files (`AGENTS.md`, `*.instructions.md`, `*.prompt.md`, `*.agent.md`, `SKILL.md`, validator, CI workflow); fixing `agent-files.yml` failures | semi-portable | - |
| [run-tests-on-wsl](./run-tests-on-wsl/SKILL.md) | "run tests on Linux", "run the Posix/PosixPath/Bash oracles", "iterate Unix tests locally"; WSL Ubuntu bootstrap, the `~/repos/touki` Linux-native mirror that sidesteps the `/mnt/` DrvFs NuGet trap, the `DOTNET_ROOT` apphost requirement, and the `iconv` UTF-16 log trick | repo-specific | `performance-testing`, `pre-pr-self-review` |
| [fuzz-testing](./fuzz-testing/SKILL.md) | "add a fuzz target", "run the fuzzer", "install fuzzing prereqs", "fuzz SpanReader/SpanWriter", coverage-guided SharpFuzz runs on net10/net481, promoting a crash into a regression | semi-portable | `security-review`, `pre-pr-self-review`, `run-tests-on-wsl` |
| [publish-release](./publish-release/SKILL.md) | "publish a new version", "release alpha.N", "ship a beta", "cut a release", "promote alpha to beta", "tag and publish" - choosing the right `Major.Minor.Patch`, alpha/beta/rc/stable channel, tag stream (`v*` vs `ts-v*`), and GitHub release notes | repo-specific | `pre-pr-self-review` |

**Portability** (mirrored from each skill's `metadata.portability`) marks how much
a skill would need to change to be reused in another repo: `portable` (generic),
`semi-portable` (general pattern with touki paths / conventions to edit out), or
`repo-specific` (tied to touki's structure). See the sharing roadmap in
[docs/skills-improvement-plan.md](../../docs/skills-improvement-plan.md).

## Disambiguation

These pairs are known to compete for auto-invocation. When both descriptions
match the user's request, follow the rule below.

### `create-pr` vs `address-pr-feedback`

Both touch PR mechanics. They are mutually exclusive by **lifecycle stage**:

- **No PR exists yet** &rarr; `create-pr`. The skill ensures changes are on a
  non-`main` branch, commits and pushes, and targets `upstream/main` when
  available.
- **PR exists, reviewer left comments / CI failed** &rarr; `address-pr-feedback`.
  Edit-only by default. Does **not** push without explicit approval.

If the user says "open a PR" while a PR already exists for this branch, ask
which they mean before invoking either.

### `polyfill-dotnet-api` vs `framework-jit-optimization`

Both can land code under `touki/Framework/`. They are mutually exclusive by
**intent**:

- **Adding/back-porting an API surface** missing on net472 &rarr;
  `polyfill-dotnet-api`. Picks the right source (Microsoft package vs PolySharp
  vs hand-rolled) and enforces namespace/`#if` rules.
- **Tuning an existing hot path** for net481 RyuJIT specifically &rarr;
  `framework-jit-optimization`. No new public surface; the file already exists.

If a polyfill needs hot-path tuning after the fact, run `polyfill-dotnet-api`
first to land the surface, then `framework-jit-optimization` for the tuning
pass.

### `performance-testing` vs `framework-jit-optimization`

Adjacent, not overlapping, but easy to confuse:

- **Harness mechanics** (writing a benchmark class, parameters, diagnosers,
  reading the output) &rarr; `performance-testing`.
- **What the JIT will do with the code under benchmark** (inlining, intrinsics,
  devirtualization, the `Unsafe.As` foot-gun) &rarr;
  `framework-jit-optimization`.

You will frequently use both in sequence.

### `scratch-buffer-strategy` vs `performance-testing`

Both mention "evaluating allocations", but they answer different questions:

- **Which buffer strategy should this hot path use** (zeroed `stackalloc` vs
  `[SkipLocalsInit]` vs `BufferScope<T>` vs an `ArrayPool` rental), and at what
  size does renting beat zeroing &rarr; `scratch-buffer-strategy`. It hands back
  a decision, not a measurement.
- **How do I measure a buffer/allocation cost** (author a benchmark, add
  `[MemoryDiagnoser]`, read `Allocated`) &rarr; `performance-testing`.

Use `scratch-buffer-strategy` to pick the design; use `performance-testing` to
verify it on both TFMs.

## Maintenance

Freshness is tracked from git history, not from a manual column. CI warns
(does not fail) when a skill directory has no commits in the last 90 days
- see [.github/workflows/agent-files.yml](../../.github/workflows/agent-files.yml).

A stale warning means "re-read this skill end-to-end against the current
codebase." Confirm:

1. Every cross-reference resolves.
2. Every file path / type / API mentioned still exists.
3. Every claim about the codebase is still true.

The only way to clear the warning is to commit a change to the skill
directory - ideally the result of a real review pass, but at minimum a
whitespace touch with a commit message stating "verified still current."

When adding a new skill, append a row to the inventory above in the same
change set; the skill is not "shipped" until the catalog reflects it.
