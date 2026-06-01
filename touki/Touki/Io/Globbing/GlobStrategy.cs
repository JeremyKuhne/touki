// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Internal base type for compiled glob matching strategies. A strategy is the
///  immutable, root-independent output of <see cref="GlobSpecification.Compile(StringSegment, GlobDialect, GlobOptions, GlobPathSeparator, int)"/>;
///  it owns the encoded pattern (bytecode or specialized state) and exposes the
///  two-span <see cref="MatchCore"/> dispatch used by both the flat-string
///  <see cref="GlobSpecification.IsMatch(ReadOnlySpan{char})"/> entry point and the
///  per-enumeration <see cref="GlobMatch"/> wrapper.
/// </summary>
/// <remarks>
///  <para>
///   Strategies hold no per-enumeration state; one compiled strategy can drive any
///   number of concurrent <see cref="GlobMatch"/> wrappers, each bound to its own
///   root directory.
///  </para>
///  <para>
///   The pattern-level flags (<see cref="Negated"/>, <see cref="RootAnchored"/>,
///   <see cref="DirectoryOnly"/>, <see cref="CoalesceInputSeparators"/>,
///   <see cref="DisallowEmptyInput"/>) are stamped on the strategy by the factory
///   and consumed by <see cref="GlobSpecification"/> / <see cref="GlobMatch"/>; they
///   do not affect <see cref="MatchCore"/> directly.
///  </para>
/// </remarks>
internal abstract class GlobStrategy : DisposableBase
{
    private protected GlobStrategy(GlobDialect dialect, GlobOptions options)
    {
        Dialect = dialect;
        Options = options;
        IgnoreCaseKind = dialect.DefaultIgnoreCaseKind(options);
        MatchLeadingDot = (options & GlobOptions.MatchLeadingDot) != 0
            || dialect.MatchesLeadingDotByDefault();
        Separator = dialect.IsPathAware() ? dialect.DefaultSeparator() : '\0';
    }

    /// <summary>
    ///  The dialect this strategy was compiled with.
    /// </summary>
    public GlobDialect Dialect { get; }

    /// <summary>
    ///  The options this strategy was compiled with.
    /// </summary>
    public GlobOptions Options { get; }

    /// <summary>
    ///  The case-fold rule the strategy dispatches to when comparing characters.
    /// </summary>
    internal IgnoreCaseKind IgnoreCaseKind { get; }

    /// <summary>
    ///  <see langword="true"/> when a leading <c>.</c> in the input may be matched by a
    ///  wildcard (<c>?</c>, <c>*</c>, character class).
    /// </summary>
    private protected bool MatchLeadingDot { get; }

    /// <summary>
    ///  The path separator character for path-aware matching, or <c>'\0'</c> when the
    ///  dialect is path-unaware.
    /// </summary>
    public char Separator { get; init; }

    /// <summary>
    ///  <see langword="true"/> when the compiled pattern began with a <c>!</c> negation
    ///  marker (gitignore-style). <see cref="GlobSpecification.IsMatch"/> inverts the
    ///  result of <see cref="MatchCore"/> when this is set.
    /// </summary>
    public bool Negated { get; init; }

    /// <summary>
    ///  <see langword="true"/> when the compiled pattern began with a leading <c>/</c>
    ///  (gitignore-style root anchor).
    /// </summary>
    public bool RootAnchored { get; init; }

    /// <summary>
    ///  <see langword="true"/> when the compiled pattern ended with a trailing <c>/</c>
    ///  (gitignore-style &quot;directory only&quot;).
    /// </summary>
    public bool DirectoryOnly { get; init; }

    /// <summary>
    ///  <see langword="true"/> when runs of two or more <see cref="Separator"/>
    ///  characters in the input must be coalesced to a single separator before the
    ///  match runs.
    /// </summary>
    public bool CoalesceInputSeparators { get; init; }

    /// <summary>
    ///  <see langword="true"/> when an empty input span is never matchable by the
    ///  compiled pattern, regardless of the pattern's wildcard content.
    /// </summary>
    public bool DisallowEmptyInput { get; internal set; }

    /// <summary>
    ///  The leading separator-bounded literal prefix of the compiled pattern. Empty
    ///  when the strategy is path-unaware or the pattern starts with a wildcard.
    /// </summary>
    internal virtual string LiteralPathPrefix => string.Empty;

