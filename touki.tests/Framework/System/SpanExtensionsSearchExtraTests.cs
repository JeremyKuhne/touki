// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace System;

/// <summary>
///  Tests for the <c>LastIndexOfAnyExcept</c> / <c>IndexOfAnyExcept</c> /
///  <c>ContainsAny*</c> / <c>Count</c> overloads on <c>SpanExtensions</c>. The
///  existing <c>SpanExtensionsSearchTests</c> covers <c>int</c>; this file adds
///  the type-specialized branches the polyfill picks for <c>byte</c>, <c>char</c>,
///  <c>short</c>, <c>long</c>, plus the <c>Span&lt;T&gt;</c> wrappers and the
///  multi-value overloads. Patterns are adapted from
///  <c>dotnet/runtime/src/libraries/System.Memory/tests/Span/LastIndexOfAny*.cs</c>
///  and <c>IndexOfAnyExcept.T.cs</c>.
/// </summary>
public class SpanExtensionsSearchExtraTests
{
    // ----- LastIndexOfAnyExcept (single value) -----

    [Fact]
    public void LastIndexOfAnyExcept_SingleValue_AllMatch_ReturnsMinusOne()
    {
        ReadOnlySpan<int> span = [7, 7, 7, 7];
        span.LastIndexOfAnyExcept(7).Should().Be(-1);
    }

    [Fact]
    public void LastIndexOfAnyExcept_SingleValue_LastDifferent_ReturnsLastIndex()
    {
        ReadOnlySpan<int> span = [7, 7, 7, 5];
        span.LastIndexOfAnyExcept(7).Should().Be(3);
    }

    [Fact]
    public void LastIndexOfAnyExcept_SingleValue_FirstDifferent_ReturnsFirstIndex()
    {
        ReadOnlySpan<int> span = [5, 7, 7, 7];
        span.LastIndexOfAnyExcept(7).Should().Be(0);
    }

    [Fact]
    public void LastIndexOfAnyExcept_SingleValue_Empty_ReturnsMinusOne()
    {
        ReadOnlySpan<int> span = [];
        span.LastIndexOfAnyExcept(0).Should().Be(-1);
    }

    [Fact]
    public void LastIndexOfAnyExcept_Byte_FindsLastNonMatch()
    {
        ReadOnlySpan<byte> span = [(byte)1, (byte)1, (byte)2, (byte)1];
        span.LastIndexOfAnyExcept((byte)1).Should().Be(2);
    }

    [Fact]
    public void LastIndexOfAnyExcept_Char_FindsLastNonMatch()
    {
        ReadOnlySpan<char> span = "aaab".AsSpan();
        span.LastIndexOfAnyExcept('a').Should().Be(3);
    }

    [Fact]
    public void LastIndexOfAnyExcept_Short_FindsLastNonMatch()
    {
        ReadOnlySpan<short> span = [(short)0, (short)0, (short)1];
        span.LastIndexOfAnyExcept((short)0).Should().Be(2);
    }

    [Fact]
    public void LastIndexOfAnyExcept_Long_FindsLastNonMatch()
    {
        ReadOnlySpan<long> span = [0L, 0L, 1L];
        span.LastIndexOfAnyExcept(0L).Should().Be(2);
    }

    // ----- LastIndexOfAnyExcept (two values) -----

    [Fact]
    public void LastIndexOfAnyExcept_TwoValues_AllMatch_ReturnsMinusOne()
    {
        ReadOnlySpan<int> span = [1, 2, 1, 2];
        span.LastIndexOfAnyExcept(1, 2).Should().Be(-1);
    }

    [Fact]
    public void LastIndexOfAnyExcept_TwoValues_OneOutlier_ReturnsItsIndex()
    {
        ReadOnlySpan<int> span = [1, 2, 9, 1, 2];
        span.LastIndexOfAnyExcept(1, 2).Should().Be(2);
    }

    [Fact]
    public void LastIndexOfAnyExcept_TwoValues_Byte_FindsLastNonMatch()
    {
        ReadOnlySpan<byte> span = [(byte)1, (byte)2, (byte)9];
        span.LastIndexOfAnyExcept((byte)1, (byte)2).Should().Be(2);
    }

