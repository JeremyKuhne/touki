// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Collections;
using Touki.Io.Globbing;

namespace Touki.Io;

/// <summary>
///  Normalizes MSBuild file-name wildcard policy and selects logical or file-system matching semantics.
/// </summary>
internal readonly struct MSBuildFileNamePattern
{
    private readonly StringSegment _expression;
    private readonly StringSegment _logicalTrailingDotPattern;
    private readonly bool _hasLogicalTrailingDotPattern;
    private readonly MatchType _matchType;
    private readonly bool _matchAll;

    /// <summary>
    ///  Initializes a normalized MSBuild filename pattern.
    /// </summary>
    /// <param name="expression">The filename expression.</param>
    /// <param name="matchType">The requested pattern matching mode.</param>
    /// <param name="useFileSystemSemantics">Whether to apply file-system filename semantics.</param>
    /// <param name="useRawLogicalSemantics">Whether to use the expression without logical rewriting.</param>
    public MSBuildFileNamePattern(
        StringSegment expression,
        MatchType matchType,
        bool useFileSystemSemantics = true,
        bool useRawLogicalSemantics = false)
    {
        _matchAll = false;
        _logicalTrailingDotPattern = default;
        _hasLogicalTrailingDotPattern = false;

        if (IsAllFilesWildcard(expression))
        {
            _expression = expression;
            _matchType = MatchType.Simple;
            _matchAll = true;
        }
        else if (useRawLogicalSemantics)
        {
            _expression = expression;
            _matchType = MatchType.Simple;
        }
        else if (!useFileSystemSemantics)
        {
            if (IsSingleTrailingDotPattern(expression))
            {
                _expression = "*";
                _logicalTrailingDotPattern = expression[..^1];
                _hasLogicalTrailingDotPattern = true;
            }
            else
            {
                _expression = RewriteStarDotStarSequences(expression);
            }

            _matchType = MatchType.Simple;
        }
        else if (ShouldEnforceLogicalMatch(expression))
        {
            _expression = expression;
            _matchType = MatchType.Simple;
        }
        else if (UsesDosDotSemantics(expression) || ContainsStarDotStar(expression))
        {
            _expression = FileSystemName.TranslateWin32Expression(expression.ToString());
            _matchType = MatchType.Win32;
        }
        else
        {
            _expression = expression;
            _matchType = matchType;
        }
    }

    /// <summary>
    ///  Determines whether a filename matches the normalized pattern.
    /// </summary>
    /// <param name="fileName">The filename to match.</param>
    /// <param name="matchCasing">The case sensitivity to use.</param>
    /// <returns><see langword="true"/> if the filename matches; otherwise <see langword="false"/>.</returns>
    public bool Matches(ReadOnlySpan<char> fileName, MatchCasing matchCasing)
    {
        if (_matchAll)
        {
            return true;
        }

        if (_hasLogicalTrailingDotPattern)
        {
            return MSBuildTrailingDotFileNameMatcher.Matches(
                fileName,
                _logicalTrailingDotPattern,
                matchCasing == MatchCasing.CaseInsensitive
                    ? IgnoreCaseKind.Unicode
                    : IgnoreCaseKind.Off);
        }

        return Paths.MatchesExpression(fileName, _expression, matchCasing, _matchType);
    }

    /// <summary>
    ///  Determines whether an expression requires MSBuild filename policy handling.
    /// </summary>
    /// <param name="expression">The filename expression.</param>
    /// <param name="matchType">The requested pattern matching mode.</param>
    /// <returns><see langword="true"/> if policy handling is required; otherwise <see langword="false"/>.</returns>
    public static bool RequiresPolicy(ReadOnlySpan<char> expression, MatchType matchType) =>
        ContainsStarDotStar(expression)
        || UsesDosDotSemantics(expression)
        || (matchType == MatchType.Win32 && ShouldEnforceLogicalMatch(expression));

    /// <summary>
    ///  Rewrites effective filename-scope <c>*.*</c> sequences to <c>*</c>.
    /// </summary>
    /// <param name="expression">The expression to rewrite.</param>
    /// <param name="allowExtGlob">Whether extglob groups are recognized.</param>
    /// <returns>The original expression when unchanged; otherwise the rewritten expression.</returns>
    internal static StringSegment RewriteStarDotStarSequences(
        StringSegment expression,
        bool allowExtGlob = false) =>
        RewriteStarDotStarSequences(expression, allowExtGlob, out _);

    /// <summary>
    ///  Rewrites effective filename-scope <c>*.*</c> sequences and records source positions.
    /// </summary>
    /// <param name="expression">The expression to rewrite.</param>
    /// <param name="allowExtGlob">Whether extglob groups are recognized.</param>
    /// <param name="sourcePositions">
    ///  Receives the source index for each rewritten character, or <see langword="null"/> when unchanged.
    /// </param>
    /// <returns>The original expression when unchanged; otherwise the rewritten expression.</returns>
    internal static StringSegment RewriteStarDotStarSequences(
        StringSegment expression,
        bool allowExtGlob,
        out int[]? sourcePositions)
    {
        ReadOnlySpan<char> source = expression;
        sourcePositions = null;
        if (!ContainsStarDotStar(source))
        {
            return expression;
        }

        ValueStringBuilder builder = new(stackalloc char[256]);
        try
        {
            using ArrayPoolList<int> positions = new(source.Length);
            bool changed = false;
            RewriteScope(
                source,
                start: 0,
                source.Length,
                allowExtGlob,
                ref builder,
                positions,
                ref changed);

            if (!changed)
            {
                return expression;
            }

            sourcePositions = new int[positions.Count];
            positions.CopyTo(sourcePositions, 0);
            return builder.ToString();
        }
        finally
        {
            builder.Dispose();
        }
    }

    private static void RewriteScope(
        ReadOnlySpan<char> source,
        int start,
        int end,
        bool allowExtGlob,
        ref ValueStringBuilder builder,
        ArrayPoolList<int> sourcePositions,
        ref bool changed)
    {
        bool rewriteDirectSequences = !ContainsDirectEffectiveDoubleStar(source, start, end, allowExtGlob);
        int fileNameStart = FindScopeFileNameStart(source, start, end, allowExtGlob);
        int index = start;
        while (index < end)
        {
            if (allowExtGlob && IsExtGlobOpener(source, index, end))
            {
                int close = FindExtGlobClose(source, index, end);
                if (close < 0)
                {
                    AppendRange(source, index, end, ref builder, sourcePositions);
                    return;
                }

                AppendCharacter(source[index], index, ref builder, sourcePositions);
                AppendCharacter(source[index + 1], index + 1, ref builder, sourcePositions);
                RewriteAlternatives(
                    source,
                    index + 2,
                    close,
                    allowExtGlob,
                    ref builder,
                    sourcePositions,
                    ref changed);

                AppendCharacter(source[close], close, ref builder, sourcePositions);
                index = close + 1;
                continue;
            }

            if (rewriteDirectSequences
                && index >= fileNameStart
                && ShouldRewriteStarDotStarAt(source, index, end, allowExtGlob))
            {
                AppendCharacter('*', index, ref builder, sourcePositions);
                changed = true;
                index += 3;
                continue;
            }

            AppendCharacter(source[index], index, ref builder, sourcePositions);
            index++;
        }
    }

    private static int FindScopeFileNameStart(
        ReadOnlySpan<char> source,
        int start,
        int end,
        bool allowExtGlob)
    {
        int fileNameStart = start;
        int index = start;
        while (index < end)
        {
            if (allowExtGlob && IsExtGlobOpener(source, index, end))
            {
                int close = FindExtGlobClose(source, index, end);
                if (close < 0)
                {
                    break;
                }

                index = close + 1;
                continue;
            }

            if (source[index] is '/' or '\\')
            {
                fileNameStart = index + 1;
            }

            index++;
        }

        return fileNameStart;
    }

    private static void RewriteAlternatives(
        ReadOnlySpan<char> source,
        int start,
        int end,
        bool allowExtGlob,
        ref ValueStringBuilder builder,
        ArrayPoolList<int> sourcePositions,
        ref bool changed)
    {
        int alternativeStart = start;
        int index = start;
        while (index < end)
        {
            if (IsExtGlobOpener(source, index, end))
            {
                int close = FindExtGlobClose(source, index, end);
                if (close < 0)
                {
                    break;
                }

                index = close + 1;
                continue;
            }

            if (source[index] == '|')
            {
                RewriteScope(
                    source,
                    alternativeStart,
                    index,
                    allowExtGlob,
                    ref builder,
                    sourcePositions,
                    ref changed);

                AppendCharacter('|', index, ref builder, sourcePositions);
                alternativeStart = index + 1;
            }

            index++;
        }

        RewriteScope(
            source,
            alternativeStart,
            end,
            allowExtGlob,
            ref builder,
            sourcePositions,
            ref changed);
    }

    private static bool ContainsDirectEffectiveDoubleStar(
        ReadOnlySpan<char> source,
        int start,
        int end,
        bool allowExtGlob)
    {
        int index = start;
        while (index < end)
        {
            if (allowExtGlob && IsExtGlobOpener(source, index, end))
            {
                int close = FindExtGlobClose(source, index, end);
                if (close < 0)
                {
                    return false;
                }

                index = close + 1;
                continue;
            }

            if (source[index] != '*')
            {
                index++;
                continue;
            }

            int runStart = index;
            while (index < end && source[index] == '*')
            {
                index++;
            }

            int effectiveRunLength = index - runStart;
            bool opensExtGlob = allowExtGlob
                && effectiveRunLength >= 2
                && index < end
                && source[index] == '(';

            if (opensExtGlob)
            {
                effectiveRunLength--;
            }

            if (effectiveRunLength >= 2)
            {
                return true;
            }

            if (opensExtGlob)
            {
                int close = FindExtGlobClose(source, index - 1, end);
                if (close < 0)
                {
                    return false;
                }

                index = close + 1;
            }
        }

        return false;
    }

    /// <summary>
    ///  Determines whether an expression contains a double-star outside extglob operators.
    /// </summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <param name="allowExtGlob">Whether extglob groups are recognized.</param>
    /// <returns>
    ///  <see langword="true"/> if an effective double-star is present; otherwise <see langword="false"/>.
    /// </returns>
    internal static bool ContainsEffectiveDoubleStar(
        ReadOnlySpan<char> expression,
        bool allowExtGlob) =>
        ContainsDirectEffectiveDoubleStar(
            expression,
            start: 0,
            expression.Length,
            allowExtGlob);

    private static bool ShouldRewriteStarDotStarAt(
        ReadOnlySpan<char> expression,
        int index,
        int end,
        bool allowExtGlob) =>
        index + 2 < end
        && expression[index] == '*'
        && expression[index + 1] == '.'
        && expression[index + 2] == '*'
        && (!allowExtGlob
            || index + 3 >= end
            || expression[index + 3] != '(');

    private static bool IsExtGlobOpener(ReadOnlySpan<char> source, int index, int end) =>
        index + 1 < end
        && source[index + 1] == '('
        && source[index] is '?' or '*' or '+' or '@' or '!';

    private static int FindExtGlobClose(ReadOnlySpan<char> source, int opener, int end)
    {
        int depth = 1;
        int index = opener + 2;
        while (index < end)
        {
            if (IsExtGlobOpener(source, index, end))
            {
                depth++;
                index += 2;
                continue;
            }

            if (source[index] == ')' && --depth == 0)
            {
                return index;
            }

            index++;
        }

        return -1;
    }

    private static void AppendRange(
        ReadOnlySpan<char> source,
        int start,
        int end,
        ref ValueStringBuilder builder,
        ArrayPoolList<int> sourcePositions)
    {
        for (int index = start; index < end; index++)
        {
            AppendCharacter(source[index], index, ref builder, sourcePositions);
        }
    }

    private static void AppendCharacter(
        char character,
        int sourcePosition,
        ref ValueStringBuilder builder,
        ArrayPoolList<int> sourcePositions)
    {
        builder.Append(character);
        sourcePositions.Add(sourcePosition);
    }

    private static bool IsAllFilesWildcard(ReadOnlySpan<char> expression) =>
        expression.SequenceEqual("*".AsSpan()) || expression.SequenceEqual("*.*".AsSpan());

    private static bool ContainsStarDotStar(ReadOnlySpan<char> expression) =>
        expression.IndexOf("*.*".AsSpan(), StringComparison.Ordinal) >= 0;

    private static bool UsesDosDotSemantics(ReadOnlySpan<char> expression) =>
        expression.Length >= 2
        && (expression[^1] == '.' || (expression[^2] == '.' && expression[^1] == '*'));

    private static bool IsSingleTrailingDotPattern(ReadOnlySpan<char> expression) =>
        !expression.IsEmpty
        && expression[^1] == '.'
        && expression[..^1].IndexOf("**".AsSpan(), StringComparison.Ordinal) < 0;

    private static bool ShouldEnforceLogicalMatch(ReadOnlySpan<char> expression)
    {
        for (int index = 0; index + 1 < expression.Length; index++)
        {
            if (expression[index] == '?' && expression[index + 1] == '.')
            {
                return true;
            }
        }

        int lastDot = expression.LastIndexOf('.');
        bool hasThreeCharacterExtension = lastDot >= 0 && expression.Length - lastDot == 4;
        return (hasThreeCharacterExtension && expression.IndexOf('*') >= 0)
            || (!expression.IsEmpty && expression[^1] == '?');
    }
}