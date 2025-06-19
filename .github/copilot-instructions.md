# CoPilot instructions for this repository

# Coding
- Use modern C# features where appropriate.
- Avoid using `var`.
- Use target-typed `new` expressions where applicable.
- Lines should never end in whitespace or contain only whitespace.
- Avoid putting comments at the end of lines. Comments should be before the code they describe.
- Ensure that there is always a single blank line between methods and properties.
- Ensure line formatting is correct when making changes to existing code.
- Preserve existing whitespace when making edits.

# Testing
- Place tests in the 'touki.tests' project.
- Use descriptive test method names.
- Cover edge and negative cases.
- Do not add "Arrange, Act, Assert" comments in tests.
- Ref structs can not be used in lambdas, use try/finally blocks to validate error cases.

# General Guidance
- Create XML comment documentation for public APIs.
- Ensure code is cross-compatible with both .NET 9 and .NET Framework 4.7.2.
- Adhere to the repository's license and copyright.
- PowerShell is the terminal environment for building and testing.