    [Fact]
    public void LastIndexOfAnyExcept_TwoValues_Char_FindsLastNonMatch()
    {
        ReadOnlySpan<char> span = "abXab".AsSpan();
        span.LastIndexOfAnyExcept('a', 'b').Should().Be(2);
    }

    // ----- LastIndexOfAnyExcept (three values) -----

    [Fact]
    public void LastIndexOfAnyExcept_ThreeValues_AllMatch_ReturnsMinusOne()
    {
        ReadOnlySpan<int> span = [1, 2, 3, 1];
        span.LastIndexOfAnyExcept(1, 2, 3).Should().Be(-1);
    }

    [Fact]
    public void LastIndexOfAnyExcept_ThreeValues_OneOutlier_ReturnsItsIndex()
    {
        ReadOnlySpan<int> span = [1, 2, 3, 9, 1, 2];
        span.LastIndexOfAnyExcept(1, 2, 3).Should().Be(3);
    }

    [Fact]
    public void LastIndexOfAnyExcept_ThreeValues_Byte_FindsLastNonMatch()
    {
        ReadOnlySpan<byte> span = [(byte)1, (byte)2, (byte)3, (byte)9];
        span.LastIndexOfAnyExcept((byte)1, (byte)2, (byte)3).Should().Be(3);
    }

    // ----- LastIndexOfAnyExcept (values span) -----

    [Fact]
    public void LastIndexOfAnyExcept_ValuesSpan_Empty_ReturnsLastIndex()
    {
        // Documented contract: empty values matches no element, so the last element is "not in the empty set".
        ReadOnlySpan<int> span = [1, 2, 3];
        span.LastIndexOfAnyExcept((ReadOnlySpan<int>)[]).Should().Be(2);
    }

    [Fact]
    public void LastIndexOfAnyExcept_ValuesSpan_BothEmpty_ReturnsMinusOne()
    {
        ReadOnlySpan<int>.Empty.LastIndexOfAnyExcept((ReadOnlySpan<int>)[]).Should().Be(-1);
    }

    [Fact]
    public void LastIndexOfAnyExcept_ValuesSpan_FourValues_FindsLastOther()
    {
        ReadOnlySpan<int> span = [1, 2, 3, 4, 9, 1, 2];
        ReadOnlySpan<int> values = [1, 2, 3, 4];
        span.LastIndexOfAnyExcept(values).Should().Be(4);
    }

    [Fact]
    public void LastIndexOfAnyExcept_ValuesSpan_AllMatch_ReturnsMinusOne()
    {
        ReadOnlySpan<int> span = [1, 2, 3, 4];
        ReadOnlySpan<int> values = [1, 2, 3, 4];
        span.LastIndexOfAnyExcept(values).Should().Be(-1);
    }

    // ----- ContainsAny (multi-value) -----

    [Fact]
    public void ContainsAny_TwoValues_True()
    {
        ReadOnlySpan<int> span = [1, 2, 3];
        span.ContainsAny(2, 99).Should().BeTrue();
    }

    [Fact]
    public void ContainsAny_TwoValues_False()
    {
        ReadOnlySpan<int> span = [1, 2, 3];
        span.ContainsAny(98, 99).Should().BeFalse();
    }

    [Fact]
    public void ContainsAny_ThreeValues_True()
    {
        ReadOnlySpan<int> span = [1, 2, 3];
        span.ContainsAny(98, 99, 3).Should().BeTrue();
    }

    [Fact]
    public void ContainsAny_ThreeValues_False()
    {
        ReadOnlySpan<int> span = [1, 2, 3];
        span.ContainsAny(7, 8, 9).Should().BeFalse();
    }

    [Fact]
    public void ContainsAny_ValuesSpan_True()
    {
        ReadOnlySpan<int> span = [1, 2, 3, 4];
        ReadOnlySpan<int> values = [10, 4];
        span.ContainsAny(values).Should().BeTrue();
    }

    [Fact]
    public void ContainsAny_ValuesSpan_False()
    {
        ReadOnlySpan<int> span = [1, 2, 3, 4];
        ReadOnlySpan<int> values = [10, 11];
        span.ContainsAny(values).Should().BeFalse();
    }

