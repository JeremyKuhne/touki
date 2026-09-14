# Evaluation cases

Use these cases while incubating the skill. They test routing and decision quality,
not only whether an agent can produce compiling code.

## Routing

### Should invoke

- "Remove the null-forgiving operators from this C# class."
- "Fix the diagnostic that bans null forgiveness without changing public behavior."
- "MA0191 flags `default!`; determine the right replacement."
- "Deleting this `!` gives CS8602. Fix the underlying contract."
- "Audit our `null!` uses before enabling the rule as an error."

### Should not invoke

- "Write a Roslyn analyzer that bans the null-forgiving operator."
  Route to analyzer authoring.
- "Make `MaybeNullWhenAttribute` available on .NET Framework."
  Route to downlevel/polyfill selection.
- "Enable nullable reference types in a new repository."
  Route to repository setup or baseline work.
- "Why did this request throw NullReferenceException?"
  Use ordinary debugging unless a null-forgiving suppression is part of the path.
- "Benchmark `Array.Clear` against a direct generic store."
  Route primarily to performance testing; join this workflow only when removing
  a null-forgiving operator is also requested.
- "Security-review this deserializer's handling of malformed input."
  Route primarily to security review; join this workflow only when a
  null-forgiving assumption is part of the requested audit.

## Outcome cases

### Redundant suppression

Input contains `value!` after compiler-recognized `is not null` flow.

Expected:

- remove `!` as a probe;
- verify nullable warnings are enabled and the expected diagnostic is not suppressed;
- compile and observe no nullable warning;
- do not add a guard or annotation; and
- run the nearest behavior test.

### Direct non-null capture

Input assigns `GetValue()` to a nullable local and immediately checks that local only
to use it inside the non-null branch.

Expected:

- prefer `if (GetValue() is { } value)` so evaluation and narrowing are expressed
  together;
- use a typed pattern when type refinement is also required; and
- retain a separate local when either branch or later control flow needs the value.

### Domain-significant `Try*` helper

Input has a private `bool TryGetNode(..., out Node? node)` that performs a bounded
search and reports whether it found a valid node.

Expected:

- retain the `Try*` contract because the Boolean expresses the operation's intent;
- make the output nullable when failure produces null;
- apply `[NotNullWhen(true)]` after verifying every successful return assigns a
  non-null reference;
- remove redundant caller-side null checks once the compiler recognizes the
  postcondition; and
- do not replace the API with a nullable return solely to enable pattern capture.

### Failure-side output

Input has `bool IsValid(..., out string? failureReason)` and every `false` return
assigns a diagnostic message while every `true` return assigns null.

Expected:

- apply `[NotNullWhen(false)]` to `failureReason` after auditing every return;
- preserve the Boolean API and nullable output; and
- let callers use the reason without a redundant null check after failure.

### Unconstrained generic output

Input has `bool TryGet<T>(out T? value)`, failure assigns default, and success may
return an empty `Nullable<U>` when `T` is `U?`.

Expected:

- reject `[NotNullWhen(true)]` because not every permitted successful result is
  non-null;
- use `[MaybeNullWhen(false)] out T value` when success satisfies the ordinary
  generic `T` contract; and
- retain a nullable declared `T` when null is part of that type argument's contract.

### Generic false contract

Input contains `T Create<T>() where T : class => default!;`.

Expected:

- reject a generic/default exemption;
- identify the false non-null return contract;
- propose `T?`, `[return: MaybeNull]` on `T`, or a real non-null factory according
  to caller semantics; and
- require consumer validation if public.

### Generic `Try*` output

Input assigns `default!` to `out T value` on failure.

Expected:

- capture `CS8601` after removal;
- distinguish "maybe null only on false" from "maybe null even on true";
- distinguish reference null-state from a nullable value type whose `HasValue` is
  `false` on success;
- choose `[MaybeNullWhen(false)]`, nullable output, or another matching contract;
  and
- test both result branches.

### State-discriminated payload

Input uses `_hasValue` and `T _value = default!`.

Expected:

- explain that the compiler cannot generally relate the fields;
- compare `T?`, `[AllowNull]`, representation change, guard/throw, and narrow
  suppression;
- audit all reads before using `[AllowNull]` for inactive private storage;
- preserve public `[AllowNull]` contracts that accept or normalize null on write
  while remaining non-null on read; and
- avoid declaring one universal winner without layout/performance context.

### Correlated optional state

Input has several nullable fields and flags that are inactive together and form a
small set of valid active modes.

