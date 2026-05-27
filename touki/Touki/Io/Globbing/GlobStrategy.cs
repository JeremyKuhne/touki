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
    ///  Tests whether the logical concatenation
    ///  <paramref name="directoryPrefix"/> + <paramref name="fileName"/> matches the
    ///  compiled pattern. When <paramref name="directoryPrefix"/> is non-empty it
    ///  ends with <see cref="Separator"/>.
    /// </summary>
    internal abstract bool MatchCore(
        ReadOnlySpan<char> directoryPrefix,
        ReadOnlySpan<char> fileName);

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        // No allocations or rentals to release.
    }
}
