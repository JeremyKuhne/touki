// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Matches inputs of the form <c>prefix*suffix</c> -- a single <c>*</c> bracketed by
///  literal runs.
/// </summary>
internal sealed class PrefixSuffixGlobStrategy : GlobStrategy
{
    private readonly string _prefix;
    private readonly string _suffix;

    public PrefixSuffixGlobStrategy(string prefix, string suffix, GlobDialect dialect, GlobOptions options)
        : base(dialect, options)
    {
        _prefix = prefix;
        _suffix = suffix;
    }

    /// <inheritdoc/>
    internal override bool MatchCore(
        ReadOnlySpan<char> directoryPrefix,
        ReadOnlySpan<char> fileName)
    {
        // PrefixSuffixGlobStrategy is only chosen for path-unaware dialects; the
        // directory prefix is always empty by construction.
        Debug.Assert(directoryPrefix.IsEmpty);
        ReadOnlySpan<char> prefix = _prefix.AsSpan();
        ReadOnlySpan<char> suffix = _suffix.AsSpan();
        if (fileName.Length < prefix.Length + suffix.Length)
        {
            return false;
        }

        // The prefix is taken from the pattern and is not a wildcard, so it enforces the
        // leading-dot rule by literal compare.
        return IgnoreCaseKind switch
        {
            IgnoreCaseKind.Ascii =>
                fileName.StartsWithAsciiLetterIgnoreCase(prefix)
                && fileName.EndsWithAsciiLetterIgnoreCase(suffix),
            IgnoreCaseKind.Unicode =>
                fileName.StartsWithOrdinalIgnoreCase(prefix) && fileName.EndsWithOrdinalIgnoreCase(suffix),
            _ => fileName.StartsWith(prefix) && fileName.EndsWith(suffix),
        };
    }
}
