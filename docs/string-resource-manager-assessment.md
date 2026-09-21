# String resource manager assessment

For the final executive design and performance summary, see
[Generated string resource solution](string-resource-manager-solution.md).

## Status

Batch 1 implements the approved trusted-resource, strict-by-default, no-copy
indexed architecture. The post-review Release solution build and tests pass, as
does the three-mode Native AOT smoke. Final-source indexed mechanism measurements
pass on both JITs. Batch 2 now includes the Touki C# resx generator, generated
per-property cache, runtime-satellite selection, package integration, and a
localized fixture validated on net10.0, net11.0, and net481.

## Outcome

`StringResourceManager` is a standalone virtual base class, and
`SatelliteStringResourceManager` derives from it. The implementation provides:

- assembly, file, and stream-factory neutral sources;
- runtime satellites, loose per-culture `.resources` files, and directly parsed
  satellite DLLs through separate factories;
- strict-by-default `StringResourceManagerOptions`, with optional non-string
  omission and unconditional null rejection;
- lazy indexed lookup over retained manifest, mapped-file, memory, or seekable
  stream backing with no complete source copy;
- finalizable mapped-view leases without making every indexed table finalizable;
- ordinal, case-sensitive string lookup;
- parent-culture and neutral fallback;
- one source load per culture and cache generation, including concurrent first
  lookup;
- assembly-scoped source metadata and positive/negative satellite bind caches;
- generation-aware release without stale cache publication;
- caller-owned neutral-manager composition; and
- Native AOT-compatible direct satellite parsing.

Construction, neutral first lookup, warmed lookup, and warmed-process runtime
satellite first lookup beat the corresponding BCL modes on modern .NET RyuJIT
and .NET Framework 4.8.1 RyuJIT. All warmed rows allocate 0 B. Fresh-process IL
and ReadyToRun lookup still lose to the precompiled BCL because first-use JIT and
runtime fixups dominate; that deployment boundary remains explicitly reported.

The approved revision has these governing contracts:

- resource data is a trusted application build/deployment artifact;
- present malformed, unreadable, or unsupported resources throw instead of
  being treated as absent;
- null resources always reject the table;
- default options reject a table containing any non-string resource;
- `IgnoreNonStringResources` permits mixed tables but retains neither the names
  nor values of non-string entries, so those names behave as missing;
- complete streams and files are never copied merely to parse them; and
- every matching construction, first-load, and warmed-lookup row is an
  independent performance gate.

## Performance method

Measurements were captured on 2026-09-18 and 2026-09-19 with BenchmarkDotNet
0.16.0-preview.1 on Windows 11 25H2 and an Intel Core Ultra 7 270K Plus:

- .NET 10.0.12, x64 modern .NET RyuJIT with x86-64-v3; and
- .NET Framework 4.8.1 (4.8.9345.0), x64 .NET Framework 4.8.1 RyuJIT.

Default-job results use `[MemoryDiagnoser]`. Earlier analysis also calculated a
weighted geometric mean of construction, first load, and warmed lookup. That
score is retained below only as historical evidence; it is not an acceptance
criterion because it hid localized startup losses behind faster construction
and warmed lookup.

Fresh-process startup is not part of the weighted score. The original
BenchmarkDotNet `ColdStart` job ran one first lookup in each fresh child process,
but the measured operation did not include process launch or `GlobalSetup`, and
the generated Touki assemblies contained IL while framework BCL assemblies were
precompiled. That result is retained only as the ordinary IL deployment case.

A follow-up standalone harness used the same source and fixtures for
framework-dependent IL, framework-dependent ReadyToRun, .NET Framework with and
without NGen, and Native AOT. It measured both the lookup internally and the full
process externally over 15 randomized fresh processes per case. Elevated ETW
and process-local EventPipe captures then separated JIT, binding/parsing,
blocking, and physical I/O.

## Current optimized evidence

The tables in this section are final-source BenchmarkDotNet short-job screens.
Retained modern .NET RyuJIT default-job runs are called out separately where
available; they corroborate the screens but are not presented as the same run.

### Indexed-loader mechanism

Strict validation plus one indexed lookup was compared with prior eager
whole-table materialization. Both methods use the same array-backed source and
include reader/table disposal. The `Typical` shape uses unpadded names and
8-character string values. `LongNameAndValue` adds 128 name characters and uses
256-character values. `Mixed` adds one ignored integer to the typical table.
Error is half of the BenchmarkDotNet 99.9% confidence interval from the
three-iteration short job.

