// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Matches inputs that contain a fixed literal substring (pattern of the form <c>*needle*</c>).
/// </summary>
internal sealed class ContainsGlobStrategy : GlobStrategy
{
    private readonly string _needle;

    public ContainsGlobStrategy(string needle, GlobDialect dialect, GlobOptions options)
        : base(dialect, options)
    {
        _needle = needle;
    }

    /// <inheritdoc/>
    internal override bool MatchCore(
        ReadOnlySpan<char> directoryPrefix,
        ReadOnlySpan<char> fileName)
    {
        // ContainsGlobStrategy is only chosen for path-unaware dialects; the directory
        // prefix is always empty by construction.
        Debug.Assert(directoryPrefix.IsEmpty);
        ReadOnlySpan<char> needle = _needle.AsSpan();

        if (!MatchLeadingDot && fileName.Length > 0 && fileName[0] == '.')
        {
            // Leading-dot rule: the leading '*' cannot consume the '.', so the needle must
            // begin at index 0 (and the needle itself must therefore start with '.').
            if (fileName.Length < needle.Length)
            {
                return false;
            }

            return IgnoreCaseKind switch
            {
                IgnoreCaseKind.Ascii => fileName.StartsWithAsciiLetterIgnoreCase(needle),
                IgnoreCaseKind.Unicode => fileName.StartsWith(needle, StringComparison.OrdinalIgnoreCase),
                _ => fileName.StartsWith(needle),
            };
        }

        return IgnoreCaseKind switch
        {
            IgnoreCaseKind.Ascii => IndexOfAsciiLetterIgnoreCase(fileName, needle) >= 0,
            IgnoreCaseKind.Unicode => fileName.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0,
            _ => fileName.IndexOf(needle) >= 0,
        };
    }

    /// <summary>
    ///  Returns the index of the first occurrence of <paramref name="needle"/> in
    ///  <paramref name="haystack"/> using ASCII-letter-only case folding (the POSIX
    ///  ignore-case rule), or <c>-1</c> when not found. Non-ASCII characters compare
    ///  ordinally.
    /// </summary>
    private static int IndexOfAsciiLetterIgnoreCase(ReadOnlySpan<char> haystack, ReadOnlySpan<char> needle)
    {
        if (needle.IsEmpty)
        {
            return 0;
        }

        int limit = haystack.Length - needle.Length;
        for (int i = 0; i <= limit; i++)
        {
            if (haystack.Slice(i, needle.Length).EqualsAsciiLetterIgnoreCase(needle))
            {
                return i;
            }
        }
        return -1;
    }
}
