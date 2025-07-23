# CoPilot instructions for this repository

# Coding
- Use the latest C# features (C# 13) where applicable.
- Always use C# keywords for types (e.g., `int`, `string`, `bool`) instead of their aliases (e.g., `Int32`, `String`, `Boolean`).
- Always use `nint` and `nuint` for native integer types (not `IntPtr` and `UIntPtr`).
- Avoid using `var` - always use explicit type declarations.
- Use target-typed `new` expressions where applicable (e.g., `List<string> list = new()` instead of `var list = new List<string>()`).
- When instantiating objects, prefer `TypeName instance = new()` over `var instance = new TypeName()`.
- Use `is null` and `is not null` for null checks instead of `== null` or `!= null`.
- Use the following header for all C# files:
```c#
// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information
```

# Comments
- Avoid putting comments at the end of lines.
- Comments should be before the code they describe, or inside blocks to describe the condition the block is handling.
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
- Use `<inheritdoc/>` and `<inheritdoc cref="..."/>` to inherit documentation from base classes, interfaces where applicable.
- For method overloads, use `<inheritdoc cref="MethodName"/>` to inherit documentation from the method with the most arguments,
  overriding tags where they differ.
- Use see langword tags for language keywords in comments (e.g. `<see langword='true'/>` instead of `true`).

# Line breaks and white space
- Never put multiple statements on a single line.
- Ensure that there is always a single blank line between methods and properties.
- Ensure line formatting is correct when making changes to existing code.
- Preserve existing spaces and line breaks when making edits, except when fixing whitespace issues.
- Indents are 4 spaces for all code except for XML (including XML comments in sources), which should have nested tags
  and content indented by a space for each level.
- Lines should never end in or contain only spaces or tabs.
- Lines should be broken before 120 characters if they would otherwise exceed 150 characters.
- When breaking statements, the next lines should be indented.
- When breaking statements, operators (e.g., `+`, `-`, `&&`, `||`, `or`, `and`) should not be at the end of the previous line, except for `=>`.
- When breaking lines in a method call, all parameters should be indented on their own lines.

# Testing
- Place tests in the 'touki.tests' project.
- Test classes should be put in the same namespace as the class they are testing, with 'Tests' appended to the class name (e.g., `ListBaseTests` for `ListBase`).
- Test methods should be named using the format `MethodName_StateUnderTest_ExpectedBehavior` (e.g., `MoveNext_AtStart_ReturnsTrue`).
- Test methods should be ordered by the method they are testing.
- Cover edge and negative cases.
- Do not add "Arrange, Act, Assert" comments in tests.
- Ref structs can not be used in lambdas, use try/finally blocks to validate error cases.
- Use FluentAssertions for assertions in tests.
- FluentAssertions and Xunit are already global usings, don't add new usings for these to test files.

# General Guidance

- Ensure code is cross-compatible with both .NET 9 and .NET Framework 4.7.2.
- Adhere to the repository's license and copyright.
- PowerShell is the terminal environment for building and testing.
- Check GlobalUsings.cs for global usings and don't add unnecessary usings.
