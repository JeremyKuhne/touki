# Nullability remediation matrix

Use this page after removing one null-forgiving operator and capturing the
compiler result. The warning and runtime contract select the remedy; the original
spelling does not.

## First question: what did `!` suppress?

Before calling `!` redundant, verify that nullable warnings are enabled at the site
and that the expected diagnostic is not hidden by `NoWarn`, editor configuration, or
a pragma. If removing `!` then produces no nullable warning, remove it and run the
focused behavior check without adding a replacement annotation or guard.

Common compiler diagnostics are:

| Diagnostic | Meaning at the site | First remedies to evaluate |
| --- | --- | --- |
| `CS8600` | Maybe-null value converted to a non-null variable | Preserve nullability, narrow first, or validate at the boundary. |
| `CS8601` | Maybe-null value assigned | Make storage nullable, describe write preconditions, or redesign inactive state. |
| `CS8602` | Maybe-null value dereferenced | Prove flow with a guard/pattern or reject null at a contract boundary. |
| `CS8603` | Maybe-null value returned | Correct the return annotation or enforce a non-null result. |
| `CS8618` | Non-null member not initialized | Initialize in construction, describe helper initialization, or model external ownership. |
| `CS8625` | Null passed to a non-null parameter | Fix the caller, change the contract, or preserve an intentional negative test explicitly. |
| `CS8766` | Implementation return nullability disagrees with its contract | Honor the inherited contract or identify a protocol/API mismatch. |

The same `!` can suppress different warnings after a conversion or refactor. Always
use the diagnostic from the current target framework and configuration.

## Remedy selection

### Compiler flow already proves non-null

Delete `!`. Prefer compiler-recognized patterns (`is not null`, property patterns,
coalescing, and direct control flow) over an assertion method that the compiler
cannot interpret. Do not add a second check when the existing flow is sufficient.

When a nullable-producing expression is evaluated once and its value is needed only
in the non-null branch, capture it directly:

```csharp
if (GetValue() is { } value)
{
    Use(value);
}
```

Prefer this over declaring a nullable local solely for an immediately following null
check. Use `is SomeType value` when the branch also needs type refinement. Keep a
separate local when the result is needed outside the branch, must be inspected in the
null branch, or its identity must be shared across later control flow.

Apply this preference at the call site; do not rewrite a `Try*` API into a nullable
return merely to enable pattern capture. Preserve `Try*` when the Boolean communicates
a search, parse, bound, or other domain-significant success/failure result.

For any Boolean method with a correlated nullable output, encode the postcondition
on the output parameter after auditing every return path:

- use `[NotNullWhen(true)] out T?` when every `true` return assigns a non-null
  reference;
- use `[NotNullWhen(false)] out T?` when every `false` return assigns a non-null
  reference, such as a failure reason; and
- use `[MaybeNullWhen(false)] out T` for an unconstrained generic output whose
  ordinary `T` contract holds on success but whose failure value may be default.

These rules apply whether or not the method name begins with `Try`. Keep the output
nullable when the opposite result may produce null, and do not make callers repeat
`result is not null` to compensate for a missing postcondition. Do not add a
`[NotNullWhen]` promise for a result side that can legitimately produce null, such
as a successful "not applicable" result or a successful lookup of a stored null.
`[MaybeNullWhen(false)] out T` permits a maybe-default result only on `false`; every
`true` return must still satisfy the ordinary `T` contract. If success can produce a
null reference where `T` is non-nullable, declare a nullable output instead. If the
target lacks the attribute, use the repository's approved downlevel package or
source generator.

### Null is a valid result

Make the contract say so:

- use a concrete nullable reference type such as `string?` when callers should
  observe and handle null;
- use unconstrained `T?` when generic callers should observe a maybe-default
  result;
- use `[return: MaybeNull]` on a `T` return when preserving the ordinary generic
  declaration;
- use `[MaybeNullWhen(false)]` for an `out T` that may be null on failure; or
- use `[NotNullWhen(true)] out T?` when success specifically guarantees non-null.

These attributes are not interchangeable for an unconstrained type parameter.
`[MaybeNullWhen(false)] out T` relaxes the ordinary `T` contract only on failure,
while `[NotNullWhen(true)] out T?` says a successful reference result is non-null.
Choose between them by asking whether success can produce null for any permitted
type argument. Do not use `[NotNullWhen]` as a substitute for a
`Nullable<T>.HasValue` contract when a successful operation can produce an empty
nullable value; prefer `[MaybeNullWhen(false)] out T` when success returns the
ordinary generic `T` contract.

Do not apply a non-null success postcondition when success can legitimately produce
null. A failure-side allowance such as `[MaybeNullWhen(false)]` can still be the
truthful contract. Public annotation changes require consumer validation.

### Null is invalid at this boundary

Check once where ownership begins using a guard available on every affected target.
When `ArgumentNullException.ThrowIfNull` is available directly or through an
approved polyfill:

```csharp
ArgumentNullException.ThrowIfNull(value);
```

For a nullable-returning external API whose null means a broken runtime contract,
use a descriptive exception:

