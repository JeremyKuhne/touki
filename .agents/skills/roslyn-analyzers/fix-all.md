# Designing a correct FixAll provider

Detail for the [roslyn-analyzers](SKILL.md) skill. Read this when a
`CodeFixProvider` supports more than one diagnostic per document, project, or
solution.

## Choose the provider from the edit shape

`WellKnownFixAllProviders.BatchFixer` invokes the ordinary code fix independently
for each diagnostic against a fork of the original document, then merges the
resulting text changes. Use it only when those independent changes are guaranteed
not to conflict.

Do not use `BatchFixer` without further analysis when fixes can:

- rewrite nested or overlapping syntax;
- insert text at the same position;
- run formatting, simplification, or other cleanup that expands the effective
  text-change spans;
- depend on edits made for another diagnostic; or
- replace an ancestor and one of its descendants.

Two zero-length insertions at one position are still ambiguous. A conflict can
discard all text changes produced by one independently computed fix, not merely
the overlapping hunk.

When any of these shapes are possible, implement a custom `FixAllProvider` that
collects the applicable diagnostics for each document and computes one coherent
document transformation.

## Apply one coherent document edit

Use one `SyntaxEditor` for all edits in a document. Resolve diagnostics against a
single syntax root and schedule replacements in an order that preserves node
identity:

1. Filter diagnostics to the selected `CodeActionEquivalenceKey` when the provider
   offers multiple actions for one diagnostic.
2. Resolve and validate every target against the same original root.
3. Order nested edits inner-to-outer. For equal starts, process shorter spans first.
4. Track equivalent annotations or recompute from the editor's current tree when a
   later edit depends on an earlier replacement. Do not retain stale syntax-node
   references across replacements.
5. Run simplification or formatting once on the combined result rather than once
   per diagnostic.

Collection operations can invalidate syntax identity too. In particular, repeated
`SeparatedSyntaxList.Remove(node)` calls against nodes retained from an earlier
list can silently miss later removals. Compute the final list once, use indices or
stable keys, or rebuild it from the retained elements.

Project- and solution-scoped FixAll should use this document transform once per
affected document, then return one accumulated `Solution`.

## Keep action selection stable

Every registered `CodeAction` needs a stable, descriptive `equivalenceKey`. A
custom FixAll provider must apply only the action represented by
`FixAllContext.CodeActionEquivalenceKey`; it must not collapse several semantic
choices into one bulk edit.

If one action preserves semantics and another intentionally changes them, give
them distinct titles and keys. Include `(may change semantics)` in the title of
the semantic-changing action.

## Prove FixAll rather than only the single fix

The FixAll test must contain multiple applicable diagnostics and assert the final
combined source. Cover the conflict shapes the implementation permits:

- sibling edits;
- nested edits;
- same-position insertions;
- edits whose cleanup spans can overlap; and
- multiple actions filtered by equivalence key.

Assert the expected number of FixAll iterations or otherwise instrument the
provider so the test proves it used the intended bulk path. Include a positive
control with at least two independent occurrences: mutating the fixture or
provider to process only one diagnostic must fail the test. A passing single-item
fixture does not distinguish FixAll from an ordinary code fix.