    // ----- ContainsAnyExcept -----

    [Fact]
    public void ContainsAnyExcept_SingleValue_True_WhenAnyDiffers()
    {
        ReadOnlySpan<int> span = [1, 1, 9, 1];
        span.ContainsAnyExcept(1).Should().BeTrue();
    }

    [Fact]
    public void ContainsAnyExcept_SingleValue_False_WhenAllMatch()
    {
        ReadOnlySpan<int> span = [1, 1, 1, 1];
        span.ContainsAnyExcept(1).Should().BeFalse();
    }

    [Fact]
    public void ContainsAnyExcept_TwoValues_TrueAndFalse()
    {
        ReadOnlySpan<int> matching = [1, 2, 1, 2];
        matching.ContainsAnyExcept(1, 2).Should().BeFalse();

        ReadOnlySpan<int> withOther = [1, 2, 9];
        withOther.ContainsAnyExcept(1, 2).Should().BeTrue();
    }

    [Fact]
    public void ContainsAnyExcept_ThreeValues_TrueAndFalse()
    {
        ReadOnlySpan<int> matching = [1, 2, 3, 1];
        matching.ContainsAnyExcept(1, 2, 3).Should().BeFalse();

        ReadOnlySpan<int> withOther = [1, 2, 3, 9];
        withOther.ContainsAnyExcept(1, 2, 3).Should().BeTrue();
    }

    [Fact]
    public void ContainsAnyExcept_ValuesSpan_TrueAndFalse()
    {
        ReadOnlySpan<int> matching = [1, 2, 3, 4];
        matching.ContainsAnyExcept((ReadOnlySpan<int>)[1, 2, 3, 4]).Should().BeFalse();

        ReadOnlySpan<int> withOther = [1, 2, 3, 9];
        withOther.ContainsAnyExcept((ReadOnlySpan<int>)[1, 2, 3]).Should().BeTrue();
    }

    // ----- Count -----

    [Fact]
    public void Count_SingleValue_CountsOccurrences()
    {
        ReadOnlySpan<int> span = [1, 2, 1, 3, 1];
        span.Count(1).Should().Be(3);
    }

    [Fact]
    public void Count_SingleValue_NotPresent_ReturnsZero()
    {
        ReadOnlySpan<int> span = [1, 2, 3];
        span.Count(99).Should().Be(0);
    }

    [Fact]
    public void Count_SingleValue_Empty_ReturnsZero()
    {
        ReadOnlySpan<int>.Empty.Count(1).Should().Be(0);
    }

    [Fact]
    public void Count_Byte_CountsOccurrences()
    {
        ReadOnlySpan<byte> span = [(byte)1, (byte)2, (byte)1, (byte)1];
        span.Count((byte)1).Should().Be(3);
    }

    [Fact]
    public void Count_Char_CountsOccurrences()
    {
        ReadOnlySpan<char> span = "banana".AsSpan();
        span.Count('a').Should().Be(3);
        span.Count('n').Should().Be(2);
        span.Count('z').Should().Be(0);
    }

    [Fact]
    public void Count_Sequence_CountsNonOverlappingMatches()
    {
        ReadOnlySpan<char> span = "abcabcabc".AsSpan();
        span.Count("abc".AsSpan()).Should().Be(3);
    }

    [Fact]
    public void Count_Sequence_OverlappingPattern_CountsNonOverlapping()
    {
        // "aa" inside "aaaa" non-overlapping count is 2.
        ReadOnlySpan<char> span = "aaaa".AsSpan();
        span.Count("aa".AsSpan()).Should().Be(2);
    }

    [Fact]
    public void Count_Sequence_EmptyValue_ReturnsZero()
    {
        ReadOnlySpan<char> span = "abc".AsSpan();
        span.Count((ReadOnlySpan<char>)[]).Should().Be(0);
    }

    [Fact]
    public void Count_Sequence_LongerThanSource_ReturnsZero()
    {
        ReadOnlySpan<char> span = "ab".AsSpan();
        span.Count("abc".AsSpan()).Should().Be(0);
    }

    // ----- Span<T> wrappers -----

