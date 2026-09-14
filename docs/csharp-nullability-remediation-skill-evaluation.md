# C# nullability-remediation skill evaluation

## Status

This records the first read-only comparison for the locally incubated
[`csharp-nullability-remediation`](../.agents/skills/csharp-nullability-remediation/SKILL.md)
skill. It is evidence for the
[implementation plan](csharp-nullability-remediation-skill-plan.md), not a claim
that the evaluation corpus is complete.

Date: 2026-09-08

Case: remove `default!` from the public implicit conversion in
[`SinglyLinkedList<T>.Node`](../touki/Touki/Collections/SinglyLinkedList.Node.cs)
while preserving intended behavior and assessing public compatibility.

Both runs were read-only. Neither changed source nor executed the proposed build
and test commands.

## Compared runs

### Baseline

The baseline agent was explicitly told not to read the new skill, plan, or generic
assessment. It inspected the production source and relevant tests.

It correctly recommended:

```csharp
[return: MaybeNull]
public static implicit operator T(Node? node) => node is null ? default : node.Value;
```

It identified that:

- runtime behavior and the CLR signature remain unchanged;
- nullable metadata changes and can produce new warnings for warning-as-error
  consumers;
- reference-type test locals should become nullable; and
- the main project and focused/full tests should be run.

### Skill-guided

The second agent read the core, remediation matrix, verification guide, and Touki
overlay before inspecting the same source and tests.

It reached the same preferred code change, then added:

- a completion ledger naming the site, target frameworks, suppressed `CS8603`,
  runtime contract, proposed remedy, and residual risk;
- an explicit distinction between preserving runtime/binary compatibility and
  preserving the existing optimistic nullable metadata;
- a narrow `CS8603` pragma as the fallback when consumer-warning compatibility is
  a hard requirement, while labeling that fallback as retaining a false contract;
- an actual-site removal probe before relying on the separate generic compiler
  experiment;
- a proposed validation matrix for `net10.0`, `net11.0`, and `net472`, plus the
  test consumer targets and full Release tests; and
- an evidence-not-obtained section covering actual-site diagnostics, emitted
  metadata, external consumers, IL, and performance.

## Comparison

| Criterion | Baseline | Skill-guided |
| --- | --- | --- |
| Correct preferred remedy | Pass | Pass |
| Identifies false public nullable contract | Pass | Pass |
| Separates CLR and nullable-metadata compatibility | Partial | Pass |
| Proposes a nullable-metadata-preserving fallback | No | Pass |
| Requires actual-site diagnostic evidence | No | Pass |
| Plans checks for all Touki library targets | Partial | Pass |
| Produces completion ledger | No | Pass |
| Names unverified evidence | No | Pass |
| Avoids unnecessary performance work | Implicit | Explicit |

The skill improved rigor, workflow closure, and reporting. It did not improve the
core technical diagnosis on this case; the baseline agent already found the correct
annotation from source and tests. Future evaluations need harder cases where the
tempting warning-free rewrite is misleading, especially state-dependent generic
storage, `IEnumerator<T>.Current`, and a hot generic slot clear.

## Observed skill strengths

- The removal-probe rule prevented treating the separate scratch experiment as
  proof of the actual site.
- The compatibility branch prevented "same CLR signature" from being mistaken for
  complete compatibility.
- The ledger forced retained and unresolved risk to remain visible.
- The Touki overlay supplied the missing `net11.0` target and full Release-test
  expectation.
- The performance section correctly avoided demanding a benchmark when only
  compile-time syntax and nullable metadata would change.

## Improvements from this run

No core edit is justified by this single comparison. The new skill already encoded
the behaviors that differentiated the skilled run.

The next comparison should use the state-discriminated payload case. It should test
whether a baseline agent applies `[AllowNull]` merely because compilation becomes
quiet, while the skill-guided agent audits every read and preserves layout and
performance uncertainty.

## Validation status

The skill skeleton and this evaluation were validated locally with portable
PowerShell 7.6.5 from the ignored `artifacts/` directory:

- `tools/Validate-AgentFiles.ps1`: passed;
- `tools/Validate-AgentSkills.ps1`: passed;
- `tools/Test-AgentFileLinks.ps1`: passed; and
- `git diff --check`: passed.

The validators were rerun after this evaluation and the plan status update. A
separate local-link, trailing-whitespace, line-length, and final-newline check also
passed for all three new documents.
