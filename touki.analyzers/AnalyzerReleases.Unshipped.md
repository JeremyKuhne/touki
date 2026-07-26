; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md
;
; Suppressions are listed in the same shape as a rule, under a 'Suppression' category, but
; commented out so the release tracking parser skips them. They cannot be tracked for real:
; a '### Suppressions' heading fails with RS2007, and putting a suppression id in the table
; below fails with RS2002, because a DiagnosticSuppressor declares SupportedSuppressions
; rather than SupportedDiagnostics and so is not a supported diagnostic of any analyzer.
; Maintain these rows by hand, and move them with the release when one is cut.
;
; Rule ID | Category | Severity | Notes
; --------|----------|----------|-------
; TOUKISUPPRESS0001 | Suppression | Info | Suppresses IDE1006 for a thread-static field named as TOUKI0040 requires

### New Rules
Rule ID | Category | Severity | Notes
--------|----------|----------|-------
TOUKI0011 | Reliability | Warning | stackalloc larger than the configured maximum byte size
TOUKI0021 | Maintainability | Warning | File name does not match a type declared in the file
TOUKI0040 | Naming | Warning | Thread-static field does not carry the thread-static prefix