    [Fact]
    public void Span_LastIndexOfAnyExcept_SingleValue_DelegatesToReadOnly()
    {
        Span<int> span = [1, 1, 2, 1 ];
        span.LastIndexOfAnyExcept(1).Should().Be(2);
    }

    [Fact]
    public void Span_LastIndexOfAnyExcept_TwoValues_DelegatesToReadOnly()
    {
        Span<int> span = [1, 2, 9, 1 ];
        span.LastIndexOfAnyExcept(1, 2).Should().Be(2);
    }

    [Fact]
    public void Span_LastIndexOfAnyExcept_ThreeValues_DelegatesToReadOnly()
    {
        Span<int> span = [1, 2, 3, 9, 1 ];
        span.LastIndexOfAnyExcept(1, 2, 3).Should().Be(3);
    }

    [Fact]
    public void Span_LastIndexOfAnyExcept_ValuesSpan_DelegatesToReadOnly()
    {
        Span<int> span = [1, 2, 3, 9 ];
        span.LastIndexOfAnyExcept((ReadOnlySpan<int>)[1, 2, 3]).Should().Be(3);
    }

    [Fact]
    public void Span_IndexOfAnyExcept_SingleValue_DelegatesToReadOnly()
    {
        Span<int> span = [1, 1, 9, 1 ];
        span.IndexOfAnyExcept(1).Should().Be(2);
    }

    [Fact]
    public void Span_IndexOfAnyExcept_TwoValues_DelegatesToReadOnly()
    {
        Span<int> span = [1, 2, 9, 1, 2 ];
        span.IndexOfAnyExcept(1, 2).Should().Be(2);
    }

    [Fact]
    public void Span_IndexOfAnyExcept_ThreeValues_DelegatesToReadOnly()
    {
        Span<int> span = [1, 2, 3, 9, 1 ];
        span.IndexOfAnyExcept(1, 2, 3).Should().Be(3);
    }

    [Fact]
    public void Span_IndexOfAnyExcept_ValuesSpan_DelegatesToReadOnly()
    {
        Span<int> span = [1, 2, 3, 9 ];
        span.IndexOfAnyExcept((ReadOnlySpan<int>)[1, 2, 3]).Should().Be(3);
    }

    // ----- CommonPrefixLength comparer paths -----

    [Fact]
    public void CommonPrefixLength_DefaultComparer_String()
    {
        ReadOnlySpan<string> a = ["x", "y", "z"];
        ReadOnlySpan<string> b = ["x", "y", "Z"];
        a.CommonPrefixLength(b, EqualityComparer<string>.Default).Should().Be(2);
        a.CommonPrefixLength(b, StringComparer.OrdinalIgnoreCase).Should().Be(3);
    }

    [Fact]
    public void CommonPrefixLength_NullComparer_FallsBackToDefault_String()
    {
        ReadOnlySpan<string> a = ["x", "y", "z"];
        ReadOnlySpan<string> b = ["x", "y", "z", "extra"];
        a.CommonPrefixLength(b, comparer: null).Should().Be(3);
    }

    // ----- Span<T> wrappers (delegate to ReadOnlySpan<T> overloads) -----

    [Fact]
    public void ContainsAny_Span_TwoValues_True()
    {
        int[] data = [1, 2, 3];
        Span<int> span = data;
        span.ContainsAny(2, 99).Should().BeTrue();
    }

    [Fact]
    public void ContainsAny_Span_TwoValues_False()
    {
        int[] data = [1, 2, 3];
        Span<int> span = data;
        span.ContainsAny(98, 99).Should().BeFalse();
    }

    [Fact]
    public void ContainsAny_Span_ThreeValues_True()
    {
        int[] data = [1, 2, 3];
        Span<int> span = data;
        span.ContainsAny(7, 8, 3).Should().BeTrue();
    }

    [Fact]
    public void ContainsAny_Span_ThreeValues_False()
    {
        int[] data = [1, 2, 3];
        Span<int> span = data;
        span.ContainsAny(7, 8, 9).Should().BeFalse();
    }

    [Fact]
    public void ContainsAny_Span_ValuesSpan_True()
    {
        int[] data = [1, 2, 3];
        Span<int> span = data;
        ReadOnlySpan<int> values = [9, 3];
        span.ContainsAny(values).Should().BeTrue();
    }

