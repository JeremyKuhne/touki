# BinaryFormattedObject performance baseline

This document records the July 11, 2026 baseline for trusted NRBF deserialization through
`BinaryFormatter`, Touki's `BinaryFormattedObject`, and the exact source at
[`JeremyKuhne/binaryformat`](https://github.com/JeremyKuhne/binaryformat) commit
`aaa1dd1bf7ee8ce626b82c3c55343dfee4a71743`.

The benchmark uses only payloads generated during `GlobalSetup`. It is not evidence that
`BinaryFormatter` or NRBF deserialization is safe for untrusted data.

The agent-workflow and filtrace lessons from this investigation are captured in
[performance-investigation-agent-tooling-retrospective.md](performance-investigation-agent-tooling-retrospective.md).

## Environment

| Item | Value |
| --- | --- |
| Date | 2026-07-11 |
| OS | Windows 11 25H2, build 10.0.26200.8655 |
| CPU | Intel Core i9-14900K, 24 physical / 32 logical cores |
| Memory | 127.72 GiB |
| BenchmarkDotNet | 0.16.0-preview.1 |
| SDK | 11.0.100-preview.5.26302.115 |
| .NET 10 | 10.0.9, x64 RyuJIT x86-64-v3, concurrent workstation GC |
| .NET 11 | 11.0.0-preview.5.26302.115, x64 RyuJIT x86-64-v3, concurrent workstation GC |
| .NET Framework | 4.8.1 (4.8.9325.0), x64 RyuJIT, concurrent workstation GC |
| BinaryFormatter package | System.Runtime.Serialization.Formatters 10.0.9 |
| Upstream BinaryFormat | Clean Release build of `aaa1dd1bf7ee8ce626b82c3c55343dfee4a71743` |

The modern end-to-end rows use BenchmarkDotNet's adaptive `DefaultJob`. Paired parser
and net10 materialization rows use fixed `MediumRun` jobs. All builds are Release builds.
The upstream project targets net8.0 but runs in the selected net10.0 or net11.0 benchmark
host. It cannot be loaded by the net481 host.

## Scenarios

Every implementation consumes identical serialized bytes from its own reusable stream.
Setup validates the entire result graph and verifies that repeated end-to-end calls do not
reuse mutable result state.

| Scenario | Payload |
| --- | --- |
| `Int32Array_1K` | An `int[1024]` containing deterministic non-default values |
| `StringList_128` | A `List<string>` containing 128 distinct strings |
| `CustomObject` | A custom object with an integer, string, UTC `DateTime`, and `int[64]` |
| `ObjectTree_127` | A depth-7 binary tree containing 127 custom nodes and strings |
| `SharedCycle_128` | A 128-node graph with a ring, shared references, and a node array |
| `SerializableCallback` | An `ISerializable` object with `int[256]`, a serialization constructor, and callback |

## Reproduction

Run from the repository root in PowerShell:

```powershell
# BinaryFormatter versus Touki on .NET Framework.
dotnet run -c Release -f net481 --project touki.perf -- `
  --filter '*BinaryFormattedObjectPerf*' --allCategories EndToEnd

# Exact-source three-way end-to-end comparisons.
./tools/Run-BinaryFormatComparison.ps1 `
  -TargetFramework net10.0 -Category EndToEnd
./tools/Run-BinaryFormatComparison.ps1 `
  -TargetFramework net11.0 -Category EndToEnd

# Fixed-depth paired parser comparisons.
./tools/Run-BinaryFormatComparison.ps1 `
  -TargetFramework net10.0 -Category ParseOnly -Job Medium
./tools/Run-BinaryFormatComparison.ps1 `
  -TargetFramework net11.0 -Category ParseOnly -Job Medium

# Touki's net10 record-model-to-object materialization phase.
dotnet run -c Release -f net10.0 --project touki.perf -- `
  --filter '*BinaryFormattedObjectPerf*' --allCategories MaterializeOnly --job medium
```

The comparison script creates a clean detached checkout, verifies its commit and status,
builds it in Release with Touki's pinned SDK, verifies the produced assembly metadata,
and removes the temporary checkout afterward.

## End-to-end latency

Ratios use `BinaryFormatter` as `1.00`. Lower is better.

### .NET 10 x64 RyuJIT

| Scenario | BinaryFormatter (us/op) | Touki (us/op) | Touki ratio | Upstream `aaa1dd1` (us/op) | Upstream ratio |
| --- | ---: | ---: | ---: | ---: | ---: |
| `CustomObject` | 1.006 | 1.256 | 1.25 | 1.740 | 1.73 |
| `Int32Array_1K` | 0.411 | 0.242 | 0.59 | 0.262 | 0.64 |
| `ObjectTree_127` | 32.416 | 50.700 | 1.56 | 34.240 | 1.06 |
| `SerializableCallback` | 1.284 | 1.606 | 1.25 | 2.113 | 1.65 |
| `SharedCycle_128` | 32.546 | 50.963 | 1.57 | 28.953 | 0.89 |
| `StringList_128` | 6.232 | 7.245 | 1.16 | 7.151 | 1.15 |

### .NET 11 x64 RyuJIT

| Scenario | BinaryFormatter (us/op) | Touki (us/op) | Touki ratio | Upstream `aaa1dd1` (us/op) | Upstream ratio |
| --- | ---: | ---: | ---: | ---: | ---: |
| `CustomObject` | 1.002 | 1.212 | 1.21 | 1.712 | 1.71 |
| `Int32Array_1K` | 0.890 | 0.484 | 0.56 | 0.533 | 0.62 |
| `ObjectTree_127` | 52.448 | 88.086 | 1.68 | 61.226 | 1.17 |
| `SerializableCallback` | 3.403 | 3.951 | 1.16 | 5.626 | 1.65 |
| `SharedCycle_128` | 55.835 | 90.660 | 1.62 | 53.991 | 0.97 |
| `StringList_128` | 11.086 | 12.181 | 1.10 | 13.874 | 1.25 |

### .NET Framework 4.8.1 x64 RyuJIT

| Scenario | BinaryFormatter (us/op) | Touki (us/op) | Touki ratio |
| --- | ---: | ---: | ---: |
| `CustomObject` | 1.943 | 3.317 | 1.71 |
| `Int32Array_1K` | 0.620 | 0.527 | 0.85 |
| `ObjectTree_127` | 82.066 | 158.360 | 1.93 |
| `SerializableCallback` | 2.227 | 4.703 | 2.11 |
| `SharedCycle_128` | 85.126 | 167.992 | 1.97 |
| `StringList_128` | 12.100 | 18.886 | 1.56 |

## End-to-end allocation

The net10 and net11 allocation counts were identical. Lower is better.

### Modern .NET

| Scenario | BinaryFormatter (KiB/op) | Touki (KiB/op) | Touki ratio | Upstream `aaa1dd1` (KiB/op) | Upstream ratio |
| --- | ---: | ---: | ---: | ---: | ---: |
| `CustomObject` | 9.20 | 4.07 | 0.44 | 4.17 | 0.45 |
| `Int32Array_1K` | 9.95 | 4.96 | 0.50 | 5.36 | 0.54 |
| `ObjectTree_127` | 73.81 | 93.75 | 1.27 | 103.20 | 1.40 |
| `SerializableCallback` | 10.11 | 5.74 | 0.57 | 5.68 | 0.56 |
| `SharedCycle_128` | 79.31 | 97.05 | 1.22 | 100.05 | 1.26 |
| `StringList_128` | 31.36 | 29.44 | 0.94 | 28.72 | 0.92 |

### .NET Framework 4.8.1

| Scenario | BinaryFormatter (B/op) | Touki (B/op) | Touki ratio |
| --- | ---: | ---: | ---: |
| `CustomObject` | 10,335 | 5,103 | 0.49 |
| `Int32Array_1K` | 10,452 | 5,235 | 0.50 |
| `ObjectTree_127` | 76,978 | 110,390 | 1.43 |
| `SerializableCallback` | 10,961 | 6,764 | 0.62 |
| `SharedCycle_128` | 82,538 | 113,629 | 1.38 |
| `StringList_128` | 33,314 | 31,549 | 0.95 |

## Paired parser baseline

These `MediumRun` rows measure construction of the parsed NRBF record model only. The
ratio is upstream divided by Touki, so values below `1.00` favor upstream.

### .NET 10 x64 RyuJIT

| Scenario | Touki (us/op) | Upstream (us/op) | Ratio | Touki (KiB/op) | Upstream (KiB/op) |
| --- | ---: | ---: | ---: | ---: | ---: |
| `CustomObject` | 0.882 | 0.372 | 0.42 | 3.20 | 1.95 |
| `Int32Array_1K` | 0.208 | 0.194 | 0.93 | 4.48 | 4.48 |
| `ObjectTree_127` | 18.537 | 11.712 | 0.63 | 58.27 | 53.58 |
| `SerializableCallback` | 0.779 | 0.331 | 0.43 | 3.55 | 2.46 |
| `SharedCycle_128` | 13.866 | 8.404 | 0.61 | 50.20 | 39.92 |
| `StringList_128` | 7.087 | 4.692 | 0.66 | 27.55 | 25.00 |

### .NET 11 x64 RyuJIT

| Scenario | Touki (us/op) | Upstream (us/op) | Ratio | Touki (KiB/op) | Upstream (KiB/op) |
| --- | ---: | ---: | ---: | ---: | ---: |
| `CustomObject` | 0.974 | 0.360 | 0.38 | 3.20 | 1.95 |
| `Int32Array_1K` | 0.244 | 0.243 | 1.00 | 4.48 | 4.48 |
| `ObjectTree_127` | 16.203 | 11.409 | 0.70 | 58.27 | 53.58 |
| `SerializableCallback` | 0.733 | 0.341 | 0.46 | 3.55 | 2.46 |
| `SharedCycle_128` | 13.783 | 7.973 | 0.58 | 50.20 | 39.92 |
| `StringList_128` | 6.250 | 4.513 | 0.73 | 27.55 | 25.00 |

## .NET 10 Touki phase split

`BinaryFormattedObject` construction creates the NRBF record model. `Deserialize()` then
materializes that model into the result graph. The direct materialization benchmark parses
a bounded batch of 64 independent record models in `IterationSetup`, deserializes every
model exactly once in the measured invocation, reports per-model results through
`OperationsPerInvoke`, and releases the batch in `IterationCleanup`.

The batch is deliberately bounded. Reusing one parsed model would return or retain mutable
state for some record shapes and would not represent fresh deserialization. Larger batches
also retain enough decoded graph state to distort graph-heavy timings. A 64-model batch
amortizes BenchmarkDotNet's invocation timer while keeping that retained state to a few MiB.
BenchmarkDotNet still reports its expected minimum-iteration-time warning for the cheap
scenarios because iteration setup forces one measured invocation; the fixed 15-iteration
`MediumRun` provides the quoted distribution.

### Latency

Materialization share is calculated from the sum of the independently measured phase means.
The phase sum is within 6.7% of the independently measured end-to-end mean in every scenario.

| Scenario | Decode (us/op) | Materialize (us/op) | Materialize share | Phase sum (us/op) | End-to-end (us/op) |
| --- | ---: | ---: | ---: | ---: | ---: |
| `CustomObject` | 0.882 | 0.366 | 29% | 1.248 | 1.256 |
| `Int32Array_1K` | 0.208 | 0.050 | 19% | 0.258 | 0.242 |
| `ObjectTree_127` | 18.537 | 32.847 | 64% | 51.383 | 50.700 |
| `SerializableCallback` | 0.779 | 0.836 | 52% | 1.615 | 1.606 |
| `SharedCycle_128` | 13.866 | 37.188 | 73% | 51.053 | 50.963 |
| `StringList_128` | 7.087 | 0.615 | 8% | 7.702 | 7.245 |

### Allocation

Phase allocation sums match the independently measured end-to-end allocation at the
reported precision.

| Scenario | Decode (KiB/op) | Materialize (KiB/op) | Phase sum (KiB/op) | End-to-end (KiB/op) |
| --- | ---: | ---: | ---: | ---: |
| `CustomObject` | 3.20 | 0.88 | 4.08 | 4.07 |
| `Int32Array_1K` | 4.48 | 0.48 | 4.96 | 4.96 |
| `ObjectTree_127` | 58.27 | 35.48 | 93.75 | 93.75 |
| `SerializableCallback` | 3.55 | 2.20 | 5.75 | 5.74 |
| `SharedCycle_128` | 50.20 | 46.85 | 97.05 | 97.05 |
| `StringList_128` | 27.55 | 1.88 | 29.43 | 29.44 |

## Baseline conclusions

- Touki is faster than `BinaryFormatter` for the primitive array on all measured runtimes
  and allocates about half as much memory.
- Touki allocates substantially less than `BinaryFormatter` for custom and callback
  payloads, but is currently slower.
- Object trees and cyclic graphs are the largest Touki deficits. On net10, materialization
  accounts for 64% and 73% of their measured phase sums, respectively; both phases also
  allocate more than the corresponding `BinaryFormatter` path.
- String-list cost is parser-dominated: record creation accounts for about 92% of the phase
  sum. Materializer changes should not be expected to move that scenario substantially.
- Exact upstream parsing is faster for every non-primitive scenario and allocates less for
  five of six scenarios. Touki nevertheless wins end-to-end for custom and callback objects,
  showing that parser and materializer work must be profiled separately.
- Optimization claims should preserve fresh-object semantics, report both time and
  allocation, and avoid static caches whose size grows with process lifetime. Callback
  metadata is cached on `RegisteredTypeResolver`, where growth is bounded by the resolver's
  finite registration set and lifetime; custom resolvers use per-deserialization metadata.

## First deserializer optimization pass

The first pass was driven by symbol-complete net10 EventPipe traces of the one-shot
materialization benchmark. Before the changes, collection growth accounted for about 85%
of sampled tree self-time and 45% of sampled cycle self-time. Cycle deserialization also
spent 23% in the mandatory `SerializationRecord.Matches` validation performed by
`ArrayRecord.GetArray(Type)`.

Three changes were retained:

- The deserialized-object dictionary keeps its zero-capacity initial state. After the
  eighth reachable record, it reserves a capacity hint derived from the record count.
- The parser stack is local to object-graph deserialization, starts with eight entries, and
  reserves a larger hint only after traversal reaches that depth. Direct primitive arrays
  no longer allocate an unused stack object.
- Completion callbacks use the actual instance type that was already resolved, validated,
  and instantiated, instead of resolving the serialized type name a second time.

Both capacity hints are capped at 256 entries. Larger graphs grow incrementally based on
records actually reached, so a payload with many unreachable records cannot force an
unbounded speculative reservation. Graph state remains scoped to one `Deserialize()` call;
callback metadata remains bounded by the resolver's registration set rather than a
process-lifetime type cache.

The resolver-scoped callback cache was remeasured after review. The `SerializableCallback`
end-to-end row was 1.547 us and 5.78 KiB per operation on modern .NET RyuJIT (baseline
1.581 us and 5.77 KiB), and 4.654 us and 6.65 KiB on .NET Framework 4.8.1 RyuJIT
(baseline 4.675 us and 6.64 KiB). Both are within the measured run-to-run envelope.

The fixed-depth comparison below uses the same 64-record one-shot `MediumRun` as the
baseline. Time deltas are arithmetic differences between means from separate runs, not
paired estimates. The primitive-array intervals overlap and that row is unchanged within
measurement uncertainty; the table retains its raw mean delta for completeness.

| Scenario | Baseline (ns/op) | Optimized (ns/op) | Time delta | Baseline (B/op) | Optimized (B/op) | Allocation delta |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| `CustomObject` | 366.15 | 324.95 | -11.3% | 896 | 920 | +24 B |
| `Int32Array_1K` | 49.57 | 48.92 | -1.3% | 496 | 456 | -40 B |
| `ObjectTree_127` | 32,846.76 | 27,524.88 | -16.2% | 36,328 | 34,792 | -1,536 B |
| `SerializableCallback` | 835.72 | 796.26 | -4.7% | 2,248 | 2,272 | +24 B |
| `SharedCycle_128` | 37,187.56 | 31,411.94 | -15.5% | 47,972 | 38,884 | -9,088 B |
| `StringList_128` | 614.95 | 548.55 | -10.8% | 1,928 | 1,952 | +24 B |

### Post-optimization end-to-end confirmation

These adaptive `DefaultJob` runs were captured after the optimization pass. The modern
tables include a freshly cloned, clean Release build of exact upstream commit `aaa1dd1` in
the same benchmark process. Times are microseconds per operation; allocations are managed
KiB per operation.

#### .NET 10 x64 RyuJIT

| Scenario | BinaryFormatter (us) | Touki (us) | Upstream (us) | Touki (KiB) | Upstream (KiB) |
| --- | ---: | ---: | ---: | ---: | ---: |
| `CustomObject` | 1.059 | 1.263 | 1.740 | 4.09 | 4.17 |
| `Int32Array_1K` | 0.436 | 0.244 | 0.266 | 4.92 | 5.36 |
| `ObjectTree_127` | 32.817 | 47.879 | 35.495 | 92.25 | 103.20 |
| `SerializableCallback` | 1.370 | 1.581 | 2.190 | 5.77 | 5.68 |
| `SharedCycle_128` | 33.123 | 46.104 | 31.260 | 88.17 | 100.05 |
| `StringList_128` | 6.680 | 7.652 | 7.572 | 29.46 | 28.72 |

#### .NET 11 x64 RyuJIT

| Scenario | BinaryFormatter (us) | Touki (us) | Upstream (us) | Touki (KiB) | Upstream (KiB) |
| --- | ---: | ---: | ---: | ---: | ---: |
| `CustomObject` | 1.028 | 1.173 | 1.753 | 4.09 | 4.17 |
| `Int32Array_1K` | 0.441 | 0.242 | 0.281 | 4.92 | 5.36 |
| `ObjectTree_127` | 32.080 | 43.944 | 32.937 | 92.25 | 103.20 |
| `SerializableCallback` | 1.372 | 1.537 | 2.157 | 5.77 | 5.68 |
| `SharedCycle_128` | 32.217 | 45.518 | 30.235 | 88.17 | 100.05 |
| `StringList_128` | 6.254 | 6.530 | 7.207 | 29.46 | 28.72 |

#### .NET Framework 4.8.1 x64 RyuJIT

| Scenario | BinaryFormatter (us) | Touki (us) | Touki (KiB) |
| --- | ---: | ---: | ---: |
| `CustomObject` | 2.111 | 3.239 | 5.01 |
| `Int32Array_1K` | 0.627 | 0.532 | 5.06 |
| `ObjectTree_127` | 83.827 | 153.103 | 107.75 |
| `SerializableCallback` | 2.308 | 4.675 | 6.64 |
| `SharedCycle_128` | 85.697 | 163.098 | 110.91 |
| `StringList_128` | 12.083 | 19.026 | 30.83 |

The 24-byte increase on shallow object graphs is the bounded cost of seeding the local
parser stack. It buys a 3,080-byte stack reduction in the deep cycle and removes the stack
entirely from direct primitive-array deserialization. Final adaptive end-to-end runs retain
the graph improvement direction on net10, net11, and .NET Framework. Cross-run percentages
should still be read as directional because each adaptive matrix was collected separately.

A pooled parser stack was measured and rejected. It reduced managed bytes but made tree
materialization about 9% slower and cycle materialization about 8% slower in the smoke
matrix; virtual list operations and pool lifecycle cost more than the removed arrays.

## Class field assignment investigation

The field-assignment investigation uses a dedicated serializable class with 32 independent
`int` fields. `BinaryFormattedObjectFieldAssignmentPerf` parses a bounded batch of 512
independent record graphs outside the measured region and materializes each graph exactly
once. Fixed `MediumRun` results are:

```powershell
foreach ($tfm in 'net10.0', 'net481') {
    dotnet run -c Release -f $tfm --project touki.perf -- `
      --filter '*BinaryFormattedObjectFieldAssignmentPerf*' --job medium
    dotnet run -c Release -f $tfm --project touki.perf -- `
      --filter '*ClassFieldAssignmentPerf*' --job medium
    dotnet run -c Release -f $tfm --project touki.perf -- `
      --filter '*ClassRecordMemberLookupPerf*' --job medium
}
```

| Runtime | Materialize mean | Error | StdDev | Allocated |
| --- | ---: | ---: | ---: | ---: |
| .NET 10 x64 RyuJIT | 623.4 ns | 12.19 ns | 17.48 ns | 1,016 B |
| .NET Framework 4.8.1 x64 RyuJIT | 3.331 us | 0.035 us | 0.049 us | 1.09 KiB |

Fresh net10 EventPipe traces resolved 100% of managed symbols. Within
`ClassRecordFieldInfoDeserializer.Continue`, member-name lookup and equality were more
visible than `FieldInfo.SetValue`. The public `ClassRecord` API requires two dictionary
lookups for version-tolerant deserialization: `HasMember(name)` followed by
`GetRawValue(name)`.

### Assignment primitive

`ClassFieldAssignmentPerf` compares 32 individual `FieldInfo.SetValue` calls with the only
portable batch API, `FormatterServices.PopulateObjectMembers`. The preallocated row gives
the batch API an existing values array. The realistic row allocates and fills the values
array that the deserializer would need to add.

| Runtime | Method | Mean | Error | Allocated |
| --- | --- | ---: | ---: | ---: |
| .NET 10 x64 RyuJIT | `FieldInfo.SetValue` | 143.1 ns | 1.54 ns | 144 B |
| .NET 10 x64 RyuJIT | `PopulateObjectMembers`, preallocated | 144.3 ns | 10.02 ns | 144 B |
| .NET 10 x64 RyuJIT | `PopulateObjectMembers`, realistic | 158.7 ns | 1.08 ns | 424 B |
| .NET Framework 4.8.1 x64 RyuJIT | `FieldInfo.SetValue` | 1.533 us | 0.010 us | 144 B |
| .NET Framework 4.8.1 x64 RyuJIT | `PopulateObjectMembers`, preallocated | 1.497 us | 0.016 us | 144 B |
| .NET Framework 4.8.1 x64 RyuJIT | `PopulateObjectMembers`, realistic | 1.519 us | 0.007 us | 425 B |

The preallocated modern-.NET row was bimodal and does not establish a win. Including the
required values array makes batching 11% slower on modern .NET RyuJIT and nearly triples
managed allocation on both runtimes. On .NET Framework 4.8.1 RyuJIT, its small mean
difference is within overlapping confidence intervals and does not justify 281 additional
bytes per object. Production therefore retains individual `FieldInfo.SetValue` calls.

Runtime-generated setters were not adopted. They require dynamic code, conflict with the
trim/native-AOT contract on modern .NET, and need a cache whose lifetime and type retention
would exceed the current per-deserialization state. A Framework-only generated-setter path
would be a separate design with cold-start, partial-trust, and cache-bound measurements.

### Member lookup

`ClassRecordMemberLookupPerf` measures the current safe two-lookup sequence against direct
`GetRawValue` when all 32 names are known to exist. The direct row is a lower bound, not a
valid production implementation: supported version-tolerant payloads may omit fields.

| Runtime | `HasMember` + `GetRawValue` | Known-present `GetRawValue` | Ratio | Allocated |
| --- | ---: | ---: | ---: | ---: |
| .NET 10 x64 RyuJIT | 330.9 ns | 187.5 ns | 0.57 | 0 B |
| .NET Framework 4.8.1 x64 RyuJIT | 1.030 us | 0.682 us | 0.66 | 0 B |

A supported one-lookup API could remove 43% of member-access time on modern .NET and 34%
on Framework for this shape. Touki cannot reproduce it safely through the current public
surface: catching `KeyNotFoundException` changes malformed-input cost into allocation and
exception pressure, private reflection is not trim/AOT-safe, and a local record-layout cache
adds allocation while relying on undocumented metadata identity. The actionable next step
is an upstream `ClassRecord.TryGetRawValue(string, out object?)` API in
`System.Formats.Nrbf`; until that exists, the production field path remains unchanged.
The paste-ready runtime issue draft is in
[classrecord-trygetrawvalue-api-proposal.md](classrecord-trygetrawvalue-api-proposal.md).

### Remaining plan

The final traces resolve all managed symbols. They show the following order for further
work:

1. Preserve `ArrayRecord.GetArray(Type)` validation. Its recursive NRBF type-name matching
  is now the dominant cycle frame, but bypassing it would weaken payload/type validation.
  Optimize only through a BCL-supported validated path or an upstream `System.Formats.Nrbf`
  change.
2. Pursue a supported one-lookup member accessor in `System.Formats.Nrbf`. Re-run
  `ClassRecordMemberLookupPerf` and the wide-object materialization benchmark if such an API
  becomes available; do not substitute exception-driven lookup or private reflection.
3. Investigate the serialization-constructor path separately. Callback traces point at
  `PendingSerializationInfo.GetDeserializationConstructor`, parameter discovery, and
  delegate binding; graph optimizations should not be assumed to help it.
4. Keep parser work separate. String lists remain decode-dominated, and exact upstream
  parsing is still the stronger reference for reducing record-model time and allocation.
5. Re-run the same six-scenario split and all three end-to-end runtime matrices after each
  retained change. Reject any optimization that improves graph means by trading for an
  unbounded cache or a material regression in primitive/custom/callback scenarios.
