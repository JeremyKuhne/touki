; Unshipped resource-generator diagnostics
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules
Rule ID | Category | Severity | Notes
--------|----------|----------|-------
TOUKIRESX0001 | Touki.Resources | Warning | Resource entry is not a string
TOUKIRESX0002 | Touki.Resources | Error | Resource generator option is not supported
TOUKIRESX0003 | Touki.Resources | Error | Resource file cannot be generated
TOUKIRESX0004 | Touki.Resources | Error | Resource member name conflicts with another generated member
TOUKIRESX0005 | Touki.Resources | Error | Multiple resource files generate the same class