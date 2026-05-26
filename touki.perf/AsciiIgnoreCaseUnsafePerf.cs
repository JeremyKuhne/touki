// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace touki.perf;

/// <summary>
///  Three implementations of an ASCII-fold equality compare for character spans.
///  All produce the same result; they differ only in how they access span data.
///  The point is to quantify the "slow span" tax on net481 RyuJIT and see whether
///  <c>ref char</c> + <see cref="Unsafe"/> or a pinned <c>char*</c> can recover it.
/// </summary>
/// <remarks>
///  <para>
///   Run with <c>[DisassemblyDiagnoser]</c> to inspect codegen on each TFM.
///   Findings feed back into <c>Touki.Text.AsciiIgnoreCase</c> in production code.
///  </para>
/// </remarks>
[MemoryDiagnoser]
[DisassemblyDiagnoser(maxDepth: 2, printSource: true, exportGithubMarkdown: true)]
[SimpleJob(RuntimeMoniker.HostProcess, warmupCount: 1, iterationCount: 5, launchCount: 1)]
public class AsciiIgnoreCaseUnsafePerf
{
    [Params(5, 10, 20, 64)]
    public int Length { get; set; }

    private string _a = null!;
    private string _b = null!;

    [GlobalSetup]
    public void Setup()
    {
        _a = new string('a', Length);
        _b = new string('A', Length);
    }

    [Benchmark(Baseline = true)]
    public bool Span_AsciiFold() => SpanFold(_a.AsSpan(), _b.AsSpan());

    [Benchmark]
    public bool Ref_AsciiFold() => RefFold(_a.AsSpan(), _b.AsSpan());

    [Benchmark]
    public bool Pinned_AsciiFold() => PinnedFold(_a.AsSpan(), _b.AsSpan());

    /// <summary>
    ///  Span-based baseline. Identical to <c>Touki.Text.AsciiIgnoreCase.EqualsAsciiFold</c>.
    /// </summary>
    private static bool SpanFold(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        for (int i = 0; i < a.Length; i++)
        {
            char x = a[i];
            char y = b[i];

            if (((uint)x | y) > 0x7F)
            {
                return a[i..].Equals(b[i..], StringComparison.OrdinalIgnoreCase);
            }

            if (x != y)
            {
                if ((uint)((x | 0x20) - 'a') > 'z' - 'a'
                    || (uint)((y | 0x20) - 'a') > 'z' - 'a'
                    || (x | 0x20) != (y | 0x20))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    ///  Resolves the span data pointer once via <c>MemoryMarshal.GetReference</c> and
    ///  walks with <see cref="Unsafe.Add{T}(ref T, int)"/>. The slow-span pointer dance
    ///  on net481 happens at most twice (once per span) instead of per character.
    /// </summary>
    private static bool RefFold(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
    {
        int length = a.Length;
        if (length != b.Length)
        {
            return false;
        }

        ref char pa = ref MemoryMarshal.GetReference(a);
        ref char pb = ref MemoryMarshal.GetReference(b);

        for (int i = 0; i < length; i++)
        {
            char x = Unsafe.Add(ref pa, i);
            char y = Unsafe.Add(ref pb, i);

            if (((uint)x | y) > 0x7F)
            {
                return a[i..].Equals(b[i..], StringComparison.OrdinalIgnoreCase);
            }

            if (x != y)
            {
                if ((uint)((x | 0x20) - 'a') > 'z' - 'a'
                    || (uint)((y | 0x20) - 'a') > 'z' - 'a'
                    || (x | 0x20) != (y | 0x20))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    ///  Pins both spans and walks raw <c>char*</c> pointers. Eliminates the managed-ref
    ///  bookkeeping entirely inside the loop at the cost of two GC pin operations on
    ///  entry/exit.
    /// </summary>
    private static unsafe bool PinnedFold(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
    {
        int length = a.Length;
        if (length != b.Length)
        {
            return false;
        }

        fixed (char* pa = a)
        fixed (char* pb = b)
        {
            for (int i = 0; i < length; i++)
            {
                char x = pa[i];
                char y = pb[i];

                if (((uint)x | y) > 0x7F)
                {
                    return a[i..].Equals(b[i..], StringComparison.OrdinalIgnoreCase);
                }

                if (x != y)
                {
                    if ((uint)((x | 0x20) - 'a') > 'z' - 'a'
                        || (uint)((y | 0x20) - 'a') > 'z' - 'a'
                        || (x | 0x20) != (y | 0x20))
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }
}
