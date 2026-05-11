# AGENTS.md

Instructions for AI coding agents working in this repository. Applies to GitHub Copilot
(VS Code, Visual Studio, CLI, github.com cloud agent), Claude Code, OpenAI Codex, Cursor,
Aider, Gemini CLI, and any other tool that supports the [AGENTS.md](https://agents.md/)
standard.

This file is the single source of truth. `.github/copilot-instructions.md` mirrors this
file for Copilot features that read it directly; do not edit the mirror by hand.

For broader contributor guidance, see [CONTRIBUTING.md](CONTRIBUTING.md) and
[docs/coding_guidelines.md](docs/coding_guidelines.md). For how to add or update agent
customizations (skills, prompts, custom agents, path-specific instructions), see
[docs/agent-customization.md](docs/agent-customization.md).

## Project overview

`touki` is a C# library that targets both **.NET 10** and **.NET Framework 4.7.2**.
All code must compile and behave correctly on both targets.

Top-level layout:

- `touki/` &mdash; main library
- `touki.tests/` &mdash; xUnit tests (uses FluentAssertions; access to internals via `InternalsVisibleTo`)
- `touki.testsupport/` &mdash; shared test helpers (`TestAccessor`, etc.)
- `touki.perf/` &mdash; BenchmarkDotNet performance projects
- `sample/` &mdash; usage samples
- `docs/` &mdash; contributor and design documentation

## Environment setup

- On Unix, run `./setup.sh` once after cloning to install the .NET 10 SDK and update PATH.
- On Windows, install the .NET SDK from <https://dotnet.microsoft.com/download> and use
  `dotnet` directly. PowerShell is the preferred terminal.
- Use the `dotnet` CLI for building and testing. CI runs `dotnet build` and `dotnet test`.

## Coding style

- Use the latest C# features (C# 14) where applicable.
- Always use C# keywords for types (`int`, `string`, `bool`) instead of their aliases
  (`Int32`, `String`, `Boolean`).
- Always use `nint` and `nuint` for native integer types (not `IntPtr` and `UIntPtr`).
- Avoid `var` &mdash; always use explicit type declarations.
- Use target-typed `new` expressions where applicable
  (e.g. `List<string> list = new()` instead of `var list = new List<string>()`).
- When instantiating objects, prefer `TypeName instance = new()` over
  `var instance = new TypeName()`.
- Prefer
  [collection expressions](https://learn.microsoft.com/dotnet/csharp/language-reference/operators/collection-expressions)
  for array, list, and span literals (e.g. `int[] values = [1, 2, 3];` and
  `Point[] points = [new(1, 2), new(5, 6)];`) over `new T[] { ... }` or
  `new List<T> { ... }` initializers. Combine with target-typed `new()` for
  element instantiation where the element type is reasonably obvious.
- Use `is null` and `is not null` for null checks instead of `== null` or `!= null`.
- For enums wrapped in `Value`, always call `Value.Create()` instead of `new Value()`.
- Use the following header for all C# files:

```c#
// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information
```

## Comments and XML documentation

- Avoid putting comments at the end of lines.
- Comments should be before the code they describe, or inside blocks to describe the
  condition the block is handling.
- Create XML comment documentation for all public methods, properties, and types.
- Use `<para>` blocks in `<remarks>` to separate different sections of remarks.
- Indent XML comments by one space for each level of nesting:

```c#
/// <summary>
///  This is the summary.
/// </summary>
/// <remarks>
///  <para>
///   This is a remark.
///  </para>
/// </remarks>
```

- Use `<inheritdoc/>` and `<inheritdoc cref="..."/>` to inherit documentation from base
  classes and interfaces where applicable.
- For method overloads, use `<inheritdoc cref="MethodName"/>` to inherit documentation
  from the method with the most arguments, overriding tags where they differ.
- Use `<see langword="..."/>` tags for language keywords in comments
  (e.g. `<see langword="true"/>` instead of `true`).

## Line breaks and whitespace

- Never put multiple statements on a single line.
- Ensure there is always a single blank line between methods and properties.
- Preserve existing spaces and line breaks when making edits, except when fixing
  whitespace issues.
- Indents are 4 spaces for all code except for XML (including XML comments in sources),
  which should have nested tags and content indented by one space per level.
- Lines should never end in or contain only spaces or tabs.
- Lines should be broken before 120 characters if they would otherwise exceed 150
  characters.
- When breaking statements, the next lines should be indented.
- When breaking statements, operators (`+`, `-`, `&&`, `||`, `or`, `and`) should not be
  at the end of the previous line, except for `=>`.
- When breaking lines in a method call, all parameters should be indented on their own
  lines.
- After multiple edits to a file, verify that blank lines and whitespace remain correct.
- When using search-and-replace tools, include 3&ndash;5 lines of context before and
  after the target text.

## Testing

Detailed test conventions live in
[.github/instructions/tests.instructions.md](.github/instructions/tests.instructions.md)
(applies to `touki.tests/**/*.cs`). Headline rules: place tests in `touki.tests`;
name them `MethodName_StateUnderTest_ExpectedBehavior`; use FluentAssertions (global
using); access internals directly via `InternalsVisibleTo` and private members via
`TestAccessor`; ref structs can't be used in lambdas &mdash; use `try`/`finally`.

Performance-test conventions for `touki.perf/` (BenchmarkDotNet, Release-only,
JIT-naming rule, regression thresholds) live in
[.github/instructions/perf.instructions.md](.github/instructions/perf.instructions.md).

## Working with the user on changes

These rules exist because they have been violated and cost a review round-trip.
Treat them as hard requirements.

- **Never `git commit` or `git push` without explicit user approval, every
  time.** No exceptions. This applies to: opening the initial PR, fixing a
  failing build/test/CI run, addressing PR review comments, follow-up
  cleanup, "obvious" small changes, and anything else. After making
  changes, describe what you did and stop. Wait for the user to say
  "commit", "push", "ship it", or an equivalent **publishing verb**.

  Phrasings that are **not** approval (recurring violations on this repo):
  - "open a PR" / "make a PR" / "create the PR" &mdash; these authorize the
    work, not the publish. Stage and propose a commit message, then stop.
  - "address the review comments" / "fix the comments" / "reply to the
    comments" &mdash; edit-only.
  - "do the next step" / "finish the rollout" / "go ahead to the next
    thing" / a bare "go ahead" attached to a task description &mdash;
    selects which task to work on, not whether to publish it.
  - "fix the CI failure" &mdash; edit-only.

  When in doubt, it is **not** approval. Ask one short yes/no question.
  Do not stack follow-up commits trying to "make CI green" or "finish
  the review pass".
- **Stage by path, never `git add -A`/`git add .`** when the working tree spans
  more than one logical change set. Run `git status --short` first; if topics
  are intermingled, ask how to split before staging.
- **Run `dotnet test -c Release` before declaring a fix done.** Release-mode
  inlining surfaces bugs Debug doesn't &mdash; `Unsafe.As` on a method
  parameter is a known foot-gun on net481 RyuJIT.

The JIT-naming rule for performance claims (".NET Framework 4.8.1 RyuJIT" vs
"modern .NET RyuJIT") and BenchmarkDotNet conventions live in
[.github/instructions/perf.instructions.md](.github/instructions/perf.instructions.md).

### Enforcement

The publish-boundary rule above is also enforced mechanically so that model
rationalization cannot bypass it:

- **VS Code Copilot agent mode.** [.vscode/settings.json](.vscode/settings.json)
  configures `chat.tools.terminal.autoApprove` to deny `git commit`, `git push`,
  `git reset --hard`, `git rebase`, `git merge`, `git cherry-pick`, `git tag`,
  destructive `git branch -d/-D`, and `gh pr create|merge|close|edit`. Each
  invocation requires an explicit in-chat **Allow** click; that click is the
  approval. Routine read-only verbs (`git status`, `git diff`, `git log`,
  `git show`, `git branch`, `git stash list`, `git rev-parse`, `git ls-files`)
  are auto-approved so review work stays frictionless.
- **Branch protection on `main`.** Configured in repo settings: PR required,
  status checks required, force pushes and branch deletion blocked. This covers
  surfaces the local denylist does not (Copilot cloud agent, other agents,
  scripted runs).

The prose rules above still apply: they explain *why*, and they apply on
surfaces where the mechanical gate doesn't fire (e.g. cloud-agent runs that
bypass the local terminal). Treat the mechanical gate as a backstop, not a
license to skip the conversation.

## General guidance

- Ensure code is cross-compatible with both .NET 10 and .NET Framework 4.7.2.
- Check `GlobalUsings.cs` for global usings and don't add unnecessary usings.
- **Prefer `Touki.EnumExtensions` over hand-written enum bitwise code** in
  production library code. `AreFlagsSet`, `AreAnyFlagsSet`,
  `IsOnlyOneFlagSet`, `SetFlags`, `ClearFlags` inline to the same
  instructions as `&`/`|`/`==` on both TFMs and avoid the `Enum.HasFlag`
  boxing penalty on net472/net481 (~20&times; faster, zero alloc).
- When adding a polyfill for a modern .NET API on .NET Framework, follow the
  [`polyfill-dotnet-api`](.agents/skills/polyfill-dotnet-api/SKILL.md) skill:
  prefer Microsoft-shipped packages (`System.Memory`, `Microsoft.Bcl.*`,
  `Microsoft.IO.Redist`), then PolySharp source-gen for compiler attributes,
  and only hand-roll under `touki/Framework/Polyfills/<BclNamespace>/` as a
  last resort. Hand-rolled polyfills declare the BCL namespace they're
  polyfilling (`Polyfills/System/Foo.cs` &rarr; `namespace System;`,
  `Polyfills/System.Text/Foo.cs` &rarr; `namespace System.Text;`).
  Touki-specific code that does not polyfill a modern .NET API stays under
  `touki/Framework/Touki/...` with `Touki.*` namespaces. See
  [docs/polyfill-layout.md](docs/polyfill-layout.md) for the user-facing
  description and the `extern alias` recipe for type-name conflicts.

## Path-specific instructions

Additional rules apply to specific file types and live under
[.github/instructions/](.github/instructions/). Tools that support the
`*.instructions.md` format (Copilot cloud agent, Copilot code review, VS Code) load them
automatically based on each file's `applyTo` glob.

Currently:

- [.github/instructions/msbuild.instructions.md](.github/instructions/msbuild.instructions.md) &mdash; rules for `*.csproj`, `*.props`, `*.targets`.
- [.github/instructions/tests.instructions.md](.github/instructions/tests.instructions.md) &mdash; conventions for `touki.tests/**/*.cs`.
- [.github/instructions/perf.instructions.md](.github/instructions/perf.instructions.md) &mdash; BenchmarkDotNet conventions and the JIT-naming rule for `touki.perf/**/*.cs`.
- [.github/instructions/polyfills.instructions.md](.github/instructions/polyfills.instructions.md) &mdash; the two non-negotiable rules for `touki/Framework/Polyfills/**/*.cs`.