| Runtime | Entries | Shape | Eager mean +/- error | Indexed mean +/- error | Eager allocation | Indexed allocation |
| --- | ---: | --- | ---: | ---: | ---: | ---: |
| modern .NET RyuJIT | 1 | Typical | 68.21 +/- 1.58 ns | 59.39 +/- 0.70 ns | 400 B | 184 B |
| modern .NET RyuJIT | 256 | Typical | 9.99 +/- 0.81 us | 1.63 +/- 0.03 us | 44,936 B | 184 B |
| modern .NET RyuJIT | 1,024 | Typical | 42.16 +/- 4.49 us | 5.93 +/- 0.14 us | 192,424 B | 184 B |
| modern .NET RyuJIT | 1,024 | LongNameAndValue | 113.59 +/- 7.79 us | 7.74 +/- 0.45 us | 962,472 B | 680 B |
| modern .NET RyuJIT | 1,024 | Mixed | 42.22 +/- 0.67 us | 6.22 +/- 0.17 us | 192,424 B | 184 B |
| .NET Framework 4.8.1 RyuJIT | 1 | Typical | 294.0 +/- 26.8 ns | 240.3 +/- 8.7 ns | 417 B | 193 B |
| .NET Framework 4.8.1 RyuJIT | 256 | Typical | 52.61 +/- 1.96 us | 13.60 +/- 0.43 us | 49,170 B | 193 B |
| .NET Framework 4.8.1 RyuJIT | 1,024 | Typical | 211.61 +/- 2.65 us | 54.02 +/- 5.07 us | 209,279 B | 192 B |
| .NET Framework 4.8.1 RyuJIT | 1,024 | LongNameAndValue | 410.36 +/- 84.88 us | 59.23 +/- 0.73 us | 981,600 B | 690 B |
| .NET Framework 4.8.1 RyuJIT | 1,024 | Mixed | 212.76 +/- 3.99 us | 54.85 +/- 5.11 us | 209,279 B | 192 B |

At one entry, indexed lookup is competitive on modern .NET RyuJIT: faster for
the typical shape, within the measured error for mixed resources, and 9% slower
for the long-name shape while allocating 41-54% less. It is 18% faster on .NET
Framework 4.8.1 RyuJIT and allocates 41-54% less. At 256-1,024 entries, indexed
lookup is 6.1-14.7x faster on modern .NET RyuJIT and 3.9-6.9x faster on .NET Framework
4.8.1 RyuJIT while avoiding allocation growth with table size. The manager
therefore retains the indexed architecture.
File-backed sources retain memory mapping: a seekable `FileStream` allocated
5-10x more and became about 8x slower on modern .NET RyuJIT and 23x slower on
.NET Framework 4.8.1 RyuJIT at 256 entries.

### Repeated MSBuild-table lookup

Microsoft.Build 18.9.6 uses long-lived static primary and shared resource
managers. Its main table contains 587 strings and its shared table contains 64;
average values are 92 and 99 characters respectively. Retained default-job
benchmarks compare warmed BCL and Touki managers over those exact embedded
tables in 256-call batches:

| Runtime | Locality | Main Touki/BCL | Shared Touki/BCL | Touki allocation |
| --- | --- | ---: | ---: | ---: |
| modern .NET RyuJIT | Same key | 0.05 | 0.05 | 0 B |
| modern .NET RyuJIT | 75% adjacent hits | 0.48 | 0.43 | 15.2-16.5 KB |
| modern .NET RyuJIT | 50% adjacent hits | 0.89 | 0.81 | 31.0-31.3 KB |
| modern .NET RyuJIT | No adjacent hits | 1.98 | 1.77 | 60.0-62.5 KB |
| .NET Framework 4.8.1 RyuJIT | Same key | 0.08-0.09 | 0.09 | 0 B |
| .NET Framework 4.8.1 RyuJIT | 75% adjacent hits | 0.86 | 0.83 | 15.5-16.7 KB |
| .NET Framework 4.8.1 RyuJIT | 50% adjacent hits | 1.58 | 1.34 | 31.5-31.9 KB |
| .NET Framework 4.8.1 RyuJIT | No adjacent hits | 3.15 | 2.99 | 61.2-63.8 KB |

The one-entry cache therefore needs about 50% adjacent reuse to win CPU time on
modern .NET RyuJIT and between 50% and 75% on .NET Framework 4.8.1 RyuJIT.
Every cache miss still allocates a decoded string and cache entry. BCL warmed
lookup allocates 0 B because it retains decoded values by name, so Touki can add
material GC pressure even when its CPU ratio is below 1.0.

This is a single-threaded direct-table sensitivity test. It does not replay an
MSBuild build, primary-to-shared fallback, localization, or concurrent lookup.
Concurrent callers share the same last-entry cache and can reduce effective
locality. A production MSBuild assessment therefore still requires a key and
thread trace from representative builds.

#### Generated property cache

`MSBuildGeneratedResourceWrapperPerf` measures eight representative generated
properties from the 587-string Microsoft.Build main table. Its proposed wrapper
uses one nullable field per property on a nested cache instance and the generated
shape `s_cache.Foo ??= GetResourceString(nameof(Foo), Culture)`. The benchmark
models a 4,720-byte cache object, including its culture reference, with sampled
fields spread across the same byte range as 587 string references. Retained
default-job results are:

