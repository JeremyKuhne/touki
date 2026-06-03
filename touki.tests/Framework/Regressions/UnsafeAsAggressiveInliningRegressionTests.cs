// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#pragma warning disable CS8500 // takes the address of, gets the size of, or declares a pointer to a managed type

namespace Touki.Framework.Regressions;

/// <summary>
///  Regression coverage for the .NET Framework 4.8.1 RyuJIT codegen bug
///  documented in <c>touki.perf/ReplaceUnsafeAsPerf.cs</c> and in the
///  <c>polyfill-dotnet-api</c> skill. Inside an
///  <see cref="MethodImplOptions.AggressiveInlining"/> method,
///  <c>Unsafe.As&lt;T, byte&gt;(ref methodParameter)</c> on a literal
///  signed-primitive caller (<c>(sbyte)-1</c>, <c>(short)-1</c>) leaks the
///  32-bit sign-extended constant <c>0xFFFFFFFF</c> through the JIT's
///  constant tracker. The byte-domain compare against a <c>movzx</c>-loaded
///  byte (range [0, 0xFF]) is then always false. The masked form
///  <c>(byte)(Unsafe.As&lt;T, byte&gt;(ref v) &amp; 0xFF)</c> forces the
///  truncation in the int domain and produces correct codegen.
/// </summary>
/// <remarks>
///  <para>
///   This test must run on Release on net481 to actually exercise the
///   buggy codegen path. Debug disables AggressiveInlining; modern .NET
///   RyuJIT does not exhibit the bug. The test is correct on every
///   configuration; it only *pins* the bug class on net481 Release.
///  </para>
/// </remarks>
[TestClass]
public class UnsafeAsAggressiveInliningRegressionTests
{
    [TestMethod]
    public void Replace_SByteNegativeOne_ReplacesAllOccurrences()
    {
        sbyte[] data = [-1, 2, -1, 4, -1, 6, -1, 8];
        ReplaceMasked<sbyte>(data, -1, 0);
        data.Should().Equal((sbyte)0, (sbyte)2, (sbyte)0, (sbyte)4, (sbyte)0, (sbyte)6, (sbyte)0, (sbyte)8);
    }

    [TestMethod]
    public void Replace_ByteMaxValue_ReplacesAllOccurrences()
    {
        byte[] data = [0xFF, 2, 0xFF, 4, 0xFF, 6, 0xFF, 8];
        ReplaceMasked<byte>(data, 0xFF, 0);
        data.Should().Equal((byte)0, (byte)2, (byte)0, (byte)4, (byte)0, (byte)6, (byte)0, (byte)8);
    }

    [TestMethod]
    public void Replace_ShortNegativeOne_ReplacesAllOccurrences()
    {
        short[] data = [-1, 2, -1, 4, -1, 6, -1, 8];
        ReplaceMaskedShort<short>(data, -1, 0);
        data.Should().Equal((short)0, (short)2, (short)0, (short)4, (short)0, (short)6, (short)0, (short)8);
    }

    [TestMethod]
    public void Replace_UShortMaxValue_ReplacesAllOccurrences()
    {
        ushort[] data = [0xFFFF, 2, 0xFFFF, 4, 0xFFFF, 6, 0xFFFF, 8];
        ReplaceMaskedShort<ushort>(data, 0xFFFF, 0);
        data.Should().Equal((ushort)0, (ushort)2, (ushort)0, (ushort)4, (ushort)0, (ushort)6, (ushort)0, (ushort)8);
    }

    // ----- Search-shape coverage (IndexOfAnyExcept / LastIndexOfAnyExcept) -----
    //
    // The same JIT bug class affects SpanExtensions.Search.cs's
    // IndexOfAnyExcept / LastIndexOfAnyExcept polyfills. Tests in
    // SpanExtensionsSearchExtraTests.cs already cover these for sbyte with
    // (sbyte)(-1) literal inputs; this section exists so the regression is
    // pinned by name to the JIT-bug class, not just to the public API.
    // Removing the `& 0xFF` / `& 0xFFFF` mask in
    // touki/Framework/Polyfills/System/SpanExtensions.Search.cs will fail
    // these tests on net481 Release.

