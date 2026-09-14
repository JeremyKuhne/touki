# Assessing a generic `default!` exception for TOUKI0005

## Decision

Do not add an automatic generic exception to TOUKI0005.

The investigation did not establish a repository case where `default!` is the
only way to preserve correct behavior while nullable warnings remain enabled. It
did find three real limitations in C# null-state analysis:

- array elements are not tracked as initialized or uninitialized;
- null-state cannot be related to an arbitrary state discriminator such as
  `_hasItem`; and
- `IEnumerator<T>.Current` does not express that its value is undefined outside a
  successful `MoveNext` position.

Those limitations explain why `default!` can be convenient in low-level generic
storage. They do not define a safe analyzer exception. Each limitation has another
way to avoid `!`, although the alternative may move rather than resolve the trust
boundary, change nullable metadata, add a call or branch, or require a
representation change.

The attempted exception became a semantic and contextual classifier without gaining
enough information to distinguish an intentional inactive slot from a false
non-null contract. That is the wrong tradeoff for a rule whose useful property is
that it is simple and predictable.

## Scope and evidence

This assessment is based on PR #299 at commit `a215ad3` and the experimental generic
exception developed on top of it.

The decision evidence was:

- an exact source inventory and manual classification of every active `default!`
  occurrence on the baseline branch;
- force-enabled TOUKI0005 builds of the main library for `net10.0` and `net472`;
  these were selected probes, not an exhaustive scan of the additional `net11.0`
  target; and
- a compiler probe of nullable alternatives on both `net10.0` and `net472`, using
  PolySharp for downlevel nullable attributes.

The baseline branch contains 30 active source-level `default!` occurrences. This
count excludes comments, string contents used as analyzer-test input, generated
artifacts, and the experimental tests added while evaluating the exception. There
were no active `default(T)!` occurrences; that form was examined through compiler
probes and focused analyzer tests.

An exploratory whole-repository scanner also produced broader null-forgiving
totals, but its hand-written preprocessor-symbol sets did not exactly reproduce
every project and target. It missed at least two non-default operators behind
`NET5_0_OR_GREATER`. Those broader totals are deliberately not used in this
assessment. The omission did not affect the independently reviewed 30-site
`default!` inventory.

## What "unavoidable" means

There are two materially different claims:

1. **Language necessity:** no C# spelling or nullable annotation can express the
   behavior without `!` while nullable warnings remain enabled.
2. **Practical necessity:** alternatives exist, but all are unacceptable because
   they change an established public nullability contract, layout, allocation,
   control flow, or measured hot-path performance.

No language-necessity case was found.

Some storage cases might eventually establish practical necessity, but that would
require case-specific compatibility or performance evidence. Being generic is not
that evidence. An analyzer cannot infer those constraints from a `default!`
expression.

Disabling a diagnostic with `#pragma` is also technically an alternative to `!`.
This assessment does not count that as resolving the underlying nullability model,
but it remains preferable to a global analyzer false negative when a reviewed case
must preserve its current shape.

## Inventory of `default!`

Of the 30 active occurrences, 28 have a contextual type that is a type parameter and
two have the concrete reference type `CacheEntry`.

The intent categories are:

- **State-dependent generic storage or `Current` value: 14.** Representative
  types are
  [`ArraySegmentEnumerator<T>`](../touki/Touki/Collections/ArraySegmentEnumerator.cs),
  [`SingleOptimizedList<TItem, TList>`](../touki/Touki/Collections/SingleOptimizedList.cs),
  and [`EnumerableBase<T>`](../touki/Touki/Collections/EnumerableBase.cs).
  Alternatives include `[AllowNull]` storage, `T?`, a different state
  representation, or an invalid-state throw.
- **Generic array-slot clearing: 2.** These occur in
  [`ArrayBackedList<T>`](../touki/Touki/Collections/ArrayBackedList.cs) and
  [`ContiguousListTests`](../touki.tests/Touki/Collections/ContiguousListTests.cs).
  `Array.Clear` or another clearing helper avoids the direct assignment.
- **`Try*` output assignment: 12.** These occur in
  [`Value.TryGetValue<T>`](../touki/Touki/Value.cs) and
  [`RefCountedCache.GetEntry`](../touki/Touki/Collections/RefCountedCache.cs).
  Alternatives include a nullable postcondition matching the actual success
  contract, a nullable output, or a flow rewrite.