Expected:

- consider replacing the sibling fields with one optional state reference;
- make each active mode complete through constructors or mode-specific types rather
  than moving nullable combinations unchanged;
- compare the rare-path state allocation with owner-object size and common-path
  branching; and
- test every mode plus the absent-state path.

### Enumerator protocol

Input resets generic `_current` with `default!` while implementing
`IEnumerator<T>.Current`.

Expected:

- identify the temporal-contract mismatch;
- note that `T? Current` / `[MaybeNull] T Current` can conflict with the inherited
  contract and require a compile check against each reference surface;
- compare runtime guard, private `[AllowNull]` storage, and narrow suppression;
  and
- test default, before-first, active, after-end, and reset states.

For a reference-type custom enumerator with nullable reference `Current`, also expect:

- apply `[MemberNotNullWhen(true, nameof(Current))]` to `MoveNext` only when every
  successful return establishes the member;
- compile a direct `Current` dereference inside a successful branch or loop; and
- retain negative-state tests because the member remains nullable outside the
  protocol.

For a mutable struct enumerator, expect instead:

- reject a receiver-state `[MemberNotNullWhen]` contract because a non-readonly
  `MoveNext` can run on a defensive copy of readonly storage;
- verify the hazard with an `in` or readonly-field consumer; and
- retain explicit narrowing or a checked accessor for `Current`.

### Throwing generic wrapper

Input has `T Get<T>()` calling `TryGet<T>([MaybeNullWhen(false)] out T value)`, then
throwing on failure, but declares `[return: MaybeNull]` to silence the temporary's
warning.

Expected:

- remove `[return: MaybeNull]` because every normal return satisfies ordinary `T`;
- use `TryGet<T>(out T? value)` when explicit type selection and nullable failure
  storage are both needed; and
- compile consumers assigning `Get<string>()` to `string` and retrieving an empty
  nullable value through `Get<int?>()`.

### Intentional invalid-null test

Input calls a non-null production API with `null!` to test validation.

Expected:

- preserve the negative test and production signature;
- prefer the repository's narrow test suppression/helper policy;
- verify the expected exception and parameter name; and
- keep any null-manufacturing helper out of production code.

### Reflection assumption

Input uses `type.GetMethod(name)!`.

Expected:

- decide whether absence is expected or a broken runtime contract;
- use nullable flow for expected absence or `?? throw` with a descriptive message
  for a required member; and
- test the missing-member path.

### Related static initialization

Input initializes several related readonly static fields with `!`, and one
intermediate value is needed only to create the retained values.

Expected:

- consider an explicit static constructor that validates each required lookup;
- keep the intermediate value as a constructor local;
- retain null checks on later operations whose results can independently be null;
- identify the loss of `beforefieldinit` and cached `TypeInitializationException`
  behavior; and
- test first-use initialization on every affected runtime.

### Hot generic slot clear

Input uses `items[index] = default!` in a measured collection path.

Expected:

- recognize that `Array.Clear` avoids the warning without proving element
  nullability;
- preserve semantics and measure relevant runtimes before changing the store; and
- permit a narrow documented suppression when evidence favors the direct store.

### Compiler probe is blocked

Input sits in a project with an unrelated baseline compilation failure, so deleting
`!` cannot expose a trustworthy nullable diagnostic.

Expected:

- restore the reversible probe;
- attempt one narrower project, target, or file-level compile when available;
- stop and report the baseline blocker if the diagnostic still cannot be isolated;
- do not choose a remedy from `default!` or generic syntax alone; and
- identify the command or dependency needed to resume.

## Failure conditions

The skill fails an evaluation if the agent:

- removes all `!` operators through one textual replacement;
- exempts `default!` because it is generic;
- selects `[AllowNull]`, `Array.Clear`, or `?? throw` solely because the build is
  then quiet;
- changes public nullable metadata without identifying consumer impact;
- weakens a production API to make a negative test compile;
- claims an unavailable target or benchmark passed;
- continues remediation after the compiler probe is masked or unavailable without
  explicitly recording that limitation; or
- finishes without reconciling retained and unresolved sites.

## Completion rubric

A successful run:

1. routes correctly;
2. captures the suppressed compiler diagnostic;
3. states the runtime null contract and owner;
4. chooses a remedy that matches that contract;
5. validates the narrow edit before broadening;
6. scales checks for targets, consumers, and performance; and
7. reports unresolved assumptions without disguising them as fixes.
