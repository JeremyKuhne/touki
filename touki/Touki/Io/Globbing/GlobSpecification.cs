// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Threading;

namespace Touki.Io.Globbing;

/// <summary>
///  An immutable, root-independent compiled glob specification.
/// </summary>
/// <remarks>
///  <para>
///   <see cref="GlobSpecification"/> is the output of
///   <see cref="Compile(string, GlobDialect, GlobOptions, GlobPathSeparator, int)"/>:
///   a thread-safe parse result that holds the encoded pattern (literal table,
///   opcode program, etc.) and the pattern-level flags. Common evaluation paths
///   allocate no managed objects; separator coalescing and complex extglob matching
///   may rent or allocate temporary storage.
///   The specification is not bound to any enumeration root and may be reused
///   concurrently against many different roots via <see cref="CreateFileSystemMatcher"/>.
///  </para>
///  <para>
///   For one-shot flat-string testing use <see cref="IsMatch(ReadOnlySpan{char})"/>;
///   it does not consult any per-directory cache and is safe to call from multiple
///   threads concurrently. To drive a file-system enumeration via
///   <see cref="FileSystemMatchEnumerator{TResult}"/>, call <see cref="CreateFileSystemMatcher"/>
///   to produce a reusable definition. Each enumeration creates an independent root-bound session.
///  </para>
///  <para>
///   This mirrors the <see cref="MSBuildSpecification"/> / <see cref="MatchMSBuild"/>
///   split: the specification is the value-ish parse output; the reusable matcher is
///   root-independent, and each session binds it to a root and owns mutable enumeration state.
///  </para>
/// </remarks>
public sealed partial class GlobSpecification
{
    private IFileSystemMatcher? _fileSystemMatcher;

    private readonly GlobStrategy _strategy;
    private readonly StringSegment _msbuildTrailingDotFileNamePattern;
    private readonly bool _hasMSBuildTrailingDotFileNamePattern;
    private readonly CompiledGlobStrategy? _msbuildTrailingDotExtGlobStrategy;
    private readonly CompiledGlobStrategy? _msbuildTrailingDotRawExtGlobStrategy;
    private readonly GlobSpecification[]? _msbuildTrailingDotNegatedAlternatives;
    private readonly GlobSpecification[]? _msbuildTrailingDotPositiveAlternatives;
    private readonly bool _msbuildTrailingDotNeverMatches;

    private GlobSpecification(
        GlobStrategy strategy,
        StringSegment pattern,
        StringSegment msbuildTrailingDotFileNamePattern,
        bool hasMSBuildTrailingDotFileNamePattern,
        CompiledGlobStrategy? msbuildTrailingDotExtGlobStrategy,
        CompiledGlobStrategy? msbuildTrailingDotRawExtGlobStrategy,
        GlobSpecification[]? msbuildTrailingDotNegatedAlternatives,
        GlobSpecification[]? msbuildTrailingDotPositiveAlternatives,
        bool msbuildTrailingDotNeverMatches)
    {
        _strategy = strategy;
        _msbuildTrailingDotFileNamePattern = msbuildTrailingDotFileNamePattern;
        _hasMSBuildTrailingDotFileNamePattern = hasMSBuildTrailingDotFileNamePattern;
        _msbuildTrailingDotExtGlobStrategy = msbuildTrailingDotExtGlobStrategy;
        _msbuildTrailingDotRawExtGlobStrategy = msbuildTrailingDotRawExtGlobStrategy;
        _msbuildTrailingDotNegatedAlternatives = msbuildTrailingDotNegatedAlternatives;
        _msbuildTrailingDotPositiveAlternatives = msbuildTrailingDotPositiveAlternatives;
        _msbuildTrailingDotNeverMatches = msbuildTrailingDotNeverMatches;
        Pattern = pattern.ToString();
    }

