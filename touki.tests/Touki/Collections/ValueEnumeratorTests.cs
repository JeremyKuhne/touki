// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections;

namespace Touki.Collections;

public class ValueEnumeratorTests
{
    /// <summary>
    ///  Simple test enumerator for testing ValueEnumerator wrapper.
    /// </summary>
    private struct TestEnumerator<T> : IEnumerator<T>, IDisposable
    {
        private readonly T[] _items;
        private int _index;

        public TestEnumerator(T[] items)
        {
            _items = items;
            _index = -1;
        }

        public readonly T Current => _index >= 0 && _index < _items.Length ? _items[_index] : default!;

        readonly object? IEnumerator.Current => Current;

        public readonly void Dispose() { }

        public bool MoveNext()
        {
            _index++;
            return _index < _items.Length;
        }

        public void Reset()
        {
            _index = -1;
        }
    }

    [Fact]
    public void Constructor_WithValidEnumerator_InitializesCorrectly()
    {
        int[] items = new int[] { 1, 2, 3 };
        TestEnumerator<int> innerEnumerator = new(items);
        ValueEnumerator<TestEnumerator<int>, int> enumerator = new(innerEnumerator);

        enumerator.Current.Should().Be(0);
    }

    [Fact]
    public void Current_ReturnsInnerEnumeratorCurrent()
    {
        string[] items = new string[] { "hello", "world" };
        TestEnumerator<string> innerEnumerator = new(items);
        ValueEnumerator<TestEnumerator<string>, string> enumerator = new(innerEnumerator);

        enumerator.MoveNext();
        string current = enumerator.Current;

        current.Should().Be("hello");
    }

    [Fact]
    public void MoveNext_DelegatesToInnerEnumerator()
    {
        int[] items = new int[] { 10, 20, 30 };
        TestEnumerator<int> innerEnumerator = new(items);
        ValueEnumerator<TestEnumerator<int>, int> enumerator = new(innerEnumerator);

        bool first = enumerator.MoveNext();
        first.Should().BeTrue();
        enumerator.Current.Should().Be(10);

        bool second = enumerator.MoveNext();
        second.Should().BeTrue();
        enumerator.Current.Should().Be(20);

        bool third = enumerator.MoveNext();
        third.Should().BeTrue();
        enumerator.Current.Should().Be(30);

        bool fourth = enumerator.MoveNext();
        fourth.Should().BeFalse();
    }

    [Fact]
    public void MoveNext_WithEmptyEnumerator_ReturnsFalse()
    {
        int[] items = new int[0];
        TestEnumerator<int> innerEnumerator = new(items);
        ValueEnumerator<TestEnumerator<int>, int> enumerator = new(innerEnumerator);

        bool result = enumerator.MoveNext();

        result.Should().BeFalse();
    }

    [Fact]
    public void Reset_DelegatesToInnerEnumerator()
    {
        double[] items = new double[] { 1.1, 2.2 };
        TestEnumerator<double> innerEnumerator = new(items);
        ValueEnumerator<TestEnumerator<double>, double> enumerator = new(innerEnumerator);

        enumerator.MoveNext();
        enumerator.Current.Should().Be(1.1);

        enumerator.Reset();
        enumerator.Current.Should().Be(0.0);

        enumerator.MoveNext();
        enumerator.Current.Should().Be(1.1);
    }

    [Fact]
    public void ValueEnumerator_CanBeUsedInForeachPattern()
    {
        char[] items = new char[] { 'a', 'b', 'c', 'd' };
        TestEnumerator<char> innerEnumerator = new(items);
        ValueEnumerator<TestEnumerator<char>, char> enumerator = new(innerEnumerator);

        List<char> results = new List<char>();

        // Manual foreach pattern (compiler would generate similar code)
        while (enumerator.MoveNext())
        {
            results.Add(enumerator.Current);
        }

        results.Should().Equal(new char[] { 'a', 'b', 'c', 'd' });
    }

    [Fact]
    public void ValueEnumerator_IsRefStruct()
    {
        Type type = typeof(ValueEnumerator<,>);

        type.IsValueType.Should().BeTrue();
        type.IsByRefLike.Should().BeTrue();
    }

