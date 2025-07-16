// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections;

namespace Touki.Collections;

public class ArraySegmentEnumeratorTests
{
    [Fact]
    public void Constructor_WithValidArraySegment_InitializesCorrectly()
    {
        int[] array = new int[] { 1, 2, 3, 4, 5 };
        ArraySegment<int> segment = new(array, 1, 3);
        ArraySegmentEnumerator<int> enumerator = new(segment);

        enumerator.Current.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithNullArray_ThrowsArgumentNullException()
    {
        ArraySegment<int> segment = default;
        Action act = () => new ArraySegmentEnumerator<int>(segment);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MoveNext_WithEmptySegment_ReturnsFalse()
    {
        int[] array = new int[] { 1, 2, 3 };
        ArraySegment<int> segment = new(array, 1, 0);
        ArraySegmentEnumerator<int> enumerator = new(segment);

        bool result = enumerator.MoveNext();

        result.Should().BeFalse();
        enumerator.Current.Should().Be(0);
    }

    [Fact]
    public void MoveNext_WithValidSegment_EnumeratesCorrectly()
    {
        int[] array = new int[] { 10, 20, 30, 40, 50 };
        ArraySegment<int> segment = new(array, 1, 3);
        ArraySegmentEnumerator<int> enumerator = new(segment);

        bool first = enumerator.MoveNext();
        first.Should().BeTrue();
        enumerator.Current.Should().Be(20);

        bool second = enumerator.MoveNext();
        second.Should().BeTrue();
        enumerator.Current.Should().Be(30);

        bool third = enumerator.MoveNext();
        third.Should().BeTrue();
        enumerator.Current.Should().Be(40);

        bool fourth = enumerator.MoveNext();
        fourth.Should().BeFalse();
        enumerator.Current.Should().Be(0);
    }

    [Fact]
    public void MoveNext_CalledAfterEnd_ConsistentlyReturnsFalse()
    {
        int[] array = new int[] { 1, 2 };
        ArraySegment<int> segment = new(array, 0, 1);
        ArraySegmentEnumerator<int> enumerator = new(segment);

        enumerator.MoveNext().Should().BeTrue();
        enumerator.MoveNext().Should().BeFalse();
        enumerator.MoveNext().Should().BeFalse();
        enumerator.Current.Should().Be(0);
    }

    [Fact]
    public void Reset_ResetsEnumeratorToInitialState()
    {
        int[] array = new int[] { 100, 200, 300 };
        ArraySegment<int> segment = new(array, 0, 2);
        ArraySegmentEnumerator<int> enumerator = new(segment);

        enumerator.MoveNext();
        enumerator.Current.Should().Be(100);

        enumerator.Reset();
        enumerator.Current.Should().Be(0);

        enumerator.MoveNext();
        enumerator.Current.Should().Be(100);
    }

    [Fact]
    public void Current_NonGeneric_ReturnsCurrentElement()
    {
        int[] array = new int[] { 42, 84, 126 };
        ArraySegment<int> segment = new(array, 1, 1);
        ArraySegmentEnumerator<int> enumerator = new(segment);
        IEnumerator nonGenericEnumerator = enumerator;

        enumerator.MoveNext();
        object? current = nonGenericEnumerator.Current;

        current.Should().Be(84);
    }

    [Fact]
    public void Current_NonGeneric_BeforeMoveNext_ReturnsDefault()
    {
        int[] array = new int[] { 1, 2, 3 };
        ArraySegment<int> segment = new(array, 0, 1);
        ArraySegmentEnumerator<int> enumerator = new(segment);
        IEnumerator nonGenericEnumerator = enumerator;

        object? current = nonGenericEnumerator.Current;

        current.Should().Be(0);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        int[] array = new int[] { 1, 2, 3 };
        ArraySegment<int> segment = new(array, 0, 2);
        ArraySegmentEnumerator<int> enumerator = new(segment);

        Action act = () => enumerator.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void EnumerateReferenceTypes_WorksCorrectly()
    {
        string[] array = new string[] { "zero", "one", "two", "three", "four" };
        ArraySegment<string> segment = new(array, 2, 2);
        ArraySegmentEnumerator<string> enumerator = new(segment);

        List<string> results = new List<string>();
        while (enumerator.MoveNext())
        {
            results.Add(enumerator.Current);
        }

        results.Should().Equal(new string[] { "two", "three" });
    }

    [Fact]
    public void EnumerateWithOffsetAndCount_WorksCorrectly()
    {
        char[] array = new char[] { 'a', 'b', 'c', 'd', 'e', 'f' };
        ArraySegment<char> segment = new(array, 2, 3);
        ArraySegmentEnumerator<char> enumerator = new(segment);

        List<char> results = new List<char>();
        while (enumerator.MoveNext())
        {
            results.Add(enumerator.Current);
        }

        results.Should().Equal(new char[] { 'c', 'd', 'e' });
    }

    [Fact]
    public void EnumerateSingleElement_WorksCorrectly()
    {
        double[] array = new double[] { 1.1, 2.2, 3.3 };
        ArraySegment<double> segment = new(array, 1, 1);
        ArraySegmentEnumerator<double> enumerator = new(segment);

        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Should().Be(2.2);
        enumerator.MoveNext().Should().BeFalse();
    }
}