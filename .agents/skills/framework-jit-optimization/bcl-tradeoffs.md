# BCL trade-offs on `net481`

The System.Memory primitives `IndexOf` and `SequenceEqual` are not vectorized on
`net481`, but they're not naive per-element loops either. They use
**integer-stride scalar tricks** &mdash; comparing `ulong`-sized chunks (4 chars
or 8 bytes at a time) with bit operations. This makes the choice between
"specialize my own loop" and "lean on the BCL" non-obvious.

## Decision rule

> Specialize when your loop visits most elements.
> Defer to the BCL primitive when the realistic input lets it skip large runs cheaply.

The decision is about **the realistic input distribution**, not the worst case.
A method that's used to scan dense data should specialize; a method that's used
to find sparse markers in mostly-irrelevant data should defer.

## Cases where specializing wins (full-scan workloads)

The hot loop visits every element. The BCL primitive's per-call overhead and
slightly less aggressive unrolling lose to a hand-written `ushort*` /
`byte*` loop with the unroll-4 form.

| Method | Realistic input | Win factor (length 4096) |
| --- | --- | --- |
| `Span<T>.Replace(T, T)` | mutate every match in place | 2.18&ndash;3.08&times; faster than `IndexOf`-walking |
| `IndexOfAnyExcept(T)` | typically scans most of the buffer (e.g. trim, parse) | 0.34&times; vs scalar = 3&times; faster |

These are the methods that already have `typeof(T)` specialization in
`touki/Framework/`.

## Cases where deferring to the BCL wins (skip-run / log-probe workloads)

The hot loop **doesn't** visit every element &mdash; the BCL's stride trick
skips long non-match runs faster than per-element compares can advance.

### `Count(T)` via repeated `IndexOf` slicing

```c#
public int Count(T value)
{
    ReadOnlySpan<T> remaining = span;
    int count = 0;
    int index;
    while ((index = remaining.IndexOf(value)) >= 0)
    {
        count++;
        remaining = remaining[(index + 1)..];
    }

    return count;
}
```

Measured ratios vs a full-scan `ushort*` unroll-4 specialization on `net481`:

| Match density (1 in N) | BCL `IndexOf`-walk vs specialized |
| ---: | --- |
| 1   | **9&times; slower** (specialization wins) |
| 7   | 1.85&times; slower (specialization still wins) |
| 64  | **0.45&times; slower** (BCL wins) |

Realistic `Count` calls are sparse (counting newlines in source, separators in a
path, error markers in a log line). The BCL form is correct.

### Exponential `SequenceEqual` probe for `CommonPrefixLength`

```c#
int matched = 0;
int probe = 16;
while (matched + probe <= length
    && span.Slice(matched, probe).SequenceEqual(other.Slice(matched, probe)))
{
    matched += probe;
    if (probe < 1024)
    {
        probe *= 2;
    }
}

// Scalar tail for the last < probe elements.
for (int i = matched; i < length; i++) { /* ... */ }
return length;
```

`O(log n)` BCL calls instead of `O(n)` element compares. At length 4096 with a
full match this beats any scalar loop by **3.3&times;** even on `net481`.

The early-divergence case (mismatch at index 8) is roughly tied with a scalar
specialization &mdash; absolute saving is ~8 ns &mdash; not worth a 3.3&times;
regression on the long-prefix case (path normalization, string interning,
sorted-key compression are the realistic callers).

## How to decide for a new method

1. Identify the hottest call pattern. What does the input usually look like?
   Dense or sparse matches? Long shared prefixes or short ones? Full scan or
   early exit?
2. Write **both** versions. Don't guess.
3. Benchmark with `[Params]` covering the realistic range plus the dense /
   worst case at the extreme. The `MatchEvery` / `Diverge` params in
   [SpanCountAndPrefixPerf.cs](../../../touki.perf/SpanCountAndPrefixPerf.cs)
   are good templates.
4. Pick the form whose worst case on the realistic distribution is best,
   not the form whose absolute peak is best.

## What about both?

It's tempting to combine the two: short-circuit with a BCL `IndexOf` to skip
forward, then specialize the tight inner loop. **Don't do this without
measurement.** The branch on whether the BCL primitive returned `< 0` adds
overhead, and you lose the simple unrolled body. The cases above all picked
either-or; we have no benchmark showing a hybrid wins.

## Reference benchmarks

- [touki.perf/SpanCountAndPrefixPerf.cs](../../../touki.perf/SpanCountAndPrefixPerf.cs)
  &mdash; `Count` over `MatchEvery = 1, 7, 64` and `CommonPrefixLength` over
  `Diverge = 0, 8, full`.
- [touki.perf/SpanReplace.cs](../../../touki.perf/SpanReplace.cs) &mdash;
  full-scan workload where specialization wins.