    [Fact]
    public void ValueEnumerator_WithValueTypeEnumerator_OptimizesCorrectly()
    {
        // ValueEnumerator is designed to work with value type enumerators to avoid boxing
        byte[] items = new byte[] { 1, 2, 3 };
        TestEnumerator<byte> innerEnumerator = new(items);
        ValueEnumerator<TestEnumerator<byte>, byte> enumerator = new(innerEnumerator);

        List<byte> results = new List<byte>();
        while (enumerator.MoveNext())
        {
            results.Add(enumerator.Current);
        }

        results.Should().Equal(new byte[] { 1, 2, 3 });
    }

    [Fact]
    public void ValueEnumerator_WithReferenceTypes_WorksCorrectly()
    {
        string[] items = new string[] { "first", "second", "third" };
        TestEnumerator<string> innerEnumerator = new(items);
        ValueEnumerator<TestEnumerator<string>, string> enumerator = new(innerEnumerator);

        List<string> results = new List<string>();
        while (enumerator.MoveNext())
        {
            results.Add(enumerator.Current);
        }

        results.Should().Equal(new string[] { "first", "second", "third" });
    }

    [Fact]
    public void ValueEnumerator_HandlesNullValues()
    {
        string?[] items = new string?[] { "test", null, "value" };
        TestEnumerator<string?> innerEnumerator = new(items);
        ValueEnumerator<TestEnumerator<string?>, string?> enumerator = new(innerEnumerator);

        List<string?> results = new List<string?>();
        while (enumerator.MoveNext())
        {
            results.Add(enumerator.Current);
        }

        results.Should().Equal(new string?[] { "test", null, "value" });
    }

    [Fact]
    public void Current_BeforeMoveNext_ReturnsDefault()
    {
        int[] items = new int[] { 42 };
        TestEnumerator<int> innerEnumerator = new(items);
        ValueEnumerator<TestEnumerator<int>, int> enumerator = new(innerEnumerator);

        int current = enumerator.Current;

        current.Should().Be(0);
    }

    [Fact]
    public void Current_AfterReset_ReturnsDefault()
    {
        bool[] items = new bool[] { true, false };
        TestEnumerator<bool> innerEnumerator = new(items);
        ValueEnumerator<TestEnumerator<bool>, bool> enumerator = new(innerEnumerator);

        enumerator.MoveNext();
        enumerator.Current.Should().BeTrue();

        enumerator.Reset();
        enumerator.Current.Should().BeFalse();
    }

    [Fact]
    public void ValueEnumerator_ReadonlyMethods_WorkCorrectly()
    {
        int[] items = new int[] { 100, 200 };
        TestEnumerator<int> innerEnumerator = new(items);
        ValueEnumerator<TestEnumerator<int>, int> enumerator = new(innerEnumerator);

        enumerator.MoveNext();

        // Current and MoveNext are readonly methods - test they can be called on readonly reference
        int current = enumerator.Current;
        bool canMoveNext = enumerator.MoveNext();

        current.Should().Be(100);
        canMoveNext.Should().BeTrue();
        enumerator.Current.Should().Be(200);
    }

    [Fact]
    public void ValueEnumerator_MultipleIterations_WorksCorrectly()
    {
        int[] items = new int[] { 5, 10, 15 };
        TestEnumerator<int> innerEnumerator = new(items);
        ValueEnumerator<TestEnumerator<int>, int> enumerator = new(innerEnumerator);

        // First iteration
        List<int> firstResults = new List<int>();
        while (enumerator.MoveNext())
        {
            firstResults.Add(enumerator.Current);
        }
        firstResults.Should().Equal(new int[] { 5, 10, 15 });

        // Reset and iterate again
        enumerator.Reset();
        List<int> secondResults = new List<int>();
        while (enumerator.MoveNext())
        {
            secondResults.Add(enumerator.Current);
        }
        secondResults.Should().Equal(new int[] { 5, 10, 15 });
    }

    [Fact]
    public void ValueEnumerator_GenericConstraints_EnforceCorrectTypes()
    {
        // ValueEnumerator<TEnumerator, TValue> where TEnumerator : struct, IEnumerator<TValue>, IDisposable
        Type type = typeof(ValueEnumerator<,>);
        Type[] genericArguments = type.GetGenericArguments();

        Type enumeratorType = genericArguments[0];
        Type valueType = genericArguments[1];

        // First generic parameter (TEnumerator) should have struct constraint and interface constraints
        GenericParameterAttributes attributes = enumeratorType.GenericParameterAttributes;
        attributes.Should().HaveFlag(GenericParameterAttributes.NotNullableValueTypeConstraint);

        Type[] constraints = enumeratorType.GetGenericParameterConstraints();
        constraints.Should().Contain(typeof(IDisposable));
    }
}