```csharp
MethodInfo method = type.GetMethod(name, flags)
    ?? throw new InvalidOperationException($"Required method '{name}' was not found.");
```

Do not use a throw when null is an expected absence result.

### A constructor establishes the invariant

When every usable instance requires the value, validate it once in the constructor
and assign a non-nullable readonly field. Prefer direct assignments that nullable
flow can verify over nullable backing fields, checked getters, or
`[MemberNotNull]` applied after construction. Audit every constructor and prevent
`this` from escaping before initialization completes.

An explicit static constructor is useful when required dependencies or interdependent
static values come from nullable-producing operations. Keep intermediate values as
constructor locals and retain only the fields needed after initialization. This
proves those fields are initialized; it does not prove that later operations using
them produce non-null results.

Do not hoist a result into static state when it depends on a call argument or mutable
instance. The constructor can cache the immutable dependency used to perform the
operation, but each instance-dependent result must still be obtained and validated.

Adding an explicit static constructor removes `beforefieldinit` and can change when
initialization runs. A failure is surfaced through `TypeInitializationException` and
is cached for the type. Verify that the stricter timing and failure behavior match the
type's contract before choosing this shape.

### A helper initializes members

Prefer direct constructor initialization. When a helper establishes the state,
use `[MemberNotNull]` only if every normal return satisfies the postcondition. Use
`[MemberNotNullWhen(value, ...)]` only if every return with the specified Boolean
value satisfies it. The conditional form describes a protocol transition such as a
reference-type enumerator's `MoveNext()` guaranteeing a nullable `Current` property
is non-null on `true`. Audit every matching return and any mutating methods that can
invalidate the state. Do not use a receiver-state postcondition to describe state
established by a non-readonly mutable struct method: calling it through a readonly
variable, field, or `in` parameter can mutate a defensive copy while flow analysis
narrows the original receiver. A readonly discriminator that only observes existing
struct state can still have a truthful postcondition. These attributes describe state
after a completed call; they do not describe a framework callback that might never
run.

### A framework initializes the member later

Check, in order:

1. constructor injection or a constructor/factory that returns a valid object;
2. `required` only when compiler-checked creation and nullable warnings enforce non-null assignment;
3. framework-specific annotations or contracts enforced before every read;
4. nullable storage with a checked accessor;
5. a lifecycle method with a truthful `[MemberNotNull]` contract; and
6. a narrow compiler/rule suppression with a reason naming the framework guarantee.

`required` is a C# 11 construction-site obligation, not a runtime lifecycle
guarantee. Reflection and framework activators can bypass object-initializer
enforcement, and source callers can explicitly assign null subject to nullable
warnings.

Do not change a required runtime dependency into an optional API merely to remove
`!`. Do not claim that dependency injection, deserialization, or model binding ran
unless the lifecycle makes that guarantee before every read.

For benchmark or test-framework fields initialized by a setup callback, verify that
the callback is unconditional for every consuming method. A private `[AllowNull]`
field can preserve a non-null read contract without adding a measured-path guard,
but only after every read and cleanup path has been audited.

### Generic `default!`

Generic syntax is not an exemption. Classify the use:

- **Return:** keep `T` when `default(T)` is an ordinary value, including for
  `where T : struct`. When default represents absence, use `T?` if that preserves
  the intended type semantics, or `[return: MaybeNull]` on `T` if the declaration
  must remain unchanged.
- **`Try*` output:** use a conditional postcondition matching actual success
  semantics.
- **Inactive field:** consider `T?`, `[AllowNull]`, a tagged representation, or a
  narrow `CS8601` suppression around `default`.
- **Array slot clear:** `Array.Clear` performs equivalent zeroing without a direct
  assignment warning, but it does not make element nullability more truthful.
- **`where T : class` / `notnull`:** returning default as `T` is a false non-null
  contract, not an unavoidable generic case.
- **`where T : struct`:** default cannot be a null reference, so forgiveness is
  unnecessary.

For inactive storage, `[AllowNull]` permits a null write while reads retain the
non-null declared type. It moves trust to the declaration and can hide an invalid
read, so use this remedy only for private storage whose state guard is independently
enforced and tested. Public `[AllowNull]` is valid when the public write contract
accepts null while reads remain non-null, such as a property setter that normalizes
null; verify both accessor contracts.

### Generic constraints

A generic constraint can repair a missing contract when every valid instantiation
already excludes null. It is not a general replacement for `!`:

- use `where T : notnull` when the abstraction accepts non-nullable reference and
  value types but never accepts null;
- use `where T : struct` when the abstraction is genuinely limited to non-nullable
  value types and the stronger source and runtime constraint is compatible;
- treat either addition as a contract change and compile every caller, including
  nullable-disabled consumers; and
- do not infer a constraint from the expression being remediated. Establish it from
  the abstraction's valid inputs and uses.

