# C# nullability-remediation skill plan

## Implementation status

As of 2026-09-12:

- Phases 1-3 are complete. Touki's repository-wide remediation exercised and
  hardened the portable workflow across direct flow, generic contracts,
  conditional attributes, constructor invariants, correlated state, public
  metadata, protocol state, and performance-sensitive storage.
- Phase 4 is complete upstream. Agent-skills PR #85 merged as commit
  `27ab2e08a7e564181169135fffbc5f0c200d82c7` with the portable four-file core,
  catalog/routing updates, and deterministic contract tests.
- Commons validation on the merged candidate passes: Markdown lint reports 0
  issues across 174 files; all 25 cores pass `skills-ref` 0.1.5 and the strict
  portfolio validator; the catalog, agent mirror, and 151-file link check are
  current; and Pester reports 401 passed, 11 skipped, and 0 failed across 16
  shards.
- Distribution canaries pass: plugin smoke installs 25 skills, 2 agents, and 1
  MCP configuration; the synthetic consumer installs 25 skills and 24 overlays
  with no broken links or source-worktree change; and the stable library/MSTest
  scaffold builds, tests, and packs.
- Manual `gpt-5.4` evaluation ran against the shared create-PR scenarios. The
  report-only calibration passed 8 of 8 runs. The three-run baseline passed 23
  of 24 quality runs and all 24 safety checks, with no infrastructure failures;
  the current suite has no nullability-specific model scenario.
- Phase 5 is complete using the immutable merge commit rather than waiting for a
  release tag. Touki's overlay and generated provenance record that commit and
  its exact skill tree.

## Decision

Vendor the portable core from immutable `agent-skills` merge commit
`27ab2e08a7e564181169135fffbc5f0c200d82c7` while keeping Touki-specific bindings
in `overlay.md`. A future release may replace the commit pin after the overlay is
reviewed against that release; no release tag is required for this adoption.

## Goal

Give agents a repeatable way to remove or audit C# null-forgiving operators
without replacing one suppression with another unproven contract. The workflow
must discover what compiler warning `!` suppresses, identify who owns the null
contract, choose the smallest truthful remedy, and validate behavior and public
annotations on every affected target framework.

The skill should handle requests such as:

- "Remove the null-forgiving operators from this class."
- "Fix TOUKI0005 or MA0191."
- "Deleting `default!` produces CS8601; what should replace it?"
- "Audit these suppressions before we enable a rule that bans `!`."

It should not own authoring the analyzer that reports `!`; that remains the
[`roslyn-analyzers`](../.agents/skills/roslyn-analyzers/SKILL.md) workflow. It
should hand downlevel attribute availability to
[`dotnet-polyfills`](../.agents/skills/dotnet-polyfills/SKILL.md) and measured
hot-path tradeoffs to
[`performance-testing`](../.agents/skills/performance-testing/SKILL.md).

## Evidence

The initial decision model comes from
[the generic `default!` assessment](null-forgiving-generic-assessment.md). That
investigation found real limits in nullable flow analysis, but no general
generic exception that safely identifies unavoidable suppressions.

The focused assessment established these lessons:

- Generic syntax does not establish that `default!` is safe.
- Remove one `!` and compile before selecting a remedy; the suppressed compiler
  diagnostic is controlling evidence.
- `T?`, `[MaybeNull]`, and `[MaybeNullWhen]` express many generic result
  contracts.
- `[AllowNull]` and `Array.Clear` can avoid warnings while moving or obscuring
  the trust boundary; they are not proof of safety.
- State-discriminated storage and `IEnumerator<T>.Current` expose real analysis
  gaps that require local design judgment.
- Public nullable annotations can affect warning-as-error consumers even when
  the CLR signature is unchanged.

Broader repository triage and existing engineering policy add these workflow
requirements; they are not findings from the 30-site generic assessment:

- Reflection, deserialization, and external API assumptions usually need an
  explicit check or exception rather than forgiveness.
- Intentional invalid-null tests and framework-initialized fields need a
  documented local policy, not production-contract distortion.

## Prototype layout

The first Touki version adds:

```text
.agents/skills/csharp-nullability-remediation/
  SKILL.md
  remediation.md
  verification.md
  evaluations.md
  overlay.md
```

- `SKILL.md` owns routing, non-negotiable rules, and the short execution loop.
- `remediation.md` maps compiler diagnostics and code shapes to truthful
  alternatives and records misleading substitutions.
- `verification.md` scales validation for private implementation changes,
  public nullable metadata, cross-target code, tests, and hot paths.
- `evaluations.md` defines should-invoke, should-not-invoke, and outcome cases.
- `overlay.md` binds the portable workflow to Touki's targets, commands,
  conventions, and evidence.

The repository skill catalog must list the prototype as `portable`. The plan and
overlay must identify its local incubation status so the missing upstream
provenance is intentional rather than silent drift.

## Workflow to prototype

1. Establish the exact project, target frameworks, nullable context, and source
   scope. Exclude generated and vendored code only through existing repository
   policy.
2. Inventory suppression syntax without treating textual `!` matches as
   diagnostics.
3. Select one representative occurrence, remove `!` as a reversible probe, and
   compile the narrowest affected target.
  If the compiler is unavailable or a baseline failure masks the nullable
  diagnostic, restore the probe, try one narrower command, and stop with the
  blocker rather than inferring a remedy.
4. Record the emitted compiler diagnostic, natural and converted types, runtime
   null possibility, and contract owner.
5. Classify the site: redundant suppression, missing flow guard, inaccurate
   API annotation, failed `Try*` output, external initialization,
   state-dependent storage, protocol mismatch, intentional invalid input, or
   unchecked external assumption.