    [Fact]
    public void ContainsAny_Span_ValuesSpan_False()
    {
        int[] data = [1, 2, 3];
        Span<int> span = data;
        ReadOnlySpan<int> values = [7, 8];
        span.ContainsAny(values).Should().BeFalse();
    }

    [Fact]
    public void ContainsAnyExcept_Span_SingleValue_True()
    {
        int[] data = [1, 2, 1];
        Span<int> span = data;
        span.ContainsAnyExcept(1).Should().BeTrue();
    }

    [Fact]
    public void ContainsAnyExcept_Span_SingleValue_False()
    {
        int[] data = [1, 1, 1];
        Span<int> span = data;
        span.ContainsAnyExcept(1).Should().BeFalse();
    }

    [Fact]
    public void ContainsAnyExcept_Span_TwoValues_True()
    {
        int[] data = [1, 2, 3];
        Span<int> span = data;
        span.ContainsAnyExcept(1, 2).Should().BeTrue();
    }

    [Fact]
    public void ContainsAnyExcept_Span_TwoValues_False()
    {
        int[] data = [1, 2, 1, 2];
        Span<int> span = data;
        span.ContainsAnyExcept(1, 2).Should().BeFalse();
    }

    [Fact]
    public void ContainsAnyExcept_Span_ThreeValues_True()
    {
        int[] data = [1, 2, 3, 4];
        Span<int> span = data;
        span.ContainsAnyExcept(1, 2, 3).Should().BeTrue();
    }

    [Fact]
    public void ContainsAnyExcept_Span_ThreeValues_False()
    {
        int[] data = [1, 2, 3];
        Span<int> span = data;
        span.ContainsAnyExcept(1, 2, 3).Should().BeFalse();
    }

    [Fact]
    public void ContainsAnyExcept_Span_ValuesSpan_True()
    {
        int[] data = [1, 2, 3, 9];
        Span<int> span = data;
        ReadOnlySpan<int> values = [1, 2, 3];
        span.ContainsAnyExcept(values).Should().BeTrue();
    }

    [Fact]
    public void ContainsAnyExcept_Span_ValuesSpan_False()
    {
        int[] data = [1, 2, 3];
        Span<int> span = data;
        ReadOnlySpan<int> values = [1, 2, 3];
        span.ContainsAnyExcept(values).Should().BeFalse();
    }

    [Fact]
    public void Count_Span_SingleValue_CountsOccurrences()
    {
        int[] data = [1, 2, 1, 3, 1];
        Span<int> span = data;
        span.Count(1).Should().Be(3);
    }

    [Fact]
    public void Count_Span_ValuesSpan_CountsNonOverlappingOccurrences()
    {
        int[] data = [1, 2, 1, 2, 1, 2];
        Span<int> span = data;
        ReadOnlySpan<int> values = [1, 2];
        span.Count(values).Should().Be(3);
    }

    [Fact]
    public void CommonPrefixLength_Span_DelegatesToReadOnly()
    {
        int[] dataA = [1, 2, 3, 4];
        int[] dataB = [1, 2, 3, 5];
        Span<int> a = dataA;
        ReadOnlySpan<int> b = dataB;
        a.CommonPrefixLength(b).Should().Be(3);
    }

    [Fact]
    public void CommonPrefixLength_Span_WithComparer_DelegatesToReadOnly()
    {
        string[] dataA = ["x", "y", "z"];
        string[] dataB = ["X", "Y", "Z"];
        Span<string> a = dataA;
        ReadOnlySpan<string> b = dataB;
        a.CommonPrefixLength(b, StringComparer.OrdinalIgnoreCase).Should().Be(3);
        a.CommonPrefixLength(b, EqualityComparer<string>.Default).Should().Be(0);
    }

    // ----- IndexOfAnyExcept extra coverage -----

    [Fact]
    public void IndexOfAnyExcept_SingleValue_AllMatch_ReturnsMinusOne()
    {
        ReadOnlySpan<int> span = [7, 7, 7];
        span.IndexOfAnyExcept(7).Should().Be(-1);
    }

