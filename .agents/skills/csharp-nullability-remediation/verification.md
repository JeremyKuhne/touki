# Verification for nullability remediation

Validation must prove more than disappearance of the original diagnostic. Scale the
checks to the contract that changed.

## Focused loop

After each representative edit:

1. compile the narrowest project and target framework that reaches the site;
2. confirm each nullable or null-forgiving diagnostic observed by the probe is gone;
3. check for new warnings at callers and implementations; and
4. run the nearest behavior test, including the null or invalid-state branch.

Do not edit another category before this loop can distinguish a correct remedy from
a moved warning.

## Target-framework matrix

Run every target whose reference annotations or compiler inputs differ. A modern
target compiling cleanly does not prove an older target sees the same BCL metadata or
nullable-analysis attributes.

When an attribute is absent downlevel, use the repository's approved package or
source generator. Do not hand-roll nullable-analysis attributes solely for the fix.

## Public nullable contracts

For a public method, property, operator, delegate, or interface implementation:

- identify whether emitted nullable metadata changes;
- compile representative consumers, especially warning-as-error consumers;
- follow newly exposed nullable warnings through callers until the warning chain
  closes rather than treating the first clean declaration as completion;
- test overrides and interface implementations for `CS876x` mismatches;
- preserve success/failure semantics for `Try*` APIs; and
- document intentional compatibility impact.

The CLR signature remaining identical is not sufficient validation.

## Behavior checks by category

| Change | Required behavior evidence |
| --- | --- |
| Added guard or throw | Null path throws the intended exception at the intended boundary; valid path is unchanged. |
| Constructor-established invariant | Every constructor establishes it; valid construction exposes only initialized state. |
| Static-constructor invariant | First use initializes successfully; missing dependencies fail at the intended type boundary. |
| Nullable return/output | Callers observe and handle null; success guarantees match annotations. |
| State-dependent storage | Every read is valid-state guarded; reset/clear/default construction is covered. |
| Enumerator state | Before-first, active, after-end, reset, and default-constructed behavior is covered. |
| Framework initialization | Test the real lifecycle or the closest supported integration boundary. |
| Reflection/external lookup | Missing member/result produces a descriptive controlled failure. |
| Intentional invalid test input | The production null-validation path is still exercised. |

## Hot paths and layout

Do not assume an annotation or helper is free when replacing a direct store, field,
or branch in a measured path.

- Check whether field type/layout changed.
- Inspect generated code or IL when defensive copies, boxing, or generic expansion
  are plausible.
- Benchmark both the original and candidate on each relevant runtime/JIT.
- Keep the existing suppression when the alternative regresses a stated threshold
  and the invariant is otherwise pinned.

Performance evidence must name the runtime, JIT, scenario, and uncertainty. A single
stopwatch result is not enough.

## Final validation

Before calling the remediation complete:

1. rerun the diagnostic inventory and reconcile every changed count;
2. build every affected target in Release;
3. run the repository's required Release tests;
4. run public-consumer or package checks when annotations changed;
5. run performance checks for any accepted hot-path rewrite;
6. verify generated/vendored exclusions are configuration-based and documented; and
7. when a diagnostic analyzer participates, compile a representative violation and confirm each intended project emits the expected diagnostic.

Report commands that could not run and why. Do not convert an unavailable check into
a pass.

## Completion ledger

Record at least:

| Site/category | Before | Suppressed warning | Remedy or retained suppression | Focused check | Broader check | Residual risk |
| --- | --- | --- | --- | --- | --- | --- |

Separate unresolved sites from completed ones. Count reconciliation is useful, but
it does not replace contract evidence.
