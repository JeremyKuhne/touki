---
applyTo: 'touki.tests/**/*.cs'
---

# Test conventions for `touki.tests`

These rules duplicate the summary in [AGENTS.md](../../AGENTS.md) and add the
detail that would bloat the canonical file. Both are authoritative; if they
ever drift, AGENTS.md wins.

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

## Release-mode rule

- Run `dotnet test -c Release` before declaring a fix done. Release-mode
  inlining surfaces bugs Debug doesn't &mdash; `Unsafe.As` on a method
  parameter is a known foot-gun on net481 RyuJIT.