    [TestMethod]
    public void IndexOfAnyExcept_SByteNegativeOne_FindsFirstNonMatch()
    {
        ReadOnlySpan<sbyte> span = [(sbyte)(-1), (sbyte)(-1), (sbyte)9];
        span.IndexOfAnyExcept((sbyte)(-1)).Should().Be(2);
    }

    [TestMethod]
    public void IndexOfAnyExcept_TwoValues_SByteNegativeOne_FindsFirstNonMatch()
    {
        ReadOnlySpan<sbyte> span = [(sbyte)(-1), (sbyte)2, (sbyte)9];
        span.IndexOfAnyExcept((sbyte)(-1), (sbyte)2).Should().Be(2);
    }

    [TestMethod]
    public void IndexOfAnyExcept_ThreeValues_SByteNegativeOne_FindsFirstNonMatch()
    {
        ReadOnlySpan<sbyte> span = [(sbyte)(-1), (sbyte)2, (sbyte)3, (sbyte)9];
        span.IndexOfAnyExcept((sbyte)(-1), (sbyte)2, (sbyte)3).Should().Be(3);
    }

    [TestMethod]
    public void LastIndexOfAnyExcept_SByteNegativeOne_FindsLastNonMatch()
    {
        ReadOnlySpan<sbyte> span = [(sbyte)9, (sbyte)(-1), (sbyte)(-1)];
        span.LastIndexOfAnyExcept((sbyte)(-1)).Should().Be(0);
    }

    [TestMethod]
    public void LastIndexOfAnyExcept_TwoValues_SByteNegativeOne_FindsLastNonMatch()
    {
        ReadOnlySpan<sbyte> span = [(sbyte)9, (sbyte)(-1), (sbyte)2];
        span.LastIndexOfAnyExcept((sbyte)(-1), (sbyte)2).Should().Be(0);
    }

    [TestMethod]
    public void IndexOfAnyExcept_ShortNegativeOne_FindsFirstNonMatch()
    {
        ReadOnlySpan<short> span = [(short)(-1), (short)(-1), (short)9];
        span.IndexOfAnyExcept((short)(-1)).Should().Be(2);
    }

    [TestMethod]
    public void LastIndexOfAnyExcept_ShortNegativeOne_FindsLastNonMatch()
    {
        ReadOnlySpan<short> span = [(short)9, (short)(-1), (short)(-1)];
        span.LastIndexOfAnyExcept((short)(-1)).Should().Be(0);
    }

    /// <summary>
    ///  Byte/sbyte replace using the masked-int-domain pattern. Removing the
    ///  <c>&amp; 0xFF</c> mask resurrects the net481 Release bug.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void ReplaceMasked<T>(Span<T> span, T oldValue, T newValue)
        where T : struct
    {
        if (typeof(T) != typeof(byte) && typeof(T) != typeof(sbyte))
        {
            throw new InvalidOperationException();
        }

        byte oldByte = (byte)(Unsafe.As<T, byte>(ref oldValue) & 0xFF);
        byte newByte = (byte)(Unsafe.As<T, byte>(ref newValue) & 0xFF);
        fixed (T* p = span)
        {
            byte* ptr = (byte*)p;
            byte* end = ptr + span.Length;
            while (ptr < end)
            {
                if (*ptr == oldByte)
                {
                    *ptr = newByte;
                }

                ptr++;
            }
        }
    }

    /// <summary>
    ///  short/ushort replace, same pattern in the 16-bit domain.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void ReplaceMaskedShort<T>(Span<T> span, T oldValue, T newValue)
        where T : struct
    {
        if (typeof(T) != typeof(short) && typeof(T) != typeof(ushort))
        {
            throw new InvalidOperationException();
        }

        ushort oldShort = (ushort)(Unsafe.As<T, ushort>(ref oldValue) & 0xFFFF);
        ushort newShort = (ushort)(Unsafe.As<T, ushort>(ref newValue) & 0xFFFF);
        fixed (T* p = span)
        {
            ushort* ptr = (ushort*)p;
            ushort* end = ptr + span.Length;
            while (ptr < end)
            {
                if (*ptr == oldShort)
                {
                    *ptr = newShort;
                }

                ptr++;
            }
        }
    }
}
