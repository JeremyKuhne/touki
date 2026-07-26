; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md
;
; Release headings must be numeric, so they name the version line rather than the exact
; package version. The 0.4.0 rules first shipped in the 0.4.0-alpha.1 prerelease, which is
; also when touki.analyzers began packing into KlutzyNinja.Touki. 0.5.0 was the first
; stable release.

## Release 0.4.0

### New Rules
Rule ID | Category | Severity | Notes
--------|----------|----------|-------
TOUKI0001 | Usage | Warning | Use 'is null' / 'is not null' for null comparisons
TOUKI0002 | Reliability | Hidden | Defensive copy of a struct
TOUKI0003 | Reliability | Warning | Defensive copy of a [NonCopyable] struct
TOUKI0004 | Reliability | Warning | By-value copy of a [NonCopyable] struct
TOUKI0010 | Reliability | Warning | [MustDispose] value is not deterministically disposed

## Release 0.5.0

### New Rules
Rule ID | Category | Severity | Notes
--------|----------|----------|-------
TOUKI0020 | Maintainability | Warning | More than one type declared in a file, nested types included
TOUKI0030 | Performance | Warning | StringBuilder allocated to build a string that ValueStringBuilder could build