- **Public conversion with a false non-null contract: 1.** This is
  [`SinglyLinkedList<T>.Node`](../touki/Touki/Collections/SinglyLinkedList.Node.cs).
  Alternatives are `T?`, `[MaybeNull]`, or throwing for a null node.
- **Inactive concrete union arm and default-state protocol: 1.** This is
  [`RefCountedCache.Scope`](../touki/Touki/Collections/RefCountedCache.Scope.cs).
  `CacheEntry?` and `[AllowNull]` describe the inactive storage, but the public ref
  struct's implicit default value also needs an explicit initialized-state check.

These categories total 30 occurrences.

The exact location reconciliation is:

- State-dependent generic storage or `Current` value (14):
  [`ArraySegmentEnumerator.cs`](../touki/Touki/Collections/ArraySegmentEnumerator.cs)
  lines 35, 66, and 76;
  [`EnumerableBase.cs`](../touki/Touki/Collections/EnumerableBase.cs) line 21;
  [`RefCountedCache.Scope.cs`](../touki/Touki/Collections/RefCountedCache.Scope.cs)
  line 52;
  [`SingleOptimizedList.cs`](../touki/Touki/Collections/SingleOptimizedList.cs)
  lines 34, 103, 114, 217, 233, 262, and 287;
  [`SpanExtensions.SpanSplitEnumerator.cs`](../touki/Framework/Polyfills/System/SpanExtensions.SpanSplitEnumerator.cs)
  line 65; and
  [`ValueEnumeratorTests.cs`](../touki.tests/Touki/Collections/ValueEnumeratorTests.cs)
  line 29.
- Generic array-slot clearing (2):
  [`ArrayBackedList.cs`](../touki/Touki/Collections/ArrayBackedList.cs) line 98
  and [`ContiguousListTests.cs`](../touki.tests/Touki/Collections/ContiguousListTests.cs)
  line 114.
- `Try*` output assignment (12):
  [`RefCountedCache.cs`](../touki/Touki/Collections/RefCountedCache.cs) line 134
  and [`Value.cs`](../touki/Touki/Value.cs) lines 1318, 1345, 1362, 1379, 1508,
  1515, 1530, 1542, 1556, 1570, and 1619.
- Public conversion with a false non-null contract (1):
  [`SinglyLinkedList.Node.cs`](../touki/Touki/Collections/SinglyLinkedList.Node.cs)
  line 35.
- Inactive concrete union arm (1):
  [`RefCountedCache.Scope.cs`](../touki/Touki/Collections/RefCountedCache.Scope.cs)
  line 41.

The two concrete cases are:

