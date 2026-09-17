---
compatibility: Requires the .NET 10 SDK or later and its C# 14 compiler; target frameworks may be older.
description: Remove or audit C# null-forgiving operators (`!`, `null!`, `default!`) without hiding nullability defects or changing contracts accidentally. Use when asked to "remove null-forgiving operators", "fix a diagnostic that bans null forgiveness", "replace null! or default!", reduce suppressions, or resolve nullable warnings exposed after deleting `!`. Not for authoring the analyzer itself or enabling nullable across a whole repository.
license: MIT
metadata:
    applicability: dotnet
    binding: optional-overlay
    github-path: skills/csharp-nullability-remediation
    github-pinned: 27ab2e08a7e564181169135fffbc5f0c200d82c7
    github-ref: 27ab2e08a7e564181169135fffbc5f0c200d82c7
    github-repo: https://github.com/JeremyKuhne/agent-skills
    github-tree-sha: d9836e032b5124e08698b4c69095efe8a2d230d5
    maturity: experimental
    portability: portable
    related: dotnet-polyfills, performance-testing, pre-pr-self-review, roslyn-analyzers, security-review
    requires: none
    risk: local-write
name: csharp-nullability-remediation
---
# C# nullability remediation

If `overlay.md` exists beside this file, read it before acting; it contains
repository-specific bindings. This core remains usable without it.

Remove null-forgiving operators by repairing or explicitly recording the
underlying nullability contract. A warning-free build is evidence only when the
new code tells the truth about values that can be null.

## Non-negotiable rules

- Do not bulk-replace `!` or infer safety from syntax alone.
- Remove one representative `!` first and compile. Record the exact compiler
  diagnostic it was suppressing before choosing a nontrivial remedy.
- Do not grant blanket exceptions to `default!`, type parameters, tests,
  generated-looking paths, framework initialization, or assertion libraries.
- Preserve runtime behavior and public nullable contracts unless the requested
  fix deliberately changes them. Nullable metadata can break warning-as-error
  consumers even when the CLR signature is unchanged.
- Treat `[AllowNull]`, `Array.Clear`, helper methods, and pragmas as trust-boundary
  choices, not proof that a value is non-null.
- Do not add `?? throw` merely to satisfy analysis. A throw is correct only when
  the runtime contract forbids null at that boundary.
- Keep an intentional suppression narrow and explain the invariant that makes it
  necessary. An explicit unresolved case is better than a misleading fix.

## Workflow

1. **Establish scope.** Record nullable context, effective SDK/compiler, targets, source policy, and public or hot-path exposure.
2. **Inventory real operators.** Prefer compiler/analyzer diagnostics or syntax
   parsing over textual `!` matches. Group sites by code shape and contract owner.
3. **Run a removal probe.** Delete `!` at one representative site and run the
   narrowest compile that reaches it. Capture the warning id, message, natural
   type, converted type, and containing API.
4. **Classify the contract.** Use [remediation.md](remediation.md) to distinguish
   redundant flow, missing guards, inaccurate annotations, external
   initialization, state-dependent storage, protocol gaps, intentional invalid
   input, and unchecked external assumptions.
5. **Choose the smallest truthful remedy.** State whether null is a valid value,
   an invalid value, an inactive representation, or impossible under a checked
   invariant. Make the code and annotations match that answer.
6. **Edit one category.** Change a small representative cluster, preserving
   behavior and local style. Do not mix unrelated nullability models in one pass.
7. **Validate immediately.** Follow [verification.md](verification.md). Rerun the
   focused compiler check before expanding to tests, target frameworks,
   consumers, or benchmarks.
8. **Repeat by category.** Reuse a remedy only when the same contract and warning
   are established, not merely because the source text looks similar.
9. **Close with a ledger.** Report each changed, retained, and unresolved site,
   its suppressed warning, remedy, validation, and remaining compatibility or
   performance risk.

## Stop conditions

Stop and surface the decision instead of editing when:

- the project cannot compile, an unrelated baseline failure masks the probe, or
  no available command isolates the suppressed diagnostic; restore the probe,
  report the blocker, and do not infer a remedy from source shape alone;
- the only candidate changes a public nullable contract without compatibility
  evidence;
- a state or protocol invariant cannot be represented and no local suppression
  policy exists;
- replacing a direct store, branch, or field layout may affect a hot path and no
  measurement is available;
- a framework owns initialization but its lifecycle guarantee is not documented;
  or
- the relevant target framework lacks an attribute and no approved downlevel
  source is known.

## Completion output

Use a compact ledger rather than a count-only summary:

| Site | Suppressed diagnostic | Runtime null contract | Remedy | Validation |
| --- | --- | --- | --- | --- |
| `path:line` | compiler or analyzer ID, or none | valid / invalid / inactive / proven impossible | code or retained suppression | command and result |

Name any sites intentionally left unchanged and the evidence still needed.

## Related workflows

- Use `roslyn-analyzers` when the task is to create or change the diagnostic rule
  itself.
- Use `dotnet-polyfills` when nullable-analysis attributes are missing from an
  older target framework.
- Use `performance-testing` before changing a measured hot path solely to avoid
  a suppression.
- Use `security-review` when nullability assumptions guard unsafe, reflection,
  deserialization, interop, or untrusted-input boundaries.
- Run `pre-pr-self-review` after remediation changes are complete.

The consuming repository binds those skill names and concrete commands in its
overlay.
