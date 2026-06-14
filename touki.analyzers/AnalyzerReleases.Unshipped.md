; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules
Rule ID | Category | Severity | Notes
--------|----------|----------|-------
TOUKI0001 | Usage | Warning | Use 'is null' / 'is not null' for null comparisons
TOUKI0002 | Reliability | Hidden | Defensive copy of a struct
TOUKI0003 | Reliability | Warning | Defensive copy of a [NonCopyable] struct
TOUKI0004 | Reliability | Warning | By-value copy of a [NonCopyable] struct
TOUKI0010 | Reliability | Warning | [MustDispose] value is not deterministically disposed
