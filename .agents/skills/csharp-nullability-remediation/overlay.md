---
core: csharp-nullability-remediation
core-pin: 27ab2e08a7e564181169135fffbc5f0c200d82c7
---

# Touki overlay - C# nullability remediation

The portable core is vendored from `agent-skills` merge commit
`27ab2e08a7e564181169135fffbc5f0c200d82c7` (PR #85). The generated provenance in
`SKILL.md` records that commit and the exact upstream skill tree. Touki-specific
commands, policies, and evidence remain in this overlay. The implementation and
adoption history lives in
[docs/csharp-nullability-remediation-skill-plan.md](../../../docs/csharp-nullability-remediation-skill-plan.md).

## Touki evidence

- The investigation that rejected a blanket generic `default!` exception is
  [docs/null-forgiving-generic-assessment.md](../../../docs/null-forgiving-generic-assessment.md).
- TOUKI0005 was merged through
  [PR #299](https://github.com/JeremyKuhne/touki/pull/299) at this local branch
  point. Keep the skill useful for compiler warnings and other analyzers so the
  portable workflow does not depend on that Touki-specific rule.
- Touki's current guideline permits a null-forgiving operator only with a
  descriptive reason and prefers a structural fix. Read
  [docs/coding_guidelines.md](../../../docs/coding_guidelines.md) before changing
  that policy.

## Build and test bindings

The main library targets `net10.0`, `net11.0`, and `net472`. A change in shared
library source must be checked on all applicable targets:

```pwsh
dotnet build touki/touki.csproj -c Release -f net10.0
dotnet build touki/touki.csproj -c Release -f net11.0
dotnet build touki/touki.csproj -c Release -f net472
dotnet test -c Release
```

Start with the narrow project/target that exposes the warning, then run the full
Release test command before declaring a fix done.

All first-party projects except the bootstrap producer load the full current Touki
analyzer. `touki` loads the analyzer project directly; the other projects load
`touki.analyzers.bootstrap`, which links the complete analyzer source set and avoids
analyzer self-reference cycles. The bootstrap cannot analyze its own output, so its
project rejects bootstrap-owned C# source; linked source remains owned by and analyzed
in `touki.analyzers`. Validate that wiring with a normal solution build, not only a
one-off injected analyzer path.

Tests under `test/touki.tests/**/*.cs` follow
[the test instructions](../../../.github/instructions/tests.instructions.md).
Intentional invalid-null tests must continue to exercise the production validation
path and expected parameter name.

Public structs and ref structs are always default-constructible, even when their
declared constructors establish stronger state. Audit `default(T)` before preserving
a non-null property or conversion. When the default state is invalid, represent that
state explicitly and add a direct default-state test; constructor-only call-site
evidence is insufficient.

## Repository boundaries

- Code under
  [touki/Framework/Polyfills](../../../touki/Framework/Polyfills/) is faithfully
  ported. Prefer a directory-scoped analyzer exemption over restyling upstream
  source.
- Nullable-analysis attributes on `net472` come from PolySharp. Use the
  [`dotnet-polyfills`](../dotnet-polyfills/SKILL.md) workflow before adding or
  hand-writing a missing attribute.
- Follow [`performance-testing`](../performance-testing/SKILL.md) before changing
  a direct generic store, field layout, or branch in a hot path.
- Use [`security-review`](../security-review/SKILL.md) when the assumption crosses
  reflection, deserialization, unsafe code, interop, or untrusted input.
- Rule implementation changes belong to
  [`roslyn-analyzers`](../roslyn-analyzers/SKILL.md), and completed remediation
  changes finish with
  [`pre-pr-self-review`](../pre-pr-self-review/SKILL.md).

## Local completion ledger

For each category, include the project and target framework in the site column and
name the exact build/test command. If a public annotation changes, record how a
consumer was compiled. If a hot-path shape changes, record the runtime and JIT used
for measurement.