6. Apply the smallest remedy that makes the contract more truthful. Do not
   optimize for a warning-free build alone.
7. Immediately rerun the focused compiler or behavior check. Expand to public
   API, cross-target, or performance validation when the change crosses those
   boundaries.
8. Repeat by category rather than bulk-replacing syntax.
9. Report a ledger of changed, retained, and unresolved sites with evidence and
   remaining compatibility or performance risk.

## Evaluation corpus

The prototype must be exercised against at least these cases:

| Case | Expected outcome |
| --- | --- |
| `value!` after compiler-recognized flow narrowing | Remove it and prove no warning remains. |
| Nullable dereference with no guard | Add a truthful guard or propagate nullability; do not merely move `!`. |
| Generic return using `default!` | Evaluate `T?` or `[MaybeNull]`; do not exempt it because it is generic. |
| Failed generic `Try*` output | Match `[MaybeNullWhen]` or nullable output to the actual success contract. |
| `_hasValue` plus generic payload field | Compare nullable storage, `[AllowNull]`, representation changes, and a narrow suppression. |
| `IEnumerator<T>.Current` outside a valid position | Recognize the interface's temporal-contract gap and avoid claiming a perfect annotation. |
| Reflection lookup followed by `!` | Add a descriptive failure path unless the API contract proves non-null. |
| Framework-initialized property | Check constructor, `required`, nullable storage, and framework-specific annotations before suppression. |
| Test intentionally passing null to a non-null parameter | Preserve the negative test and production API; apply narrow test policy. |
| Hot generic slot clear | Require measurement before replacing a direct store with an opaque helper. |
| Public nullable annotation change | Check consumer-facing warning behavior on all shipped targets. |

Routing near-misses must include:

- "Write an analyzer that bans `!`." -> `roslyn-analyzers`.
- "Make `[MaybeNullWhen]` available on .NET Framework." ->
  `dotnet-polyfills`.
- "Enable nullable reference types across a new repository." -> the repository
  baseline/setup workflow, not this focused remediation skill.

## Success criteria

The Touki prototype is ready for upstream consideration when:

- its trigger description selects all positive routing cases and rejects the
  near-misses;
- every outcome case reaches a concrete edit, retained-suppression decision, or
  explicit blocker instead of stopping at generic advice;
- it never grants a blanket exception to `default!`, type parameters, tests, or
  generated-looking paths;
- it requires the suppressed compiler diagnostic before a nontrivial remedy;
- it distinguishes a truthful nullable contract from a warning-avoidance trick;
- it validates all affected Touki targets and Release tests;
- public annotation and hot-path changes trigger compatibility or performance
  checks;
- the skill and catalog pass all repository agent-file validators; and
- at least one agent run without the skill and one with it are compared on the
  same representative cases, with observed failure modes recorded.

The local skeleton can exist before these criteria pass. They gate upstream
consideration, not creation of the project-scoped prototype.

## Phases

### Phase 1 - Local skeleton

Create the five prototype files, add the catalog entry and disambiguation, and
run the agent-file validators. The core remains portable; Touki paths and commands
stay in the overlay.

### Phase 2 - Touki dry runs

Exercise the skill on representative sites from the assessment. Keep code changes
out of this phase unless separately requested; the purpose is to test routing,
classification, evidence gathering, and completion behavior.

Completed. The initial comparison covered the public `SinglyLinkedList<T>.Node`
conversion. The later repository-wide run exercised state-discriminated generic
storage, `IEnumerator<T>.Current`, conditional `Try*` contracts, public nullable
metadata, constructor-established invariants, and hot generic slot clearing.

### Phase 3 - Harden from observed failures

Revise the decision matrix only when an agent run exposes a repeatable mistake.
Add an evaluation that would have caught each mistake. Keep uncommon detail out of
`SKILL.md` and in the purpose-named sibling pages.

Completed. Observed failures produced explicit rules and evaluation cases for:

- preserving domain-significant `Try*` APIs instead of replacing them merely to
  enable pattern capture;
- choosing `[NotNullWhen]` and `[MaybeNullWhen]` from actual result-side contracts,
  including successful empty `Nullable<T>` values;
- compiling public and inherited nullable metadata against representative consumers
  and every shipped reference surface; and
- rejecting receiver-state `[MemberNotNullWhen]` promises on non-readonly mutating
  struct methods, where a readonly receiver can mutate a defensive copy while flow
  analysis narrows the original.

### Phase 4 - Publish upstream source

After the local evaluations pass, review the core for Touki-only assumptions,
run technical-writing and skill semantic review, and prepare a commons change.
Do not create a commit, push, or pull request without the corresponding explicit
approval.

Completed through `agent-skills` PR #85. The merged artifact is available at
commit `27ab2e08a7e564181169135fffbc5f0c200d82c7` and contains only the portable
core, catalog/routing updates, the generated portfolio row, and deterministic
tests. Semantic, portability, installed-artifact, plugin, synthetic-consumer,
scaffold, and manual model validation were completed before merge.

### Phase 5 - Re-vendor an immutable commons artifact

Completed using the PR #85 merge commit. The four core files are installed from
the immutable commit with generated `github-*` provenance, `overlay.md` retains
Touki's bindings, and the catalog identifies the installation as a vendored
portable core plus overlay.

## Open questions

- Should the eventual upstream skill remain narrowly about `!`, or also own a
  broader nullable-warning migration when no suppression is present?
- Which public-nullability compatibility check should be portable across
  repositories rather than supplied by an overlay?
- Should intentional invalid-null tests prefer a local rule suppression, a test
  helper, or repository-specific analyzer configuration?
- What performance threshold justifies retaining a direct `default!` store in a
  hot generic collection path?
