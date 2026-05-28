// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Runtime.CompilerServices;
using Touki.Text;

namespace touki.perf;

/// <summary>
///  Compares two strategies for the FileSystemGlobbing compile-time normalization
///  pass on <see cref="Touki.Io.Globbing.GlobSpecification"/>:
/// </summary>
/// <remarks>
///  <para>
///   <b>TwoPass</b> walks the source pattern once with an allocation-free scan to
///   decide whether any rewrite would fire, then walks it again into a stack-seeded
///   <see cref="ValueStringBuilder"/> only when a rewrite is needed. Patterns that
///   need no rewrite (the common case in real codebases - <c>**/*.cs</c>,
///   <c>src/**/*.cs</c>, etc.) pay zero allocations and zero string work.
///  </para>
///  <para>
///   <b>SinglePass</b> always builds the normalized form into a stack-seeded
///   <see cref="ValueStringBuilder"/>, then compares the buffer against the source
///   and only materializes a <see cref="string"/> when they differ. Patterns that
///   need no rewrite avoid the final allocation but still pay for the full build
///   pass and the equality scan.
///  </para>
///  <para>
///   The benchmark covers both shapes: realistic patterns that don't need any
///   rewrite, and a couple that exercise every rule in the rewrite catalogue.
///   Result counts and contents are validated against each other in
///   <see cref="GlobalSetup"/>.
///  </para>
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess, warmupCount: 1, iterationCount: 5, launchCount: 1)]
public class FileSystemGlobbingNormalizePerf
{
    private const char Separator = '/';

    /// <summary>
    ///  Pattern class under test:
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   - <see cref="PatternKind.NoChange"/>: realistic shapes that need no rewrite -
    ///     the common case in real-world usage.<br/>
    ///   - <see cref="PatternKind.LeadingDot"/>: <c>./**/hello.txt</c> - one segment
    ///     dropped, no rewrite of internal segments.<br/>
    ///   - <see cref="PatternKind.HeavyRewrite"/>:
    ///     <c>././**/./**/*.*</c> - every rule fires (leading <c>./</c>, internal
    ///     <c>/./</c>, adjacent <c>**/**/</c>, <c>*.*</c> segment).
    ///  </para>
    /// </remarks>
    public enum PatternKind
    {
        NoChange,
        LeadingDot,
        HeavyRewrite,
    }

    [Params(PatternKind.NoChange, PatternKind.LeadingDot, PatternKind.HeavyRewrite)]
    public PatternKind Kind { get; set; }

    private string _pattern = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _pattern = Kind switch
        {
            PatternKind.NoChange => "src/**/*.cs",
            PatternKind.LeadingDot => "./**/hello.txt",
            PatternKind.HeavyRewrite => "././**/./**/*.*",
            _ => throw new InvalidOperationException(),
        };

        // Sanity check: both strategies must agree on the rewritten output.
        ReadOnlySpan<char> twoPass = _pattern.AsSpan();
        NormalizeTwoPass(ref twoPass, Separator);

        ReadOnlySpan<char> singlePass = _pattern.AsSpan();
        NormalizeSinglePass(ref singlePass, Separator);

