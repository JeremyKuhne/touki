<!-- DO NOT EDIT. Generated mirror of /AGENTS.md. Edit AGENTS.md and run: pwsh tools/Validate-AgentFiles.ps1 -Fix -->
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

`touki` is a C# library that targets both **.NET 9** and **.NET Framework 4.7.2**. All
code must compile and behave correctly on both targets.

Top-level layout:

- `touki/` &mdash; main library
- `touki.tests/` &mdash; xUnit tests (uses FluentAssertions; access to internals via `InternalsVisibleTo`)
- `touki.testsupport/` &mdash; shared test helpers (`TestAccessor`, etc.)
- `touki.perf/` &mdash; BenchmarkDotNet performance projects
- `sample/` &mdash; usage samples
- `docs/` &mdash; contributor and design documentation

## Environment setup

- On Unix, run `./setup.sh` once after cloning to install the .NET 9 SDK and update PATH.
- On Windows, install the .NET SDK from <https://dotnet.microsoft.com/download> and use
  `dotnet` directly. PowerShell is the preferred terminal.
- Use the `dotnet` CLI for building and testing. CI runs `dotnet build` and `dotnet test`.

## Coding style

- Use the latest C# features (C# 13) where applicable.
- Always use C# keywords for types (`int`, `string`, `bool`) instead of their aliases
  (`Int32`, `String`, `Boolean`).
- Always use `nint` and `nuint` for native integer types (not `IntPtr` and `UIntPtr`).
- Avoid `var` &mdash; always use explicit type declarations.
- Use target-typed `new` expressions where applicable
  (e.g. `List<string> list = new()` instead of `var list = new List<string>()`).
- When instantiating objects, prefer `TypeName instance = new()` over
  `var instance = new TypeName()`.
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
- Use `<see langword=".."/>` for language keywords in comments
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

- Place tests in the `touki.tests` project.
- Test classes should be in the same namespace as the class they are testing, with
  `Tests` appended (e.g. `ListBaseTests` for `ListBase`).
- Test methods should be named `MethodName_StateUnderTest_ExpectedBehavior`
  (e.g. `MoveNext_AtStart_ReturnsTrue`).
- Test methods should be ordered by the method they are testing.
- Cover edge and negative cases.
- Do **not** add "Arrange, Act, Assert" comments in tests.
- Ref structs cannot be used in lambdas; use `try`/`finally` blocks to validate error
  cases.
- Use FluentAssertions for assertions in tests.
- FluentAssertions and Xunit are already global usings &mdash; do not add new usings for
  these to test files.
- Tests have access to internals via `InternalsVisibleTo`, so you can test internal
  members directly.
- For private members, use the `TestAccessor` and `TestAccessors` extension method.

## General guidance

- Ensure code is cross-compatible with both .NET 9 and .NET Framework 4.7.2.
- Adhere to the repository's MIT license and copyright.
- PowerShell is the terminal environment for building and testing.
- Check `GlobalUsings.cs` for global usings and don't add unnecessary usings.
- Write XML documentation for public APIs.

## Path-specific instructions

Additional rules apply to specific file types and live under
[.github/instructions/](.github/instructions/). Tools that support the
`*.instructions.md` format (Copilot cloud agent, Copilot code review, VS Code) load them
automatically based on each file's `applyTo` glob.

Currently:

- [.github/instructions/msbuild.instructions.md](.github/instructions/msbuild.instructions.md) &mdash; rules for `*.csproj`, `*.props`, `*.targets`.
