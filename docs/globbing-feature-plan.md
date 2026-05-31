# Glob matcher - status and next steps

The glob feature surface is **complete**. Every dialect, option, and pattern
syntax originally planned for the matcher has shipped, with one deliberate
exception: the Windows `Win32` / `WinNT` dialects are **indefinitely
postponed** and have been removed from `GlobDialect` (see
[Win32 / WinNT - indefinitely postponed](#win32--winnt---indefinitely-postponed)).

This document now serves two purposes:

1. A **record of the completed feature work** (the [Shipped features](#shipped-features)
   summary and the per-phase notes that follow it).
2. A **forward-looking plan for continual performance improvement and
   simplification** - the active section is
   [Next steps - performance and simplification](#next-steps---performance-and-simplification),
   which captures the current flame-graph investigation and the directory-pruning
   work it motivates.

Companion docs: [globbing-optimization-plan.md](globbing-optimization-plan.md)
(match-time micro-optimization slices) and
[framework-span-performance.md](framework-span-performance.md) (the net472/net481
slow-span playbook). For the per-dialect ignore-case mapping see
[touki/Touki/Io/Globbing/GlobOptions.cs](../touki/Touki/Io/Globbing/GlobOptions.cs)
`IgnoreCase` and the internal `IgnoreCaseKind` enum.

## Status snapshot

**Dialects shipped (8):** `Posix`, `Simple`, `PowerShell`, `PosixPath`
(path-aware + globstar), `MSBuild` (path-aware + globstar, case-insensitive
by default), `Bash` (path-aware, globstar opt-in, extglob opt-in via
`AllowExtGlob`), `FileSystemGlobbing` (path-aware + implicit globstar, no
classes, no escape), `Git` (path-aware + implicit globstar, with
gitignore-style `!` / leading `/` / trailing `/` markers).

**Postponed indefinitely:** `Win32` and `WinNT`. Removed from `GlobDialect`;
the defensive `FeatureNotEnabled` branch in `GlobSpecification.TryCompile`
covers any out-of-range dialect value.

**`GlobOptions` flags consumed (5 of 5):** `IgnoreCase`, `MatchLeadingDot`,
`NoEscape`, `AllowGlobStar`, `AllowExtGlob`.

**Phases complete:** Phase 1 (path-unaware dialects + extglob), Phase 2
(path-aware matching + globstar), Phase 3 (`.gitignore` negation / anchors /
directory-only / `OrderedMatchSet` / `GitIgnore` loader), Phase 5 (POSIX
bracket-expression extras). Phase 4 (brace expansion) remains an optional
`Bash`-only convenience - see [Phase 4](#phase-4---brace-expansion-bash-only).

**Tests:** the full suite is green on both TFMs (net10.0 and net481),
including the per-dialect oracle suites. New tests must be added alongside
each future change - see `touki.tests/Touki/Io/Globbing/`.

---

## Next steps - performance and simplification

This is the active work. The feature surface is frozen; the remaining effort
is making the enumeration paths faster (especially on the net472/net481
RyuJIT) and removing avoidable complexity.

### N1. Flame-graph investigation - extglob enumeration (current)

CPU profiling of [`touki.perf/MsBuildEnumeratePerf2.cs`](../touki.perf/MsBuildEnumeratePerf2.cs)
on the touki repo (`**/*.cs` minus `bin`/`obj`, 4850 `.cs` files) captured
two extglob-collapse scenarios:

- `GlobEnumeratorExtGlobSingle` - `!(bin|obj)/**/*.cs`
- `GlobEnumeratorExtGlobSingleWithRoot` - `@(!(bin|obj)/**/*.cs|*.cs)`

Measured means (BenchmarkDotNet, ratio vs the `Microsoft.Build` `FileMatcher`
baseline):

| Scenario | net10.0 (modern RyuJIT) | net481 (.NET Framework 4.8.1 RyuJIT) |
|---|---|---|
| `GlobEnumeratorReduced` (`**/*.cs` + `bin/**`,`obj/**` excludes) | 44.7 ms (0.39) | 49.0 ms (0.56) |
| `GlobEnumeratorExtGlobSingle` | 52.0 ms (0.46) | 76.3 ms (0.87) |
| `GlobEnumeratorExtGlobSingleWithRoot` | 51.9 ms (0.46) | 79.1 ms (0.90) |

The extglob collapse is only +15% over the reduced excludes on net10 but
**+56-65% on net481** - the standout regression and the reason this
investigation exists.

**Flame-graph reading.** EventPipe `CpuSampling` (net10 only; net481 has no
in-proc profiler) fully inlines the matcher predicate into
`FileSystemEnumerator<T>.MoveNext`, so the predicate shows up as `MoveNext`
self-time. Per workload iteration of the complex extglob, `MoveNext`
(~8.1 s inclusive in the trace) splits roughly:

- ~38% inlined compiled-extglob predicate (the `ExtGlobEngine` walk run per entry).
- ~44% OS directory traversal: `FindNextEntry` + per-directory `Kernel32.CloseHandle`.
- ~13% `Monitor.Enter_Slowpath` inside the BCL enumerator's own buffer/handle
  machinery (verified: no locks exist in the touki glob stack).

**Root cause (verified in source).** `!(bin|obj)/**/*.cs` compiles to a
program whose first opcode is `AltStart`, so
`CompiledGlobStrategy.ComputeLiteralPathPrefix` returns an empty literal
prefix. With no literal prefix, `GlobMatch.MatchesDirectory` returns `true`
for *every* directory - including `bin` and `obj`. The enumerator therefore
**descends into `bin`/`obj` and rejects each `.cs` file** via the full
`ExtGlobEngine` backtracking walk, instead of pruning those subtrees at the
directory boundary the way the `bin/**`,`obj/**` excludes do. On a .NET repo
the `bin`/`obj` trees dwarf the source tree, so the extglob variant runs the
most expensive matcher over the most files - exactly backwards.

### N2. Directory pruning for first-segment negations (primary proposal)

Teach the directory-recursion decision to evaluate a leading negation
(`!(a|b)/...`) against the *candidate directory name* so matching subtrees are
never descended.

- **Where:** `GlobMatch.MatchesDirectory` (called from
  `MatchEnumerator.ShouldRecurseIntoEntry`). Today it short-circuits only on a
  non-empty `LiteralPathPrefix`; extend it to recognize a leading
  `AltStart`-with-negation whose alternatives are plain literals (the
  `!(bin|obj)` shape) and return `false` when the candidate's first segment
  matches one of the negated literals.
- **Payoff:** removes the dominant avoidable cost on *both* TFMs (no per-file
  `ExtGlobEngine` walk over `bin`/`obj`) and should close most of the gap to
  `GlobEnumeratorReduced`. Largest single win, and it helps net481 most
  because that path pays the slow-span indexer per engine step.
- **Risk:** the pruning must be conservative - only prune when the leading
  construct is provably a first-segment negation of literal alternatives
  anchored at the path root. Any globstar or nested wildcard before the
  negation, or alternatives that aren't plain literals, must fall back to
  descend-and-filter. Add regression tests that pin a non-prunable shape
  (e.g. `**/!(bin)/*.cs`) still enumerates correctly.

### N3. Specialize the `!(set)/**/suffix` shape (follow-on)

If N2's directory pruning is not enough, add a dedicated `GlobStrategy` for the
`!(literal-set)/**/suffix` shape - the same approach that produced
`MultiSuffixGlobStrategy` for `**/@(*.litN|...)`. A first-segment set-exclusion
check plus a suffix check replaces the general backtracking engine entirely for
this common MSBuild collapse. Gated behind a benchmark showing N2 alone leaves
measurable headroom.

### N4. net481 span tuning of the `ExtGlobEngine` inner loop

The +56-65% net481 regression lives in the per-file engine walk. Apply the
`MemoryMarshal.GetReference` + `Unsafe.Add` hoist from
[framework-span-performance.md](framework-span-performance.md) to the
`ExtGlobEngine` opcode and state-serialization loops (the slow-span indexer
costs ~8 µops/element on net481 vs ~1 on net10). Per that doc, expect a 19-44%
Framework win at no `unsafe`-keyword cost. **Measure net10 does not regress
before `#if`-splitting**; prefer a single simple implementation when it stays
within ~5% on net10.

### N5. Simplification opportunities

- **Specialized-matcher path-aware fast paths (deferred since F2.1).**
  `Prefix`/`Suffix`/`Contains`/`PrefixSuffix` route to `CompiledGlobMatcher`
  for path-aware dialects. Either grow separator-aware fast paths *or* delete
  the dead intent and document that `CompiledGlobMatcher` owns these shapes.
  Decide based on a path-aware benchmark; do not carry the ambiguity.
- **Empty-literal-prefix `MatchesDirectory` pruning (deferred since F3.3).**
  Patterns whose literal prefix is empty (e.g. `**/*.cs`) recurse
  unconditionally. N2 handles the negation case; a separator-checkpointed NFA
  savepoint API would generalize pruning to other empty-prefix shapes - only
  if a benchmark justifies the added matcher surface.
- **Prefer one implementation over `#if`-split variants** unless net10
  measurably regresses, so the simple source keeps accruing future RyuJIT
  improvements (per the repo's span-performance guidance).

---

## Shipped features

The remainder of this document records the completed feature work and the
design decisions behind it. It is kept for reference; the active plan is
[Next steps](#next-steps---performance-and-simplification) above.

---

## Phase 1 finish - path-unaware dialects and extglob

Goal: bring every dialect that does *not* need path-mode semantics to feature
parity, plus the `AllowExtGlob` option. After this slice the matcher is a
complete drop-in for non-path use cases.

### F1.1 `PowerShell` dialect - **done**

PowerShell `-like` / `WildcardPattern` semantics. Smallest delta from `Posix`:
no `FNM_PERIOD` (a leading `.` in input is matched by `*` or `?` like any other
character). Bracket classes work the same. Escape character is `` ` `` (backtick)
rather than `\`.

- **Status:** Shipped on this branch. `PowerShell` is allowed in
  `GlobMatcher.TryCompile`. The escape character is now threaded through
  `GlobMatcherFactory.Scan` / `EncodeProgram` / `UnescapeToString` as a
  `char` parameter (with `'\0'` meaning "no escape") rather than the old
  `bool noEscape`; the dialect supplies it via
  `GlobDialectExtensions.GetEscapeChar`. The matchers also moved their
  `_matchLeadingDot` state onto a base-class `MatchLeadingDot` property
  computed from `GlobDialectExtensions.MatchesLeadingDotByDefault` (same
  pattern as `IgnoreCaseKind`).
- **Side benefit:** `Simple` now also defaults to
  `MatchLeadingDot=true`, matching the documented behavior of
  `FileSystemName.MatchesSimpleExpression`. No tests previously covered
  this for `Simple`, so the change is silent at the test boundary but
  fixes a latent spec deviation.
- **Tests added:** 17 new `GlobMatcherTests` rows
  (`IsMatch_PowerShell_BasicCases`, `IsMatch_PowerShell_BacktickEscape`,
  `Compile_PowerShell_DanglingBacktick_Throws`,
  `IsMatch_IgnoreCase_PowerShell_Unicode`) + 23 new `IgnoreCaseKindTests`
  rows covering the new `GetEscapeChar` and `MatchesLeadingDotByDefault`
  per-dialect mappings.
- **Ref:** [about_Wildcards](https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_wildcards).

### F1.2 / F1.2b `Win32` and `WinNT` dialects - **indefinitely postponed**

See [Win32 / WinNT - indefinitely postponed](#win32--winnt---indefinitely-postponed)
below. Both dialects were removed from `GlobDialect`; callers needing those
semantics call
[`System.IO.Enumeration.FileSystemName.MatchesWin32Expression`](https://learn.microsoft.com/dotnet/api/system.io.enumeration.filesystemname.matcheswin32expression)
directly (reachable on net10 via the BCL; see `Touki.Io.Paths` for the
`MatchType.Win32` enumeration shim).

### F1.3 `AllowExtGlob` option - **done**

Bash/POSIX extglob alternation constructs (also reachable via POSIX
`FNM_EXTMATCH`):

- `?(pat1|pat2|...)` - zero or one occurrence of any alternative
- `*(pat1|pat2|...)` - zero or more occurrences of any alternative
- `+(pat1|pat2|...)` - one or more occurrences of any alternative
- `@(pat1|pat2|...)` - exactly one occurrence
- `!(pat1|pat2|...)` - anything that doesn't match any alternative

Shipped surface:

- Three opcodes (`AltStart` `U+FDD6`, `AltSep` `U+FDD7`, `AltEnd` `U+FDD8`)
  with `AltStart` carrying a `(kind, blockLen)` payload so the interpreter
  can skip whole blocks in O(1) and the negation handler can clip the
  block range.
- New `Scan` / `TryEncodeProgram` recognition gated on
  `GlobOptions.AllowExtGlob`; non-extglob patterns pay one extra branch
  in the scanner and emit byte-identical bytecode.
- New `CompiledGlobStrategy._hasExtGlob` flag routes extglob programs
  through a recursive matcher
  (`CompiledGlobStrategy.ExtGlob.cs`); non-extglob programs keep the
  existing two-slot iterative path. Specialized strategies (`Literal`,
  `Prefix`, `Suffix`, `Contains`, `PrefixSuffix`, `Any`,
  `GlobStarFileName`) explicitly disqualify themselves when
  `shape.HasExtGlob`, so extglob always reaches the bytecode interpreter.
- Compile-time caps: nesting depth &le; 8, alternatives &le; 32,
  alternation-block bytecode length &le; `MaxOpcodeBodyLength`
  (`U+FFFF`). Exceeding any returns
  `GlobCompileErrorCode.FeatureLimitExceeded` (or `PatternTooLarge`).
- Compile-time error codes: `UnterminatedExtGlob`, `InvalidExtGlobBody`,
  `FeatureLimitExceeded`.

Tests: `ExtGlobScannerTests` (compile-shape + error codes - 47 rows),
`ExtGlobPositiveMatchTests` (~110 rows across `?` / `@` / `+` / `*`),
`ExtGlobNegationMatchTests` (33 rows for `!`).

Documented divergence: bash accepts `!(*)` against the empty string;
touki rejects (the matcher reads "no alternative matches the slice
exactly" as failing because `*` matches the empty slice).

- **Ref:** [fnmatch(3) FNM_EXTMATCH](https://man7.org/linux/man-pages/man3/fnmatch.3.html),
  [bash Pattern Matching](https://www.gnu.org/software/bash/manual/html_node/Pattern-Matching.html).

### F1.4 Bring `AllowExtGlob` online for Bash - **done**

`Bash` dialect compiles patterns with extglob enabled when `shopt -s extglob`
is set, off otherwise. Touki's user opts in via `GlobOptions.AllowExtGlob`
explicitly - matches what the consumer has to do in bash. The
oracle parity check lives in `ExtGlobOracleTests.Bash` (~552 rows across
24 patterns &times; 23 inputs) and runs against `bash -O extglob -O globstar`
via `BashInterop`. Skipped automatically on macOS (bash 3.2 too divergent).

---

## Phase 2 - path-aware matching

Goal: support five dialects whose distinguishing feature is path-mode
matching. The bytecode interpreter is path-unaware today; this phase adds
the separator-aware machinery.

### F2.1 Separator-aware `Any` and `AnyRun` - **done**

Add a `pathMode` flag and a `Separator` char to `CompiledGlobMatcher`
(passed in by the factory; the char comes from
[D2](#d2-matcher-requires-normalized-input-paths-separator-is-chosen-at-compile-time)).
When path mode is set:

- `Any` (one-char wildcard) does **not** match the separator.
- `AnyRun` (`*`) does not cross the separator either.
- A separate `GlobStar` op ([F2.2](#f22--globstar)) is the only thing that
  crosses separators.

Only one separator char is considered - the matcher assumes inputs are
normalized to that char (D2). No per-character dual-separator branching.

- **Status:** Shipped on this branch. `Separator` is now a `char` property
  on the `GlobMatcher` base, computed from
  `GlobDialectExtensions.IsPathAware` /
  `GlobDialectExtensions.DefaultSeparator`. `CompiledGlobMatcher` threads
  it into both `MatchOrdinal` and `MatchIgnoreCase` and short-circuits
  `Any`, `Class`/`NegClass`, and `AnyRun` expansion when the next input
  character is the separator. `GlobMatcher.TryCompile` accepted only
  `PosixPath` at the time this slice landed; the remaining path-aware
  dialects (`Bash`, `Git`, `MSBuild`, `FileSystemGlobbing`) and the
  `AllowGlobStar` option were gated to `FeatureNotEnabled` until F2.2
  shipped globstar - all are now accepted (see F2.2 / F2.3).
- **Factory routing:** Path-aware patterns skip the specialized
  `Prefix`/`Suffix`/`Contains`/`PrefixSuffix` matchers (none of those
  track the separator yet) and route to `CompiledGlobMatcher`. Pure
  literal patterns continue to hit `LiteralGlobMatcher`.
- **Tests added:** 26 new `GlobMatcherTests` rows
  (`IsMatch_PosixPath_BasicCases`, `Compile_PosixPath_ChoosesMatcher`,
  `Compile_PosixPath_SeparatorIsForwardSlash`,
  `Compile_Posix_SeparatorIsZero`,
  `Compile_AllowGlobStar_NotImplemented_Throws`) plus 18 new
  `IgnoreCaseKindTests` rows for `IsPathAware` and `DefaultSeparator`
  across all dialects. Full glob suite: 199 tests, green on both
  net481 and net10.0.
- **Known follow-up - per-segment leading-dot:** The current
  `MatchLeadingDot` check only consults `input[0]`. POSIX path-mode
  `FNM_PERIOD` actually applies at the start of *each* path segment, so
  `*/x` against `.foo/x` should fail when `MatchLeadingDot` is off.
  Implementing this requires the NFA to track segment boundaries
  during `AnyRun` expansion. Deferred - track alongside F2.2 work
  since globstar's segment-walking machinery will produce the same
  boundary tracking for free.
- **Specialized-matcher path-aware support - deferred:** The
  plan called for `Prefix/Suffix/Contains/PrefixSuffix` to grow
  separator-aware fast paths. Treating those as a perf follow-up keeps
  this slice focused; `CompiledGlobMatcher` handles them correctly
  today and any perf delta only matters once a path-aware benchmark
  exists.

### F2.2 `**` (globstar) - **done**

A new opcode `GlobStar` distinct from `AnyRun`. Matches zero or more path
segments including the separators between them. Honors `GlobOptions.AllowGlobStar`
(some dialects, e.g. POSIX without `globstar`, treat `**` as `*`).

- **Status:** Shipped on this branch. `GlobOpCodes.GlobStar = '\uFDD5'` plus
  flag bits `GlobStarFlagLead = 1` and `GlobStarFlagTrail = 2` encode the
  four shapes `**` (`GS_None`), `**/` (`GS_R`), `/**` (`GS_L`),
  `/**/` (`GS_LT`). `GlobMatcherFactory.EncodeProgram` recognizes
  segment-bounded `**` and retroactively strips trailing `/` from the
  prior `Literal` op when the GlobStar absorbs the separator.
  `CompiledGlobMatcher` runs a two-savepoint NFA (one `AnyRun` slot plus
  one `GlobStar` slot) with backtracking via the `Backtrack` helper and
  the `FirstValidGlobStarLength` / `NextValidGlobStarLength` advance
  helpers. The compile-time `_hasGlobStar` flag routes globstar-free
  programs to `MatchOrdinalSimple` / `MatchIgnoreCaseSimple` so the
  common-case single-savepoint path is unchanged.
- **Tail-anchor interaction:** `FindTrailingLiteral` was updated to skip
  over `GlobStar` opcodes (`i += 2`) while still extracting a trailing
  `Literal` for `EndsWith` fast-fail. Patterns ending in `**` carry no
  tail anchor (the trailing literal is empty).
- **Tests added:** 37 new `GlobMatcherTests` theory rows covering
  `GS_None` / `GS_R` / `GS_L` / `GS_LT` shapes and mixed patterns; full
  glob suite: 239 tests, green on net10.0 and net481.
- **DoS evaluation:** the two-savepoint design has been audited for
  catastrophic backtracking. Both savepoint slots advance monotonically;
  `Backtrack`'s outer loop terminates in at most two iterations; worst
  case is `O(input_length × pattern_complexity)`, polynomial. See
  `touki.perf/GlobMatcherBacktrackPerf.cs` for the worst-case
  microbenchmarks (~286 ns net10 / ~707 ns net481 for an 80-char
  heavily-backtracking input).

Subtleties (all handled by the shipped implementation):

- Leading `**/` matches zero or more directories at the root.
- Trailing `/**` matches everything below the parent (zero or more chars
  including separators).
- Middle `/**/` collapses to "zero or more directories"; the surrounding `/`
  characters collapse correctly so `a/**/b` matches `a/b` (zero
  directories).

### F2.3 Path-aware dialects on top of F2.1 + F2.2 - **done for `MSBuild` / `Bash` / `FileSystemGlobbing` / `Git`**

Once F2.1 and F2.2 land, five dialects become a thin wrapping job. Globstar
defaults are per [D1](#d1-allowglobstar-is-dialect-defaulted); separator
defaults are per
[D2](#d2-matcher-requires-normalized-input-paths-separator-is-chosen-at-compile-time):

| Dialect | `pathMode` | Globstar default | Separator default | Status | Notes |
|---|---|---|---|---|---|
| `PosixPath` | yes | off (opt in via `AllowGlobStar`) | `ForwardSlash` | **done** | `FNM_PATHNAME` |
| `Bash` | yes | off (opt in - matches `shopt -s globstar`) | `ForwardSlash` | **done** (extglob via `AllowExtGlob` still pending in F1.3) | classes + `\`-escape supported |
| `Git` | yes | on | `ForwardSlash` | **done** (F3.1 + F3.2 markers included) | implicit globstar; strips `!`, leading `/`, trailing `/` |
| `MSBuild` | yes | on | `ForwardSlash` | **done** | case-insensitive by default; no escape char; see below |
| `FileSystemGlobbing` | yes | on | `ForwardSlash` | **done** | no classes; no escape character; case folding follows `IgnoreCase` flag |

**`MSBuild` dialect notes (shipped):**

- Allowed in `GlobMatcher.TryCompile` (was previously gated).
- `GlobDialectExtensions.DefaultIgnoreCaseKind` returns
  `IgnoreCaseKind.Unicode` for `MSBuild` regardless of the
  `GlobOptions.IgnoreCase` flag - matches MSBuild's documented
  case-insensitive behavior.
- `GlobDialectExtensions.GetEscapeChar` returns `'\0'` for `MSBuild`
  (MSBuild has no escape character).
- `GlobDialectExtensions.DefaultSeparator` returns `'/'` for `MSBuild`
  (revised from the original `OSDefault` plan, see below).
- `GlobMatcherFactory.TryCreate` force-enables globstar when
  `dialect == GlobDialect.MSBuild` so callers don't need to pass
  `GlobOptions.AllowGlobStar`. Character classes `[...]` are also
  disabled for `MSBuild` (the dialect doesn't support them).
- **Separator revision:** D2 originally targeted `OSDefault` for
  `MSBuild` (matching how MSBuild evaluates strings on the calling OS).
  In practice, when feeding a path-aware matcher through
  `GlobMatchAdapter` (see F3.3), the adapter translates the file system's
  `\` separator into the matcher's separator at the boundary; carrying
  `\` through the compiled program would also require pattern
  pre-normalization (`/` &rarr; `\`) and forbids POSIX-shaped
  patterns. Settling on `'/'` for `MSBuild` keeps the matcher's
  encoded program identical to `PosixPath` and lets callers write
  `**/*.cs` regardless of host OS. The adapter is the single place that
  knows about `\` and translates it. Recorded as an updated entry in
  D2 below.

- **Scope:** Mostly factory wiring per dialect. No new opcodes.
- **Tests for the remaining four dialects:** Per-dialect parity rows. Golden
  cross-references:
  - `Bash`: `bash -O extglob -O globstar -c '[[ "..." == ... ]] ; echo $?'`.
  - `MSBuild`: `Microsoft.Build.Globbing.MSBuildGlob.Parse(pattern).IsMatch(input)`
    (already covered via `MsBuildEnumeratePerf2` result-count parity:
    `MSBuild = MsBuildEnumerator = GlobEnumerator = 4850` over the touki
    repo).
  - `FileSystemGlobbing`: `Matcher.AddInclude(pattern).Match(...)`.

### F2.4 Wire `GlobPathSeparator` option - **done**

Implement the enum and `Compile` overload from
[D2](#d2-matcher-requires-normalized-input-paths-separator-is-chosen-at-compile-time).
No cross-platform `\` &harr; `/` translation in the matcher; the contract is
"normalized input only".

- **Status:** Shipped on this branch.
  [`GlobPathSeparator`](../touki/Touki/Io/Globbing/GlobPathSeparator.cs)
  enum exposes `DialectDefault`, `OSDefault`, `ForwardSlash`, and
  `Backslash`. New `Compile` and `TryCompile` overloads accept the enum and
  thread it through `GlobMatcherFactory.TryCreate` &rarr;
  `GlobMatcherFactory.ResolveSeparator`, which collapses the enum to a `char`
  (or `'\0'` for path-unaware dialects). The resolved separator is applied
  via the new `Separator { get; init; }` initializer on `GlobMatcher` at the
  two reachable construction sites (`LiteralGlobMatcher`,
  `CompiledGlobMatcher`). `EncodeProgram`'s segment-bounded `**` detection
  is now separator-aware, so `**\foo` is recognized as a globstar when
  `GlobPathSeparator.Backslash` is selected.
- **Tests added:** 8 new `GlobMatcherTests` rows covering
  `DialectDefault` behavior across the five path-aware dialects, explicit
  `ForwardSlash` / `Backslash` overrides on `PosixPath` / `MSBuild` / `Git`,
  `OSDefault` resolution to `Path.DirectorySeparatorChar`, path-unaware
  dialects ignoring the parameter, and the `**\*.cs` separator-aware
  globstar case.

---

## Phase 3 - `.gitignore` specifics

Git's `.gitignore` syntax adds three orthogonal features on top of generic
path-aware globs:

### F3.1 Leading `!` negation - **done**

A pattern starting with `!` inverts the match. Used in `.gitignore` to
re-include a file that an earlier pattern excluded.

- **Status:** Shipped on this branch. `GlobMatcher` exposes a `Negated`
  init property; `IsMatch` is now a non-virtual wrapper that inverts the
  result of an internal `MatchCore` (renamed from the previous abstract
  `IsMatch`). The factory strips a leading `!` only for the `Git`
  dialect and sets the flag through an object initializer when the
  resulting matcher is constructed. Non-Git dialects treat `!` as a
  literal character.
- **Tests:** Negation theory rows + property assertions added in
  `GlobMatcherTests` under the Git section.

### F3.2 Leading `/` anchor + trailing `/` directory-only - **done**

The factory recognizes both markers and strips them from the pattern,
exposing each as a read-only property on the matcher
([D3](#d3-gitignore-directory-only--and-git-specific-attribute-filters-belong-on-the-enumerator)).
`IsMatch` itself is unchanged - it answers "does this string match this
pattern?" regardless of file-system attributes. The directory-only flag is
enforced by the `MatchEnumerator` (when it ships) using `FileAttributes`,
not by the matcher.

- **Status:** Shipped on this branch. `GlobMatcher` exposes
  `RootAnchored` and `DirectoryOnly` init properties. The factory
  strips both markers only for the `Git` dialect. Non-Git dialects
  leave these properties at <see langword="false"/>.
- **Tests:** Marker-strip property assertions on `GlobMatcher.Compile`
  for `Git`; the combined-markers case (`!/bin/`) verifies all three
  fire together.
- **Pending:** Enforcement of `RootAnchored` and `DirectoryOnly`
  belongs to the `GlobMatchAdapter` / enumerator layer and ships with
  the F3.3 segment-pruning follow-up.

### F3.3 `GlobMatcher` &rarr; `IEnumerationMatcher` adapter; sets via `MatchSet` - **partial (adapter + enumerator done; `MatchSet` composition pending)**

`.gitignore`, MSBuild item-lists, and `Microsoft.Extensions.FileSystemGlobbing.Matcher`
all evaluate **lists** of patterns, not single patterns. Some patterns
include, others exclude, with order-dependent semantics in git's case.

Per
[D5](#d5-path-aware-matching-integrates-via-ienumerationmatcher-sets-compose-via-matchset),
this is **not** a new aggregator type below the matcher layer. It is
two concrete pieces:

1. **An `IEnumerationMatcher` adapter for path-aware `GlobMatcher`.**
   The adapter holds the compiled matcher, the per-directory cached
   NFA state, and translates
   `IEnumerationMatcher.MatchesDirectory(currentDirectory, name, matchForExclusion)`
   into the matcher's segment-walking decision (full / partial / no
   match) using the same shape as `MatchMSBuild`. `DirectoryFinished()`
   invalidates the cache. The adapter consumes `RootAnchored`,
   `DirectoryOnly`, and `Negated` (from [F3.2](#f32-leading--anchor--trailing--directory-only)
   / [F3.1](#f31-leading--negation)) to short-circuit appropriately.
2. **Aggregation through [`MatchSet`](../touki/Touki/Io/MatchSet.cs).**
   Callers wrap each compiled matcher in the adapter and add it to a
   `MatchSet` as include or exclude; `MatchSet` already enforces
   excludes-before-includes, fan-out of `DirectoryFinished()`, and the
   partial-match recursion rule. No new public type is required for
   the common include + exclude case.

Git's order-sensitive "later override wins" rule is the one case
`MatchSet` does not handle today (it evaluates all excludes before any
includes). Add an `OrderedMatchSet` (or an `MatchSet` option) at the
`Touki.Io` layer when F3 ships if a `.gitignore`-faithful evaluator is
needed. This still lives at the `IEnumerationMatcher` boundary, not
inside the glob matcher.

Phase-4 in
[globbing-optimization-plan.md](globbing-optimization-plan.md#36-pshufb-based-class-classifier)
flags `SearchValues<string>` as the perf vehicle for include-set
scanning; that optimization slots in behind the adapter once a
benchmark exists.

- **Scope:** New `IEnumerationMatcher` adapter under
  `touki/Touki/Io/Globbing/` that wraps a
  path-aware `GlobMatcher`; per-directory cached NFA state on the
  adapter (not the matcher) so a single compiled `GlobMatcher` can be
  shared across multiple concurrent enumerations.
- **Touches:** New adapter file; no API changes on `GlobMatcher` itself
  beyond the read-only properties already introduced in F3.1 / F3.2.
- **Tests:** Adapter wrapping each path-aware dialect; composition
  through `MatchSet` for include + exclude golden sets; if/when
  `OrderedMatchSet` lands, the `.gitignore` order-sensitive golden
  set.

**Shipped on this branch:**

- [`Touki.Io.Globbing.GlobMatcher`](../touki/Touki/Io/Globbing/GlobMatcher.cs)
  - the matcher base class itself implements
  [`IEnumerationMatcher`](../touki/Touki/Io/IEnumerationMatcher.cs)
  directly. Setting `GlobMatcher.RootDirectory` opts the matcher in to
  path-relative enumeration; the matcher absorbs the per-directory
  prefix cache, three-valued
  [`PrefixAlignment`](../touki/Touki/Io/Globbing/GlobMatcher.cs)
  classification, and joined-buffer reuse that previously lived on a
  separate adapter. `MatchesFile` builds the relative
  `directory/filename` path in its owned buffer and runs
  `IsMatch(joined)`; per-file work in steady state is one `CopyTo` of
  the file name plus the match itself, with no allocation or pool
  rental. `MatchesDirectory` returns `false` when the candidate child
  diverges from `LiteralPathPrefix`, so the enumerator skips the
  subtree entirely. The flat-string
  `IsMatch(ReadOnlySpan<char>)` stays as the convenience entry point
  for unit tests and one-shot callers.
- [`Touki.Io.GlobEnumerator`](../touki/Touki/Io/GlobEnumerator.cs)
  - a `MatchEnumerator<string>` factory mirroring
  `MSBuildEnumerator`'s strip-project-directory shape. When there are
  no excludes, the compiled `GlobMatcher` is passed directly as the
  `IEnumerationMatcher`; with excludes, the include matcher plus one
  exclude matcher per pattern compose through
  [`MatchSet`](../touki/Touki/Io/MatchSet.cs) (same shape
  `MSBuildEnumerator` uses for MSBuild specs).
- **Move:** the globbing classes moved from `Touki.Text.Globbing` to
  `Touki.Io.Globbing` (folder `touki/Touki/Io/Globbing/`) so they sit
  alongside `MatchMSBuild`, `MatchSet`, and the other
  `IEnumerationMatcher` implementations.
- **Performance verified.** [`touki.perf/MsBuildEnumeratePerf2.cs`](../touki.perf/MsBuildEnumeratePerf2.cs)
  benchmarks `GlobEnumerator` (MSBuild dialect) against the existing
  `MsBuildEnumerator` and the raw `Microsoft.Build` `FileMatcher`.
  Numbers on the touki repo with `**/*.cs` + 10 standard excludes:
  `GlobEnumerator` is within ~3% of `MsBuildEnumerator` on net10
  (43.30 ms vs 41.88 ms) and ~16% slower on net481 (52.65 ms vs 45.48 ms);
  result counts identical (`MSBuild = MsBuildEnumerator = GlobEnumerator = 4850`).
  Both are ~2.6&times; faster than the `Microsoft.Build` baseline.

**Still pending:**

- Per-directory cached NFA state - **done.** The matcher caches
  the translated relative-directory prefix per directory and
  invalidates it on `DirectoryFinished()`, plus a three-valued
  `PrefixAlignment` derived from `GlobMatcher.LiteralPathPrefix`
  (`Beyond` / `OnPrefix` / `Diverged`). `MatchesFile` short-circuits
  when alignment is not `Beyond`. **Two-span `MatchCore(first, second)`
  is the primary entry point**: `MatchesFile` hands the cached prefix
  span and the raw file-name span to the matcher without any join.
  Both `LiteralGlobMatcher` and
  [`CompiledGlobMatcher`](../touki/Touki/Io/Globbing/CompiledGlobMatcher.cs)
  override natively - the four match loops
  (`MatchOrdinalSimple`, `MatchIgnoreCaseSimple`, `MatchOrdinal`,
  `MatchIgnoreCase`), plus `Backtrack`,
  `FirstValidGlobStarLength`, `NextValidGlobStarLength`, and the
  tail-anchor fast-fail at the top of `MatchCore`, all consume the
  virtual `(first, second)` concatenation via an inline indexer
  pattern and a `LiteralMatchesAt` straddle-aware compare helper that
  preserves the vectorized `SequenceEqual` / `EqualsOrdinalIgnoreCase`
  / `EqualsAsciiLetterIgnoreCase` paths on contiguous slices. Per-file
  work is now `O(pattern complexity)` - no `CopyTo` of the file
  name, no allocation, no pool rental. Full parity with
  [`MatchMSBuild`](../touki/Touki/Io/MatchMSBuild.cs)'s
  rents-nothing-allocates-nothing contract.
- One-time OS-separator pattern normalization - **done.**
  [`GlobMatcherFactory.TryCreate`](../touki/Touki/Io/Globbing/GlobMatcherFactory.cs)
  translates cross-separator characters in the pattern to the resolved
  separator at compile time for dialects with no escape character
  (`MSBuild`, `FileSystemGlobbing`, `Simple`; opt-in for any dialect
  via `GlobOptions.NoEscape`). `GlobMatcher.MatchesFile`'s
  prefix-cache refresh now uses a plain `CopyTo` when the matcher's
  `Separator` equals `Path.DirectorySeparatorChar`, skipping the
  per-character translation pass. Combined with `OSDefault` /
  `Backslash` separator selection, callers get a true zero-translation
  hot path on Windows for MSBuild / FileSystemGlobbing patterns
  written with portable `/` slashes. Dialects with `\` escape
  (POSIX-family, Bash, Git, PowerShell) skip normalization so escape
  sequences are not corrupted.
- Segment-level `MatchesDirectory` pruning - **partial.**
  `MatchesDirectory` already returns `false` for diverged candidates
  with a non-empty literal prefix. Patterns whose literal prefix is
  empty (e.g., `**/*.cs`) still recurse unconditionally; pruning those
  requires a separator-checkpointed NFA savepoint API on the matcher
  (different from the two-span walk above) and is its own follow-up.
- `MatchSet` composition for matchers - **done.**
- `RootAnchored` / `DirectoryOnly` enforcement - **done.**
  Per-decision wiring:
  <list type="bullet">
   <item><description>Non-anchored Git patterns: the factory recognizes the
    gitignore "match anywhere" rule and prepends <c>**/</c> at compile time
    to Git patterns without an internal separator. Patterns with an internal
    <c>/</c> stay anchored to the gitignore root.</description></item>
   <item><description><see cref="GlobMatcher.MatchesFile"/> short-circuits
    when <see cref="GlobMatcher.DirectoryOnly"/> is set - directory-only
    patterns never match files.</description></item>
   <item><description><see cref="GlobMatcher.MatchesDirectory"/> with
    <c>matchForExclusion=true</c> consults
    <see cref="GlobMatcher.DirectoryOnly"/>: when set and the pattern matches
    the candidate directory's relative path, the whole subtree is claimed
    for exclusion.</description></item>
  </list>
- **`OrderedMatchSet`** - new
  [`Touki.Io.OrderedMatchSet`](../touki/Touki/Io/OrderedMatchSet.cs)
  aggregator implementing gitignore "last matching rule wins" semantics.
  Rules are added via <c>AddInclude</c> / <c>AddExclude</c> in source order.
  At file-match time the set walks all rules and tracks the latest matching
  rule's verdict. At directory-match time the set only claims subtrees when
  a <c>DirectoryOnly</c> exclude matches AND no later include rule could
  rescue a deeper file; conservative recursion otherwise.
- **`GitIgnore`** - new
  [`Touki.Io.GitIgnore`](../touki/Touki/Io/GitIgnore.cs) static loader.
  `Parse(content, root, options)` parses a <c>.gitignore</c> file body
  (comments via leading <c>#</c>, blank lines, <c>\#</c> / <c>\!</c>
  escapes, trailing-whitespace strip, CRLF / LF / CR line endings) and
  produces an `OrderedMatchSet`. `!`-prefixed lines are stripped and added
  via `AddInclude`; everything else via `AddExclude`. Compiles each rule
  with `GlobDialect.Git` so the factory's marker stripping and `**/`
  prepend semantics apply uniformly.

---

## Phase 4 - brace expansion (`Bash` only)

`*.{jpg,jpeg,png}` expands to three separate patterns before glob matching.
Pure macro substitution, no NFA changes.

- **Scope:** A `BraceExpand(string pattern)` static helper that returns
  `string[]` (or `IEnumerable<string>`). The factory calls it for
  `GlobDialect.Bash` and returns a `GlobSet` (or routes to a new
  `UnionGlobMatcher` that ORs the per-expansion matchers).
- **Edge cases:** Nested braces (`a{b,c{d,e}}` &rarr; `ab ac{d,e}` &rarr;
  `ab acd ace`); ranges `{1..5}`, `{a..z}`; escapes `\{` and `\,`. Per bash
  docs the unclosed-brace fallback is "treat as literal".
- **Ref:** [bash Brace Expansion](https://www.gnu.org/software/bash/manual/html_node/Brace-Expansion.html).

---

## Phase 5 - POSIX bracket-expression extras - **done**

The current bracket scanner accepts `[abc]`, `[a-z]`, and `[!negated]`. The
full POSIX bracket-expression grammar also includes:

- **Character classes:** `[:alpha:]`, `[:digit:]`, `[:upper:]`, etc. inside
  the brackets, e.g. `[[:alpha:]_]`.
- **Equivalence classes:** `[=e=]` matches `e`, `é`, `è`, etc. per locale.
- **Collating elements:** `[.ch.]` matches a single collation element. In the
  C locale this is identical to `c` followed by `h` - basically never
  useful outside locale-aware libc.

- **Status:** Shipped on this branch. `EmitClass` recognizes the
  `[:NAME:]`, `[=...=]`, and `[.....]` sub-forms inside `[...]`.
  `[:NAME:]` inline-expands to the equivalent ASCII range list via
  `AppendPosixNamedClass`; recognized names are `alpha`, `digit`,
  `upper`, `lower`, `alnum`, `xdigit`, `space`, `blank`, `cntrl`,
  `print`, `graph`, `punct`. Unknown class names fall back to literal
  character handling so `[[:foo:]]` doesn't reject. `[=...=]` and
  `[.....]` accept their inner characters as a literal run (per the
  no-op contract). `SkipClass` (the scan pass) was updated to skip
  these sub-forms so the inner `:]` / `=]` / `.]` isn't mistaken for
  the outer class terminator.
- **Tests added:** 33 new `GlobMatcherTests` theory rows in
  `IsMatch_Posix_NamedClass`, `IsMatch_Posix_WhitespaceClasses`, and
  `IsMatch_Posix_EquivAndCollating_AcceptedAsLiterals`.
- **Ref:** [POSIX 9.3.5 RE Bracket Expression](https://pubs.opengroup.org/onlinepubs/9799919799/basedefs/V1_chap09.html#tag_09_03_05).

---

## Win32 / WinNT - indefinitely postponed

The Windows-native dialects `Win32` (with the `TranslateWin32Expression`
8.3-compat pre-pass) and `WinNT` (raw `FsRtlIsNameInExpression` semantics)
were planned but are **indefinitely postponed**, and the `Win32` member has
been **removed from `GlobDialect`** along with every doc cref and test row
that referenced it.

**Why postponed.** Bit-for-bit `FileSystemName.MatchesWin32Expression` parity
needs a multi-state NFA with epsilon transitions for the DOS metacharacters
(`<` DOS_STAR, `>` DOS_QM, `"` DOS_DOT) that does not fit the current
single-savepoint backtracker. It would require a dedicated `Win32GlobMatcher`
plus a compile-time `TranslateWin32Expression` pre-pass. There is no concrete
caller asking for it, and callers who need exact Windows matching today can
call the BCL directly.

**What replaces it.** Anyone needing Win32 / NT matching semantics calls
[`System.IO.Enumeration.FileSystemName.MatchesWin32Expression`](https://learn.microsoft.com/dotnet/api/system.io.enumeration.filesystemname.matcheswin32expression)
(net10 BCL) directly. `Touki.Io.Paths` already surfaces the
`System.IO.Enumeration.MatchType.Win32` enumeration mode for the file-system
enumerator. This is unrelated to `GlobDialect` and is not affected by the
removal.

**Bringing it back.** If a future caller needs it, re-add a `Win32` (and
optionally `WinNT`) member to `GlobDialect`, add it to the
`GlobSpecification.TryCompile` allowlist, and port the BCL `MatchPattern`
algorithm (`useExtendedWildcards: true` branch) into a new
`Win32GlobMatcher`. Oracle: cross-reference against
`MatchesWin32Expression` (net10 only; the API is not on net472), and
`RtlIsNameInExpression` P/Invoke on Windows. `Win32` defaults to
case-insensitive (Unicode) matching - the historical D4 decision.

- **Ref:** [FsRtlIsNameInExpression](https://learn.microsoft.com/windows-hardware/drivers/ddi/ntifs/nf-ntifs-fsrtlisnameinexpression),
  [`FileSystemName.cs` source](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/IO/Enumeration/FileSystemName.cs).

---

## Cross-cutting follow-ups

### Ignore-case customization (in
[globbing-optimization-plan.md §5](globbing-optimization-plan.md))

Today `IgnoreCaseKind` is dialect-defaulted. Future option: a
`GlobOptions.IgnoreCaseAsciiOnly` flag (or a `GlobOptions.IgnoreCaseUnicode`
flag, or a separate `IgnoreCaseKind` parameter on `Compile`) so callers can
force a kind independent of the dialect.

### Unicode-aware character-class membership

`CompiledGlobMatcher.ClassContainsIgnoreCase` currently uses ASCII fold
regardless of `IgnoreCaseKind`. For Unicode-IC dialects with character
classes containing non-ASCII characters this is incorrect. Becomes
user-visible only after Phase 2 (when MSBuild/FileSystemGlobbing patterns
start hitting class membership at scale) - defer until there's a
failing test.

### Match-time perf (
[globbing-optimization-plan.md](globbing-optimization-plan.md))

- Slice 2: `SearchValues<char>` class membership (defer until F2 produces a
  class-heavy benchmark).
- Slice 5: segment decomposition for `?`-free general patterns (defer until
  path-aware matching produces realistically long inputs).
- `IndexOfOrdinalIgnoreCase` for `ContainsGlobMatcher` (deferred).

---

## Oracle tests - reference sources per dialect

To validate `GlobMatcher`'s behavior on each dialect we want
&quot;oracle&quot; tests that compare our result against a reference
implementation. The intent is to drive a shared theory across both
implementations, fail when they disagree, and iterate until they
converge. This section catalogs the reference for each dialect and the
platform constraints for running it.

| Dialect | Reference | Run from .NET | Platform constraint |
|---|---|---|---|
| `Posix` | `fnmatch(3)` (POSIX) without `FNM_PATHNAME` | P/Invoke <c>libc</c> / <c>libSystem.B.dylib</c>, or shell-out to <c>python -c &quot;import fnmatch; ...&quot;</c> | Linux/macOS native; WSL on Windows |
| `PosixPath` | `fnmatch(3)` with `FNM_PATHNAME` flag set | Same P/Invoke; Python `pathlib.PurePath.match` is approximate, not exact | Linux/macOS native; WSL on Windows |
| `Simple` | [`System.IO.Enumeration.FileSystemName.MatchesSimpleExpression`](https://learn.microsoft.com/dotnet/api/system.io.enumeration.filesystemname.matchessimpleexpression) | Direct managed call | Any TFM net5+ - **net10 test project, cross-platform** |
| `PowerShell` | [`System.Management.Automation.WildcardPattern`](https://learn.microsoft.com/dotnet/api/system.management.automation.wildcardpattern) | Reference `System.Management.Automation` (Windows PowerShell SDK / pwsh SDK), or subprocess to <c>pwsh -c</c> | SMA assembly is Windows-only by default; <c>pwsh</c> subprocess works cross-platform if PowerShell 7+ is installed |
| `Bash` | <c>bash -O extglob -O globstar -c '[[ &quot;$input&quot; == $pattern ]] ; echo $?'</c> | Subprocess to <c>bash</c> on PATH | bash 4.0+ native on Linux/macOS; Git Bash or WSL on Windows |
| `MSBuild` | [`Microsoft.Build.Globbing.MSBuildGlob.Parse(pattern).IsMatch(input)`](https://learn.microsoft.com/dotnet/api/microsoft.build.globbing.msbuildglob) | Direct managed call (NuGet: `Microsoft.Build`) | Any TFM net472+ - **already referenced via `FileMatcherWrapper` for the enumeration parity check** |
| `FileSystemGlobbing` | [`Microsoft.Extensions.FileSystemGlobbing.Matcher`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.filesystemglobbing.matcher) | Direct managed call (NuGet: `Microsoft.Extensions.FileSystemGlobbing`) | Any TFM netstandard2.0+ - **cross-platform** |
| `Git` (pattern-level) | <c>git check-ignore --no-index --verbose -- &lt;path&gt;</c> against a temporary <c>.gitignore</c> | Subprocess to <c>git</c> on PATH | Git installed on all major platforms |
| `Git` (in-process) | [`LibGit2Sharp`](https://github.com/libgit2/libgit2sharp) <c>Repository.Ignore.IsPathIgnored</c> | NuGet: `LibGit2Sharp` (native lib bundled) | Cross-platform; LibGit2Sharp ships native binaries for Windows/Linux/macOS |

### Notes per dialect

- **`Posix` / `PosixPath`**: The canonical reference is the C `fnmatch(3)`
  function from the platform's libc. On glibc/musl the implementation is
  straightforward to P/Invoke; on Windows there is no native equivalent,
  so the oracle test class needs a <c>[Trait("Platform", "Unix")]</c>-style
  skip on Windows. A managed approximation that avoids the platform skip is
  `System.IO.Enumeration.FileSystemName.MatchesSimpleExpression`, but its
  semantic differs (no `FNM_PERIOD`, no leading-dot rule), so it's only
  useful for a partial cross-check, not a full oracle.
- **`Simple`** is among the easiest oracles because the BCL ships a managed
  implementation on every supported TFM and platform. This should be one of
  the first oracle suites stood up; it validates the matcher most users
  encounter via `Directory.EnumerateFiles`.
- **`PowerShell`** has two oracle paths. The in-process path needs a
  reference to `System.Management.Automation`, which is a Windows-only
  assembly when targeting Windows PowerShell 5.1 but is also available
  cross-platform via the <c>Microsoft.PowerShell.SDK</c> NuGet for pwsh 7+.
  The subprocess path works anywhere `pwsh` is on PATH but adds startup
  latency (~200 ms per invocation), so it's only practical for a few-dozen-row
  theory.
- **`Bash`** requires shelling out to <c>bash</c>. The wrapper has to pass
  both `pattern` and `input` as bash variables (not interpolated into the
  command string) to avoid shell-quoting nightmares with <c>*</c>, <c>?</c>,
  brackets, and escapes. The result is the exit code of the `[[ ... == ... ]]`
  test. Per-call cost is ~10-30 ms on Linux native, ~50-100 ms via
  Git Bash on Windows; theory size should be bounded accordingly.
- **`MSBuild`** and **`FileSystemGlobbing`** are the cheapest oracle paths
  for path-aware dialects. Both are pure managed, no subprocess, and the
  packages are already referenced by `touki.tests`. These should be the
  default oracle suites for the MSBuild and FileSystemGlobbing dialects.
- **`Git` (pattern-level)** is awkward: gitignore patterns are evaluated in
  the context of a <c>.gitignore</c> file plus a working tree. The simplest
  setup is a per-test scratch directory containing a single-line
  <c>.gitignore</c> + a touched file, then <c>git check-ignore</c>. Per-call
  cost is ~30-80 ms. For a high-row theory, batch multiple patterns
  into one <c>.gitignore</c> and one <c>git check-ignore</c> invocation per
  batch.
- **`Git` (in-process)** via `LibGit2Sharp` is faster (~1-5 ms per
  query) but adds a native-binary dependency to the test project. Worth it
  for high-row Git theories, optional for small ones.

### CI coverage matrix

| Oracle | Windows runner | Linux runner | macOS runner |
|---|:-:|:-:|:-:|
| BCL `Simple` | yes | yes | yes |
| `Microsoft.Build` MSBuild | yes | yes | yes |
| `Microsoft.Extensions.FileSystemGlobbing` | yes | yes | yes |
| `LibGit2Sharp` | yes | yes | yes |
| `git check-ignore` subprocess | yes (git in PATH) | yes (git in PATH) | yes (git in PATH) |
| C `fnmatch(3)` P/Invoke | no (skip) | yes | yes |
| `bash` subprocess | optional (Git Bash) | yes | yes |
| `pwsh` subprocess | yes | yes (if installed) | yes (if installed) |
| `System.Management.Automation` direct | yes (Windows PowerShell 5.1 referenced) | requires `Microsoft.PowerShell.SDK` | requires `Microsoft.PowerShell.SDK` |

The first four rows cover five of the eight dialects (`Simple`,
`MSBuild`, `FileSystemGlobbing`, `Git`) without any platform gating and
should be the first slice of oracle work. The remaining three dialects
(`Posix`, `PosixPath`, `Bash`) inherently require Unix-family tools and
should be Linux-only test classes; the Windows CI run skips them.
`PowerShell` is a Windows-first oracle that can be extended to other
platforms via `Microsoft.PowerShell.SDK` if the test surface grows.

### Suggested oracle-suite slice order

1. **`Simple` BCL oracle** (zero deps, cross-platform).
   The `MatchesSimpleExpression` algorithm is already in the BCL, so the
   test class is a thin theory comparing
   `GlobMatcher.Compile(..., Simple).IsMatch(input)` against the BCL.
2. **`MSBuild` oracle** via `MSBuildGlob.Parse` - complements the
   existing enumeration parity check by isolating the pattern-level match.
3. **`FileSystemGlobbing` oracle** via `Matcher.AddInclude(pattern).Match`
   for path-aware semantics.
4. **`Git` oracle** via `LibGit2Sharp` if/when gitignore semantics get more
   complex; subprocess `git check-ignore` for small theories meanwhile.
5. **`Posix` / `PosixPath` / `Bash` oracles** on Linux only (and macOS if
   convenient). These validate the dialects that don't have managed
   references and need a platform with the tool installed.
6. **`PowerShell` oracle** via direct SMA on Windows, optionally extended
   to other platforms via `Microsoft.PowerShell.SDK` if needed.

Each suite should be a single `[Theory]` per dialect with `[InlineData]`
rows covering the dialect's own characteristic features (e.g.,
POSIX bracket classes, MSBuild `**/`-style globstar, gitignore
`!` re-includes). The oracle invocation is gated by a static
`[ConditionalFact]`-style attribute or by the test class's TFM/OS
constraints; failures should report &quot;pattern X input Y -
matcher returned Z, oracle returned W&quot; so divergences are
self-diagnosing.

### Sequential-separator behavior - findings (in-progress)

First oracle suite landed: per-dialect `[Theory]` covering runs of
sequential `/` in the **pattern** (doubled, tripled, quadrupled, leading,
trailing, surrounding wildcards, adjacent to globstar). One dialect per
file under
[touki.tests/Touki/Io/Globbing/SequentialSeparatorOracleTests.*.cs](../touki.tests/Touki/Io/Globbing).

Empirical ground truth captured by running each row through both the
oracle and `GlobMatcher` for the dialect. The table below states the
dialect's own rule, established by the oracle - not what touki
currently does.

| Dialect | Sequential separators in the pattern | Notes |
|---|---|---|
| `MSBuild` | **Coalesced**: a run of `/` after the first character collapses to a single `/`, both at pattern-parse time and at input-match time. A *leading* `//` is preserved as a UNC-style root anchor. | `a//b` &equiv; `a///b` &equiv; `a/b` (matches inputs `a/b`, `a//b`, `a///b`). `**//*.cs` &equiv; `**/*.cs`. `a//**//b` &equiv; `a/**/b`. `//a` only matches `//a` - the leading double-separator is not collapsed. |
| `FileSystemGlobbing` | **Not coalesced**: an internal empty pattern segment between two `/` is a one-non-empty-segment wildcard (i.e. `*`). Leading empty segments are dropped; trailing empty segments are tolerated via input-side normalization. | `a//b` &equiv; `a/*/b` (matches `a/x/b`, *not* `a/b` and *not* `a//b`). `**//*.cs` &equiv; `**/*/*.cs` (does *not* match `Foo.cs`; requires at least one intermediate component). `//a` &equiv; `a`. `a//` matches `a/` and `a//`. |
| `Simple` | **Not coalesced**: `/` is a plain literal character with no separator role. Runs are preserved verbatim on both sides. | `a//b` matches *only* `a//b`. No special handling of leading or trailing `/`. Validated against [`FileSystemName.MatchesSimpleExpression`](https://learn.microsoft.com/dotnet/api/system.io.enumeration.filesystemname.matchessimpleexpression). |
| `Git` | **Touki already agrees with `LibGit2Sharp`** - all 21 sequential-separator rows pass. Empirically, the gitignore evaluator treats embedded `//` as a literal empty segment that fails to match any normal path component, so most `a//b` / `**//*.cs` / `a//**//b` patterns simply never match real files. Touki produces the same verdicts. | Pinned via [`LibGit2Sharp.Repository.Ignore.IsPathIgnored`](https://github.com/libgit2/libgit2sharp). Cross-platform; oracle runs on every CI runner. |
| `Posix`, `PosixPath` | **Not coalesced**: runs of `/` are preserved verbatim in the pattern; `fnmatch(3)` (with or without `FNM_PATHNAME`) treats each `/` as a literal. Validated against P/Invoke <c>fnmatch(3)</c> on Linux. | All rows green on Linux. Encapsulated in [`FnmatchInterop`](../touki.tests/Touki/Io/Globbing/FnmatchInterop.cs); the glibc/macOS `FNM_PATHNAME` bit difference is the only platform-specific detail. |
| `Bash` (with `extglob` + `globstar`) | **Not coalesced**: bash's `[[ str == pat ]]` preserves runs of `/` in the pattern. Validated against <c>bash -O extglob -O globstar -c '[[ "$INPUT" == $PATTERN ]]'</c>, with pattern/input passed through env vars to side-step shell quoting. | All rows green on Linux native bash and Windows Git Bash. |
| `PowerShell` | _TBD._ `PowerShell` requires `System.Management.Automation` (Windows-only in net481, `Microsoft.PowerShell.SDK` cross-platform on net10) - deferred to avoid bloating the test project until needed. | &nbsp; |

**Implementation status vs. the contract above.** All compile-time
normalization landed in `GlobMatcherFactory.TryNormalizeRuns` plus
matching runtime helpers on `GlobMatcher` (`CoalesceInputSeparators`,
`DisallowEmptyInput`). All four sequential-separator dialect suites are
green on Windows:

- **MSBuild**: 25 of 25 rows green. The compile pass collapses
  internal/trailing runs of `/` to a single `/` (preserving any leading
  double-separator), and `IsMatch` folds runs in the input the same way
  via `CoalesceInputSeparators = true`.
- **FileSystemGlobbing**: 25 of 25 rows green. The compile pass drops
  leading empty segments and replaces each internal empty segment with a
  `*` token; `CoalesceInputSeparators = true` mirrors `Matcher`'s
  input-side drop-empty-segments rule.
- **Simple**: 20 of 20 rows green. Touki already matched the BCL contract;
  the oracle pins down the regression baseline.
- **Git**: 21 of 21 rows green. Touki already matched LibGit2Sharp's
  gitignore semantics for sequential separators; the oracle pins this
  down so any future engine change that drifts will fail fast.

The Posix-family (Linux/macOS oracles via P/Invoke `fnmatch` and `bash`
subprocess) and `PowerShell` rules will be added to the table
above once their oracle runs produce data - the Posix/PosixPath/Bash
suites are checked in and ready, just skipped on Windows hosts.

### Multiple-asterisk-run behavior - findings (in-progress)

Second oracle suite landed: per-dialect `[Theory]` covering runs of
three-or-more consecutive `*` in the **pattern** (`***`, `****`),
isolated, between literals, and adjacent to a path separator. Files under
[touki.tests/Touki/Io/Globbing/MultipleAsteriskOracleTests.*.cs](../touki.tests/Touki/Io/Globbing),
with the 26 shared rows in [MultipleAsteriskRows.cs](../touki.tests/Touki/Io/Globbing/MultipleAsteriskRows.cs).

| Dialect | Multi-asterisk-run rule | Touki status |
|---|---|---|
| `MSBuild` | **A pattern containing 3 or more consecutive `*` matches nothing.** `MSBuildGlob.Parse` parses the pattern but the resulting glob rejects every input (true for `***`, `a***b`, `a/***/b`, `***/foo`, etc.). The parser only recognizes `*` and `**` as wildcard tokens; anything longer poisons the match. | **26 / 26 green.** Compile-time normalization routes any pattern with a 3+ `*` run to `NeverMatchGlobMatcher`. |
| `FileSystemGlobbing` | **`***`+ collapses to a single `*` (one path component, does not cross `/`).** `a***b` &equiv; `a*b`, `***/foo` &equiv; `*/foo`, `a/***/b` &equiv; `a/*/b`. Notably *not* equivalent to `**` - a run of `*` never gains globstar semantics. | **26 / 26 green.** Compile-time `***`+ &rarr; `*` plus `DisallowEmptyInput = true` to match `Matcher`'s empty-input rejection. |
| `Simple` | **`***`+ behaves like `*` for any non-empty input.** Path-unaware. One outlier: `***` does not match the empty string in the BCL while touki currently matches it. | **26 / 26 green.** Compile-time `***`+ &rarr; `*` plus `DisallowEmptyInput = true` to match `MatchesSimpleExpression`'s blanket empty-input rejection. |
| `Git` | **`***`+ behaves like `**` (globstar across path components)** in `wildmatch`. `***` matches every file in the tree; `a/***` matches `a/b` and `a/b/c` but not `a/` alone; `a/***/b` matches every depth `a/.../b`. | **25 / 26 green, 1 documented divergence.** Compile-time `***`+ &rarr; `**` plus `DisallowEmptyInput = true`. The remaining row (`a/***` vs `a/`) is gitignore's "trailing globstar requires &ge;1 input segment" rule, which our engine's `GlobStar` opcode doesn't enforce. Skipped with reason; trivial engine fix when needed. |
| `Bash` (with `extglob` + `globstar`) | **`***`+ behaves like `**` (globstar)** under `globstar`. Crosses path separators, with the standard globstar carve-out that a `**` enclosed by separators (e.g. `***/foo`) requires at least one path component on its globstar side. | **22 / 26 green, 4 documented divergences.** Compile-time `***`+ &rarr; `**`. The remaining four rows are the shell-glob vs `[[ == ]]` semantic split - touki models bash's *shell-glob* `**` (segment-bounded; `*` doesn't cross `/`); the oracle uses bash's *string-match* `[[ ]]` semantics. Closing these needs a per-context flag; deferred until a real user need. |
| `Posix`, `PosixPath` | All 26 rows green on Linux against `fnmatch(3)` with and without `FNM_PATHNAME`. Compile-time `***`+ &rarr; `*` matches `fnmatch` exactly. Suites skip on Windows. | **26 / 26 green** (Linux). |
| `PowerShell` | TBD - oracle not implemented (see notes above). | &nbsp; |

Cross-dialect summary: **only `Bash` and `Git` give `***`+ a globstar
meaning. Every other dialect treats it as either invalid (MSBuild) or as
a single `*` (FileSystemGlobbing, Simple, Posix-family).** The
implemented compile-time normalization is dialect-specific:

- `MSBuild` &rarr; never-match for any pattern with 3+ consecutive `*`.
- `FileSystemGlobbing`, `Simple`, `Posix` &rarr; collapse runs to a single `*`.
- `Git`, `Bash` &rarr; collapse runs to `**` (globstar).

### Normalization implementation - landed, with documented gaps

The dialect-specific rules above are implemented as a compile-time pass
in [`GlobMatcherFactory.TryNormalizeRuns`](../touki/Touki/Io/Globbing/GlobMatcherFactory.cs)
plus two runtime helpers in [`GlobMatcher.IsMatch`](../touki/Touki/Io/Globbing/GlobMatcher.cs):
`CoalesceInputSeparators` (collapses runs of separator in the input
before matching) and `DisallowEmptyInput` (short-circuits empty inputs to
no-match for dialects whose reference rejects them).

What landed:

- **MSBuild**: any pattern with a run of 3+ `*` compiles to a new
  [`NeverMatchGlobMatcher`](../touki/Touki/Io/Globbing/NeverMatchGlobMatcher.cs);
  pattern-side runs of `/` collapse to a single `/` (leading double
  preserved); the matcher exposes `CoalesceInputSeparators = true` and
  `IsMatch` walks the input collapsing runs before calling `MatchCore`;
  `DisallowEmptyInput = true`.
- **FileSystemGlobbing**: runs of 3+ `*` collapse to one `*`; leading
  empty pattern segments are dropped; internal empty segments become a
  single-`*` segment (`a//b` &rarr; `a/*/b`); trailing-only `//`
  collapses to a single `/`; `CoalesceInputSeparators = true` and
  `DisallowEmptyInput = true` to match `Microsoft.Extensions.FileSystemGlobbing.Matcher`'s
  input-side normalization (it drops empty input path segments).
- **Simple**: runs of 3+ `*` collapse to one `*`;
  `DisallowEmptyInput = true` (the BCL `MatchesSimpleExpression` rejects
  an empty input regardless of the pattern, including bare `*`).
- **Posix, PosixPath**: runs of 3+ `*` collapse to one `*`.
- **Git, Bash**: runs of 3+ `*` collapse to `**`. The Git-anywhere
  prefix prepend (`**/x`) skips when the pattern already starts with a
  globstar token (`**` followed by `/` or end-of-pattern), so
  normalized `***` stays as `**` and doesn't degenerate into `**/**`.
  Git also gets `DisallowEmptyInput = true` (gitignore has no defined
  behavior for an empty path; `LibGit2Sharp.Ignore.IsPathIgnored` rejects
  it outright).

What's left on Windows. **5 of the original 65 oracle-test rows remain
divergent.** All 5 are real engine-level semantics that don't yield to
compile-time normalization; each is marked with `Assert.Skip` and a
documented reason so the test suite goes fully green while the
divergence stays catalogued:

- **Bash dialect, 4 rows** - `***/foo` vs `foo`, `***.cs` vs
  `a/foo.cs`, `a/***/b` vs `a/b`, `a***b` vs `a/b`. Touki's Bash dialect
  models bash's *shell-glob* `**` (segment-bounded; `*` does not cross
  `/`). The `[[ str == pat ]]` oracle uses bash's *string-match*
  semantics, where `*` matches any string including `/` and `**` doesn't
  get a distinct globstar meaning. Closing these needs a per-context
  flag distinguishing the two modes; deferred until a real user need.
- **Git dialect, 1 row** - `a/***` (&rarr; `a/**`) vs `a/`. Touki's
  trailing globstar matches zero or more path components including the
  empty case; gitignore requires &ge;1. Closing this needs an engine
  flag on the trailing GlobStar opcode that suppresses the zero-segment
  match for Git; ~1 hour of work, deferred until first user request.

Test infrastructure notes that came out of this work and are worth
flagging for anyone touching the Bash oracle on Windows:

- `BashInterop.ResolveBashPath` explicitly prefers Git for Windows
  install paths over `bash.exe` discovered on `PATH`, and skips
  `%LocalAppData%\Microsoft\WindowsApps\bash.exe` - the WSL
  launcher stub installed by `wsl --install`. The stub doesn't forward
  process-level environment variables (so the oracle's `PATTERN` /
  `INPUT` env vars come through empty) and doesn't handle `[[ ]]` the
  same way Git Bash does.
- The Git oracle fixture (`SequentialSeparatorGitOracleTests.RepoFixture.IsIgnored`)
  returns `false` for empty paths instead of calling
  `LibGit2Sharp.Ignore.IsPathIgnored("")`, which throws
  `ArgumentException`.

Total state on Windows after the change: **4971 / 4971 tests pass**
(0 oracle rows red; 5 divergences explicitly skipped; 0 regressions
outside the new oracle suites).

---

## Design decisions

### D1. `AllowGlobStar` is dialect-defaulted

`GlobOptions.AllowGlobStar` becomes a *forced override*. Each dialect picks
its documented default; the flag is consulted only when set explicitly.

| Dialect | Globstar default |
|---|---|
| `Posix`, `Simple`, `PowerShell` | off (path-unaware) |
| `PosixPath` | off (requires explicit opt-in per the POSIX `**` extension) |
| `Bash` | off (matches `shopt -s globstar` being off by default) |
| `Git`, `MSBuild`, `FileSystemGlobbing` | on (their documented behavior) |

Same pattern as `IgnoreCaseKind` - computed once in the factory from
`(Dialect, Options)` and stored on the matcher.

### D2. Matcher requires normalized input paths; separator is chosen at compile time

The matcher accepts exactly **one** separator character at match time;
inputs that mix separators must be normalized by the caller. Parsing both
`\` and `/` per character is expensive (an extra branch in every
`Any`/`AnyRun`/literal step) and the cost would be paid by every match call
even when normalization is cheap or already done. We push that cost to
ingest where it can be amortized or skipped.

The separator is picked at `Compile` time via a new option:

```csharp
public enum GlobPathSeparator
{
    OSDefault,      // '\' on Windows, '/' on Unix
    ForwardSlash,   // '/'
    Backslash,      // '\'
}
```

Plumbed through as a new `Compile(pattern, dialect, options, separator)`
overload (default `GlobPathSeparator.OSDefault`). Each path-aware dialect
documents its expected default:

| Dialect | Default separator |
|---|---|
| `PosixPath`, `Bash`, `Git`, `FileSystemGlobbing`, `MSBuild` | `ForwardSlash` |

`MSBuild` was originally specified as `OSDefault` to match how MSBuild
evaluates `Include` / `Exclude` strings on the calling OS. The shipped
implementation revises this to `ForwardSlash` so the compiled bytecode is
identical to `PosixPath` and callers can write `**/*.cs` regardless of
host OS; the
[`GlobMatchAdapter`](../touki/Touki/Io/GlobMatchAdapter.cs) is the single
place that knows about `Path.DirectorySeparatorChar` and translates it.
Recorded under F2.3 above.

**Caller contract:** patterns and inputs must use the chosen separator
consistently. Documented at the type level on `GlobMatcher.IsMatch` and on
the new `GlobPathSeparator` enum.

This replaces the earlier idea of accepting both separators on Windows for
`MSBuild`/`FileSystemGlobbing`. If a consumer needs both, they
normalize first - usually a single `ReadOnlySpan<char>` walk with
`string.Replace` or a `Touki.Buffers` helper.

### D3. `.gitignore` directory-only `/` and Git-specific attribute filters belong on the enumerator

Trailing `/` (directory-only), leading `/` (root-anchor relative to the
gitignore file), and the other path-attribute predicates are **not** the
matcher's concern. `GlobMatcher.IsMatch(ReadOnlySpan<char>)` answers
"does this string match this pattern?" - nothing more.

The directory-only flag, root anchor, and any future attribute predicates
(symlink-only, hidden-only, ACL-based filters, etc.) are wired in at the
`MatchEnumerator` layer when that ships (the ultimate consumer that ties
`GlobMatcher` together with directory traversal). The matcher exposes the
parsed metadata as read-only properties:

```csharp
public bool DirectoryOnly { get; }    // from trailing '/'
public bool RootAnchored { get; }     // from leading '/'
public bool Negated { get; }          // from leading '!'
```

so the enumerator can route per-entry decisions (file vs directory, root
relative path, include vs exclude) without re-parsing.

This keeps `GlobMatcher` allocation-free and OS-agnostic, and lets the
enumerator combine glob matching with `FileAttributes` checks in a single
pass over the directory enumeration. See
[D5](#d5-path-aware-matching-integrates-via-ienumerationmatcher-sets-compose-via-matchset)
for the broader integration contract with
[`IEnumerationMatcher`](../touki/Touki/Io/IEnumerationMatcher.cs) and
[`MatchEnumerator<TResult>`](../touki/Touki/Io/MatchEnumerator.cs).

### D4. `Win32` dialect: IgnoreCase is the default - **retired**

This decision applied to the `Win32` dialect, which is now
[indefinitely postponed](#win32--winnt---indefinitely-postponed) and removed
from `GlobDialect`. Recorded here for the record: Windows file matching is
case-insensitive throughout `FsRtlIsNameInExpression`,
`FileSystemName.MatchesWin32Expression`, and the Windows file system itself,
so if `Win32` is ever reintroduced it should imply `IgnoreCaseKind.Unicode`
regardless of `GlobOptions.IgnoreCase`.

### D5. Path-aware matching integrates via `IEnumerationMatcher`; sets compose via `MatchSet`

Path-aware glob matching is **designed to consume the
[`IEnumerationMatcher`](../touki/Touki/Io/IEnumerationMatcher.cs) boundary,
not a flat path string.** The matcher must be able to run incrementally as
[`MatchEnumerator<TResult>`](../touki/Touki/Io/MatchEnumerator.cs) walks the
file system breadth-first, so the spec is split into segments at the
enumerator's natural granularity (one `(currentDirectory, name)` pair per
entry) instead of being reassembled into a full path and re-split inside
the matcher.

[`MatchMSBuild`](../touki/Touki/Io/MatchMSBuild.cs) is the reference
implementation of this pattern and the model to follow:

- **Per-directory cached state.** `DirectoryFinished()` invalidates a
  `_cacheValid` flag; the next `MatchesFile` / `MatchesDirectory` call
  rebuilds the cached match position once for the new directory and
  reuses it across every file in that directory. The glob matcher's
  segment-walking machinery (`GlobStar` / separator-aware `AnyRun`)
  must expose the same shape: derive the current NFA position from
  the directory once, hold it on the matcher instance, and answer
  many `MatchesFile` calls against it cheaply.
- **Three-valued directory result.** `MatchesDirectory` distinguishes
  `FullMatch` (recurse and files match), `PartialMatch` (recurse only
  - a deeper subdirectory may match), and `NoMatch` (skip the
  subtree entirely). `IEnumerationMatcher.MatchesDirectory` collapses
  partial vs full to a single bool, but uses `matchForExclusion` so a
  partial-match exclude doesn't block recursion. Path-aware glob
  matchers consumed through this interface must implement the same
  collapse rule: full match &rarr; always recurse; partial &rarr;
  recurse unless excluding; no match &rarr; never recurse.
- **No flat-path API on the hot path.** The matcher's public
  `IsMatch(ReadOnlySpan<char>)` stays for unit testing and one-shot
  use, but path-aware enumeration calls through
  `IEnumerationMatcher.MatchesFile(currentDirectory, fileName)` and
  `MatchesDirectory(currentDirectory, directoryName, matchForExclusion)`
  so the enumerator never has to allocate a joined path string and the
  matcher never has to re-walk segments it has already walked. This is
  why F2.2 globstar tracking is described in terms of
  per-segment NFA state, not a flat-string regex.

**Sets compose at this interface, not below it.** Multiple include /
exclude patterns are aggregated through an `IEnumerationMatcher`
composite - today
[`MatchSet`](../touki/Touki/Io/MatchSet.cs) does exactly this for the MSBuild
matcher (excludes first, includes second, breadth-first cache invalidation
fanned out to every child). There is no separate parallel "GlobSet"
type layered below `IEnumerationMatcher`. Each compiled `GlobMatcher`
that is also path-aware exposes an `IEnumerationMatcher` adapter (or
implements the interface directly), and consumers compose them with
`MatchSet`. This keeps the include/exclude evaluation order, the
per-directory cache invalidation, and the partial-vs-full match
collapse in one place - the same place MSBuild already uses -
and lets `MatchEnumerator<TResult>` drive a heterogeneous mix of glob
dialects, MSBuild specs, and bespoke matchers through a single
uniform call site.

Implementation implications:

- F2.2 `GlobStar` state machine must be expressible as a `specIndex` +
  partial-match decision at directory boundaries (the same shape as
  `MatchMSBuild.MatchSegments`), so it can answer `MatchesDirectory`
  before descending and `MatchesFile` after.
- F3.3 ships as a thin path-aware adapter (`GlobMatcher` &rarr;
  `IEnumerationMatcher`) plus documentation that users compose via
  `MatchSet`. The `GlobSet` aggregator originally sketched in this
  doc is **dropped** - `MatchSet` covers the use case.
- Gitignore order-sensitivity (later override wins) is handled by
  configuring `MatchSet` accordingly or by adding an ordered variant
  alongside `MatchSet`; either way it lives at the `IEnumerationMatcher`
  layer, not inside the glob matcher.

---

## Suggested slice order

The cheapest items first, building toward the path-aware phase:

| # | Item | Estimated effort | Unlocks |
|---:|---|---|---|
| 1 | F1.1 `PowerShell` dialect - **done** | ~half-day | One dialect closed |
| 2 | F2.1 separator-aware `Any` / `AnyRun` - **done** | ~1 day | `PosixPath` dialect unlocked; lays the groundwork for F2.2 |
| 3 | F2.2 `**` globstar - **done** | ~2-3 days | Four more path-aware dialects unblocked (Bash / Git / MSBuild / FileSystemGlobbing) |
| 4 | F2.3 `MSBuild` / `Bash` / `FileSystemGlobbing` dialect wiring - **done** | ~1 day | Three more dialects closed |
| 5 | F3.1 + F3.2 + Git dialect wiring - **done** | ~1 day | `Git` dialect feature-complete (`!`, leading `/`, trailing `/` markers exposed as init properties) |
| 6 | F3.3 `IEnumerationMatcher` adapter + `GlobEnumerator` - **done (partial; no segment pruning, no `MatchSet` composition yet)** | ~1 day | Drives enumerator integration; perf comparison against `MsBuildEnumerator` |
| 7 | F1.3 + F1.4 `AllowExtGlob` + Bash extglob wiring | ~2-3 days | One option closed; full `Bash` coverage; introduces savepoint-stack NFA reused by F1.2 |
| 8 | F2.4 `GlobPathSeparator` option - **done** | ~half-day | Caller-controlled separator (forward-slash / back-slash / OS default) |
| 9 | F3.3 segment-level pruning + `MatchSet` composition + `RootAnchored`/`DirectoryOnly` enforcement | ~1-2 days | Subtree skipping for literal-prefix excludes; mixed-dialect include/exclude sets; gitignore enforcement |
| 10 | F4 brace expansion | ~1 day | `Bash` feature-complete |
| 11 | F5 POSIX bracket extras - **done** | ~half-day | `Posix` `[[:class:]]` parity |
| - | F1.2 / F1.2b `Win32` + `WinNT` - **deferred, low priority** | ~2-3 days | Two dialects closed; needs dedicated `Win32GlobMatcher` (BCL `MatchPattern` port) |

Stop conditions between slices - same as
[globbing-optimization-plan.md §4](globbing-optimization-plan.md#4-proposed-slice-order):
each slice runs the full test suite on both TFMs and re-runs the relevant
benchmark rows; zero match-time allocations on `IsMatch_DoesNotAllocate`.