`notnull` is enforced through nullable warnings, not a CLR runtime constraint. It does
not prove that a value produced by reflection, unsafe code, oblivious code, or
`default(T)` is non-null. Runtime checks such as `typeof(T).IsEnum` and
`typeof(T).IsValueType` also do not satisfy generic constraints in nullable analysis.
When such a check is the only expressible proof, either refactor the boundary or use a
narrow `CS8714` suppression that names the checked invariant.

### State-dependent storage

A discriminator such as `_hasValue` plus a `T` payload is a relational invariant the
nullable type system cannot generally express. Options are:

- nullable payload plus a checked/narrowed read;
- `[AllowNull]` payload plus tests that prove every read is discriminator-guarded;
- one optional state object whose absence means inactive and whose constructor or
  mode-specific type makes every active state complete;
- a representation that makes invalid states unrepresentable;
- a runtime guard that throws on invalid state; or
- a narrow suppression documenting layout, compatibility, or measured performance
  constraints.

When several nullable fields activate together, prefer reasoning about one optional
state reference over rechecking relationships among sibling fields. Do not merely
move the same nullable combinations into a bag: use constructor parameters,
mode-specific implementations, or another discriminated representation so an active
object cannot represent a partial mode. Account for the extra allocation on the rare
path against the smaller owner object and simpler common-path branch.

There is no universally correct rewrite. Preserve layout and hot-path behavior until
evidence supports changing them.

Flow analysis narrows each field or property expression independently. A check on
one member does not prove a sibling member is initialized, even when a runtime state
transition sets both. Capture and narrow each required value once, or move the
shared invariant behind a checked boundary; do not add forgiveness to the sibling.

### `IEnumerator<T>.Current` and protocol state

`IEnumerator<T>.Current` is declared as `T`, while its value is undefined before the
first successful `MoveNext` and after `MoveNext` returns false. `T? Current` or
`[MaybeNull] T Current` can conflict with the inherited nullable contract; compile
the implementation against every shipped reference surface instead of inferring the
contract from a runtime implementation.

Choose deliberately among:

- throwing when the enumerator is not positioned;
- `[AllowNull]` private backing storage while retaining `T Current`;
- a nullable member on a non-interface API; or
- a narrow suppression that names the temporal protocol.

For a reference-type custom enumerator that exposes a nullable reference member,
apply `[MemberNotNullWhen(true, nameof(Current))]` to `MoveNext` when every successful
return establishes a non-null `Current`. Do not use this contract on a mutable struct
enumerator whose non-readonly `MoveNext` establishes that state: invocation through
readonly storage runs on a defensive copy, but flow analysis can still narrow the
original receiver. Test direct dereference inside a successful branch or loop, plus
default, before-first, after-end, reset, mutating, and readonly-receiver states. The
attribute expresses the post-call relation; it does not make `Current` valid outside
that protocol or after another state-changing call.

### A throwing wrapper strengthens a `Try*` contract

When `T Get<T>()` delegates to `TryGet<T>([MaybeNullWhen(false)] out T value)` and
throws on failure, its normal return satisfies the ordinary `T` contract. Do not add
`[return: MaybeNull]` merely because the temporary must hold default on failure. Use
an explicit method type with nullable temporary storage when inference would otherwise
weaken the type. Let the conditional annotation narrow nullable temporary storage:

```csharp
if (!TryGet<T>(out T? value))
{
    throw new InvalidOperationException();
}

return value;
```

Compile both a non-nullable reference consumer and a nullable value-type consumer.

### Intentional invalid-null tests

Preserve the negative test. Do not weaken the production signature to make test code
compile quietly. Prefer the repository's narrow test policy, for example a local
compiler-warning suppression around a plain `null` argument with a reason. A helper
that manufactures null can centralize policy, but it also hides the test input and
must not leak into production code.

### Generated, vendored, or faithfully ported code

Use the repository's provenance-based directory policy. Do not infer generated or
vendored status from a file name alone, and do not restyle a faithful port merely to
satisfy local house style. Scope the diagnostic off through configuration when the
repository intentionally preserves upstream source.

## Misleading substitutions

Reject these as automatic fixes:

- `value!` -> `value ?? throw` without a non-null runtime contract;
- `default!` -> `Array.Clear` while claiming the array is now null-safe;
- `default!` -> `[AllowNull]` without auditing every read;
- `null!` -> an opaque helper that still returns null as non-null;
- adding `Debug.Assert` as the only release-mode guarantee;
- changing a public return to nullable without checking consumers;
- disabling nullable analysis for the whole file/project; or
- retaining `!` with a comment that merely restates "not null" instead of naming
  the invariant and owner.

## When retaining a suppression is acceptable

Retain or replace it with a narrow pragma only when all are true:

1. the runtime contract and owner are named;
2. ordinary flow and nullable annotations cannot express that contract adequately;
3. representation, compatibility, or measured performance blocks the truthful
   structural alternative;
4. the scope is the smallest practical one; and
5. the nearest available test or authoritative contract anchors the assumption and
   records any untestable limitation.

When required evidence is unavailable, leave the existing suppression unchanged and
report the site as unresolved. Do not present retention as the selected remedy until
the conditions above are met.
