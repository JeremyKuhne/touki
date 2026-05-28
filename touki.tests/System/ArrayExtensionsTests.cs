// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace System;

public class ArrayExtensionsTests
{
    [Fact]
    public void Fill_FillsAllElements()
    {
        int[] array = new int[5];
        Array.Fill(array, 7);
        array.Should().Equal(7, 7, 7, 7, 7);
    }

    [Fact]
    public void Fill_EmptyArray_NoOp()
    {
        int[] array = [];
        Array.Fill(array, 7);
        array.Should().BeEmpty();
    }

    [Fact]
    public void Fill_Range_FillsOnlySpecifiedElements()
    {
        int[] array = new int[5];
        Array.Fill(array, 9, 1, 3);
        array.Should().Equal(0, 9, 9, 9, 0);
    }

    [Fact]
    public void Fill_Range_FullArray_FillsAll()
    {
        int[] array = new int[5];
        Array.Fill(array, 1, 0, 5);
        array.Should().Equal(1, 1, 1, 1, 1);
    }

    [Fact]
    public void Fill_Range_ZeroCount_NoChange()
    {
        int[] array = [1, 2, 3];
        Array.Fill(array, 0, 1, 0);
        array.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Fill_NullArray_Throws()
    {
        Action action = () => Array.Fill<int>(null!, 0);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Fill_Range_NullArray_Throws()
    {
        Action action = () => Array.Fill<int>(null!, 0, 0, 0);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Fill_Range_NegativeStartIndex_Throws()
    {
        int[] array = new int[5];
        Action action = () => Array.Fill(array, 0, -1, 1);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Fill_Range_StartIndexBeyondLength_Throws()
    {
        int[] array = new int[5];
        Action action = () => Array.Fill(array, 0, 6, 0);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Fill_Range_CountTooLarge_Throws()
    {
        int[] array = new int[5];
        Action action = () => Array.Fill(array, 0, 2, 4);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Fill_ReferenceType_AssignsSameReference()
    {
        string[] array = new string[3];
        Array.Fill(array, "x");
        array.Should().Equal("x", "x", "x");
    }

    [Fact]
    public void Fill_Range_NegativeCount_Throws()
    {
        int[] array = new int[5];
        Action action = () => Array.Fill(array, 0, 0, -1);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Fill_Range_StartIndexAtLength_ZeroCount_DoesNotThrow()
    {
        // BCL: startIndex == length is valid as long as count is 0.
        int[] array = new int[3];
        Array.Fill(array, 9, 3, 0);
        array.Should().Equal(0, 0, 0);
    }

    [Fact]
    public void Fill_Range_StartIndexAtLength_NonZeroCount_Throws()
    {
        int[] array = new int[3];
        Action action = () => Array.Fill(array, 9, 3, 1);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Fill_OverwritesExistingValues()
    {
        int[] array = [1, 2, 3, 4, 5];
        Array.Fill(array, 0);
        array.Should().Equal(0, 0, 0, 0, 0);
    }

    [Fact]
    public void Fill_Range_OverwritesOnlyRange()
    {
        int[] array = [1, 2, 3, 4, 5];
        Array.Fill(array, 0, 1, 3);
        array.Should().Equal(1, 0, 0, 0, 5);
    }

    [Fact]
    public void Fill_Range_NullValue_AllowedForReferenceType()
    {
        string[] array = ["a", "b", "c"];
        Array.Fill(array, null!, 0, 2);
        array[0].Should().BeNull();
        array[1].Should().BeNull();
        array[2].Should().Be("c");
    }
}