| Runtime | BCL wrapper | Current Touki wrapper | Generated cache |
| --- | ---: | ---: | ---: |
| modern .NET RyuJIT | 278.598 ns / 0 B | 474.895 ns / 2,744 B | 1.988 ns / 0 B |
| .NET Framework 4.8.1 RyuJIT | 455.207 ns / 0 B | 1,490.212 ns / 2,768 B | 8.944 ns / 0 B |

The generated cache is 51-140x faster than the BCL wrapper and 167-239x faster
than the uncached Touki wrapper for the eight-property batch. This comparison
intentionally excludes first access and rare culture-cache replacement.

The cache costs one reference per generated property plus its culture: about
4,720 B for the 587-property main table and 536 B for the 64-property shared
table on x64. Only accessed string values are retained. The generated getter is
21 IL bytes versus 12 for the pinned generator, and each generated class adds
one 38-byte shared helper. The 85 production getters and two helpers use 1,861
IL bytes versus 1,020 for the pinned getter bodies. Formatting helpers read the
cached property, while non-identity resource-name mappings retain their literal
key instead of using `nameof`.

The actual generated production property was also measured over Touki's embedded
table. Retained default-job results are:

| Runtime | BCL manager | Touki manager | Generated property |
| --- | ---: | ---: | ---: |
| modern .NET RyuJIT | 27.458 ns / 0 B | 1.559 ns / 0 B | 0.588 ns / 0 B |
| .NET Framework 4.8.1 RyuJIT | 47.792 ns / 0 B | 4.802 ns / 0 B | 2.109 ns / 0 B |

First-field population over a warm manager separates cache replacement from
lookup. A same-key read costs 4.853 ns / 48 B on modern .NET RyuJIT and
9.982 ns / 48 B on .NET Framework 4.8.1 RyuJIT. Forcing a different manager key
before every generated read raises the complete alternating operation to
84.662 ns / 232 B and 270.873 ns / 241 B respectively. Only one 32-byte
`CacheEntry` per property is redundant after the generated field is populated.
The manager cache remains enabled because a bypass would add API complexity and
weaken direct-manager locality for that one-time saving.

Exact deployment-size comparison against the pre-generator backup is:

| Artifact | Baseline | Generated | Delta |
| --- | ---: | ---: | ---: |
| net10.0 `touki.dll` | 625,664 B | 627,712 B | +2,048 B (+0.327%) |
| net11.0 `touki.dll` | 626,176 B | 627,712 B | +1,536 B (+0.245%) |
| net472 `touki.dll` | 908,288 B | 914,432 B | +6,144 B (+0.676%) |
| NuGet package | 1,538,989 B | 1,567,315 B | +28,326 B (+1.841%) |

A rooted ReadyToRun probe prints the expected value in both variants. Its
executable, app DLL, and `touki.dll` are byte-identical; auxiliary output grows
3,346 B (0.143%). A rooted Native AOT probe also executes successfully. The
Touki-generated executable is 1,329,152 B versus 2,173,440 B for the BCL wrapper
(-844,288 B, -38.846%), and total deployment is 9,363,633 B versus 13,303,199 B
(-3,939,566 B, -29.614%).

#### Frozen dictionary lookup

A retained default-job comparison materialized both Microsoft.Build tables into
an ordinal `Dictionary<string, string>` and `FrozenDictionary<string, string>`.
It performed identical 256-lookup batches using caller-owned, value-equal key
strings rather than reusing the dictionaries' key references:

| Runtime | Table | Entries | Hot ratio | Random ratio |
| --- | --- | ---: | ---: | ---: |
| modern .NET RyuJIT | Main | 587 | 0.75 | 0.90 |
| modern .NET RyuJIT | Shared | 64 | 0.75 | 0.34 |
| .NET Framework 4.8.1 RyuJIT | Main | 587 | 2.21 | 1.47 |
| .NET Framework 4.8.1 RyuJIT | Shared | 64 | 0.71 | 0.32 |

Every row allocated 0 B. Frozen lookup is data- and JIT-dependent: modern .NET
RyuJIT gains 10-66% for random access, while the 587-entry table regresses 47%
on .NET Framework 4.8.1 RyuJIT. The confidence-interval margins are at most
1.96%, well below those deltas.

Freezing requires a complete immutable table and cannot accept values as they
are decoded. This comparison also excludes construction cost. `FrozenDictionary`
is therefore not a replacement for a lazy runtime-style cache; a standard
dictionary or resource-indexed value array remains the cross-target choice for
incremental caching.

### Shared DJB2 hash

Both indexed readers now call the common
`string.GetDJB2HashCode(ReadOnlySpan<char>)` helper. It implements the
single-accumulator deterministic DJB2 variant stored in `.resources` indexes.
The existing .NET Framework-only `string.GetHashCode(ReadOnlySpan<char>)`
entrypoint remains unchanged because it implements the different
dual-accumulator .NET Framework string hash.

A retained default-job comparison used the duplicated scalar reader loop as
the baseline. Every row allocated 0 B:

