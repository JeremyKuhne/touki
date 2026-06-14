# Touki overlay - framework-jit-optimization

Repo-specific companion to the vendored [framework-jit-optimization](SKILL.md)
skill. The `SKILL.md`, its `specialization.md` / `unrolling.md` /
`bcl-tradeoffs.md` / `antipatterns.md` siblings, and the bundled
[references/framework-span-performance.md](references/framework-span-performance.md)
are a **pinned copy of the portable core** from
[JeremyKuhne/agent-skills](https://github.com/JeremyKuhne/agent-skills) (see the
`metadata.github-*` provenance in `SKILL.md`'s frontmatter). Do not hand-edit the
core or its `references/` - `gh skill update` would flag the drift. Everything
touki-specific lives here instead.

## Cross-references (the core names these skills generically)

- [`performance-testing`](../performance-testing/SKILL.md) - author/run the
  BenchmarkDotNet benchmarks and capture the before/after tables this skill's
  decisions depend on. Its [reading-codegen.md](../performance-testing/reading-codegen.md)
  is the "codegen-reading page" that `modern-net.md` and `cross-tfm-codegen.md`
  point to (sharplab, `[DisassemblyDiagnoser]`, `[HardwareCounters]`,
  `DOTNET_JitDisasm*`).
- [`il-copy-inspection`](../il-copy-inspection/SKILL.md) - the "IL-copy-inspection
  skill" that `cross-tfm-codegen.md` section 4 names for confirming defensive copies
  (the source-level counterpart is the TOUKI0002-0004 analyzers).
- [`pre-pr-self-review`](../pre-pr-self-review/SKILL.md) - the checklist to run
  before opening a PR; its framework-correctness items apply directly here.
- [`polyfill-dotnet-api`](../polyfill-dotnet-api/SKILL.md) - the upstream "how do
  I polyfill API X for net472?" decision; this skill picks up after the polyfill
  lands in `touki/Framework/`.
- [`scratch-buffer-strategy`](../scratch-buffer-strategy/SKILL.md) - choosing a
  scratch buffer (zeroed `stackalloc` vs `[SkipLocalsInit]` vs
  `Touki.Buffers.BufferScope<T>` vs an `ArrayPool` rental) and the net481/net10
  size crossovers.

## Touki source and conventions

- The Framework-only tree the core refers to generically is `touki/Framework/`
  (excluded from the modern build). The perf project is `touki.perf/`.
- The `IComparable<T>` specialization example is
  [SpanExtensions.InRange.cs](../../../touki/Framework/Polyfills/System/SpanExtensions.InRange.cs)
  (full byte/sbyte/char/short/ushort/int/uint/long/ulong specialization).
- The signed-primitive constant-propagation pitfall in
  [specialization.md](specialization.md) was confirmed by disassembly in
  [touki.perf/ReplaceUnsafeAsPerf.cs](../../../touki.perf/ReplaceUnsafeAsPerf.cs).
- The production helpers that resulted from the span-walking experiments are in
  [touki/Touki/Buffers/SpanExtensions.IgnoreCase.cs](../../../touki/Touki/Buffers/SpanExtensions.IgnoreCase.cs).

## `modern-net.md` and `cross-tfm-codegen.md` - touki examples

The two newer sibling pages are written generically; here is where touki already
applies them.

- **`BitOperations`** ([cross-tfm-codegen.md](cross-tfm-codegen.md) section 2) is
  polyfilled for the Framework target in
  [touki/Framework/Polyfills/System.Numerics/BitOperations.cs](../../../touki/Framework/Polyfills/System.Numerics/BitOperations.cs).
  Use it instead of hand-rolled bit tricks on both TFMs.
- **`Math.DivRem` / `Math.BigMul`** (same page, section 1) are polyfilled in
  [touki/Framework/Polyfills/System/MathExtensions.cs](../../../touki/Framework/Polyfills/System/MathExtensions.cs).
- **`ReadOnlySpan<byte>` RVA blob** (section 5) - the live example is
  `CharToHexLookup` in
  [touki/Touki/Text/HexConverter.cs](../../../touki/Touki/Text/HexConverter.cs).
- **Hot-path allocation anti-patterns** (section 7) - the repo's avoidance types are
  [Touki.Text.ValueStringBuilder](../../../touki/Touki/Text/ValueStringBuilder.cs)
  (stack-seeded string building), the
  [Touki.Collections](../../../touki/Touki/Collections/ContiguousList.cs) pooled
  lists, and the [EnumExtensions](../../../touki/Touki/EnumExtensions.cs) flag
  helpers (no `Enum.HasFlag` boxing).
- **Branchless vs branchful** ([cross-tfm-codegen.md](cross-tfm-codegen.md) section 3
  and [antipatterns.md](antipatterns.md)) - the tuple-swap A/B is
  [touki.perf/SpanSwapPerf.cs](../../../touki.perf/SpanSwapPerf.cs); the `Unsafe.As`
  constant-propagation pitfall is
  [touki.perf/ReplaceUnsafeAsPerf.cs](../../../touki.perf/ReplaceUnsafeAsPerf.cs).
- **BCL-first on `net10`** ([modern-net.md](modern-net.md)) - the `SearchValues<char>`
  class-membership matcher is tracked in
  [docs/dotnet-perf-discoveries.md](../../../docs/dotnet-perf-discoveries.md) and the
  [globbing feature plan](../../../docs/globbing-feature-plan.md).

## The span field manual and the touki worked example

The bundled
[references/framework-span-performance.md](references/framework-span-performance.md)
is the portable field manual (slow-span layout, the GC-frame `[SkipLocalsInit]`
carve-out, the Strategy A-E hierarchy, cost summary, decision flowchart,
anti-patterns). touki keeps the **touki-specific appendix** in
[docs/framework-span-performance.md](../../../docs/framework-span-performance.md):
the `OrdinalIgnoreCase` ASCII fast-path worked example and the touki reference
list. The deep case study with full disassembly is
[docs/bcl-ignorecase-valley-rca.md](../../../docs/bcl-ignorecase-valley-rca.md);
the running perf-observations list is
[docs/dotnet-perf-discoveries.md](../../../docs/dotnet-perf-discoveries.md).
The A/B/C span-vs-ref-vs-pinned harness is
[touki.perf/AsciiIgnoreCaseUnsafePerf.cs](../../../touki.perf/AsciiIgnoreCaseUnsafePerf.cs);
the `StringSegment.CompareTo` before/after baseline is
[touki.perf/StringSegmentIgnoreCasePerf.cs](../../../touki.perf/StringSegmentIgnoreCasePerf.cs).

## Updating

Pull upstream changes to the core (and its `references/`) with
`gh skill update framework-jit-optimization` (review the diff, re-pin). Keep
touki-specific additions in this file, not in the core.

The [modern-net.md](modern-net.md) and [cross-tfm-codegen.md](cross-tfm-codegen.md)
sibling pages originated here and were upstreamed to the
[agent-skills commons](https://github.com/JeremyKuhne/agent-skills) in
[PR #1](https://github.com/JeremyKuhne/agent-skills/pull/1); the vendored core is
pinned to `v0.6.0`, which includes them. No pending divergence remains.
