; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md
; TOUKI0028 behavior fix: do not recurse into a conditional-access expression
; when resolving its own previous formatting node. Descriptor metadata is unchanged.

### New Rules
Rule ID | Category | Severity | Notes
--------|----------|----------|-------
TOUKI0042 | Maintainability | Disabled | Use one statement per line
TOUKI0043 | Maintainability | Disabled | Format parameter and argument lists
