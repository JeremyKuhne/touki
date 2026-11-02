; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md
; TOUKI0028 behavior fix: do not recurse into a conditional-access expression
; when resolving its own previous formatting node. Descriptor metadata is unchanged.
; TOUKI0027 behavior fix: space punctuated closing lines only before same-indent
; content and preserve existing blank lines before differently indented content. Retain
; control-flow/accessor adjacency and expand paired branches consistently.
; TOUKI0042 behavior fix: use the statement body beneath labels as the
; indentation anchor instead of an outdented label.
; TOUKI0028 behavior fix: anchor leading-dot chains to their starting line and
; shift dependent call arguments and nested operators when adjusting a dot's
; indentation, including calls whose first argument stays on the dot's line.

### New Rules
Rule ID | Category | Severity | Notes
--------|----------|----------|-------
TOUKI0042 | Maintainability | Disabled | Use one statement per line
TOUKI0043 | Maintainability | Disabled | Format parameter and argument lists
