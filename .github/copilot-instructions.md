# CoPilot instructions for this repository

# Coding
- Use modern C# features where appropriate.
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

# Line breaks and white space
- Never put multiple statements on a single line.
- Ensure that there is always a single blank line between methods and properties.
- Ensure line formatting is correct when making changes to existing code.
- Preserve existing spaces and line breaks when making edits, except when fixing whitespace issues.
- Indents are 4 spaces for all code except for XML, which should be indented by one space.
- Lines should never end in or contain only spaces or tabs.
- Lines should be broken at 80 characters if they would otherwise exceed 120 characters.
- When breaking lines, next lines should be indented.
- When breaking lines, operators (e.g., `+`, `-`, `&&`, `||`, `or`, `and`) should not be at the end of the previous line, except for `=>`.
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

# General Guidance

- Ensure code is cross-compatible with both .NET 9 and .NET Framework 4.7.2.
- Adhere to the repository's license and copyright.
- PowerShell is the terminal environment for building and testing.