- [`RefCountedCache.GetEntry`](../touki/Touki/Collections/RefCountedCache.cs#L134),
  where a local `CacheEntry` is initialized for a failed lookup; and
- [`RefCountedCache.Scope`](../touki/Touki/Collections/RefCountedCache.Scope.cs#L41),
  where the inactive `_entry` field is initialized in the constructor for an
  uncached object.

A syntax-only `default!` exception would hide both. A type-parameter exception would
retain them, but would still hide generic methods that publish an incorrect non-null
contract.

The initial inactive-arm classification was not sufficient by itself. Because public
ref structs can always be default-initialized, `default(Scope)` left both storage arms
empty and could return null through a non-nullable `TValue` conversion. The final
remediation records constructed uncached state, treats a non-null cache entry as
constructed cached state, and throws when the conversion observes the implicit default
state. A regression test also preserves null as a valid uncached value when `TValue` is
nullable.

The `SinglyLinkedList<T>.Node` conversion is a concrete example. Its public return
type is `T`, but a null node returns `default!`; the existing `string` test assigns
that result to a non-nullable variable and expects it to be null. This is an
existing false nullability contract, not an unavoidable generic sentinel. A generic
exception would hide it.

## Compiler probe results

The following direct and alternative forms were compiled in one probe project with
the repository's compiler on both `net10.0` and `net472`. Both targets produced the
same material nullable diagnostics. "Clean" means that the alternative emitted none
of `CS8601`, `CS8603`, `CS8618`, `CS8625`, or `CS8766`; unrelated style diagnostics
were not part of the comparison.

| Scenario | Direct form | Result without `!` | Alternative | Alternative result |
| --- | --- | --- | --- | --- |
| Generic field initialization | `T value = default` | `CS8601` | `[AllowNull] T value = default` | Clean |
| `where T : notnull` inactive field | `T value = default` | `CS8601` | `[AllowNull] T value = default` | Clean |
| Generic auto-property initialization | `T Current { get; set; } = default` | `CS8601` | `[AllowNull] T Current { get; set; } = default` | Clean |
| Generic array-element clearing | `items[index] = default` | `CS8601` | `Array.Clear(items, index, 1)` | Clean |
| Generic return | `T Get<T>() => default` | `CS8603` | `T?` or `[return: MaybeNull]` | Clean |
| Failed generic `Try*` output | `value = default` | `CS8601` | `[MaybeNullWhen(false)] out T value` | Clean |
| Generic conversion fallback | `operator T` returning `default` | `CS8603` | `operator T?` or `[return: MaybeNull] operator T` | Clean |
| Nullable `IEnumerator<T>.Current` | `T? Current` | `CS8766` | Keep `T Current` and mark backing storage `[AllowNull]` | Clean |
| Maybe-null `IEnumerator<T>.Current` | `[MaybeNull] T Current` | `CS8766` | Keep `T Current` and mark backing storage `[AllowNull]` | Clean |

`[AllowNull]` is not proof that a later read is safe. It says that a write may be
null while the declared type remains non-null for reads. For private inactive
storage that can be an intentional contract, but it relocates the trust boundary
from each `default!` assignment to the field declaration. It can hide a bad read if
the state guard is wrong.

Likewise, `Array.Clear` does not make a `T[]` contain non-null values. It performs
the same zeroing through an API the nullable flow analysis does not model. These
alternatives prove that `!` is not uniquely required; they do not prove that the
underlying model is sound.

## Actual null-analysis gaps

### Array element initialization

`new T[length]` is accepted without a nullable warning even though every element is
`null` when `T` is instantiated with a reference type. C# does not track which array
elements have been initialized. Assigning `default` directly to an element warns,
while `Array.Clear` performs the same operation without warning.

This is a real analysis gap, but it is not specific to `default!` and does not justify
ignoring every generic default suppression.

### State-dependent storage

Types such as `SingleOptimizedList<TItem, TList>` represent state with a discriminator
and a payload:

```csharp
private bool _hasItem;
private TItem _item;
```

The intended invariant is that `_item` is valid only when `_hasItem` is true. Nullable
attributes cannot express the relation `_hasItem => _item is initialized` for an
arbitrary field. `[AllowNull]` permits the inactive value and preserves layout, but
the compiler then treats every read as non-null without proving the discriminator.
Using `T?` makes the storage truthful but pushes checks into valid-state reads unless
the representation is redesigned.

This is a real relational-analysis gap. Whether `default!`, `[AllowNull]`, a nullable
field, or a different representation is best is a local design decision.

### `IEnumerator<T>.Current` protocol state

The
[runtime source](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Collections/Generic/IEnumerator.cs)
documents that `IEnumerator<T>.Current` is undefined before the first successful
`MoveNext` and after `MoveNext` returns false. Its declaration is still `T Current`,
with no nullable annotation or protocol relation to `MoveNext`.

Changing an implementation to `T? Current` or `[MaybeNull] T Current` produces
`CS8766` because it no longer matches the interface nullability contract. Marking the
backing field `[AllowNull]` compiles and avoids `!`, but leaves the public analysis
more optimistic than the documented protocol. Throwing when the enumerator is not
positioned is another option, with different behavior and possible performance cost.

This is the strongest concrete nullability mismatch found. C# has no annotation that
truthfully expresses the temporal relation while retaining the interface contract.
Even here, `!` is not the only implementation technique: `[AllowNull]` can move the
trust boundary to private storage, and a run-time position check can throw instead.
The analyzer also cannot recognize that a containing type correctly implements the
temporal protocol.

## Cases that are not analysis holes

Generic syntax alone is not a gap in modern nullable analysis:

- `T?` represents a maybe-default result for an unconstrained type parameter;
- `[MaybeNull]` describes a non-nullable generic return that can contain the default
  value;
- `[MaybeNullWhen(false)]` describes a `Try*` output when only failure can produce a
  maybe-null value; and
- `[MemberNotNull]` and `[MemberNotNullWhen]` describe initialization performed by
  helper methods.

These contracts can have source-compatibility consequences when added to an existing
public API, but that is compatibility work rather than a missing language mechanism.

## Why an automatic generic exception is not defensible

### A type parameter does not imply an intentional sentinel

All of these expressions have a type parameter, but they do not carry the same
meaning:

```csharp
T InactiveSlot<T>() => default!;
T BrokenFactory<T>() where T : class => default!;
T BrokenNotNullFactory<T>() where T : notnull => default!;
string Dereference<T>() => default(T)!.ToString();
object Convert<T>() => default(T)!;
```

The first might be intended as an inactive value. The next two publish a false
non-null contract. The fourth can throw immediately. The last converts a generic
default into a concrete non-null contract.

Checking only `ITypeParameterSymbol` creates false negatives.

### Expression context does not establish intent

The prototype next distinguished initializers, assignments, returns, conditional
arms, switch arms, arguments, receivers, casts, and parenthesized expressions. That
still does not answer whether a value is an inactive payload, a failed `Try*` output,
or an invalid API result.

The set of expression wrappers and value-producing contexts is also open-ended.
Every added boundary creates another interaction with conversions, tuples,
collection expressions, `yield return`, lambdas, and future syntax. Complexity can
reduce obvious false negatives without proving the exception sound.

### Natural type and converted type answer different questions

For `default(T)!`, the natural type is `T`. In `object value = default(T)!`, the
converted type is `object`. Exempting by natural type misses the concrete contract;
exempting by converted type changes behavior when the same expression moves between
otherwise similar contexts.

Neither choice identifies necessity. It only selects a heuristic.

### Generic constraints do not identify safety

- `where T : struct` makes `!` unnecessary because `default(T)` cannot be a null
  reference.
- `where T : class` makes `default(T)` definitely null, so returning it as `T` is a
  direct contract violation.
- `where T : notnull` still permits a non-nullable reference type, for which the
  default value is null.
- an unconstrained `T` has the language's "maybe default" state and can usually be
  represented as `T?` or with a nullable postcondition.

Constraints narrow the possible runtime values. They do not reveal whether null is
an intentional inactive representation.

## Prior art

No established analyzer behavior was found that supports a generic-default
exception:

- [Meziantou MA0191](https://github.com/meziantou/Meziantou.Analyzer/blob/main/docs/Rules/MA0191.md)
  treats both `null!` and `default!` as smells and reports them.
- [Sonar S8969](https://github.com/SonarSource/sonar-dotnet/blob/master/analyzers/rspec/cs/S8969.html)
  asks a different question: whether a null-forgiving operator is redundant. A
  reference-typed or unconstrained-generic `default!` suppression is normally
  load-bearing because it changes the compiler's null state. A value-type default
  can be redundant.
- Microsoft's
  [nullable static-analysis attributes](https://learn.microsoft.com/dotnet/csharp/language-reference/attributes/nullable-analysis)
  document `MaybeNull`, `MaybeNullWhen`, and related contracts for generic results
  and `Try*` patterns.
- Microsoft's
  [generic nullability guidance](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/nullable-reference-types#generics)
  documents `T?` for unconstrained type parameters and the "maybe default" state.

The prior art supports explicit contracts or an unconditional style rule. It does
not provide a principled intent classifier for generic defaults.

## Disposition of PR #299

PR #299 merged as `42e2015` with the original syntax-only TOUKI0005 behavior: the
rule reports each `SuppressNullableWarningExpression` outside generated code and is
disabled by default. The experimental generic-default exception was not included.

The remaining guidance applies if Touki enables the rule in the future:

- resolve generic sites individually with `T?`, `[MaybeNull]`, or a conditional
  postcondition that matches the actual API result;
- use nullable or `[AllowNull]` storage only where that contract is intentional;
- use `Array.Clear` only after checking the relevant hot path;
- use explicit checks or throws for reflection and external API assumptions;
- suppress TOUKI0005 narrowly, with a reason, when preserving an established
  contract or measured implementation shape matters more than avoiding `!`; and
- continue excluding faithfully ported BCL polyfills at the directory level rather
  than teaching a general analyzer about repository provenance.

A future automatic exception should require a concrete source case for which all
standard annotations and local representations are shown to be invalid under stated
compatibility and performance constraints. The exception should then be designed
around that proven contract, not around generic syntax in general.

## Remaining unknowns

- The performance difference between a direct generic slot clear and `Array.Clear`
  was not benchmarked on `net472` or `net10.0`.
- The consumer impact of adding nullable metadata to existing public methods,
  properties, and operators was not assessed.
- Enumerator callers might rely on the current out-of-position value even though the
  interface documents it as undefined.
- `[AllowNull]` can be less locally visible than `!`; this assessment does not claim
  it is always the better style.

These unknowns can justify leaving an existing suppression unchanged and reporting
the site as unresolved. Selecting retention as the remedy still requires the nearest
available behavior, invariant, or authoritative-contract evidence. The unknowns do
not justify a repository-wide generic exception.