| Runtime | Length | Previous loop | Shared helper |
| --- | ---: | ---: | ---: |
| modern .NET RyuJIT | 8 | 2.701 ns | 2.702 ns |
| modern .NET RyuJIT | 32 | 10.192 ns | 10.337 ns |
| modern .NET RyuJIT | 128 | 65.539 ns | 65.590 ns |
| modern .NET RyuJIT | 1,024 | 582.337 ns | 581.659 ns |
| .NET Framework 4.8.1 RyuJIT | 8 | 4.687 ns | 4.657 ns |
| .NET Framework 4.8.1 RyuJIT | 32 | 19.847 ns | 19.828 ns |
| .NET Framework 4.8.1 RyuJIT | 128 | 73.676 ns | 73.654 ns |
| .NET Framework 4.8.1 RyuJIT | 1,024 | 585.811 ns | 586.160 ns |

The shared helper is within 0.1% of the previous loop in every modern case
except the 32-character input, where it is 1.4% slower. Framework results range
from 0.6% faster to 0.1% slower. Final-source short-job screens of the complete
`RawResourceReader` and `StreamStringResourceReader` paths show no consistent
timing regression, and their allocation counts are unchanged.

### Neutral sources

| Runtime | Phase | BCL assembly | Touki assembly | BCL file | Touki file | Touki stream |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| modern .NET RyuJIT | Construction | 284.25 ns | 4.15 ns | 15.99 ns | 3.71 ns | 4.07 ns |
| modern .NET RyuJIT | First lookup | 777.8 ns | 178.1 ns | 71.59 us | 59.09 us | 123.6 ns |
| modern .NET RyuJIT | Warmed lookup | 30.72 ns | 1.55 ns | 27.17 ns | 1.54 ns | 1.54 ns |
| .NET Framework 4.8.1 RyuJIT | Construction | 616.59 ns | 5.16 ns | 28.66 ns | 4.54 ns | 5.14 ns |
| .NET Framework 4.8.1 RyuJIT | First lookup | 7.41 us | 0.53 us | 42.28 us | 24.31 us | 0.29 us |
| .NET Framework 4.8.1 RyuJIT | Warmed lookup | 47.62 ns | 5.05 ns | 45.53 ns | 5.02 ns | 4.88 ns |

Every warmed row allocates 0 B. Final-source first lookup allocated 576/840/488 B
for modern Touki assembly/file/stream sources and 602/1,252/1,067 B on
.NET Framework 4.8.1 RyuJIT, versus BCL's 2,320/6,320 B and 6,676/9,797 B.
A retained modern .NET RyuJIT default-job run measured 175.2 ns, 54.03 us, and
118.8 ns for the same three Touki first-lookups, with warmed values of
1.535-1.536 ns.

### Localized managed sources

Assembly metadata and successful/missing satellite binds are cached per resource
assembly. That changes repeated manager-local first lookup from a culture-chain
binding exercise into indexed table setup:

| Runtime | Scenario | BCL runtime | Touki runtime | Ratio | Touki allocation |
| --- | --- | ---: | ---: | ---: | ---: |
| modern .NET RyuJIT | Exact | 1.585 us | 0.286 us | 0.18 | 1.35 KB |
| modern .NET RyuJIT | Parent | 8.789 us | 0.311 us | 0.04 | 1.38 KB |
| modern .NET RyuJIT | Missing | 15.208 us | 0.323 us | 0.02 | 1.41 KB |
| .NET Framework 4.8.1 RyuJIT | Exact | 9.523 us | 0.762 us | 0.08 | 1.39 KB |
| .NET Framework 4.8.1 RyuJIT | Parent | 132.243 us | 0.839 us | 0.006 | 1.42 KB |
| .NET Framework 4.8.1 RyuJIT | Missing | 266.319 us | 0.851 us | 0.003 | 1.44 KB |

Final-source warmed lookup is 2.37-2.81 ns on modern .NET RyuJIT and
11.13-13.50 ns on .NET Framework 4.8.1 RyuJIT, versus BCL ranges of
26.82-27.81 ns and 46.30-46.80 ns respectively. Every warmed row allocates 0 B.
The retained modern .NET RyuJIT default-job runtime-satellite run measured
282.0/307.4/325.4 ns for exact/parent/missing first lookup and 2.35-2.69 ns
warmed.

External loose/direct modes still pay file mapping or PE metadata setup on their
first load. They are separate deployment modes rather than like-for-like runtime
satellite replacements, so their absolute first-load costs are:

| Runtime | Scenario | Loose resources | Direct satellite | Allocation (loose/direct) |
| --- | --- | ---: | ---: | ---: |
| modern .NET RyuJIT | Exact | 58.23 us | 62.76 us | 1.97/6.10 KB |
| modern .NET RyuJIT | Parent | 73.01 us | 78.74 us | 3.44/7.45 KB |
| modern .NET RyuJIT | Missing | 17.61 us | 17.04 us | 4.27/4.02 KB |
| .NET Framework 4.8.1 RyuJIT | Exact | 25.73 us | 30.61 us | 2.48/6.71 KB |
| .NET Framework 4.8.1 RyuJIT | Parent | 47.01 us | 53.25 us | 4.87/8.82 KB |
| .NET Framework 4.8.1 RyuJIT | Missing | 39.39 us | 38.40 us | 6.13/5.59 KB |

