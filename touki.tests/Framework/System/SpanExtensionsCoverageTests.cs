// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Framework.System;

/// <summary>
///  Targets the primitive-specialization paths in
///  <c>SpanExtensions.Replace.cs</c> and <c>SpanExtensions.InRange.cs</c>,
///  plus a few small helpers that were previously uncovered.
/// </summary>
public class SpanExtensionsCoverageTests
{
    // ---------- Replace<T>(this Span<T>, T, T) primitive specializations ----------

    [Test]
    public void Replace_Byte_ReplacesAllOccurrences()
    {
        byte[] data = [1, 2, 3, 1, 2, 3, 1, 2, 3, 1];
        Span<byte> span = data;
        span.Replace((byte)2, (byte)9);
        span.ToArray().Should().Equal((byte)1, (byte)9, (byte)3, (byte)1, (byte)9, (byte)3, (byte)1, (byte)9, (byte)3, (byte)1);
    }

    [Test]
    public void Replace_Byte_AllSame_ReplacesEverything()
    {
        byte[] data = [5, 5, 5, 5, 5, 5, 5, 5];
        Span<byte> span = data;
        span.Replace((byte)5, (byte)0);
        span.ToArray().Should().AllSatisfy(b => b.Should().Be(0));
    }

    [Test]
    public void Replace_Byte_Empty_NoOp()
    {
        byte[] data = [];
        Span<byte> span = data;
        span.Replace((byte)1, (byte)2);
        span.IsEmpty.Should().BeTrue();
    }

    [Test]
    public void Replace_SByte_ReplacesAllOccurrences()
    {
        sbyte[] data = [-1, 2, -1, 4, -1, 6, -1, 8, -1, 10];
        Span<sbyte> span = data;
        span.Replace((sbyte)(-1), (sbyte)0);
        for (int i = 0; i < data.Length; i++)
        {
            data[i].Should().Be(i % 2 == 0 ? (sbyte)0 : (sbyte)(i + 1));
        }
    }

    [Test]
    public void Replace_Short_ReplacesAllOccurrences()
    {
        short[] data = [-1000, 2, -1000, 4, -1000, 6, -1000, 8, -1000, 10];
        Span<short> span = data;
        span.Replace((short)(-1000), (short)0);
        for (int i = 0; i < data.Length; i++)
        {
            data[i].Should().Be(i % 2 == 0 ? (short)0 : (short)(i + 1));
        }
    }

    [Test]
    public void Replace_UShort_ReplacesAllOccurrences()
    {
        ushort[] data = [1000, 2, 1000, 4, 1000, 6, 1000, 8, 1000, 10];
        Span<ushort> span = data;
        span.Replace((ushort)1000, (ushort)0);
        span.ToArray().Should().Equal((ushort)0, (ushort)2, (ushort)0, (ushort)4, (ushort)0, (ushort)6, (ushort)0, (ushort)8, (ushort)0, (ushort)10);
    }

    [Test]
    public void Replace_Char_ReplacesAllOccurrencesIncludingTailRemainder()
    {
        // length not a multiple of 4 to exercise the tail loop
        char[] data = "aXaXaXaXaXa".ToCharArray();
        Span<char> span = data;
        span.Replace('X', '_');
        new string(data).Should().Be("a_a_a_a_a_a");
    }