        if (!twoPass.SequenceEqual(singlePass))
        {
            throw new InvalidOperationException(
                $"Strategies disagree on '{_pattern}': two-pass='{twoPass.ToString()}', single-pass='{singlePass.ToString()}'.");
        }
    }

    [Benchmark(Baseline = true)]
    public int TwoPass()
    {
        ReadOnlySpan<char> pattern = _pattern.AsSpan();
        NormalizeTwoPass(ref pattern, Separator);
        return pattern.Length;
    }

    [Benchmark]
    public int SinglePass()
    {
        ReadOnlySpan<char> pattern = _pattern.AsSpan();
        NormalizeSinglePass(ref pattern, Separator);
        return pattern.Length;
    }

    // ---- Two-pass: detect, then rewrite only when needed ----

    [SkipLocalsInit]
    private static void NormalizeTwoPass(ref ReadOnlySpan<char> pattern, char separator)
    {
        if (!NeedsRewrite(pattern, separator))
        {
            return;
        }

        ValueStringBuilder builder = new(stackalloc char[256]);
        BuildRewritten(pattern, separator, ref builder);
        pattern = builder.ToStringAndDispose().AsSpan();
    }

    private static bool NeedsRewrite(ReadOnlySpan<char> pattern, char separator)
    {
        int n = pattern.Length;
        if (n == 0)
        {
            return false;
        }

        if (pattern[0] == separator && (n == 1 || pattern[1] != separator))
        {
            return true;
        }

        if (n >= 2 && pattern[0] == '.' && pattern[1] == separator)
        {
            return true;
        }

        int segStart = 0;
        int segIndex = 0;
        bool prevWasDoubleStar = false;

        for (int i = 0; i <= n; i++)
        {
            if (i < n && pattern[i] != separator)
            {
                continue;
            }

            ReadOnlySpan<char> seg = pattern[segStart..i];

            if (seg.Length == 1 && seg[0] == '.')
            {
                return true;
            }

            if (seg.Length == 3 && seg[0] == '*' && seg[1] == '.' && seg[2] == '*')
            {
                return true;
            }

            bool isDoubleStar = seg.Length == 2 && seg[0] == '*' && seg[1] == '*';

            if (isDoubleStar && prevWasDoubleStar)
            {
                return true;
            }

            if (isDoubleStar && i == n && segIndex > 0)
            {
                return true;
            }

            prevWasDoubleStar = isDoubleStar;
            segIndex++;
            segStart = i + 1;
        }

        return false;
    }

    // ---- Single-pass: always build, allocate only on difference ----

    [SkipLocalsInit]
    private static void NormalizeSinglePass(ref ReadOnlySpan<char> pattern, char separator)
    {
        if (pattern.IsEmpty)
        {
            return;
        }

        ValueStringBuilder builder = new(stackalloc char[256]);
        BuildRewritten(pattern, separator, ref builder);

        if (builder.AsSpan().SequenceEqual(pattern))
        {
            builder.Dispose();
            return;
        }

        pattern = builder.ToStringAndDispose().AsSpan();
    }

    // ---- Shared rewrite body ----

    private static void BuildRewritten(ReadOnlySpan<char> pattern, char separator, ref ValueStringBuilder builder)
    {
        int n = pattern.Length;
        int i = 0;

        while (i < n)
        {
            if (i + 1 < n && pattern[i] == '.' && pattern[i + 1] == separator)
            {
                i += 2;
                continue;
            }

            if (pattern[i] == separator && (i + 1 >= n || pattern[i + 1] != separator))
            {
                i++;
                continue;
            }

            break;
        }

        bool firstEmitted = true;
        bool prevWasDoubleStar = false;
        int segStart = i;

        while (true)
        {
            while (i < n && pattern[i] != separator)
            {
                i++;
            }

            ReadOnlySpan<char> seg = pattern[segStart..i];
            bool atEnd = i == n;

            bool dropSeg = seg.Length == 1 && seg[0] == '.';

            if (!dropSeg)
            {
                if (seg.Length == 3 && seg[0] == '*' && seg[1] == '.' && seg[2] == '*')
                {
                    seg = "*".AsSpan();
                }

                bool isDoubleStar = seg.Length == 2 && seg[0] == '*' && seg[1] == '*';

                if (isDoubleStar && prevWasDoubleStar)
                {
                    // Drop adjacent "**" segment.
                }
                else
                {
                    if (!firstEmitted)
                    {
                        builder.Append(separator);
                    }

                    builder.Append(seg);
                    firstEmitted = false;
                    prevWasDoubleStar = isDoubleStar;
                }
            }

            if (atEnd)
            {
                break;
            }

            i++;
            segStart = i;
        }

        if (builder.Length > 3
            && builder[builder.Length - 1] == '*'
            && builder[builder.Length - 2] == '*'
            && builder[builder.Length - 3] == separator)
        {
            builder.Length -= 2;
            builder.Append('*');
            builder.Append(separator);
            builder.Append('*');
            builder.Append('*');
        }
    }
}