### Fresh-process exact lookup

Fifteen randomized fresh-process measurements from identical optimized source:

| Deployment | BCL runtime | Touki runtime | BCL process | Touki process |
| --- | ---: | ---: | ---: | ---: |
| IL | 0.83 ms | 6.13 ms | 47.11 ms | 51.71 ms |
| ReadyToRun | 0.79 ms | 2.99 ms | 46.34 ms | 48.51 ms |

The optimized loader reduced the earlier IL exact lookup from 7.29 ms to about
6.1 ms and removed the former 2.3-2.6 ms eager-loader JIT hotspot. A current
marked IL trace attributed 5.05 ms of its 5.56 ms lookup-window JIT to Touki;
remaining compilation is distributed across culture orchestration and indexed
reader methods. A current R2R trace showed only 0.52 ms JIT in a 2.88 ms lookup
window, leaving runtime type/fixup and binding initialization as the dominant
fresh-process remainder.

## Historical profile scores

These scores describe the previous eager implementation and cannot establish
current performance success. They are retained only to explain the investigation
history; current acceptance is per phase.

### Neutral sources

| Runtime | Profile | Source | Construction | First load | Warmed | Score |
| --- | --- | --- | ---: | ---: | ---: | ---: |
| modern .NET RyuJIT | Embedded | Assembly | 0.013 | 0.336 | 0.227 | **0.186** |
| .NET Framework 4.8.1 RyuJIT | Embedded | Assembly | 0.007 | 0.108 | 0.250 | **0.173** |
| modern .NET RyuJIT | Raw file | `.resources` file | 0.209 | 0.793 | 0.213 | **0.237** |
| .NET Framework 4.8.1 RyuJIT | Raw file | `.resources` file | 0.132 | 0.421 | 0.263 | **0.259** |

The stream-factory source has no like-for-like `ResourceManager` mode, so its
rows are reported but excluded from the normalized score.

### Localized sources

| Runtime | Source | Construction | First load | Warmed | Score |
| --- | --- | ---: | ---: | ---: | ---: |
| modern .NET RyuJIT | Runtime satellites | 0.067 | 2.112 | 0.235 | **0.254** |
| modern .NET RyuJIT | Resources directory | 0.484 | 8.558 | 0.241 | **0.344** |
| modern .NET RyuJIT | Satellite directory | 0.483 | 9.142 | 0.235 | **0.339** |
| .NET Framework 4.8.1 RyuJIT | Runtime satellites | 0.040 | 0.638 | 0.433 | **0.367** |
| .NET Framework 4.8.1 RyuJIT | Resources directory | 0.277 | 0.507 | 0.433 | **0.423** |
| .NET Framework 4.8.1 RyuJIT | Satellite directory | 0.280 | 0.562 | 0.433 | **0.427** |

Runtime satellites have the best normalized score on modern .NET RyuJIT and
.NET Framework 4.8.1 RyuJIT. Direct
satellite parsing has a slightly lower warmed ratio but a higher first-load
ratio than loose resources on modern .NET RyuJIT. Loose resources have a lower
first-load ratio on .NET Framework 4.8.1 RyuJIT, while warmed results are
effectively the same. The aggregate direct/loose score differences are too small
to rank without repeat-run noise measurements; the runtime-satellite advantage
is the actionable comparison.

## Historical retained rows

The following rows predate the indexed no-copy loader and assembly metadata/bind
caches. Keep them only as the baseline that motivated the current implementation.

### Construction

| Runtime | Implementation and source | Mean | Error | Allocated |
| --- | --- | ---: | ---: | ---: |
| modern .NET RyuJIT | BCL assembly | 280.757 ns | 0.8446 ns | 520 B |
| modern .NET RyuJIT | Touki assembly | 3.771 ns | 0.0226 ns | 56 B |
| modern .NET RyuJIT | BCL file | 16.077 ns | 0.1510 ns | 256 B |
| modern .NET RyuJIT | Touki file | 3.354 ns | 0.0362 ns | 56 B |
| .NET Framework 4.8.1 RyuJIT | BCL assembly | 613.820 ns | 2.4217 ns | 489 B |
| .NET Framework 4.8.1 RyuJIT | Touki assembly | 4.321 ns | 0.0417 ns | 56 B |
| .NET Framework 4.8.1 RyuJIT | BCL file | 28.335 ns | 0.2263 ns | 489 B |
| .NET Framework 4.8.1 RyuJIT | Touki file | 3.743 ns | 0.0419 ns | 56 B |

File-backed `BaseName` extraction is lazy so construction records only the path.

