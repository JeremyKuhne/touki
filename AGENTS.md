# Codex Instructions

This repository contains C# code that targets both .NET 9 and .NET Framework 4.7.2. Follow the style and testing rules below when contributing.

## Environment setup
- Run `./setup.sh` once after cloning to install the .NET 9 SDK and update your PATH.
- Use the `dotnet` CLI for building and testing. The CI runs `dotnet build` and `dotnet test`.

## Coding
- Use modern C# features and **avoid `var`**.
- Prefer target‑typed `new` expressions where applicable.
- Do not end lines with spaces or create lines that contain only whitespace.
- Never put multiple statements on the same line and avoid inline end‑of‑line comments.
- Keep a single blank line between methods and properties and preserve existing spacing when editing.
- Check formatting after each change. Remove trailing whitespace and whitespace‑only lines.
- For multi‑line expressions, place each argument on its own line and indent properly.
- When using search‑and‑replace tools, include 3–5 lines of context before and after the target text.

## Testing
- Add tests to the `touki.tests` project with descriptive method names.
- Cover edge and negative cases. Do **not** add "Arrange, Act, Assert" comments.
- Ref structs cannot be used in lambdas; use `try/finally` blocks to check error cases.
- For enums wrapped in `Value`, always call `Value.Create()` instead of `new Value()`.

## General guidance
- Write XML documentation for public APIs.
- Ensure code works on both .NET 9 and .NET Framework 4.7.2.
- PowerShell is the preferred terminal environment for building and testing.
- Adhere to the MIT license included with this repository.
- After multiple edits to a file, verify that blank lines and whitespace remain correct.
