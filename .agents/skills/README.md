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
| [polyfill-dotnet-api](./polyfill-dotnet-api/SKILL.md) | "polyfill", "backport", "add a span overload for net472/net481", "make API X available downlevel", missing-on-net472-present-on-net10 | repo-specific | `dotnet-polyfills`, `pre-pr-self-review`, `performance-testing`, `framework-jit-optimization`, `create-pr` |
| [dotnet-polyfills](./dotnet-polyfills/SKILL.md) | "use a modern .NET API on net472/net481", which official downlevel package (`System.Memory`, `Microsoft.Bcl.*`) supplies a type, setting up PolySharp, "is this already polyfilled" - the package-choosing/consume side (vs authoring) | vendored (portable core) + overlay | `pre-pr-self-review`, `framework-jit-optimization`, `polyfill-dotnet-api` (via [overlay](./dotnet-polyfills/overlay.md)) |
| [framework-jit-optimization](./framework-jit-optimization/SKILL.md) | hot-path tuning for `net481` in `touki/Framework/`, generic specialization, scalar/unrolled vs BCL delegation, net481 RyuJIT regressions; the `net10` counterpart (vectorization, intrinsics, struct-generic, devirtualization); cross-TFM codegen (arithmetic/branchless lowering, struct layout, allocation anti-patterns) | vendored (portable core) + overlay | `performance-testing`, `scratch-buffer-strategy`, `pre-pr-self-review`, `il-copy-inspection` (via [overlay](./framework-jit-optimization/overlay.md)) |
| [performance-testing](./performance-testing/SKILL.md) | authoring/running BenchmarkDotNet benchmarks in `touki.perf`, comparing implementations, evaluating allocations, reading the generated code (sharplab/`[DisassemblyDiagnoser]`/`[HardwareCounters]`/`DOTNET_JitDisasm*`) | vendored (portable core) + overlay | `framework-jit-optimization`, `scratch-buffer-strategy`, `pre-pr-self-review`, `filtrace`, `il-copy-inspection` (via [overlay](./performance-testing/overlay.md)) |
| [filtrace](./filtrace/SKILL.md) | "where's the time/memory in this trace or benchmark", which method or source line is hot, why a run regressed vs a baseline, what a captured `.nettrace`/`.etl` holds, rank / drill / diff / export a CPU / alloc / exception / GC / JIT / thread-time profile, profiling net481 via ETW | vendored (tool repo) + overlay | `performance-testing` (via [overlay](./filtrace/overlay.md)) |
| [scratch-buffer-strategy](./scratch-buffer-strategy/SKILL.md) | choosing a scratch-buffer strategy (zeroed `stackalloc` vs `[SkipLocalsInit]` vs `BufferScope<T>` vs `ArrayPool` rental), "should I rent or stackalloc?", net481/net10 size crossovers, weighing `[SkipLocalsInit]` | vendored (portable core) + overlay | `performance-testing`, `framework-jit-optimization` (via [overlay](./scratch-buffer-strategy/overlay.md)) |
| [pre-pr-self-review](./pre-pr-self-review/SKILL.md) | self-review checklist plus a read-only agentic review pass before opening or pushing a PR; missing tests, unchecked length sums, empty-span foot-guns, TFM phrasing | vendored (portable core) + overlay | `create-pr`, `address-pr-feedback`, `security-review`, `performance-testing`, `polyfill-dotnet-api` (via [overlay](./pre-pr-self-review/overlay.md)) |
| [security-review](./security-review/SKILL.md) | "assess for security vulnerabilities", "do a security review", "check for ReDoS / DoS", "audit untrusted input handling"; any member accepting caller-supplied data, any use of `unsafe` / `Unsafe.*` / `MemoryMarshal.*` / `Marshal.*`, or any BCL API whose name or doc says "unsafe" / "caller must" - abusive-input handling, length / integer overflow, allocation and algorithmic DoS, caller-validated preconditions, argument validation | vendored (portable core) + overlay | `pre-pr-self-review`, `performance-testing`, `fuzz-testing` (via [overlay](./security-review/overlay.md)) |
| [create-pr](./create-pr/SKILL.md) | "make a PR", "open a pull request", "push and PR", "publish for review" - **initial** publish | vendored (portable core) + overlay | `pre-pr-self-review`, `address-pr-feedback` |
| [address-pr-feedback](./address-pr-feedback/SKILL.md) | "address the review", "fix the comments", "address Copilot's feedback", "fix the CI failure" - **post-PR** review-cycle work | vendored (portable core) + overlay | `create-pr`, `pre-pr-self-review`, `agent-files-review` |
| [agent-files-review](./agent-files-review/SKILL.md) | reviewing/validating agent-customization files (`AGENTS.md`, `*.instructions.md`, `*.prompt.md`, `*.agent.md`, `SKILL.md`, validator, CI workflow); fixing `agent-files.yml` failures | vendored (portable core) + overlay | `manage-skills` |
| [run-tests-on-wsl](./run-tests-on-wsl/SKILL.md) | "run tests on Linux", "run the Posix/PosixPath/Bash oracles", "iterate Unix tests locally"; WSL Ubuntu bootstrap, the `~/repos/touki` Linux-native mirror that sidesteps the `/mnt/` DrvFs NuGet trap, the `DOTNET_ROOT` apphost requirement, and the `iconv` UTF-16 log trick | repo-specific | `performance-testing`, `pre-pr-self-review` |
| [fuzz-testing](./fuzz-testing/SKILL.md) | "add a fuzz target", "run the fuzzer", "install fuzzing prereqs", "fuzz SpanReader/SpanWriter", coverage-guided SharpFuzz runs on net10/net481, promoting a crash into a regression | vendored (portable core) + overlay | `security-review`, `pre-pr-self-review`, `run-tests-on-wsl` (via [overlay](./fuzz-testing/overlay.md)) |
| [publish-release](./publish-release/SKILL.md) | "publish a new version", "release alpha.N", "ship a beta", "cut a release", "promote alpha to beta", "tag and publish" - choosing the right `Major.Minor.Patch`, alpha/beta/rc/stable channel, tag stream (`v*` vs `ts-v*`), and GitHub release notes | repo-specific | `pre-pr-self-review`, `create-pr` |
| [manage-skills](./manage-skills/SKILL.md) | "find a skill", "build a skill" / "create a skill" (checks for an existing one first), "update the skill", sync a local change upstream vs into an overlay; the find-first build path, tiered search, pull/push update flow | vendored (portable core) | `agent-files-review` |
| [roslyn-analyzers](./roslyn-analyzers/SKILL.md) | "write an analyzer", "create a Roslyn/diagnostic analyzer", "add an analyzer rule", "add a code fix", "enforce a convention at build time", "flag a pattern in code"; find-first check of existing `CA`/`IDE` rules, `BannedApiAnalyzers`, EditorConfig, Roslynator/StyleCop/Meziantou before authoring; `touki.analyzers` layout, packing into `KlutzyNinja.Touki`, statelessness/`IOperation` design, the `Microsoft.CodeAnalysis.Testing` harness, in-IDE perf budget | vendored (portable core) + overlay | `performance-testing`, `security-review`, `pre-pr-self-review`, `il-copy-inspection`, `create-pr` (via [overlay](./roslyn-analyzers/overlay.md)) |
| [il-copy-inspection](./il-copy-inspection/SKILL.md) | "find struct copies", "where does the compiler copy this struct", "is this a defensive copy", "check for boxing in IL", "did the compiler emit a copy", "confirm the analyzer's defensive-copy warning", "audit a `[NonCopyable]` type's copies after build"; reading emitted IL (`ildasm`/`ilspycmd`/Cecil/`MetadataReader`) for the `ldobj`/`stloc`/`ldloca` defensive-copy signature, `box`, by-value field/arg/return copies, and PDB offset-to-source mapping | vendored (portable core) + overlay | `roslyn-analyzers`, `framework-jit-optimization`, `performance-testing`, `scratch-buffer-strategy` (via [overlay](./il-copy-inspection/overlay.md)) |
| [code-comprehension](./code-comprehension/SKILL.md) | "review this for readability", "is this too complex", "reduce nesting / cognitive load", "reasonable method length / parameter count / nesting depth", judging whether code will be hard to understand | vendored (portable core) + overlay | `pre-pr-self-review` (via [overlay](./code-comprehension/overlay.md)) |