    /// <summary>
    ///  Compiles the supplied <paramref name="pattern"/> for the specified
    ///  <paramref name="dialect"/> and <paramref name="options"/>, returning a
    ///  <see cref="GlobSpecification"/> wrapping the cheapest implementation strategy
    ///  that can evaluate it. <paramref name="separator"/> overrides the dialect's
    ///  documented default; see <see cref="GlobPathSeparator"/> for the semantics of
    ///  each value (ignored for path-unaware dialects).
    /// </summary>
    /// <param name="pattern">The glob pattern.</param>
    /// <param name="dialect">The glob dialect.</param>
    /// <param name="options">The glob options.</param>
    /// <param name="separator">The path separator behavior.</param>
    /// <param name="maxPatternLength">
    ///  Optional upper bound on <paramref name="pattern"/>'s length, in characters.
    ///  Pass <c>-1</c> to disable the check. Callers that compile patterns supplied
    ///  by untrusted input should set this to an application-specific limit;
    ///  oversized patterns fail with <see cref="GlobCompileErrorCode.PatternTooLarge"/>.
    /// </param>
    /// <returns>The compiled glob specification.</returns>
    /// <exception cref="GlobFormatException">The pattern is invalid for the requested dialect or options.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="pattern"/> is <see langword="null"/>.</exception>
    public static GlobSpecification Compile(
        string pattern,
        GlobDialect dialect,
        GlobOptions options = GlobOptions.None,
        GlobPathSeparator separator = GlobPathSeparator.DialectDefault,
        int maxPatternLength = -1)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        return Compile(new StringSegment(pattern), dialect, options, separator, maxPatternLength);
    }

    /// <inheritdoc cref="Compile(string, GlobDialect, GlobOptions, GlobPathSeparator, int)"/>
    /// <remarks>
    ///  <para>
    ///   This Touki-specific overload retains a slice of a backing string without copying.
    ///  </para>
    /// </remarks>
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

    /// <inheritdoc cref="TryCompile(string, GlobDialect, GlobOptions, GlobPathSeparator, int, out GlobSpecification, out GlobCompileError)"/>
    public static bool TryCompile(
        string pattern,
        GlobDialect dialect,
        [NotNullWhen(true)] out GlobSpecification? result,
        out GlobCompileError error)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        return TryCompile(new StringSegment(pattern), dialect, out result, out error);
    }

    /// <inheritdoc cref="TryCompile(string, GlobDialect, GlobOptions, GlobPathSeparator, int, out GlobSpecification, out GlobCompileError)"/>
    public static bool TryCompile(
        string pattern,
        GlobDialect dialect,
        GlobOptions options,
        [NotNullWhen(true)] out GlobSpecification? result,
        out GlobCompileError error)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        return TryCompile(new StringSegment(pattern), dialect, options, out result, out error);
    }

    /// <summary>
    ///  Attempts to compile <paramref name="pattern"/>. On failure,
    ///  <paramref name="result"/> is <see langword="null"/> and
    ///  <paramref name="error"/> is populated.
    /// </summary>
    /// <param name="pattern">The glob pattern.</param>
    /// <param name="dialect">The pattern dialect.</param>
    /// <param name="options">Options that modify compilation and matching.</param>
    /// <param name="separator">Explicit path separator override; ignored for path-unaware dialects.</param>
    /// <param name="maxPatternLength">
    ///  Optional upper bound on <paramref name="pattern"/>'s length, in characters. Pass
    ///  <c>-1</c> to disable the check; otherwise oversized patterns fail with
    ///  <see cref="GlobCompileErrorCode.PatternTooLarge"/>.
    /// </param>
    /// <param name="result">The compiled specification on success; otherwise <see langword="null"/>.</param>
    /// <param name="error">The compilation error on failure; otherwise the default value.</param>
    /// <returns><see langword="true"/> when compilation succeeds; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="pattern"/> is <see langword="null"/>.</exception>
    public static bool TryCompile(
        string pattern,
        GlobDialect dialect,
        GlobOptions options,
        GlobPathSeparator separator,
        int maxPatternLength,
        [NotNullWhen(true)] out GlobSpecification? result,
        out GlobCompileError error)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        return TryCompile(
            new StringSegment(pattern),
            dialect,
            options,
            separator,
            maxPatternLength,
            out result,
            out error);
    }

    /// <inheritdoc cref="TryCompile(string, GlobDialect, GlobOptions, GlobPathSeparator, int, out GlobSpecification, out GlobCompileError)"/>
    public static bool TryCompile(
        StringSegment pattern,
        GlobDialect dialect,
        [NotNullWhen(true)] out GlobSpecification? result,
        out GlobCompileError error) =>
        TryCompile(pattern, dialect, GlobOptions.None, out result, out error);

    /// <inheritdoc cref="TryCompile(string, GlobDialect, GlobOptions, GlobPathSeparator, int, out GlobSpecification, out GlobCompileError)"/>
    public static bool TryCompile(
        StringSegment pattern,
        GlobDialect dialect,
        GlobOptions options,
        [NotNullWhen(true)] out GlobSpecification? result,
        out GlobCompileError error) =>
        TryCompile(pattern, dialect, options, GlobPathSeparator.DialectDefault, maxPatternLength: -1, out result, out error);

    /// <inheritdoc cref="TryCompile(string, GlobDialect, GlobOptions, GlobPathSeparator, int, out GlobSpecification, out GlobCompileError)"/>
    /// <remarks>
    ///  <para>
    ///   This Touki-specific overload retains a slice of a backing string without copying.
    ///  </para>
    /// </remarks>
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

        if (maxPatternLength >= 0 && pattern.Length > maxPatternLength)
        {
            error = new GlobCompileError(
                GlobCompileErrorCode.PatternTooLarge,
                position: maxPatternLength,
                message: $"Pattern length {pattern.Length} exceeds the configured limit of {maxPatternLength}.");

            return false;
        }

        StringSegment compiledPattern = pattern;
        StringSegment msbuildTrailingDotFileNamePattern = default;
        bool hasMSBuildTrailingDotFileNamePattern = false;
        int msbuildFileNameStart = 0;
        int[]? msbuildRewriteSourcePositions = null;
        bool allowExtGlob = (options & GlobOptions.AllowExtGlob) != 0;
        if (dialect == GlobDialect.MSBuild)
        {
            compiledPattern = NormalizeMSBuildFileNamePattern(
                pattern,
                allowExtGlob,
                out msbuildFileNameStart,
                out msbuildTrailingDotFileNamePattern,
                out hasMSBuildTrailingDotFileNamePattern,
                out msbuildRewriteSourcePositions);
        }

        if (!Factory.TryCreate(
            compiledPattern,
            dialect,
            options,
            separator,
            maxPatternLength,
            out GlobStrategy? strategy,
            out error))
        {
            if (msbuildRewriteSourcePositions is not null && error.Position >= 0)
            {
                error = new GlobCompileError(
                    error.Code,
                    RemapMSBuildStarDotStarErrorPosition(
                        pattern,
                        error.Position,
                        msbuildFileNameStart,
                        msbuildRewriteSourcePositions),
                    error.Message);
            }

            return false;
        }

        CompiledGlobStrategy? msbuildTrailingDotExtGlobStrategy = null;
        CompiledGlobStrategy? msbuildTrailingDotRawExtGlobStrategy = null;
        GlobSpecification[]? msbuildTrailingDotNegatedAlternatives = null;
        GlobSpecification[]? msbuildTrailingDotPositiveAlternatives = null;
        bool msbuildTrailingDotNeverMatches = false;
        if (hasMSBuildTrailingDotFileNamePattern
            && (options & GlobOptions.AllowExtGlob) != 0
            && ContainsExtGlobOpener(msbuildTrailingDotFileNamePattern))
        {
            StringSegment pathPrefix = pattern[..msbuildFileNameStart];
            string fullTrailingDotPattern = $"{pathPrefix}{msbuildTrailingDotFileNamePattern}";
            if (!Factory.TryCreate(
                fullTrailingDotPattern,
                GlobDialect.MSBuild,
                options,
                separator,
                maxPatternLength,
                out GlobStrategy? trailingDotStrategy,
                out error,
                markEffectiveDoubleStarRuns: true))
            {
                strategy.Dispose();
                if (error.Position >= 0)
                {
                    error = new GlobCompileError(
                        error.Code,
                        error.Position,
                        error.Message);
                }

                return false;
            }

            if (!TryCompileMSBuildTrailingDotNegatedAlternatives(
                msbuildTrailingDotFileNamePattern,
                pathPrefix,
                options,
                separator,
                out msbuildTrailingDotNegatedAlternatives,
                out error))
            {
                trailingDotStrategy.Dispose();
                strategy.Dispose();
                return false;
            }

            if (msbuildTrailingDotNegatedAlternatives is null
                && !TryCompileMSBuildTrailingDotPositiveAlternatives(
                    msbuildTrailingDotFileNamePattern,
                    pathPrefix,
                    options,
                    separator,
                    out msbuildTrailingDotPositiveAlternatives,
                    out error))
            {
                trailingDotStrategy.Dispose();
                strategy.Dispose();
                return false;
            }

            if (msbuildTrailingDotNegatedAlternatives is null
                && msbuildTrailingDotPositiveAlternatives is null)
            {
                if (trailingDotStrategy is NeverMatchGlobStrategy)
                {
                    trailingDotStrategy.Dispose();
                    msbuildTrailingDotNeverMatches = true;
                }
                else if (trailingDotStrategy is not CompiledGlobStrategy compiledTrailingDotStrategy)
                {
                    trailingDotStrategy.Dispose();
                    strategy.Dispose();
                    error = new GlobCompileError(
                        GlobCompileErrorCode.FeatureNotEnabled,
                        position: -1,
                        message: "MSBuild trailing-dot extglob requires the compiled matching strategy.");
                    return false;
                }
                else
                {
                    compiledTrailingDotStrategy.EnableMSBuildTrailingDotMatching();
                    msbuildTrailingDotExtGlobStrategy = compiledTrailingDotStrategy;

                    string rawFileNamePattern = $"{fullTrailingDotPattern}.";
                    if (!Factory.TryCreate(
                        rawFileNamePattern,
                        GlobDialect.MSBuild,
                        options,
                        separator,
                        maxPatternLength,
                        out GlobStrategy? rawTrailingDotStrategy,
                        out error,
                        markEffectiveDoubleStarRuns: true))
                    {
                        msbuildTrailingDotExtGlobStrategy.Dispose();
                        strategy.Dispose();
                        if (error.Position >= 0)
                        {
                            error = new GlobCompileError(
                                error.Code,
                                error.Position,
                                error.Message);
                        }

                        return false;
                    }

                    if (rawTrailingDotStrategy is not CompiledGlobStrategy compiledRawTrailingDotStrategy)
                    {
                        rawTrailingDotStrategy.Dispose();
                        msbuildTrailingDotExtGlobStrategy.Dispose();
                        strategy.Dispose();
                        error = new GlobCompileError(
                            GlobCompileErrorCode.FeatureNotEnabled,
                            position: -1,
                            message: "MSBuild raw trailing-dot extglob requires the compiled matching strategy.");
                        return false;
                    }

                    compiledRawTrailingDotStrategy.RequireEffectiveDoubleStar();
                    msbuildTrailingDotRawExtGlobStrategy = compiledRawTrailingDotStrategy;
                }
            }
            else
            {
                trailingDotStrategy.Dispose();
            }
        }

        result = new GlobSpecification(
            strategy,
            pattern,
            msbuildTrailingDotFileNamePattern,
            hasMSBuildTrailingDotFileNamePattern,
            msbuildTrailingDotExtGlobStrategy,
            msbuildTrailingDotRawExtGlobStrategy,
            msbuildTrailingDotNegatedAlternatives,
            msbuildTrailingDotPositiveAlternatives,
            msbuildTrailingDotNeverMatches);
        return true;
    }

    private static bool TryCompileMSBuildTrailingDotPositiveAlternatives(
        StringSegment pattern,
        StringSegment pathPrefix,
        GlobOptions options,
        GlobPathSeparator separator,
        out GlobSpecification[]? alternatives,
        out GlobCompileError error)
    {
        alternatives = null;
        error = default;
        if (pattern.Length < 3 || pattern[0] != '@' || pattern[1] != '(')
        {
            return true;
        }

        int close = FindExtGlobClose(pattern, opener: 0);
        if (close != pattern.Length - 1)
        {
            return true;
        }

        return TryCompileMSBuildTrailingDotAlternativeList(
            pattern,
            bodyStart: 2,
            close,
            sourceOffset: 0,
            pathPrefix,
            options,
            separator,
            out alternatives,
            out error);
    }

    private static bool TryCompileMSBuildTrailingDotNegatedAlternatives(
        StringSegment pattern,
        StringSegment pathPrefix,
        GlobOptions options,
        GlobPathSeparator separator,
        out GlobSpecification[]? alternatives,
        out GlobCompileError error)
    {
        alternatives = null;
        error = default;
        int sourceOffset = 0;
        while (pattern.Length >= 3 && pattern[0] == '@' && pattern[1] == '(')
        {
            int wrapperClose = FindExtGlobClose(pattern, opener: 0);
            if (wrapperClose != pattern.Length - 1
                || ContainsTopLevelAlternativeSeparator(pattern[2..wrapperClose]))
            {
                break;
            }

            pattern = pattern[2..wrapperClose];
            sourceOffset += 2;
        }

        if (pattern.Length < 3 || pattern[0] != '!' || pattern[1] != '(')
        {
            return true;
        }

        int close = FindExtGlobClose(pattern, opener: 0);
        if (close != pattern.Length - 1)
        {
            return true;
        }

        return TryCompileMSBuildTrailingDotAlternativeList(
            pattern,
            bodyStart: 2,
            close,
            sourceOffset,
            pathPrefix,
            options,
            separator,
            out alternatives,
            out error);
    }

    private static bool TryCompileMSBuildTrailingDotAlternativeList(
        StringSegment pattern,
        int bodyStart,
        int close,
        int sourceOffset,
        StringSegment pathPrefix,
        GlobOptions options,
        GlobPathSeparator separator,
        out GlobSpecification[]? alternatives,
        out GlobCompileError error)
    {
        alternatives = null;
        error = default;
        List<GlobSpecification> compiled = [];
        int alternativeStart = bodyStart;
        int index = alternativeStart;
        while (index <= close)
        {
            bool atEnd = index == close;
            if (!atEnd && IsExtGlobOpenerAt(pattern, index))
            {
                int nestedClose = FindExtGlobClose(pattern, index);
                if (nestedClose < 0)
                {
                    break;
                }

                index = nestedClose + 1;
                continue;
            }

            if (atEnd || pattern[index] == '|')
            {
                string alternativePattern = $"{pathPrefix}{pattern[alternativeStart..index]}.";
                if (!TryCompile(
                    alternativePattern,
                    GlobDialect.MSBuild,
                    options,
                    separator,
                    maxPatternLength: -1,
                    out GlobSpecification? specification,
                    out error))
                {
                    if (error.Position >= 0)
                    {
                        int alternativePosition = error.Position - pathPrefix.Length;
                        error = new GlobCompileError(
                            error.Code,
                            alternativePosition < 0
                                ? error.Position
                                : pathPrefix.Length + sourceOffset + alternativeStart + alternativePosition,
                            error.Message);
                    }

                    return false;
                }

                compiled.Add(specification);
                alternativeStart = index + 1;
            }

            index++;
        }

        alternatives = [.. compiled];
        return true;
    }

    private static bool ContainsTopLevelAlternativeSeparator(ReadOnlySpan<char> pattern)
    {
        int index = 0;
        while (index < pattern.Length)
        {
            if (IsExtGlobOpenerAt(pattern, index))
            {
                int close = FindExtGlobClose(pattern, index);
                if (close < 0)
                {
                    return false;
                }

                index = close + 1;
                continue;
            }

            if (pattern[index] == '|')
            {
                return true;
            }

            index++;
        }

        return false;
    }

    private static int FindExtGlobClose(ReadOnlySpan<char> pattern, int opener)
    {
        int depth = 1;
        int index = opener + 2;
        while (index < pattern.Length)
        {
            if (IsExtGlobOpenerAt(pattern, index))
            {
                depth++;
                index += 2;
                continue;
            }

            if (pattern[index] == ')' && --depth == 0)
            {
                return index;
            }

            index++;
        }

        return -1;
    }

    private static bool IsExtGlobOpenerAt(ReadOnlySpan<char> pattern, int index) =>
        index + 1 < pattern.Length
        && pattern[index + 1] == '('
        && pattern[index] is '?' or '*' or '+' or '@' or '!';

    private static bool ContainsExtGlobOpener(ReadOnlySpan<char> pattern)
    {
        for (int index = 0; index + 1 < pattern.Length; index++)
        {
            if (pattern[index + 1] == '('
                && pattern[index] is '?' or '*' or '+' or '@' or '!')
            {
                return true;
            }
        }

        return false;
    }

    private static StringSegment NormalizeMSBuildFileNamePattern(
        StringSegment pattern,
        bool allowExtGlob,
        out int fileNameStart,
        out StringSegment trailingDotFileNamePattern,
        out bool hasTrailingDotFileNamePattern,
        out int[]? rewriteSourcePositions)
    {
        trailingDotFileNamePattern = default;
        hasTrailingDotFileNamePattern = false;
        rewriteSourcePositions = null;
        fileNameStart = 0;
        if (pattern.IsEmpty)
        {
            return pattern;
        }

        fileNameStart = FindMSBuildFileNameStart(pattern, allowExtGlob);
        StringSegment fileName = pattern[fileNameStart..];

        if (!fileName.IsEmpty
            && fileName[^1] == '.'
            && !MSBuildFileNamePattern.ContainsEffectiveDoubleStar(fileName[..^1], allowExtGlob))
        {
            trailingDotFileNamePattern = fileName[..^1];
            hasTrailingDotFileNamePattern = true;
            if (fileNameStart == 0)
            {
                return "*";
            }

            using ValueStringBuilder builder = new(stackalloc char[256]);
            builder.Append(pattern[..fileNameStart]);
            builder.Append('*');
            return builder.ToString();
        }

        StringSegment rewrittenFileName = MSBuildFileNamePattern.RewriteStarDotStarSequences(
            fileName,
            allowExtGlob,
            out int[]? fileNameSourcePositions);
        if (rewrittenFileName.Equals(fileName))
        {
            return pattern;
        }

        rewriteSourcePositions = fileNameSourcePositions;

        if (fileNameStart == 0)
        {
            return rewrittenFileName;
        }

        using (ValueStringBuilder builder = new(stackalloc char[256]))
        {
            builder.Append(pattern[..fileNameStart]);
            builder.Append(rewrittenFileName);
            return builder.ToString();
        }
    }

    private static int RemapMSBuildStarDotStarErrorPosition(
        ReadOnlySpan<char> originalPattern,
        int rewrittenPosition,
        int fileNameStart,
        ReadOnlySpan<int> fileNameSourcePositions)
    {
        if (rewrittenPosition < fileNameStart)
        {
            return rewrittenPosition;
        }

        int fileNamePosition = rewrittenPosition - fileNameStart;
        if ((uint)fileNamePosition < (uint)fileNameSourcePositions.Length)
        {
            return fileNameStart + fileNameSourcePositions[fileNamePosition];
        }

        return originalPattern.Length;
    }

    private static int FindMSBuildFileNameStart(ReadOnlySpan<char> pattern, bool allowExtGlob)
    {
        int lastSeparator = -1;
        int index = 0;
        while (index < pattern.Length)
        {
            if (allowExtGlob && IsExtGlobOpenerAt(pattern, index))
            {
                int close = FindExtGlobClose(pattern, index);
                if (close < 0)
                {
                    break;
                }

                index = close + 1;
                continue;
            }

            if (pattern[index] is '/' or '\\')
            {
                lastSeparator = index;
            }

            index++;
        }

        return lastSeparator + 1;
    }

    /// <summary>
    ///  The pattern source as supplied to
    ///  <see cref="Compile(string, GlobDialect, GlobOptions, GlobPathSeparator, int)"/>.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   The <see langword="string"/> overload retains the caller's original instance.
    ///   The Touki-specific <see cref="StringSegment"/> overload materializes a new
    ///   string when the segment is a partial slice. Dialect-specific normalization that the factory may
    ///   apply (separator coalescing, gitignore marker stripping, etc.) does
    ///   <em>not</em> flow back into this property - it always reflects
    ///   the user-supplied input.
    ///  </para>
    /// </remarks>
    public string Pattern { get; }

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
    ///   The returned string is owned by the compiled strategy and remains valid for
    ///   the lifetime of this specification.
    ///  </para>
    /// </remarks>
    public string LiteralPathPrefix => _strategy.LiteralPathPrefix;

    /// <summary>
    ///  <see langword="true"/> when the compiled pattern contains at least one
    ///  extglob negation (<c>!(...)</c>). Lets
    ///  <see cref="GlobMatch.MatchesDirectory"/> gate directory-mode pruning behind a
    ///  single field load.
    /// </summary>
    internal bool HasNegation => _strategy.HasNegation;

    /// <summary>
    ///  Classifies a candidate directory path
    ///  (<paramref name="directoryPrefix"/> + <paramref name="directoryName"/>)
    ///  against the compiled pattern so the enumerator can prune subtrees that an
    ///  anchored negation provably excludes. See
    ///  <see cref="GlobStrategy.MatchDirectory"/> for the conservative contract.
    /// </summary>
    /// <param name="directoryPrefix">The directory prefix preceding the candidate name.</param>
    /// <param name="directoryName">The candidate directory name.</param>
    /// <returns>The conservative match outcome for the candidate directory.</returns>
    internal MatchOutcome MatchDirectory(
        ReadOnlySpan<char> directoryPrefix,
        ReadOnlySpan<char> directoryName) =>
        _strategy.MatchDirectory(directoryPrefix, directoryName);

    /// <summary>
    ///  Gets whether the compiled specification treats a path separator specially.
    /// </summary>
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
    /// <param name="input">The input to match.</param>
    /// <returns><see langword="true"/> if the input matches; otherwise <see langword="false"/>.</returns>
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
    ///  Creates a reusable definition that binds this specification to a root for each file-system enumeration.
    /// </summary>
    /// <returns>The reusable matcher definition.</returns>
    public IFileSystemMatcher CreateFileSystemMatcher()
    {
        IFileSystemMatcher? matcher = Volatile.Read(ref _fileSystemMatcher);
        if (matcher is not null)
        {
            return matcher;
        }

        matcher = new GlobFileSystemMatcher(this);
        return Interlocked.CompareExchange(ref _fileSystemMatcher, matcher, null) ?? matcher;
    }

    /// <summary>
    ///  Creates a matcher session for this specification.
    /// </summary>
    /// <param name="rootDirectory">The enumeration root, or <see langword="null"/> when unbound.</param>
    /// <returns>The created matcher session.</returns>
    internal GlobMatch CreateSession(string? rootDirectory = null) => new(this, rootDirectory);

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
            return MatchCore(default, input);
        }

        int lastSeparator = input.LastIndexOf(Separator);
        return lastSeparator < 0
            ? MatchCore(default, input)
            : MatchCore(input[..(lastSeparator + 1)], input[(lastSeparator + 1)..]);
    }

    /// <summary>
    ///  Invokes the underlying strategy with a (prefix, fileName) pair. Used by the
    ///  <see cref="GlobMatch"/> hot path; routes through the strategy directly to
    ///  bypass the <see cref="IsMatch"/> wrapper's separator-run coalescing.
    /// </summary>
    /// <param name="directoryPrefix">The directory prefix preceding the file name.</param>
    /// <param name="fileName">The file name to match.</param>
    /// <returns><see langword="true"/> if the split input matches; otherwise <see langword="false"/>.</returns>
    internal bool MatchCore(ReadOnlySpan<char> directoryPrefix, ReadOnlySpan<char> fileName)
    {
        if (TryMatchMSBuildTrailingDotComposition(directoryPrefix, fileName, out bool composedMatch))
        {
            return composedMatch;
        }

        if (_msbuildTrailingDotExtGlobStrategy is not null || _msbuildTrailingDotNeverMatches)
        {
            return MatchesMSBuildTrailingDotPattern(directoryPrefix, fileName);
        }

        return MatchesMSBuildTrailingDotPattern(directoryPrefix, fileName)
            && _strategy.MatchCore(directoryPrefix, fileName);
    }

    private bool TryMatchMSBuildTrailingDotComposition(
        ReadOnlySpan<char> directoryPrefix,
        ReadOnlySpan<char> fileName,
        out bool result)
    {
        if (_msbuildTrailingDotNegatedAlternatives is not null)
        {
            if (!_strategy.MatchCore(directoryPrefix, fileName))
            {
                result = false;
                return true;
            }

            foreach (GlobSpecification alternative in _msbuildTrailingDotNegatedAlternatives)
            {
                if (alternative.MatchCore(directoryPrefix, fileName))
                {
                    result = false;
                    return true;
                }
            }

            result = true;
            return true;
        }

        if (_msbuildTrailingDotPositiveAlternatives is not null)
        {
            foreach (GlobSpecification alternative in _msbuildTrailingDotPositiveAlternatives)
            {
                if (alternative.MatchCore(directoryPrefix, fileName))
                {
                    result = true;
                    return true;
                }
            }

            result = false;
            return true;
        }

        result = false;
        return false;
    }

    private bool MatchesMSBuildTrailingDotPattern(
        ReadOnlySpan<char> directoryPrefix,
        ReadOnlySpan<char> fileName)
    {
        if (!_hasMSBuildTrailingDotFileNamePattern)
        {
            return true;
        }

        if (_msbuildTrailingDotNeverMatches)
        {
            return false;
        }

        if (_msbuildTrailingDotExtGlobStrategy is null)
        {
            return MSBuildTrailingDotFileNameMatcher.Matches(
                fileName,
                _msbuildTrailingDotFileNamePattern,
                IgnoreCaseKind);
        }

        ReadOnlySpan<char> normalized = MSBuildTrailingDotFileNameMatcher.NormalizeExtGlobInput(
            fileName,
            out bool isAllDotInput);
        bool trailingDotMatch = isAllDotInput
            ? _msbuildTrailingDotExtGlobStrategy.MatchesMSBuildTrailingDotAllDotInput(directoryPrefix)
            : _msbuildTrailingDotExtGlobStrategy.MatchCore(directoryPrefix, normalized);
        return trailingDotMatch
            || _msbuildTrailingDotRawExtGlobStrategy!.MatchCore(directoryPrefix, fileName);
    }

    private static bool ContainsSeparatorRun(ReadOnlySpan<char> input, char separator)
    {
        // Callers gate on `CoalesceInputSeparators`, which is only set for path-aware
        // dialects, so `separator` is guaranteed non-zero here. A run is two adjacent
        // separators. On Windows a leading double-separator is a UNC root anchor and
        // must be preserved verbatim, so the scan starts at index 2. On non-Windows
        // UNC does not apply and leading runs are ordinary runs, so the scan starts
        // at index 1.
        Debug.Assert(separator != '\0');

        int start = Path.DirectorySeparatorChar == '\\' ? 2 : 1;
        for (int i = start; i < input.Length; i++)
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
        // On Windows any leading run is a UNC root anchor and must be preserved
        // verbatim. On non-Windows leading runs are ordinary runs and collapse to a
        // single separator the same way internal and trailing runs do.
        int srcIndex = 0;
        int dstIndex = 0;
        if (Path.DirectorySeparatorChar == '\\')
        {
            while (srcIndex < input.Length && input[srcIndex] == separator)
            {
                destination[dstIndex++] = input[srcIndex++];
            }
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

}