Localized manager construction also beats BCL. Runtime-satellite construction
costs 18.56 ns / 344 B on modern .NET RyuJIT and 24.70 ns / 353 B on
.NET Framework 4.8.1 RyuJIT. Directory factory construction costs about 135 ns
on modern .NET RyuJIT and 170-172 ns on .NET Framework 4.8.1 RyuJIT because it
captures a fully qualified root.

### Warmed lookup

| Runtime | BCL | Touki assembly/file/stream | Localized Touki range |
| --- | ---: | ---: | ---: |
| modern .NET RyuJIT | 26.760-26.957 ns | 5.666-6.087 ns | 6.061-7.607 ns |
| .NET Framework 4.8.1 RyuJIT | 45.18-47.37 ns | 11.69-11.90 ns | 19.10-22.17 ns |

Every warmed row allocates 0 B. Localized values cover exact, parent, and
missing-culture lookup. Touki is 3.6-4.6x faster on modern .NET RyuJIT and
2.1-2.5x faster on .NET Framework 4.8.1 RyuJIT for those localized cases.

### File lifecycle

The complete lifecycle includes construction, first lookup, and release for both
implementations:

| Runtime | BCL | Touki | BCL allocation | Touki allocation |
| --- | ---: | ---: | ---: | ---: |
| modern .NET RyuJIT | 71.32 us | 60.55 us | 6,440 B | 952 B |
| .NET Framework 4.8.1 RyuJIT | 32.21 us | 17.00 us | 9.98 KB | 5.50 KB |

This replaces the earlier asymmetric benchmark that charged release only to the
BCL file manager.

### Fresh-process first lookup

#### Modern .NET RyuJIT deployment comparison

The table reports the median lookup measured inside each fresh process. Ordinary
IL and ReadyToRun use identical harness source and fixtures; PE inspection
confirmed that the IL harness and `touki.dll` have no managed-native header,
while both ReadyToRun assemblies do.

| Deployment | Source | Exact | Parent | Missing |
| --- | --- | ---: | ---: | ---: |
| IL | BCL runtime | 0.856 ms | 1.223 ms | 1.143 ms |
| IL | Touki runtime satellites | 7.285 ms | 8.050 ms | 8.393 ms |
| IL | Touki loose resources | 8.004 ms | 8.674 ms | 8.764 ms |
| IL | Touki direct satellite | 10.914 ms | 11.394 ms | 8.810 ms |
| ReadyToRun | BCL runtime | 0.810 ms | 1.178 ms | 1.078 ms |
| ReadyToRun | Touki runtime satellites | 2.660 ms | 3.394 ms | 3.434 ms |
| ReadyToRun | Touki loose resources | 3.312 ms | 4.288 ms | 3.823 ms |
| ReadyToRun | Touki direct satellite | 5.950 ms | 6.556 ms | 3.745 ms |

ReadyToRun changes BCL by only 4-6%, but reduces Touki first lookup by:

- 58-64% for runtime satellites;
- 51-59% for loose resources; and
- 42-57% for direct satellite parsing.

The corresponding whole-process medians move by only 8-10%, from roughly
46-55 ms to 45-50 ms, because runtime startup dominates a process that performs
one lookup and immediately exits. The R2R `touki.dll` grows from 613,376 bytes to
1,134,592 bytes (+85%); the small harness grows from 9,728 bytes to 28,672 bytes.

Composite ReadyToRun did not help. Across the same cases it was 1.6-6.5% slower
than ordinary ReadyToRun, so it is not recommended for this workload.

#### Exact-culture JIT attribution

Process-local EventPipe traces used explicit lookup start/stop events and verbose
JIT completion events. The following is one diagnostic run per exact-culture
case; the 15-process medians above remain the latency evidence.

| Deployment and source | Lookup window | JIT in window | JIT share | Touki JIT |
| --- | ---: | ---: | ---: | ---: |
| IL, BCL runtime | 0.29 ms | 0.09 ms | 31% | 0 ms |
| IL, Touki runtime | 7.45 ms | 6.78 ms | 91% | 6.37 ms |
| IL, loose resources | 8.20 ms | 7.15 ms | 87% | 6.67 ms |
| IL, direct satellite | 12.31 ms | 9.12 ms | 74% | 7.97 ms |
| ReadyToRun, BCL runtime | 0.22 ms | 0 ms | 0% | 0 ms |
| ReadyToRun, Touki runtime | 2.73 ms | 0.42 ms | 15% | 0.38 ms |
| ReadyToRun, loose resources | 4.81 ms | 0.50 ms | 10% | 0.39 ms |
| ReadyToRun, direct satellite | 6.01 ms | 1.34 ms | 22% | 0.39 ms |

The dominant IL compile was
`StringResourceTableLoader.LoadTableFromResourcesFile`: 2.31-2.56 ms by itself.
Direct DLL mode additionally compiled
`LoadTableFromAssembly(ReadOnlyMemory<byte>, string)` for 1.19-1.28 ms. The
culture-cache path was about 0.6 ms. Whole-process JIT totals were 7.07 ms for
the BCL case, 14.30-14.50 ms for Touki runtime/loose modes, and 15.88 ms for
direct DLL parsing. ReadyToRun lowered those totals to 6.46-8.14 ms and removed
all non-generic `Touki.Resources` JIT; its residual lookup JIT is shared generic
span/parser code.

