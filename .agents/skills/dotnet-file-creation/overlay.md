---
core: dotnet-file-creation
core-pin: v0.16.1
---

# Touki overlay - dotnet-file-creation

Repo-specific companion to the vendored [dotnet-file-creation](SKILL.md) skill.
The core and its sibling pages are a **pinned copy of the portable core** from
[JeremyKuhne/agent-skills](https://github.com/JeremyKuhne/agent-skills). Do not
hand-edit them; keep Touki-specific paths and cross-skill links here.

## Touki bindings

- Production library code must compile for both `net10.0` and `net472`. Before
  using a file API introduced after .NET Framework 4.7.2, follow
  [`dotnet-polyfills`](../dotnet-polyfills/SKILL.md) to check for an official
  downlevel package, then [`polyfill-dotnet-api`](../polyfill-dotnet-api/SKILL.md)
  only when a hand-rolled implementation is necessary.
- [`Touki.Io.TempFolder`](../../../touki/Touki/Io/TempFolder.cs) is a
  lifecycle-only temporary-directory helper. Its tests pin creation, conversion,
  deletion, and repeated disposal, but it does not establish exclusive creation
  or owner-only Unix permissions. Do not use it where privacy or hostile local
  races are part of the contract.
- [`TOUKI0032`](../../../docs/analyzers.md#touki0032) enforces `Path.Join` instead
  of `Path.Combine` and selects the correct modern or downlevel API in its code
  fix.

## Cross-references

- [`security-review`](../security-review/SKILL.md) owns traversal, containment,
  and privileged-file-operation risks from untrusted input.
- [`run-tests-on-wsl`](../run-tests-on-wsl/SKILL.md) owns local Linux verification
  when behavior differs between Windows and Unix.

## Updating

Pull upstream changes with `gh skill update dotnet-file-creation` (review the
diff, re-pin). Keep Touki-specific additions in this file, not in the core.
