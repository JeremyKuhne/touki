// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  An immutable, root-independent compiled glob specification.
/// </summary>
/// <remarks>
///  <para>
///   <see cref="GlobSpecification"/> is the output of
///   <see cref="Compile(StringSegment, GlobDialect, GlobOptions, GlobPathSeparator, int)"/>:
///   a thread-safe, allocation-free-to-evaluate parse result that holds the encoded
///   pattern (literal table, opcode program, etc.) and the pattern-level flags.
///   The specification is not bound to any enumeration root and may be reused
///   concurrently against many different roots via <see cref="CreateMatcher(string?)"/>.
///  </para>
///  <para>
///   For one-shot flat-string testing use <see cref="IsMatch(ReadOnlySpan{char})"/>;
///   it does not consult any per-directory cache and is safe to call from multiple
///   threads concurrently. To drive a file-system enumeration via
///   <see cref="MatchEnumerator{TResult}"/>, call <see cref="CreateMatcher"/> to
///   produce a <see cref="GlobMatch"/> bound to the enumeration root - the wrapper
///   owns the per-directory cache and is single-threaded against its
///   <see cref="IEnumerationMatcher"/> entry points.
///  </para>
///  <para>
///   This mirrors the <see cref="MSBuildSpecification"/> / <see cref="MatchMSBuild"/>
///   split: the specification is the value-ish parse output; the matcher binds it to
///   a root and owns mutable enumeration state.
///  </para>
/// </remarks>
public sealed partial class GlobSpecification : DisposableBase
{
    private readonly GlobStrategy _strategy;

    private GlobSpecification(GlobStrategy strategy, StringSegment pattern)
    {
        _strategy = strategy;
        Pattern = pattern;
    }

    /// <summary>
    ///  Compiles the supplied <paramref name="pattern"/> for the specified
    ///  <paramref name="dialect"/> and <paramref name="options"/>, returning a
    ///  <see cref="GlobSpecification"/> wrapping the cheapest implementation strategy
    ///  that can evaluate it. <paramref name="separator"/> overrides the dialect's
    ///  documented default; see <see cref="GlobPathSeparator"/> for the semantics of
    ///  each value (ignored for path-unaware dialects).
    /// </summary>
    /// <param name="pattern">
    ///  The glob pattern. Accepted as a <see cref="StringSegment"/> so callers that
    ///  already hold a backing string (item-include strings, gitignore lines, glob
    ///  lists from configuration) can slice without copying.
    /// </param>
    /// <param name="maxPatternLength">
    ///  Optional upper bound on <paramref name="pattern"/>'s length, in characters.
    ///  Pass <c>-1</c> to disable the check. Callers that compile patterns supplied
    ///  by untrusted input should set this to an application-specific limit;
    ///  oversized patterns fail with <see cref="GlobCompileErrorCode.PatternTooLarge"/>.
    /// </param>
    /// <exception cref="GlobFormatException">
    ///  The pattern is invalid for the requested dialect or options.
    /// </exception>
    public static GlobSpecification Compile(
        StringSegment pattern,
        GlobDialect dialect,
        GlobOptions options = GlobOptions.None,
        GlobPathSeparator separator = GlobPathSeparator.DialectDefault,
        int maxPatternLength = -1) => TryCompile(
            pattern,
            dialect,
            options,
            separator,
            maxPatternLength,
            out GlobSpecification? result,
            out GlobCompileError error)
            ? result
            : throw new GlobFormatException(error);

    /// <summary>
    ///  Attempts to compile <paramref name="pattern"/>. On failure,
    ///  <paramref name="result"/> is <see langword="null"/> and
    ///  <paramref name="error"/> is populated.
    /// </summary>
    public static bool TryCompile(
        StringSegment pattern,
        GlobDialect dialect,
        GlobOptions options,
        out GlobSpecification? result,
        out GlobCompileError error) =>
        TryCompile(pattern, dialect, options, GlobPathSeparator.DialectDefault, maxPatternLength: -1, out result, out error);