    [Test]
    public void Replace_Byte_LongSpan_ExercisesUnrolledLoop()
    {
        byte[] data = new byte[33];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i % 3);
        }

        Span<byte> span = data;
        span.Replace((byte)1, (byte)9);

        for (int i = 0; i < data.Length; i++)
        {
            data[i].Should().Be(i % 3 == 1 ? (byte)9 : (byte)(i % 3));
        }
    }

    // ---------- Replace(ReadOnlySpan<T>, Span<T>, T, T) primitive specializations ----------

    [Test]    public void Replace_ReadOnly_SByte_CopiesAndReplaces()
    {
        sbyte[] sourceArray = [-1, 2, -1, 4, -1];
        ReadOnlySpan<sbyte> source = sourceArray;
        Span<sbyte> dest = stackalloc sbyte[5];
        source.Replace(dest, (sbyte)(-1), (sbyte)0);
        for (int i = 0; i < dest.Length; i++)
        {
            dest[i].Should().Be(i % 2 == 0 ? (sbyte)0 : (sbyte)(i + 1));
        }
    }

    [Test]
    public void Replace_ReadOnly_Short_CopiesAndReplaces()
    {
        short[] sourceArray = [-1, 2, -1, 4, -1, 6, -1];
        ReadOnlySpan<short> source = sourceArray;
        Span<short> dest = stackalloc short[7];
        source.Replace(dest, (short)(-1), (short)0);
        for (int i = 0; i < dest.Length; i++)
        {
            dest[i].Should().Be(i % 2 == 0 ? (short)0 : (short)(i + 1));
        }
    }

    [Test]    public void Replace_ReadOnly_Byte_CopiesAndReplaces()
    {
        ReadOnlySpan<byte> source = [ 1, 2, 3, 1, 2, 3, 1, 2, 3, 1 ];
        Span<byte> dest = stackalloc byte[10];
        source.Replace(dest, (byte)2, (byte)9);
        dest.ToArray().Should().Equal((byte)1, (byte)9, (byte)3, (byte)1, (byte)9, (byte)3, (byte)1, (byte)9, (byte)3, (byte)1);
    }

    [Test]

    public void Replace_ReadOnly_Char_CopiesAndReplaces()
    {
        ReadOnlySpan<char> source = "aXbXcXdXeXf".AsSpan();
        Span<char> dest = stackalloc char[source.Length];
        source.Replace(dest, 'X', '_');
        dest.ToString().Should().Be("a_b_c_d_e_f");
    }

    [Test]
    public void Replace_ReadOnly_Byte_LongSpan_ExercisesUnrolledLoop()
    {
        byte[] sourceArray = new byte[33];
        for (int i = 0; i < sourceArray.Length; i++)
        {
            sourceArray[i] = (byte)(i % 3);
        }

        ReadOnlySpan<byte> source = sourceArray;
        Span<byte> dest = stackalloc byte[33];
        source.Replace(dest, (byte)1, (byte)9);

        for (int i = 0; i < dest.Length; i++)
        {
            dest[i].Should().Be(i % 3 == 1 ? (byte)9 : (byte)(i % 3));
        }
    }

    [Test]
    public void Replace_ReadOnly_DestinationTooShort_Throws()
    {
        byte[] data = [1, 2, 3, 4, 5];
        byte[] destBuffer = new byte[3];

        Action action = () =>
        {
            ReadOnlySpan<byte> source = data;
            Span<byte> dest = destBuffer;
            source.Replace(dest, (byte)1, (byte)0);
        };

        action.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Replace_ReadOnly_OldEqualsNew_CopiesUnchanged()
    {
        ReadOnlySpan<byte> source = [ 1, 2, 3, 4, 5 ];
        Span<byte> dest = stackalloc byte[5];
        source.Replace(dest, (byte)1, (byte)1);
        dest.ToArray().Should().Equal((byte)1, (byte)2, (byte)3, (byte)4, (byte)5);
    }

    // ---------- IndexOfAnyInRange specializations (sbyte / short / ushort / int / uint / ulong) ----------

    [Test]
    public void IndexOfAnyInRange_SByte_FindsFirst()
    {
        sbyte[] data = [-100, -50, 0, 50, 100];
        ((ReadOnlySpan<sbyte>)data).IndexOfAnyInRange((sbyte)(-10), (sbyte)10).Should().Be(2);
        ((ReadOnlySpan<sbyte>)data).IndexOfAnyInRange((sbyte)127, (sbyte)127).Should().Be(-1);
    }

    [Test]
    public void IndexOfAnyInRange_Short_FindsFirst()
    {
        short[] data = [-1000, -500, 0, 500, 1000];
        ((ReadOnlySpan<short>)data).IndexOfAnyInRange((short)(-100), (short)100).Should().Be(2);
        ((ReadOnlySpan<short>)data).IndexOfAnyInRange((short)2000, (short)3000).Should().Be(-1);
    }

    [Test]
    public void IndexOfAnyInRange_UShort_FindsFirst()
    {
        ushort[] data = [1000, 2000, 3000, 4000];
        ((ReadOnlySpan<ushort>)data).IndexOfAnyInRange((ushort)2500, (ushort)3500).Should().Be(2);
        ((ReadOnlySpan<ushort>)data).IndexOfAnyInRange((ushort)10000, (ushort)20000).Should().Be(-1);
    }

    [Test]
    public void IndexOfAnyInRange_Int_FindsFirst()
    {
        int[] data = [-1000, -500, 0, 500, 1000];
        ((ReadOnlySpan<int>)data).IndexOfAnyInRange(-100, 100).Should().Be(2);
        ((ReadOnlySpan<int>)data).IndexOfAnyInRange(2000, 3000).Should().Be(-1);
    }

    [Test]
    public void IndexOfAnyInRange_UInt_FindsFirst()
    {
        uint[] data = [1, 100, 200, 300, 400];
        ((ReadOnlySpan<uint>)data).IndexOfAnyInRange((uint)150, (uint)250).Should().Be(2);
        ((ReadOnlySpan<uint>)data).IndexOfAnyInRange((uint)1000, (uint)2000).Should().Be(-1);
    }

    [Test]
    public void IndexOfAnyInRange_ULong_FindsFirst()
    {
        ulong[] data = [1, 100, 200, 300, 400];
        ((ReadOnlySpan<ulong>)data).IndexOfAnyInRange((ulong)150, (ulong)250).Should().Be(2);
        ((ReadOnlySpan<ulong>)data).IndexOfAnyInRange((ulong)1000, (ulong)2000).Should().Be(-1);
    }

    [Test]
    public void IndexOfAnyExceptInRange_SByte_FindsFirst()
    {
        sbyte[] data = [1, 2, 3, -50, 4, 5];
        ((ReadOnlySpan<sbyte>)data).IndexOfAnyExceptInRange((sbyte)1, (sbyte)10).Should().Be(3);
    }

    [Test]
    public void IndexOfAnyExceptInRange_UShort_FindsFirst()
    {
        ushort[] data = [10, 20, 30, 5000, 40];
        ((ReadOnlySpan<ushort>)data).IndexOfAnyExceptInRange((ushort)0, (ushort)100).Should().Be(3);
    }

    [Test]
    public void IndexOfAnyExceptInRange_Int_FindsFirst()
    {
        int[] data = [10, 20, 30, -5000, 40];
        ((ReadOnlySpan<int>)data).IndexOfAnyExceptInRange(0, 100).Should().Be(3);
    }

    [Test]
    public void IndexOfAnyExceptInRange_ULong_FindsFirst()
    {
        ulong[] data = [10, 20, 30, 50000, 40];
        ((ReadOnlySpan<ulong>)data).IndexOfAnyExceptInRange((ulong)0, (ulong)100).Should().Be(3);
    }

    // ---------- LastIndexOfAnyInRange specializations ----------

    [Test]
    public void LastIndexOfAnyInRange_Generic_FindsLast()
    {
        // string is IComparable<string> but not specialized → exercises the fallback.
        string[] data = ["a", "g", "k", "z"];
        ((ReadOnlySpan<string>)data).LastIndexOfAnyInRange("b", "m").Should().Be(2);
        ((ReadOnlySpan<string>)data).LastIndexOfAnyInRange("0", "9").Should().Be(-1);
    }

    [Test]
    public void LastIndexOfAnyInRange_Int_FindsLast()
    {
        int[] data = [10, 20, 30, 40, 50];
        ((ReadOnlySpan<int>)data).LastIndexOfAnyInRange(15, 35).Should().Be(2);
        ((ReadOnlySpan<int>)data).LastIndexOfAnyInRange(100, 200).Should().Be(-1);
    }

    [Test]
    public void LastIndexOfAnyExceptInRange_Int_FindsLast()
    {
        int[] data = [1, 2, 3, 999, 4, 5];
        ((ReadOnlySpan<int>)data).LastIndexOfAnyExceptInRange(0, 10).Should().Be(3);
    }

    [Test]
    public void LastIndexOfAnyInRange_Byte_NoMatch()
    {
        byte[] data = [1, 2, 3, 4];
        ((ReadOnlySpan<byte>)data).LastIndexOfAnyInRange((byte)100, (byte)200).Should().Be(-1);
    }

    [Test]
    public void LastIndexOfAnyInRange_Char_NoMatch()
    {
        // Exercises the no-match return in the char specialization.
        char[] data = ['a', 'b', 'c'];
        ((ReadOnlySpan<char>)data).LastIndexOfAnyInRange('0', '9').Should().Be(-1);
    }

    [Test]
    public void IndexOfAnyInRange_Long_NoMatch()
    {
        // Exercises the no-match return in the long specialization.
        long[] data = [long.MinValue, -1L, 0L];
        ((ReadOnlySpan<long>)data).IndexOfAnyInRange(100L, 200L).Should().Be(-1);
    }

    [Test]
    public void IndexOfAnyInRange_StringFallback_NoMatch_ReturnsMinusOne()
    {
        // Exercises the no-match return in the generic IComparable fallback.
        string[] data = ["apple", "banana", "cherry"];
        ((ReadOnlySpan<string>)data).IndexOfAnyInRange("0", "9").Should().Be(-1);
    }

    // ---------- Span<T> overloads (just delegate to ReadOnlySpan) ----------

    [Test]
    public void IndexOfAnyInRange_SpanOverload_Int()
    {
        Span<int> data = stackalloc int[] { -10, 0, 5, 10, 100 };
        data.IndexOfAnyInRange(1, 9).Should().Be(2);
        data.IndexOfAnyExceptInRange(-100, 100).Should().Be(-1);
    }

    [Test]
    public void LastIndexOfAnyInRange_SpanOverload_Int()
    {
        Span<int> data = stackalloc int[] { 1, 5, 10, 5, 1 };
        data.LastIndexOfAnyInRange(4, 6).Should().Be(3);
        data.LastIndexOfAnyExceptInRange(4, 6).Should().Be(4);
    }

    [Test]
    public void ContainsAnyInRange_SpanOverload_Byte()
    {
        Span<byte> data = stackalloc byte[] { 1, 2, 3, 50, 4 };
        data.ContainsAnyInRange((byte)40, (byte)60).Should().BeTrue();
        data.ContainsAnyInRange((byte)100, (byte)200).Should().BeFalse();
        data.ContainsAnyExceptInRange((byte)1, (byte)10).Should().BeTrue();
        data.ContainsAnyExceptInRange((byte)0, (byte)100).Should().BeFalse();
    }
}

