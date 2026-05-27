// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Matches inputs that are byte-for-byte equal to a fixed literal.
/// </summary>
internal sealed class LiteralGlobStrategy : GlobStrategy
{
    private readonly string _literal;
    private readonly string _literalPathPrefix;

    public LiteralGlobStrategy(string literal, GlobDialect dialect, GlobOptions options)
        : base(dialect, options)
    {
        _literal = literal;
        _literalPathPrefix = ComputeLiteralPathPrefix(literal, Separator);
    }

    /// <inheritdoc/>
    /// <remarks>
    ///  <para>
    ///   Compares the logical concatenation <paramref name="directoryPrefix"/> +
    ///   <paramref name="fileName"/> against the stored literal without copying either
    ///   span. The literal is split at <c>directoryPrefix.Length</c> so each half stays
    ///   contiguous on its source span and the inner comparisons use the same vectorized
    ///   routines as the path-unaware fast paths. Path-unaware matchers receive an
    ///   empty <paramref name="directoryPrefix"/> and the implementation collapses to a
    ///   single full-span equality. When <paramref name="directoryPrefix"/> is
    ///   non-empty the caller has appended <see cref="GlobStrategy.Separator"/> at
    ///   the end, so the split at <c>directoryPrefix.Length</c> aligns exactly with
    ///   the literal's directory / file-name boundary.
    ///  </para>
    /// </remarks>
    internal override bool MatchCore(
        ReadOnlySpan<char> directoryPrefix,
        ReadOnlySpan<char> fileName)
    {
        int total = directoryPrefix.Length + fileName.Length;
        if (total != _literal.Length)
        {
            return false;
        }

        ReadOnlySpan<char> literal = _literal.AsSpan();
        ReadOnlySpan<char> literalPrefix = literal[..directoryPrefix.Length];
        ReadOnlySpan<char> literalFileName = literal[directoryPrefix.Length..];

        return IgnoreCaseKind switch
        {
            IgnoreCaseKind.Ascii =>
                directoryPrefix.EqualsAsciiLetterIgnoreCase(literalPrefix)
                && fileName.EqualsAsciiLetterIgnoreCase(literalFileName),
            IgnoreCaseKind.Unicode =>
                directoryPrefix.EqualsOrdinalIgnoreCase(literalPrefix)
                && fileName.EqualsOrdinalIgnoreCase(literalFileName),
            _ =>
                directoryPrefix.SequenceEqual(literalPrefix)
                && fileName.SequenceEqual(literalFileName),
        };
    }

    /// <inheritdoc/>
    internal override string LiteralPathPrefix => _literalPathPrefix;

    /// <summary>
    ///  Returns the literal up to and including its last separator. The whole literal
    ///  is the pattern, so any relative directory that diverges from this prefix can
    ///  never lead to a match.
    /// </summary>
    private static string ComputeLiteralPathPrefix(string literal, char separator)
    {
        if (separator == '\0' || literal.Length == 0)
        {
            return string.Empty;
        }

        int lastSeparator = literal.LastIndexOf(separator);
        return lastSeparator < 0 ? string.Empty : literal[..(lastSeparator + 1)];
    }
}
