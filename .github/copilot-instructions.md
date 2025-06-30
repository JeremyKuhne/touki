# CoPilot instructions for this repository

# Coding
- Use modern C# features where appropriate.
- Avoid using `var` - always use explicit type declarations.
- Use target-typed `new` expressions where applicable (e.g., `List<string> list = new()` instead of `var list = new List<string>()`).
- When instantiating objects, prefer `TypeName instance = new()` over `var instance = new TypeName()`.
- Use `is null` and `is not null` for null checks instead of `== null` or `!= null`.
- Lines should never end in or contain only spaces or tabs.
- Never put multiple statements on a single line.
- Avoid putting comments at the end of lines. Comments should be before the code they describe.
- Ensure that there is always a single blank line between methods and properties.
- Ensure line formatting is correct when making changes to existing code.
- Preserve existing spaces and line breaks when making edits, except when fixing whitespace issues.
- Use the following header for all C# files:
```c#
// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information
```

# Line Breaks
- Lines should be broken at 80 characters if they would otherwise exceed 120 characters.
- When breaking lines, next lines should be indented.
- When breaking lines, operators (e.g., `+`, `-`, `&&`, `||`, `or`, `and`) should not be at the end of the previous line, except for `=>`.
- When breaking lines in a method call, all parameters should be indented on their own lines.

# Testing
- Place tests in the 'touki.tests' project.
- Use descriptive test method names.
- Cover edge and negative cases.
- Do not add "Arrange, Act, Assert" comments in tests.
- Ref structs can not be used in lambdas, use try/finally blocks to validate error cases.
- Use FluentAssertions for assertions in tests.
- **For enums, always use `Value.Create()` instead of `new Value()` constructor when wrapping enum values.**

# General Guidance
- Indents are 4 spaces for all code except for XML.
- Create XML comment documentation for public APIs.
- Ensure code is cross-compatible with both .NET 9 and .NET Framework 4.7.2.
- Adhere to the repository's license and copyright.
- PowerShell is the terminal environment for building and testing.
- **When making multiple edits to the same file, verify formatting after each edit and fix any whitespace issues immediately.**
- **When using tools like replace_string_in_file, include sufficient context (3-5 lines) before and after the target string to ensure unique identification.**
- **Always verify that edits maintain proper blank line spacing between code sections.**
