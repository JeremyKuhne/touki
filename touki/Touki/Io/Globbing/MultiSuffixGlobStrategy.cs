// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Matches inputs that end with one of a fixed set of literal suffixes
///  (pattern of the form <c>@(*suffix1|*suffix2|...)</c>). Conceptually a
///  multi-arm <see cref="SuffixGlobStrategy"/>.
/// </summary>
/// <remarks>
///  <para>
///   The factory selects this strategy when the source pattern is
///   <c>**/&#x40;(*lit1|*lit2|...)</c> (or just <c>&#x40;(*lit1|*lit2|...)</c>
///   in path-unaware dialects) and each alternative is a pure
///   <c>AnyRun + Literal</c> shape. Routing the pattern here lets the
///   per-file hot path skip the bytecode interpreter and run a tight
///   <c>EndsWith</c> sweep over the suffix table, matching the throughput
///   of a hand-built <c>MatchSet</c> of N <c>SuffixGlobStrategy</c>
///   includes while keeping a single compiled spec.
///  </para>
///  <para>
///   <b>net481 design note.</b> The recursive extglob interpreter walks
///   each alternative under a separator-aware <c>AnyRun</c> loop, which
///   on .NET Framework 4.8.1 RyuJIT pays slow-span indexing for every
///   try-position. Specializing this shape collapses the work to one
///   <c>EndsWith</c> per suffix per file (vectorized on both target
///   frameworks). On the
///   <c>GlobEnumerateExtGlobPerf</c> benchmark this is the dominant
///   win on net481, closing the gap with the <c>MatchSet</c> baseline
///   on the canonical <c>**/&#x40;(*.cs|*.md|...)</c> shape. If you
///   change the strategy's selection criteria, verify the benchmark
///   on net481 first - modern .NET RyuJIT will tolerate
///   regressions the framework JIT cannot.
///  </para>
/// </remarks>
internal sealed class MultiSuffixGlobStrategy : GlobStrategy
{
    private readonly string[] _suffixes;

    public MultiSuffixGlobStrategy(string[] suffixes, GlobDialect dialect, GlobOptions options)
        : base(dialect, options)
    {
        Debug.Assert(suffixes.Length > 0);
        _suffixes = suffixes;
    }

    /// <inheritdoc/>
    internal override bool MatchCore(
        ReadOnlySpan<char> directoryPrefix,
        ReadOnlySpan<char> fileName)
    {
        // MultiSuffixGlobStrategy is only chosen for path-unaware dialects (or as the
        // segment matcher inside GlobStarFileNameStrategy, which calls with an empty
        // prefix); the directory prefix is always empty by construction.
        Debug.Assert(directoryPrefix.IsEmpty);

        IgnoreCaseKind caseKind = IgnoreCaseKind;
        string[] suffixes = _suffixes;

        // Leading-dot rule: the `*` at the start of every alternative is not allowed
        // to consume a leading `.`. If input starts with `.`, the alternative must
        // exactly equal the input (suffix begins at index 0 and itself starts with `.`).
        // Mirrors SuffixGlobStrategy.MatchCore but iterates the suffix table.
        if (!MatchLeadingDot && fileName.Length > 0 && fileName[0] == '.')
        {
            for (int i = 0; i < suffixes.Length; i++)
            {
                ReadOnlySpan<char> suffix = suffixes[i].AsSpan();
                bool match = caseKind switch
                {
                    IgnoreCaseKind.Ascii => fileName.EqualsAsciiLetterIgnoreCase(suffix),
                    IgnoreCaseKind.Unicode => fileName.EqualsOrdinalIgnoreCase(suffix),
                    _ => fileName.SequenceEqual(suffix),
                };
                if (match)
                {
                    return true;
                }
            }

            return false;
        }

        // Common case: per-suffix EndsWith. The strategy is exercised by **/@(...) on
        // every enumerated file so the inner loop stays tight: no allocations, no
        // bytecode, no path concatenation. EndsWith uses vectorized SequenceEqual on
        // both TFMs; on net481 this is the dominant reason the strategy exists.
        for (int i = 0; i < suffixes.Length; i++)
        {
            ReadOnlySpan<char> suffix = suffixes[i].AsSpan();
            bool match = caseKind switch
            {
                IgnoreCaseKind.Ascii => fileName.EndsWithAsciiLetterIgnoreCase(suffix),
                IgnoreCaseKind.Unicode => fileName.EndsWithOrdinalIgnoreCase(suffix),
                _ => fileName.EndsWith(suffix),
            };
            if (match)
            {
                return true;
            }
        }

        return false;
    }
}
