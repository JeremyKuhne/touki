---
applyTo: 'touki.tests/**/*.cs'
---

# Test conventions for `touki.tests`

[AGENTS.md](../../AGENTS.md) is canonical. This file is a path-scoped
elaboration of the same rules for `touki.tests/**/*.cs` &mdash; it adds detail
that would bloat the canonical file but must not contradict it. If the two
ever drift, AGENTS.md wins; update this file to match.

## Placement and naming

- Place tests in the `touki.tests` project.
- Test classes live in the same namespace as the class under test, with `Tests`
  appended (e.g. `ListBaseTests` for `ListBase`).
- Test methods are named `MethodName_StateUnderTest_ExpectedBehavior`
  (e.g. `MoveNext_AtStart_ReturnsTrue`).
- Order test methods by the method they are testing.
- Cover edge and negative cases.

## Style

- Do **not** add "Arrange, Act, Assert" comments in tests.
- Use FluentAssertions for assertions. `FluentAssertions` and `Xunit` are
  already global usings &mdash; do not add new usings for these to test files.
- Prefer `[Theory]` with `[InlineData]` only when there are 3+ inputs;
  otherwise `[Fact]`.

## Internals and private access

- Tests have access to internals via `InternalsVisibleTo`, so you can test
  internal members directly.
- For private members, use the `TestAccessor` and `TestAccessors` extension
  methods from `touki.testsupport`. Do not use reflection by hand.

## Ref structs

- Ref structs cannot be used in lambdas. To validate error cases that would
  otherwise want `Assert.Throws(() => ...)`, use a `try`/`finally` block and
  assert on the caught exception explicitly.

## Disposables in test bodies

Anything that allocates a real resource &mdash; `TempFolder`,
`IEnumerationMatcher`, `MSBuildMatchBuilder.FromSpecification`,
`ArrayPoolList<T>`, etc. &mdash; must be cleaned up even when assertions
fail. Default to `using` declarations.

- For "happy path" tests, write `using TempFolder folder = new();` and
  `using IEnumerationMatcher matcher = ...;`. Do not assign to a bare
  local; the resource leaks on failure.
- When the test itself exercises explicit `Dispose()` semantics (e.g.
  double-dispose, dispose-after-external-deletion), `using` would call
  `Dispose()` before the test body runs the assertion. Use
  `try`/`finally` instead and call `Dispose()` defensively in the
  `finally` &mdash; `DisposableBase` guards against double disposal:

  ```c#
  TempFolder folder = new();
  try
  {
      // ... explicit Dispose() calls under test ...
  }
  finally
  {
      folder.Dispose();
  }
  ```

- Reviewer Copilot will flag any bare `IDisposable` local in a test as a
  potential resource leak. Address it before merging.

## Culture-sensitive assertions

Many touki formatting APIs (`string.FormatValue`,
`string.FormatValues`, `ValueStringBuilder.AppendFormat`, the
`StringBuilderExtensions.AppendFormatted` overloads that don't take a
provider) construct the underlying `ValueStringBuilder` with
`provider: null`, which makes numeric and date formatting follow
`CultureInfo.CurrentCulture`. Hard-coding `InvariantCulture` expectations
makes the test locale-dependent and flaky on non-en-US machines.

- For formats that include culture-sensitive separators or symbols (`N`,
  `C`, `P`, `D` for dates, etc.), derive the expected string from
  `CultureInfo.CurrentCulture` &mdash; or, when the test is meant to
  pin behavior under a specific culture, set
  `Thread.CurrentThread.CurrentCulture` (and restore in `finally`).
- Culture-insensitive formats (`X`, `B`, default `G` on integers,
  literal pass-through) are safe with hard-coded expected strings.

## Release-mode rule

- Run `dotnet test -c Release` before declaring a fix done. Release-mode
  inlining surfaces bugs Debug doesn't &mdash; `Unsafe.As` on a method
  parameter is a known foot-gun on net481 RyuJIT.

## Allocation assertions

- Use [`Touki.TestSupport.MemoryWatch`](../../touki.tests/TestSupport/MemoryWatch.cs)
  to assert that a region of code does not allocate. Open it in a
  `using` block on the same thread as the code under test; the watch
  records `GC.GetAllocatedBytesForCurrentThread()` on entry and throws
  on disposal if any bytes were allocated. Warm up generics or
  delegate-creation paths once before the watch so the JIT itself is
  not measured.
- Allocation assertions are only meaningful when the JIT optimizations
  they're measuring against are in effect, so guard them with
  `#if !DEBUG` when the path under test relies on inlining or
  enregistration (typical for ref-struct and generic value-type code).
  Debug-mode net481 in particular adds allocations the test cannot
  control.