    [Fact]
    public void IndexOfAnyExcept_SingleValue_Empty_ReturnsMinusOne()
    {
        ReadOnlySpan<int>.Empty.IndexOfAnyExcept(0).Should().Be(-1);
    }

    [Fact]
    public void IndexOfAnyExcept_TwoValues_FindsFirstOther()
    {
        ReadOnlySpan<int> span = [1, 2, 9, 1, 2];
        span.IndexOfAnyExcept(1, 2).Should().Be(2);
    }

    [Fact]
    public void IndexOfAnyExcept_TwoValues_AllMatch_ReturnsMinusOne()
    {
        ReadOnlySpan<int> span = [1, 2, 1, 2];
        span.IndexOfAnyExcept(1, 2).Should().Be(-1);
    }

    [Fact]
    public void IndexOfAnyExcept_TwoValues_Empty_ReturnsMinusOne()
    {
        ReadOnlySpan<int>.Empty.IndexOfAnyExcept(1, 2).Should().Be(-1);
    }

    [Fact]
    public void IndexOfAnyExcept_ThreeValues_FindsFirstOther()
    {
        ReadOnlySpan<int> span = [1, 2, 3, 9, 1];
        span.IndexOfAnyExcept(1, 2, 3).Should().Be(3);
    }

    [Fact]
    public void IndexOfAnyExcept_ThreeValues_AllMatch_ReturnsMinusOne()
    {
        ReadOnlySpan<int> span = [1, 2, 3, 1, 2];
        span.IndexOfAnyExcept(1, 2, 3).Should().Be(-1);
    }

    [Fact]
    public void IndexOfAnyExcept_ThreeValues_Empty_ReturnsMinusOne()
    {
        ReadOnlySpan<int>.Empty.IndexOfAnyExcept(1, 2, 3).Should().Be(-1);
    }

    // ----- IndexOfAnyExcept type-specialized branches (byte, sbyte, char, short, ushort) -----

    [Fact]
    public void IndexOfAnyExcept_TwoValues_Byte_FindsFirstNonMatch()
    {
        ReadOnlySpan<byte> span = [(byte)1, (byte)2, (byte)9, (byte)1];
        span.IndexOfAnyExcept((byte)1, (byte)2).Should().Be(2);
    }

    [Fact]
    public void IndexOfAnyExcept_TwoValues_Byte_AllMatch_ReturnsMinusOne()
    {
        ReadOnlySpan<byte> span = [(byte)1, (byte)2, (byte)1, (byte)2];
        span.IndexOfAnyExcept((byte)1, (byte)2).Should().Be(-1);
    }

    [Fact]
    public void IndexOfAnyExcept_TwoValues_SByte_FindsFirstNonMatch()
    {
        ReadOnlySpan<sbyte> span = [(sbyte)(-1), (sbyte)2, (sbyte)9];
        span.IndexOfAnyExcept((sbyte)(-1), (sbyte)2).Should().Be(2);
    }

    [Fact]
    public void IndexOfAnyExcept_TwoValues_Char_FindsFirstNonMatch()
    {
        ReadOnlySpan<char> span = "abXab".AsSpan();
        span.IndexOfAnyExcept('a', 'b').Should().Be(2);
    }

    [Fact]
    public void IndexOfAnyExcept_TwoValues_Char_AllMatch_ReturnsMinusOne()
    {
        ReadOnlySpan<char> span = "abab".AsSpan();
        span.IndexOfAnyExcept('a', 'b').Should().Be(-1);
    }

    [Fact]
    public void IndexOfAnyExcept_TwoValues_Short_FindsFirstNonMatch()
    {
        ReadOnlySpan<short> span = [(short)0, (short)1, (short)9];
        span.IndexOfAnyExcept((short)0, (short)1).Should().Be(2);
    }

    [Fact]
    public void IndexOfAnyExcept_TwoValues_UShort_FindsFirstNonMatch()
    {
        ReadOnlySpan<ushort> span = [(ushort)0, (ushort)1, (ushort)9];
        span.IndexOfAnyExcept((ushort)0, (ushort)1).Should().Be(2);
    }

