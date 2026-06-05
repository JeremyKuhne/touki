# Audit checklist

Detail for the [security-review](SKILL.md) skill. Walk every new/modified
member through the categories below. The question is whether the *input shape*
or *unchecked precondition* could push the code into the failure mode, not
whether it currently does.

The largest category - `unsafe` / `Unsafe.*` / `MemoryMarshal.*` and other
caller-validated APIs (&sect;7) - lives in [unsafe-apis.md](unsafe-apis.md).

When attention is limited, allocate it by tier:

- **Critical (never skip):** &sect;1, &sect;4, [unsafe-apis.md](unsafe-apis.md).
- **Standard (every member):** &sect;2, &sect;5, &sect;9.
- **Conditional (when the code shape matches):** &sect;3 (allocates
  proportional to input), &sect;6 (encoder uses in-band sentinels),
  &sect;8 (path-handling API).

## 1. Length / size of inputs

- Is there an upper bound? What stops a caller from passing a
  multi-MB / multi-GB value?
- Is the bound at the API boundary, not buried in a helper?
- For composed inputs (list of strings, stream of records), is the
  **total** size bounded, not just per-element?
- Is the default safe for untrusted callers, with an opt-out for
  trusted ones? The standard shape is a `DefaultMaxLength` constant
  plus a `maxLength` parameter where a sentinel (e.g. `-1`) disables
  the check.

**Tests:** at-limit success, over-limit failure with documented
error, opt-out (e.g. `-1`) disables the check.

## 2. Integer / length-field overflow

- Are length sums (`a.Length + b.Length`, `count * elementSize`)
  protected by `checked()` where crafted input can overflow?
- Are running lengths cast to a narrower type (`(char)len`,
  `(byte)count`)? Silent truncation lets oversized fields slip
  through and be re-read as smaller values, misdecoding downstream
  data as structural metadata. Check the counter *before* the cast.

**Tests:** boundary inside the representable range plus boundary
outside. Specialized fast paths often bypass the affected code
- pick an input shape that **forces the general path**.

## 3. Allocation pressure / memory DoS

- Does work allocate proportional to (or worse than) input size?
- Is the worst case bounded by an explicit cap (&sect;1) or only by
  available memory?
- Do buffers rent from `ArrayPool<T>.Shared` (good), allocate fresh
  per call (acceptable if bounded), or grow without limit (red flag)?

**Tests:** a "large but legitimate" input completes within a
`Stopwatch` bound (`Should().BeLessThan(TimeSpan.FromSeconds(2))`).
Reach for BenchmarkDotNet's `MemoryDiagnoser` only to pin a specific
allocation count.

## 4. Computational complexity / algorithmic DoS

- Worst-case runtime: linear, polynomial, or exponential?
- ReDoS shape: matcher with multiple wildcards (`*`, `**`, `?(...)`,
  alternation) that retries every wildcard on mismatch. Safe shape:
  **fixed savepoint slots** (each new wildcard overwrites the
  previous slot of the same kind), bounding the worst case to
  O(n&middot;m).
- Hashtable attacks: are user-controlled keys hashed with a
  randomized hash (modern .NET `string.GetHashCode()` and
  `Dictionary<,>` are per-process randomized), or a deterministic one
  a caller could pre-image into a single bucket?

**Tests** (pin the safe property):

```csharp
[Fact]
public void Method_PathologicalInput_TerminatesPromptly()
{
    /* construct adversarial pattern + input */

    Stopwatch sw = Stopwatch.StartNew();
    bool result = m.IsMatch(input);
    sw.Stop();

    result.Should().Be(/* expected */);
    sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
}
```

Add one per "we use the safe algorithm" claim; the property is
invisible to refactors, the test is the lock.

## 5. Malformed structure / parser robustness

- Truncated input handled gracefully (no `IndexOutOfRangeException`
  past the end)?
- Unmatched opens (`[`, `{`, `(`, dangling `\\` / `%`) produce a
  defined error, not an unrelated runtime exception?

**Tests:** one per malformed shape asserting the exact error type;
empty, single-character, and "exactly the sentinel" inputs (`""`,
`"["`, `"\\"`).

## 6. Sentinel / in-band markers colliding with input

- Encoder uses in-band sentinel values (opcode chars, length prefix
  bytes, control chars)? If user input contains those same values,
  are they distinguishable from real structure (escaping, length
  framing, reserved noncharacter code points)?

**Test:** input containing every sentinel value used as ordinary
data; the member treats them as data.

## 7. `unsafe`, `Unsafe.*`, `MemoryMarshal.*`, and other caller-validated APIs

Mandatory whenever a PR touches one of these. The compiler offloads
safety to you; the review *is* the safety check. The per-API table
(precondition, failure mode, required test shape) lives in
[unsafe-apis.md](unsafe-apis.md).

## 8. Path traversal / sandbox escape

- API takes a root + relative input and returns paths - results
  stay inside the root? Symlink behavior is the caller's documented
  choice, not silently re-enabled?
- Caller-supplied fragments concatenated without normalization?
  `Path.Combine` honors absolute inner segments, which is a foot-gun.

**Tests:** inputs with `..` segments, absolute paths, alternate
separators, and symlink-style names; enumerator stays inside the
configured root.

## 9. Argument validation at the boundary

- `ArgumentNullException.ThrowIfNull` on every reference-type
  parameter the contract forbids `null` for.
- `ArgumentOutOfRangeException.ThrowIfNegative` / `ThrowIfGreaterThan`
  on numeric range checks.
- Enum parameters: validated against the declared range, or
  documented to accept any underlying value with explicit defaulting.

**Test:** one per parameter for each forbidden violation mode. Don't
trust the downstream BCL primitive to throw - the contract is
yours.
