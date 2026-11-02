; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md
; TOUKI0028 behavior fix: do not recurse into a conditional-access expression
; when resolving its own previous formatting node. Descriptor metadata is unchanged.
; TOUKI0027 behavior fix: require spacing after punctuated closing lines; only
; standalone } and EOF suppress it, retaining control-flow/accessor exceptions.
; TOUKI0028 behavior fix: anchor leading-dot chains to their starting line and
; shift dependent call arguments when adjusting a dot's indentation.

### New Rules
Rule ID | Category | Severity | Notes
--------|----------|----------|-------
TOUKI0042 | Maintainability | Disabled | Use one statement per line
TOUKI0043 | Maintainability | Disabled | Format parameter and argument lists