    [Fact]
    public void IndexOfAnyExcept_ThreeValues_Byte_FindsFirstNonMatch()
    {
        ReadOnlySpan<byte> span = [(byte)1, (byte)2, (byte)3, (byte)9, (byte)1];
        span.IndexOfAnyExcept((byte)1, (byte)2, (byte)3).Should().Be(3);
    }

    [Fact]
    public void IndexOfAnyExcept_ThreeValues_Byte_AllMatch_ReturnsMinusOne()
    {
        ReadOnlySpan<byte> span = [(byte)1, (byte)2, (byte)3, (byte)1];
        span.IndexOfAnyExcept((byte)1, (byte)2, (byte)3).Should().Be(-1);
    }

    [Fact]
    public void IndexOfAnyExcept_ThreeValues_SByte_FindsFirstNonMatch()
    {
        ReadOnlySpan<sbyte> span = [(sbyte)(-1), (sbyte)2, (sbyte)3, (sbyte)9];
        span.IndexOfAnyExcept((sbyte)(-1), (sbyte)2, (sbyte)3).Should().Be(3);
    }

    [Fact]
    public void IndexOfAnyExcept_ThreeValues_Char_FindsFirstNonMatch()
    {
        ReadOnlySpan<char> span = "abcXab".AsSpan();
        span.IndexOfAnyExcept('a', 'b', 'c').Should().Be(3);
    }

    [Fact]
    public void IndexOfAnyExcept_ThreeValues_Char_AllMatch_ReturnsMinusOne()
    {
        ReadOnlySpan<char> span = "abcabc".AsSpan();
        span.IndexOfAnyExcept('a', 'b', 'c').Should().Be(-1);
    }

    [Fact]
    public void IndexOfAnyExcept_ThreeValues_Short_FindsFirstNonMatch()
    {
        ReadOnlySpan<short> span = [(short)0, (short)1, (short)2, (short)9];
        span.IndexOfAnyExcept((short)0, (short)1, (short)2).Should().Be(3);
    }

    [Fact]
    public void IndexOfAnyExcept_ThreeValues_UShort_FindsFirstNonMatch()
    {
        ReadOnlySpan<ushort> span = [(ushort)0, (ushort)1, (ushort)2, (ushort)9];
        span.IndexOfAnyExcept((ushort)0, (ushort)1, (ushort)2).Should().Be(3);
    }

    // ----- IndexOfAnyExcept(T) single-value type-specialized branches -----

    [Fact]
    public void IndexOfAnyExcept_SingleValue_Byte_LongSpan_HitsUnrolledLoop()
    {
        byte[] data = new byte[33];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (byte)1;
        }

