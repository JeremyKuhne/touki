---
applyTo: 'touki/Framework/Polyfills/**/*.cs'
---

# Polyfill conventions for `touki/Framework/Polyfills/`

These two rules are non-negotiable. The full workflow lives in the
[`polyfill-dotnet-api`](../../.agents/skills/polyfill-dotnet-api/SKILL.md)
skill; this file restates the rules at the file-pattern level so agents that
don't auto-invoke the skill still pick them up.

## Rule 1: namespace = BCL namespace being polyfilled

Files under `touki/Framework/Polyfills/<BclNamespace>/` declare the BCL
namespace they are polyfilling, **not** a `Touki.*` namespace:

- `Polyfills/System/Foo.cs` &rarr; `namespace System;`
- `Polyfills/System.Text/Foo.cs` &rarr; `namespace System.Text;`
- `Polyfills/System.Buffers/Foo.cs` &rarr; `namespace System.Buffers;`

Touki-specific code that is **not** a polyfill of a modern .NET API stays
under `touki/Framework/Touki/...` with `Touki.*` namespaces.

## Rule 2: no `#if NETFRAMEWORK` inside `touki/Framework/`

Everything under `touki/Framework/` already compiles only for the framework
target. `#if NETFRAMEWORK` inside this tree is dead-code-by-construction and
adds noise. Don't add it. If a polyfill needs to vary by sub-target (rare),
prefer separate files per target with appropriate `<Compile>` conditions in
the project file.

## Source-preference order (summary)

The full preference order with examples is in the
[`polyfill-dotnet-api`](../../.agents/skills/polyfill-dotnet-api/SKILL.md)
skill. Headline:

1. Microsoft-shipped package (`System.Memory`, `Microsoft.Bcl.*`,
   `Microsoft.IO.Redist`).
2. PolySharp source-gen for compiler attributes.
3. Hand-rolled polyfill in this directory - **last resort**, only with
   a real caller. Don't polyfill for completeness.

See [docs/polyfill-layout.md](../../docs/polyfill-layout.md) for the
user-facing description and the `extern alias` recipe for type-name conflicts.