    /// <inheritdoc cref="TryCompile(StringSegment, GlobDialect, GlobOptions, out GlobSpecification, out GlobCompileError)"/>
    /// <param name="separator">Explicit path separator override; ignored for path-unaware dialects.</param>
    /// <param name="maxPatternLength">
    ///  Optional upper bound on <paramref name="pattern"/>'s length, in characters. Pass
    ///  <c>-1</c> to disable the check; otherwise oversized patterns fail with
    ///  <see cref="GlobCompileErrorCode.PatternTooLarge"/>.
    /// </param>
    public static bool TryCompile(
        StringSegment pattern,
        GlobDialect dialect,
        GlobOptions options,
        GlobPathSeparator separator,
        int maxPatternLength,
        [NotNullWhen(true)] out GlobSpecification? result,
        out GlobCompileError error)
    {
        result = null;

        if (dialect is not (GlobDialect.Posix
            or GlobDialect.PosixPath
            or GlobDialect.Simple
            or GlobDialect.PowerShell
            or GlobDialect.MSBuild
            or GlobDialect.Bash
            or GlobDialect.FileSystemGlobbing
            or GlobDialect.Git))
        {
            error = new GlobCompileError(
                GlobCompileErrorCode.FeatureNotEnabled,
                position: -1,
                message: $"Dialect '{dialect}' is not implemented yet.");

            return false;
        }

        if (!Factory.TryCreate(pattern.AsSpan(), dialect, options, separator, maxPatternLength, out GlobStrategy? strategy, out error))
        {
            return false;
        }

        result = new GlobSpecification(strategy, pattern);
        return true;
    }

    /// <summary>
    ///  The pattern source as supplied to <see cref="Compile"/>.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   When the caller passes a backing string (the common case via
    ///   <see cref="StringSegment"/>), this property slices that string rather
    ///   than allocating. Dialect-specific normalization that the factory may
    ///   apply (separator coalescing, gitignore marker stripping, etc.) does
    ///   <em>not</em> flow back into this property &#8211; it always reflects
    ///   the user-supplied input.
    ///  </para>
    /// </remarks>
    public StringSegment Pattern { get; }

    /// <summary>
    ///  The dialect this specification was compiled with.
    /// </summary>
    public GlobDialect Dialect => _strategy.Dialect;

    /// <summary>
    ///  The options this specification was compiled with.
    /// </summary>
    public GlobOptions Options => _strategy.Options;

    /// <summary>
    ///  The path separator character for path-aware matching, or <c>'\0'</c> when the
    ///  dialect is path-unaware.
    /// </summary>
    public char Separator => _strategy.Separator;

    /// <summary>
    ///  <see langword="true"/> when the compiled pattern began with a <c>!</c> negation
    ///  marker (gitignore-style).
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   <see cref="IsMatch"/> inverts the match result when this is set.
    ///  </para>
    /// </remarks>
    public bool Negated => _strategy.Negated;

    /// <summary>
    ///  <see langword="true"/> when the compiled pattern began with a leading <c>/</c>
    ///  (gitignore-style root anchor).
    /// </summary>
    public bool RootAnchored => _strategy.RootAnchored;

    /// <summary>
    ///  <see langword="true"/> when the compiled pattern ended with a trailing <c>/</c>
    ///  (gitignore-style &quot;directory only&quot;).
    /// </summary>
    public bool DirectoryOnly => _strategy.DirectoryOnly;

    /// <summary>
    ///  <see langword="true"/> when an empty input span is never matchable by the
    ///  compiled pattern.
    /// </summary>
    internal bool DisallowEmptyInput => _strategy.DisallowEmptyInput;

    /// <summary>
    ///  <see langword="true"/> when runs of two or more <see cref="Separator"/>
    ///  characters in the input must be coalesced before the match runs.
    /// </summary>
    internal bool CoalesceInputSeparators => _strategy.CoalesceInputSeparators;

    /// <summary>
    ///  The leading separator-bounded literal prefix of the compiled pattern, up to
    ///  and including the last separator that precedes any wildcard / class /
    ///  globstar opcode. Empty when the specification is path-unaware or the pattern
    ///  starts with a wildcard.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Exposed as a <see cref="StringSegment"/> so consumers can slice without
    ///   copying. The underlying bytes are owned by the strategy and remain valid
    ///   for the lifetime of this specification.
    ///  </para>
    /// </remarks>
    public StringSegment LiteralPathPrefix => _strategy.LiteralPathPrefix;

    internal bool IsPathAware => Separator != '\0';

    // Internal so GlobMatch can route through the strategy directly and tests can
    // inspect the encoded form via TestAccessor.
    /// <summary>
    ///  The underlying strategy.
    /// </summary>
    internal GlobStrategy Strategy => _strategy;

    // Internal accessor used by GlobMatch when classifying alignment.
    /// <summary>
    ///  The strategy's <see cref="IgnoreCaseKind"/>.
    /// </summary>
    internal IgnoreCaseKind IgnoreCaseKind => _strategy.IgnoreCaseKind;

