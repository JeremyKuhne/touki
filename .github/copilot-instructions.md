# CoPilot instructions for this repository

# Coding
- Use modern C# features where appropriate.
- Avoid using `var`.
- Use target-typed `new` expressions where applicable.
- Lines should never end in spaces or contain only spaces.
- Never put multiple statements on a single line.
- Avoid putting comments at the end of lines. Comments should be before the code they describe.
- Ensure that there is always a single blank line between methods and properties.
- Ensure line formatting is correct when making changes to existing code.
- Preserve existing spaces and line breaks when making edits, except when fixing whitespace issues.
- **When making edits, always check for and remove any lines that contain only whitespace characters.**
- **Multi-line method calls and expressions should have proper indentation with each parameter/argument on its own line when appropriate.**
- **When using tools like replace_string_in_file, include sufficient context (3-5 lines) before and after the target string to ensure unique identification.**

# Testing
- Place tests in the 'touki.tests' project.
- Use descriptive test method names.
- Cover edge and negative cases.
- Do not add "Arrange, Act, Assert" comments in tests.
- Ref structs can not be used in lambdas, use try/finally blocks to validate error cases.
- **For enums, always use `Value.Create()` instead of `new Value()` constructor when wrapping enum values.**

# General Guidance
- Create XML comment documentation for public APIs.
- Ensure code is cross-compatible with both .NET 9 and .NET Framework 4.7.2.
- Adhere to the repository's license and copyright.
- PowerShell is the terminal environment for building and testing.
- **When making multiple edits to the same file, verify formatting after each edit and fix any whitespace issues immediately.**
- **Always verify that edits maintain proper blank line spacing between code sections.**