**Portability** (mirrored from each skill's `metadata.portability`) marks how much
a skill would need to change to be reused in another repo: `portable` (generic),
`semi-portable` (general pattern with touki paths / conventions to edit out), or
`repo-specific` (tied to touki's structure). A skill shown as
`vendored (portable core) + overlay` is a pinned copy of a portable core from the
[agent-skills commons](https://github.com/JeremyKuhne/agent-skills) (see the
`metadata.github-*` provenance in its `SKILL.md`) paired with a local `overlay.md`
that carries the touki-specific cross-references and example links.
`vendored (tool repo) + overlay` is an exact skill payload from the tool's own
repository plus a consumer-owned `overlay.md`. Filtrace is pinned to the exact
`v0.6.3` tool release together with its executable packages. Do not
hand-edit a vendored core; update it from an exact tool release or source
revision.

## Disambiguation

These pairs are known to compete for auto-invocation. When both descriptions
match the user's request, follow the rule below.

### `manage-skills` vs `agent-files-review`

Both touch skill files. They are mutually exclusive by **scope**:

- **The catalog lifecycle** - discovering a skill, adding one, vendoring from the
  commons, syncing a local change up or down -> `manage-skills`.
- **Validating one agent file** - frontmatter, mirror sync, whitespace, the
  `agent-files.yml` gate -> `agent-files-review`.

The normal order is to run `manage-skills` to bring a skill in or push one out,
then `agent-files-review` to validate the file you ended up with.

### `create-pr` vs `address-pr-feedback`

Both touch PR mechanics. They are mutually exclusive by **lifecycle stage**:

- **No PR exists yet** -> `create-pr`. The skill ensures changes are on a
  non-`main` branch, commits and pushes, and targets `upstream/main` when
  available.
- **PR exists, reviewer left comments / CI failed** -> `address-pr-feedback`.
  Edit-only by default. Does **not** push without explicit approval.

If the user says "open a PR" while a PR already exists for this branch, ask
which they mean before invoking either.

### `dotnet-polyfills` vs `polyfill-dotnet-api`

Both fire on "polyfill". They split by **which side of the decision** you are on:

- **"Which package or generator already supplies this downlevel?"** ->
  `dotnet-polyfills`. The vendored survey of the official Microsoft backport
  packages, PolySharp, and the `KlutzyNinja.Touki` runtime layer - the
  consume/choose side. Start here.
- **"None of them have it - write it by hand."** -> `polyfill-dotnet-api`.
  Touki's authoring rules for a hand-rolled polyfill under `touki/Framework/`
  (layout, behavior-parity, the net481 codegen gotchas).

Come to `dotnet-polyfills` first; hand off to `polyfill-dotnet-api` only once the
answer is genuinely "no package covers this."

### `polyfill-dotnet-api` vs `framework-jit-optimization`

Both can land code under `touki/Framework/`. They are mutually exclusive by
**intent**:

- **Adding/back-porting an API surface** missing on net472 ->
  `polyfill-dotnet-api`. Picks the right source (Microsoft package vs PolySharp
  vs hand-rolled) and enforces namespace/`#if` rules.
- **Tuning an existing hot path** for net481 RyuJIT specifically ->
  `framework-jit-optimization`. No new public surface; the file already exists.

If a polyfill needs hot-path tuning after the fact, run `polyfill-dotnet-api`
first to land the surface, then `framework-jit-optimization` for the tuning
pass.

### `performance-testing` vs `framework-jit-optimization`

Adjacent, not overlapping, but easy to confuse:

- **Harness mechanics** (writing a benchmark class, parameters, diagnosers,
  reading the output) -> `performance-testing`.
- **What the JIT will do with the code under benchmark** (inlining, intrinsics,
  devirtualization, the `Unsafe.As` foot-gun) ->
  `framework-jit-optimization`.

You will frequently use both in sequence.

### `scratch-buffer-strategy` vs `performance-testing`

Both mention "evaluating allocations", but they answer different questions:

- **Which buffer strategy should this hot path use** (zeroed `stackalloc` vs
  `[SkipLocalsInit]` vs `BufferScope<T>` vs an `ArrayPool` rental), and at what
  size does renting beat zeroing -> `scratch-buffer-strategy`. It hands back
  a decision, not a measurement.
- **How do I measure a buffer/allocation cost** (author a benchmark, add
  `[MemoryDiagnoser]`, read `Allocated`) -> `performance-testing`.

### `performance-testing` vs `filtrace`

Both describe "where's the time / which method is hot", but they sit on opposite
sides of a hand-off:

- **Author or run a benchmark, capture a touki trace, and interpret the result**
  (isolated all-case manifest capture, the EventPipe-vs-ETW divergence, reading
  the method/line evidence) -> `performance-testing`.
- **Drive the filtrace analyzer over an existing trace** - the `cpu` / `rank` /
  `callers` / `lines` / `diff` / `batch` / `export` verbs and `trace_*` tools,
  the provider/source quality gates, the trap catalog -> `filtrace`.

The usual flow is `performance-testing` to produce the trace, then `filtrace` to
rank and drill it.

Use `scratch-buffer-strategy` to pick the design; use `performance-testing` to
verify it on both TFMs.

### `roslyn-analyzers` vs `performance-testing`

Both talk about "performance", but about different things:

- **How fast the analyzer runs in the IDE** (per-keystroke budget, cheap-filter
  ordering, `ReportAnalyzer`) -> `roslyn-analyzers`. The subject is a
  `DiagnosticAnalyzer`'s own execution time.
- **How fast the library code runs at execution time** (BenchmarkDotNet `Mean`,
  `Allocated`, both TFMs) -> `performance-testing`. The subject is the shipped
  product code.

They share no harness and no budget. If the request is "make this analyzer faster
to type against", it is `roslyn-analyzers`; if it is "make this method faster at
run time", it is `performance-testing`.

### `il-copy-inspection` vs `roslyn-analyzers` vs the perf skills

"Find the struct copies" is ambiguous across four artifact layers. Route by what is
being read:

- **Predict copies from source, live in the IDE** -> `roslyn-analyzers`
  (the TOUKI0002-0004 defensive-copy / `[NonCopyable]` rules over `IOperation`).
- **Read the compiler's emitted IL** for the actual `ldobj`/`box`/by-value copies
  -> `il-copy-inspection`. It is post-build and sees synthesized copies the
  analyzer cannot.
- **Read the JIT's machine code** (was the copy elided?) ->
  `framework-jit-optimization` + `[DisassemblyDiagnoser]`.
- **Measure the runtime cost** (allocation / time) -> `performance-testing`.

`il-copy-inspection` never runs the code and never measures time; if a request needs
a number, it belongs to `performance-testing`. The natural chain is analyzer
prediction -> IL confirmation -> asm/runtime cost.

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
