# Review procedure and reporting

Detail for the [security-review](SKILL.md) skill.

## Review procedure

1. **Inventory.** `git status --short` and read every new/modified
   file. Tag each member that takes external input or uses a
   caller-validated API.
2. **Walk each tagged member through the tiered categories** in
   [checklist.md](checklist.md). The test you'd write to prove it's
   fine is the deliverable.
3. **Place tests in `<TypeName>.Security.cs` partial-class files**
   beside the production type. Create one if it doesn't exist.
4. **Add safe-property tests now (principle 1); commit failing /
   wrong-output tests only with the fix.**
5. **If a test fails against current code, stop and report.** Don't
   patch without surfacing first.
6. **Run tests on every TFM.** Timing bounds and allocation behavior
   differ across BCL versions.

## Options report format

When the report contains any finding that requires a production-code
change: output the report and **end your turn**. Don't produce the
patch, don't produce a failing test, don't run further tool calls
beyond what's needed to classify the finding. Resume only after the
user replies with an approval verb (`go`, `apply Option A`, `fix it`,
or similar). Passing safe-property tests (principle 1) may be added
on the same turn; they don't change production behavior.

```text
### Finding #N -- <one-line summary>

<which file(s), which lines, what the failure mode is>
**Severity:** High / Medium / Low (per rubric)

**Option A -- <approach>**
- <change shape, ~LoC>
- Pro: ...
- Con: ...

**Option B -- ...**

**My recommendation:** <one of the above, with rationale.>
```

Low-severity findings can be deferred entirely; Medium/High deferrals
need an explicit reason in the PR body.

## Don'ts

- **Don't patch production code without surfacing the finding first.**
- **Don't pin observable wrong output in a test.** That locks the bug
  in. Either ship the corrected-behavior test *alongside* the fix, or
  skip the test until the fix lands. Safe-property tests (principle 1)
  are not this case.
- **Don't claim "safe by construction" without a regression test.**
  The safe property is invisible to a future refactor.
- **Don't assume `internal` / `InternalsVisibleTo` members are out of
  scope.** Tests reach them; refactors promote them. Audit them on
  the same basis as `public`.