    /// <summary>
    ///  <see langword="true"/> when the compiled program contains at least one
    ///  extglob negation (<c>!(...)</c>) construct. Captured at compile time so the
    ///  per-directory hot path can gate the (relatively expensive) directory-mode
    ///  evaluation in <see cref="MatchDirectory"/> behind a single field load: a
    ///  pattern with no negation can never prune a subtree this way.
    /// </summary>
    internal virtual bool HasNegation => false;

    /// <summary>
    ///  Tests whether the logical concatenation
    ///  <paramref name="directoryPrefix"/> + <paramref name="fileName"/> matches the
    ///  compiled pattern. When <paramref name="directoryPrefix"/> is non-empty it
    ///  ends with <see cref="Separator"/>.
    /// </summary>
    internal abstract bool MatchCore(
        ReadOnlySpan<char> directoryPrefix,
        ReadOnlySpan<char> fileName);

    /// <summary>
    ///  Evaluates the compiled pattern against a candidate directory path
    ///  (<paramref name="directoryPrefix"/> + <paramref name="directoryName"/>),
    ///  asking whether the subtree rooted at that directory can be pruned.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Returns <see cref="MatchOutcome.Negative"/> only when the pattern provably
    ///   excludes the directory - an anchored negation rejects one of its segments,
    ///   so no descendant can match and the enumerator may skip the whole subtree.
    ///   Returns <see cref="MatchOutcome.None"/> for a viable prefix (keep
    ///   descending) and <see cref="MatchOutcome.Positive"/> when the directory path
    ///   itself is a complete match. The default never prunes; only strategies that
    ///   carry a negation (see <see cref="HasNegation"/>) override it.
    ///  </para>
    ///  <para>
    ///   The contract is conservative: a directory with any matching descendant is
    ///   never reported <see cref="MatchOutcome.Negative"/>, so a wrong answer can
    ///   only forgo a pruning opportunity, never skip a file that should match.
    ///  </para>
    ///  <para>
    ///   Pruning keys off a negation that <em>fails</em> for the directory's own
    ///   segment, not off one that matches. The sign is inverted: a directory whose
    ///   name satisfies the <c>!(...)</c> group is allowed (keep descending); one
    ///   whose name is excluded by the group is the only candidate for pruning.
    ///   Worked examples (root <c>R</c>, MSBuild dialect with extglob):
    ///  </para>
    ///  <para>
    ///   <c>!(bin|obj)/**/*.cs</c> - the negation is anchored to the first segment.
    ///   <c>R/src</c> and <c>R/binx</c> satisfy <c>!(bin|obj)</c> (their names are
    ///   neither <c>bin</c> nor <c>obj</c>), so both are descended and files such as
    ///   <c>src/a.cs</c> or <c>src/bin/d.cs</c> match. <c>R/bin</c> and <c>R/obj</c>
    ///   fail the group, so the whole subtree is pruned and <c>bin/Release/bin.cs</c>
    ///   never enumerates. A nested <c>src/bin</c> is <em>not</em> pruned: only the
    ///   first segment is constrained.
    ///  </para>
    ///  <para>
    ///   <c>**/!(bin)/*.cs</c> - the negation floats behind a globstar, so it is not
    ///   anchored to any fixed segment. The root <c>R/bin</c> must <em>not</em> be
    ///   pruned: <c>bin/Release/bin.cs</c> matches because its immediate parent
    ///   (<c>Release</c>) satisfies <c>!(bin)</c>. Pruning a floating negation by the
    ///   directory's own name would wrongly drop that file.
    ///  </para>
    ///  <para>
    ///   <c>src/!(bin)/**/*.cs</c> - the negation is anchored to the second segment
    ///   under a literal <c>src</c> prefix. <c>src/bin</c> fails the group and is
    ///   pruned; <c>src/lib</c> and <c>src/nested</c> satisfy it and are descended.
    ///   The root <c>R/bin</c> is pruned by the literal <c>src</c> prefix, not by the
    ///   negation.
    ///  </para>
    /// </remarks>
    internal virtual MatchOutcome MatchDirectory(
        ReadOnlySpan<char> directoryPrefix,
        ReadOnlySpan<char> directoryName) => MatchOutcome.None;

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        // No allocations or rentals to release.
    }
}