    /// <summary>
    ///  Tests whether <paramref name="input"/> matches the compiled pattern.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   When <see cref="Negated"/> is <see langword="true"/> the underlying match
    ///   result is inverted before being returned.
    ///  </para>
    ///  <para>
    ///   For path-aware dialects the input is split at the last <see cref="Separator"/>
    ///   into a directory-prefix span (with the trailing separator) and a file-name
    ///   span before dispatch. Path-unaware dialects pass the full input as the
    ///   file-name span. When <see cref="CoalesceInputSeparators"/> is set the input
    ///   is first coalesced into a rented buffer; that allocation is unique to this
    ///   entry point and never occurs on the <see cref="GlobMatch"/> per-file hot
    ///   path.
    ///  </para>
    /// </remarks>
    public bool IsMatch(ReadOnlySpan<char> input)
    {
        if (DisallowEmptyInput && input.IsEmpty)
        {
            return Negated;
        }

        bool matched;
        if (CoalesceInputSeparators && ContainsSeparatorRun(input, Separator))
        {
            char[] rented = ArrayPool<char>.Shared.Rent(input.Length);
            int length = CoalesceSeparatorRuns(input, Separator, rented);
            matched = MatchCoreSplit(rented.AsSpan(0, length));
            ArrayPool<char>.Shared.Return(rented);
        }
        else
        {
            matched = MatchCoreSplit(input);
        }

        return Negated ? !matched : matched;
    }

    /// <summary>
    ///  Creates a new <see cref="GlobMatch"/> bound to <paramref name="rootDirectory"/>
    ///  that can drive a file-system enumeration via <see cref="IEnumerationMatcher"/>.
    /// </summary>
    /// <param name="rootDirectory">
    ///  The directory the enumeration walks. When <see langword="null"/> or the
    ///  specification is path-unaware, the matcher falls back to matching the bare
    ///  file name without using path context.
    /// </param>
    /// <remarks>
    ///  <para>
    ///   One specification can produce any number of <see cref="GlobMatch"/> wrappers
    ///   concurrently, each owning its own per-directory cache.
    ///  </para>
    /// </remarks>
    public GlobMatch CreateMatcher(string? rootDirectory = null) => new(this, rootDirectory);

    /// <summary>
    ///  Splits <paramref name="input"/> into a directory-prefix span (ending with
    ///  <see cref="Separator"/>) and a file-name span at the last separator and
    ///  dispatches to <see cref="GlobStrategy.MatchCore"/>. For path-unaware
    ///  specifications and inputs with no separator the directory prefix is empty.
    /// </summary>
    private bool MatchCoreSplit(ReadOnlySpan<char> input)
    {
        if (!IsPathAware)
        {
            return _strategy.MatchCore(default, input);
        }

        int lastSeparator = input.LastIndexOf(Separator);
        return lastSeparator < 0
            ? _strategy.MatchCore(default, input)
            : _strategy.MatchCore(input[..(lastSeparator + 1)], input[(lastSeparator + 1)..]);
    }

    /// <summary>
    ///  Invokes the underlying strategy with a (prefix, fileName) pair. Used by the
    ///  <see cref="GlobMatch"/> hot path; routes through the strategy directly to
    ///  bypass the <see cref="IsMatch"/> wrapper's separator-run coalescing.
    /// </summary>
    internal bool MatchCore(ReadOnlySpan<char> directoryPrefix, ReadOnlySpan<char> fileName) =>
        _strategy.MatchCore(directoryPrefix, fileName);

    private static bool ContainsSeparatorRun(ReadOnlySpan<char> input, char separator)
    {
        // Callers gate on `CoalesceInputSeparators`, which is only set for path-aware
        // dialects, so `separator` is guaranteed non-zero here. A run is two adjacent
        // separators; skip the very first character so a leading double-separator
        // (UNC-style root anchor on Windows) stays as-is.
        Debug.Assert(separator != '\0');

        for (int i = 2; i < input.Length; i++)
        {
            if (input[i] == separator && input[i - 1] == separator)
            {
                return true;
            }
        }
        return false;
    }

    private static int CoalesceSeparatorRuns(ReadOnlySpan<char> input, char separator, Span<char> destination)
    {
        // Preserve any leading run (e.g., UNC `//`) verbatim, then collapse internal
        // and trailing runs to a single separator.
        int srcIndex = 0;
        int dstIndex = 0;
        while (srcIndex < input.Length && input[srcIndex] == separator)
        {
            destination[dstIndex++] = input[srcIndex++];
        }

        while (srcIndex < input.Length)
        {
            char c = input[srcIndex];
            destination[dstIndex++] = c;
            srcIndex++;
            if (c == separator)
            {
                while (srcIndex < input.Length && input[srcIndex] == separator)
                {
                    srcIndex++;
                }
            }
        }

        return dstIndex;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _strategy.Dispose();
        }
    }
}