        data[10] = 9;
        ReadOnlySpan<byte> span = data;
        span.IndexOfAnyExcept((byte)1).Should().Be(10);
    }

    [Fact]
    public void IndexOfAnyExcept_SingleValue_Byte_TailRemainder_HitsScalarLoop()
    {
        byte[] data = [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 9];
        ReadOnlySpan<byte> span = data;
        span.IndexOfAnyExcept((byte)1).Should().Be(13);
    }

    [Fact]
    public void IndexOfAnyExcept_SingleValue_Byte_AllMatch_ReturnsMinusOne()
    {
        byte[] data = [1, 1, 1, 1, 1];
        ReadOnlySpan<byte> span = data;
        span.IndexOfAnyExcept((byte)1).Should().Be(-1);
    }

    [Fact]
    public void IndexOfAnyExcept_SingleValue_SByte_FindsFirstNonMatch()
    {
        ReadOnlySpan<sbyte> span = [(sbyte)(-1), (sbyte)(-1), (sbyte)9];
        span.IndexOfAnyExcept((sbyte)(-1)).Should().Be(2);
    }

    [Fact]
    public void IndexOfAnyExcept_SingleValue_Char_LongSpan_HitsUnrolledLoop()
    {
        char[] data = new string('a', 33).ToCharArray();
        data[15] = 'X';
        ReadOnlySpan<char> span = data;
        span.IndexOfAnyExcept('a').Should().Be(15);
    }

    [Fact]
    public void IndexOfAnyExcept_SingleValue_Char_TailRemainder_HitsScalarLoop()
    {
        char[] data = "aaaaaaaaaaaaaX".ToCharArray();
        ReadOnlySpan<char> span = data;
        span.IndexOfAnyExcept('a').Should().Be(13);
    }

    [Fact]
    public void IndexOfAnyExcept_SingleValue_Char_AllMatch_ReturnsMinusOne()
    {
        ReadOnlySpan<char> span = "aaaa".AsSpan();
        span.IndexOfAnyExcept('a').Should().Be(-1);
    }

    [Fact]
    public void IndexOfAnyExcept_SingleValue_Short_FindsFirstNonMatch()
    {
        ReadOnlySpan<short> span = [(short)0, (short)0, (short)9];
        span.IndexOfAnyExcept((short)0).Should().Be(2);
    }

    [Fact]
    public void IndexOfAnyExcept_SingleValue_UShort_FindsFirstNonMatch()
    {
        ReadOnlySpan<ushort> span = [(ushort)0, (ushort)0, (ushort)9];
        span.IndexOfAnyExcept((ushort)0).Should().Be(2);
    }

    [Fact]
    public void IndexOfAnyExcept_SingleValue_String_FindsFirstNonMatch()
    {
        ReadOnlySpan<string> span = ["a", "a", "x"];
        span.IndexOfAnyExcept("a").Should().Be(2);
    }

    [Fact]
    public void IndexOfAnyExcept_SingleValue_String_NullValue_FindsFirstNonNull()
    {
        ReadOnlySpan<string?> span = [null, null, "x"];
        span.IndexOfAnyExcept((string?)null).Should().Be(2);
    }

    // ----- LastIndexOfAnyExcept type-specialized branches -----

    [Fact]
    public void LastIndexOfAnyExcept_SingleValue_Byte_TailRemainder()
    {
        byte[] data = [9, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1];
        ReadOnlySpan<byte> span = data;
        span.LastIndexOfAnyExcept((byte)1).Should().Be(0);
    }

    [Fact]
    public void LastIndexOfAnyExcept_ThreeValues_Short_FindsLastNonMatch()
    {
        ReadOnlySpan<short> span = [(short)0, (short)1, (short)2, (short)9];
        span.LastIndexOfAnyExcept((short)0, (short)1, (short)2).Should().Be(3);
    }

    [Fact]
    public void LastIndexOfAnyExcept_TwoValues_Short_FindsLastNonMatch()
    {
        ReadOnlySpan<short> span = [(short)0, (short)1, (short)9];
        span.LastIndexOfAnyExcept((short)0, (short)1).Should().Be(2);
    }

    [Fact]
    public void IndexOfAnyExcept_ValuesSpan_FourOrMoreValues_GenericPath()
    {
        ReadOnlySpan<int> span = [1, 2, 3, 4, 9, 1];
        ReadOnlySpan<int> values = [1, 2, 3, 4];
        span.IndexOfAnyExcept(values).Should().Be(4);
    }

    [Fact]
    public void LastIndexOfAnyExcept_ValuesSpan_FourOrMoreValues_GenericPath()
    {
        ReadOnlySpan<int> span = [1, 2, 3, 4, 9, 1, 2];
        ReadOnlySpan<int> values = [1, 2, 3, 4];
        span.LastIndexOfAnyExcept(values).Should().Be(4);
    }

    [Fact]
    public void IndexOfAnyExcept_ValuesSpan_SingleValue_DelegatesToSingleValueOverload()
    {
        ReadOnlySpan<int> span = [1, 1, 9, 1];
        ReadOnlySpan<int> values = [1];
        span.IndexOfAnyExcept(values).Should().Be(2);
    }

    [Fact]
    public void IndexOfAnyExcept_ValuesSpan_TwoValues_DelegatesToTwoValueOverload()
    {
        ReadOnlySpan<int> span = [1, 2, 9, 1, 2];
        ReadOnlySpan<int> values = [1, 2];
        span.IndexOfAnyExcept(values).Should().Be(2);
    }

    [Fact]
    public void IndexOfAnyExcept_ValuesSpan_ThreeValues_DelegatesToThreeValueOverload()
    {
        ReadOnlySpan<int> span = [1, 2, 3, 9, 1];
        ReadOnlySpan<int> values = [1, 2, 3];
        span.IndexOfAnyExcept(values).Should().Be(3);
    }
}