After JIT and process-wide binding are warm, the default-job exact first loads
are only 1.70 us for runtime satellites, 70.0 us for loose resources, and
80.5 us for direct DLL parsing. This confirms that the multi-millisecond IL
result is compilation, not the steady parser algorithm.

#### ETW call-tree attribution

Elevated ETW traces used exact harness/Touki PDBs. Aggregate native method-name
resolution was lower because runtime frames were native, but Touki source mapping
was 87-100%. The individual cold operations are too short for line-level sampling;
the evented thread-time trees and exact JIT events provide the useful split.

- **Modern .NET RyuJIT IL runtime satellites (9 ms trace):** 8 ms under `GetString`,
  7 ms under culture-cache initialization, 6 ms under source loading, 5 ms in
  assembly/resource loading, and 2 ms in the binary resource parser.
- **Modern .NET RyuJIT IL loose resources (8.94 ms):** 6 ms under source loading,
  2 ms in the resource parser, and about 1 ms opening the mapping.
- **Modern .NET RyuJIT IL direct satellite (12.99 ms):** 9.96 ms under source loading,
  6.96 ms in assembly-image parsing, 4 ms loading the embedded resource,
  2 ms parsing `.resources`, and about 1 ms opening the mapping.
- **.NET Framework 4.8.1 RyuJIT runtime satellites (22.97 ms):** 21 ms under `GetString`,
  18 ms under source loading, 16 ms in assembly/resource loading, and 13 ms in
  the binary resource parser.
- **.NET Framework 4.8.1 RyuJIT loose resources (29.97 ms):** 20 ms under source loading,
  13 ms in the parser, and 6.05 ms constructing the directory manager, including
  4.09 ms in first-use path normalization.
- **.NET Framework 4.8.1 RyuJIT direct satellite (57.00 ms):** 48 ms under source loading,
  41 ms in assembly-image parsing, about 15 ms each in metadata-reader setup and
  embedded-resource parsing, 13 ms in the `.resources` parser, and 7 ms in PE
  header initialization.

These trees are still JIT-inclusive: the native leaf frames are overwhelmingly
CLR/JIT compiler routines. Physical disk service attributed to the resource file
or satellite DLL was 0 ms in every trace, and blocked time inside the IL loose
lookup was only 0.01 ms (none appeared in direct parsing). The measured files
were page-cache hits; disk I/O does not explain the gap.

#### .NET Framework 4.8.1 RyuJIT NGen comparison

The .NET Framework 4.8.1 RyuJIT harness and Touki assembly had no installed NGen image initially.
Installing native images only for those two assemblies reduced exact first lookup
from 21.37 to 1.80 ms for runtime satellites, 27.02 to 8.46 ms for loose
resources, and 53.73 to 33.16 ms for direct DLL parsing. ETW identified the
remaining loose/direct cost in `Microsoft.IO.Redist`,
`System.Reflection.Metadata`, immutable collections, and span support.

Temporarily NGen-compiling that dependency set reduced exact first lookup further:

| Source | .NET Framework 4.8.1 RyuJIT IL | App + Touki NGen | Full dependency NGen |
| --- | ---: | ---: | ---: |
| BCL runtime | 0.72 ms | 0.70 ms | 0.69 ms |
| Touki runtime satellites | 21.37 ms | 1.80 ms | 1.85 ms |
| Touki loose resources | 27.02 ms | 8.46 ms | 4.03 ms |
| Touki direct satellite | 53.73 ms | 33.16 ms | 6.73 ms |

All temporary NGen roots were removed after measurement. NGen is an
installer/machine-servicing option, not something a library NuGet package can
reliably provide.

#### Native AOT comparison

Native AOT cannot bind external runtime satellites, but both external Touki modes
work. Fifteen-process medians were:

| Source | Exact | Parent | Missing | Whole process |
| --- | ---: | ---: | ---: | ---: |
| Loose resources | 0.115 ms | 0.167 ms | 0.120 ms | 16.7-17.0 ms |
| Direct satellite | 0.134 ms | 0.180 ms | 0.119 ms | 16.4-16.9 ms |

Native AOT therefore removes 98% or more of the IL first-lookup cost for the
supported external modes. The native BCL/runtime-satellite path returned neutral
resources, as expected; external runtime binding remains unsupported.

### Cold-start mitigations

