// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Matches inputs that end with a fixed literal suffix (pattern of the form <c>*suffix</c>).
/// </summary>
internal sealed class SuffixGlobStrategy : GlobStrategy
{
    private readonly string _suffix;

    public SuffixGlobStrategy(string suffix, GlobDialect dialect, GlobOptions options)
        : base(dialect, options)
    {
        _suffix = suffix;
    }

    /// <inheritdoc/>
    internal override bool MatchCore(
        ReadOnlySpan<char> directoryPrefix,
        ReadOnlySpan<char> fileName)
    {
        // SuffixGlobStrategy is only chosen for path-unaware dialects; the directory
        // prefix is always empty by construction.
        Debug.Assert(directoryPrefix.IsEmpty);
        ReadOnlySpan<char> suffix = _suffix.AsSpan();

        // Leading-dot rule: the '*' at the start of the pattern is not allowed to consume
        // a leading '.'. If input starts with '.', the suffix would have to begin at index 0
        // (and itself start with '.') to satisfy the rule. We require the suffix to exactly
        // equal the input in that case.
        if (!MatchLeadingDot && fileName.Length > 0 && fileName[0] == '.')
        {
            return IgnoreCaseKind switch
            {
                IgnoreCaseKind.Ascii => fileName.EqualsAsciiLetterIgnoreCase(suffix),
                IgnoreCaseKind.Unicode => fileName.EqualsOrdinalIgnoreCase(suffix),
                _ => fileName.SequenceEqual(suffix),
            };
        }

        return IgnoreCaseKind switch
        {
            IgnoreCaseKind.Ascii => fileName.EndsWithAsciiLetterIgnoreCase(suffix),
            IgnoreCaseKind.Unicode => fileName.EndsWithOrdinalIgnoreCase(suffix),
            _ => fileName.EndsWith(suffix),
        };
    }
}