| Mitigation | Measured effect | Tradeoff / recommendation |
| --- | --- | --- |
| Publish with ordinary ReadyToRun | Cuts modern Touki first lookup by 42-64%; removes non-generic Touki JIT | Recommended for managed applications where startup matters. Costs about +85% for `touki.dll`; total one-shot process time improves only 8-10%. |
| Composite ReadyToRun | 1.6-6.5% slower than ordinary R2R | Do not use for this workload. |
| Native AOT | External lookup falls to 0.115-0.180 ms; process median about 16.4-17.0 ms | Best startup result. Runtime satellite binding cannot provide external localization; use loose resources or direct DLL parsing. |
| NGen on .NET Framework 4.8.1 RyuJIT | Runtime 21.37->1.85 ms; loose 27.02->4.03 ms; direct 53.73->6.73 ms with dependencies | Effective only when an application installer owns native-image servicing. Not a general NuGet-library solution. |
| Prewarm one lookup | Removes JIT, type initialization, and binding from the first user-visible request | Deployment workaround only; it does not satisfy the loader performance goal. |
| Direct indexed lookup | Removes table-size-dependent loading. | Keep; typical/mixed allocate 184-193 B and long values allocate 680-690 B. |
| Reduce parser JIT surface | The former eager `.resources` loader cost about 2.4 ms to compile; direct assembly parsing added about 1.2 ms. The indexed loader removed that hotspot, but first-use compilation remains distributed across culture orchestration and reader methods. | Continue to report IL and ReadyToRun startup separately from warmed throughput. |
| Prefer runtime satellites for managed defaults | Avoids PE metadata parsing and has the lowest Touki cold cost | Keep runtime satellites as the normal managed default. Reserve direct DLL parsing for conventional external deployment/AOT; loose resources are the lower-startup external option on .NET Framework 4.8.1 RyuJIT. |
| Defer path canonicalization | Would avoid part of the .NET Framework 4.8.1 RyuJIT first-use path cost | Not recommended: canonicalization provides stable, traversal-resistant directory semantics. Prewarm or native-compile it instead. |

## Source selection

The batch-2 generated managed default is runtime satellites. That mode has the
lowest managed first-load cost and preserves runtime assembly identity checks.
The other source modes remain explicit deployment choices:

1. **Runtime satellites** - preferred when normal runtime binding does not impose
  a significant measured penalty. This preserves runtime assembly
  identity/version/token checks. It does not support external satellite loading
  in Native AOT.
2. **Resources directory** - simplest external deployment and lower first-load
  cost than direct DLL parsing, especially on .NET Framework 4.8.1 RyuJIT. It
  requires shipping loose `.resources` files.
3. **Satellite directory** - preserves conventional satellite deployment and
   works for side-loaded Native AOT because DLLs are parsed as data. It has the
   highest exact/parent cold-start cost.

The public API retains all three. Round 2 will choose the automatic Native AOT
external layout separately; direct satellites preserve conventional deployment,
while loose resources have the lower .NET Framework 4.8.1 RyuJIT startup cost.

## Trust and ownership contract

- Resource files, manifest streams, and satellite DLLs are trusted outputs from
  the application's build and deployment pipeline. Arbitrary untrusted payloads
  are outside the API contract.
- Structural bounds checks remain required for memory safety. They throw on bad
  compiled data and never enable fallback.
- Only a genuinely absent localized candidate continues parent or neutral
  fallback. Present unreadable, malformed, unsupported, or incorrectly bundled
  resources throw.
- Null resources are invalid under every option. Default options reject any
  table containing a non-string resource. `IgnoreNonStringResources` discards
  non-string names and values, so those names behave as missing.
- Directory roots are converted to fully qualified paths during construction.
  Resource base names, culture names, and satellite assembly names must be
  single path segments to preserve deterministic probe layout.
- Every requested culture and missing candidate remains cached until
  `ReleaseAllResources()` so a source opens at most once per generation.
- Runtime assembly metadata and successful or missing satellite bind results are
  cached per resource assembly for the process lifetime.
- The loader must not make a complete managed copy of a file or stream. A direct
  backing may be retained until release and is then disposed according to source
  ownership.

## Validation

- Complete Release and Debug solution test runs: 0 failures.
- Complete Release matrix: 24,637 passed, 260 platform skips, 0 failed.
- Complete Debug matrix: 24,605 passed, 260 platform skips, 0 failed.
- Release `touki.tests`:
  - net10.0: 7,337 passed, 119 platform skips;
  - net11.0: 7,337 passed, 119 platform skips; and
  - net481: 8,668 passed, 22 platform skips.
- `touki.resources.generator.tests`: 53/53 passed.
- `touki.aot.tests`: 14/14 passed in managed mode.
- `touki.analyzers.tests`: 1,228/1,228 passed.
- Native AOT `win-x64` publish succeeded with no IL2xxx/IL3xxx diagnostics.
- The native executable returned success for embedded neutral `Hello`, then for
  side-loaded direct-satellite `Hallo` and loose-resource `Hallo` after each
  German artifact was copied into its deployment layout.

The Native AOT publish currently emits ILC messages for test methods rooted from
the AOT test assembly because MSTest.TestFramework is unavailable to those
rooted methods. The production resource-manager path and all three executable
smoke cases succeed; removing the test-harness rooting message is separate
validation infrastructure work.