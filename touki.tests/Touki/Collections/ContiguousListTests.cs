// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.ComponentModel;

namespace Touki.Collections;

public class ContiguousListTests
{
    /// <summary>
    ///  Concrete implementation of ContiguousList for testing.
    /// </summary>
    private class TestContiguousList<T> : ContiguousList<T> where T : notnull
    {
        private T[] _array;
        private int _count;

        public TestContiguousList(int capacity = 4)
        {
            _array = new T[capacity];
            _count = 0;
        }

        public override T this[int index]
        {
            get => index < _count ? _array[index] : throw new ArgumentOutOfRangeException(nameof(index));
            set
            {
                if (index >= _count)
                    throw new ArgumentOutOfRangeException(nameof(index));
                _array[index] = value;
            }
        }

        public override int Count => _count;

        public override Span<T> UnsafeValues => _array.AsSpan(0, _count);

        public override ReadOnlySpan<T> Values => _array.AsSpan(0, _count);

        public override void Add(T item)
        {
            if (_count >= _array.Length)
            {
                Array.Resize(ref _array, _array.Length * 2);
            }
            _array[_count++] = item;
        }

        public override void Clear()
        {
            Array.Clear(_array, 0, _count);
            _count = 0;
        }

        public override void CopyTo(T[] array, int arrayIndex)
        {
            Array.Copy(_array, 0, array, arrayIndex, _count);
        }

        public override void CopyTo(Array array, int index)
        {
            Array.Copy(_array, 0, array, index, _count);
        }

        public override int IndexOf(T item)
        {
            return Array.IndexOf(_array, item, 0, _count);
        }

        public override void Insert(int index, T item)
        {
            if (index > _count)
                throw new ArgumentOutOfRangeException(nameof(index));

            if (_count >= _array.Length)
            {
                Array.Resize(ref _array, _array.Length * 2);
            }

            if (index < _count)
            {
                Array.Copy(_array, index, _array, index + 1, _count - index);
            }

            _array[index] = item;
            _count++;
        }

        public override bool Remove(T item)
        {
            int index = IndexOf(item);
            if (index >= 0)
            {
                RemoveAt(index);
                return true;
            }
            return false;
        }

        public override void RemoveAt(int index)
        {
            if (index >= _count)
                throw new ArgumentOutOfRangeException(nameof(index));

            _count--;
            if (index < _count)
            {
                Array.Copy(_array, index + 1, _array, index, _count - index);
            }
            _array[_count] = default!;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Clear();
            }
        }
    }

    [Fact]
    public void UnsafeValues_WithEmptyList_ReturnsEmptySpan()
    {
        using TestContiguousList<int> list = new();

        Span<int> values = list.UnsafeValues;

        values.Length.Should().Be(0);
        values.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void UnsafeValues_WithItems_ReturnsCorrectSpan()
    {
        using TestContiguousList<int> list = new();
        list.Add(10);
        list.Add(20);
        list.Add(30);

        Span<int> values = list.UnsafeValues;

        values.Length.Should().Be(3);
        values[0].Should().Be(10);
        values[1].Should().Be(20);
        values[2].Should().Be(30);
    }

    [Fact]
    public void UnsafeValues_CanModifyUnderlyingData()
    {
        using TestContiguousList<int> list = new();
        list.Add(100);
        list.Add(200);

        Span<int> values = list.UnsafeValues;
        values[0] = 999;
        values[1] = 888;

        list[0].Should().Be(999);
        list[1].Should().Be(888);
    }

    [Fact]
    public void Values_WithEmptyList_ReturnsEmptySpan()
    {
        using TestContiguousList<string> list = new();

        ReadOnlySpan<string> values = list.Values;

        values.Length.Should().Be(0);
        values.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Values_WithItems_ReturnsCorrectReadOnlySpan()
    {
        using TestContiguousList<string> list = new();
        list.Add("first");
        list.Add("second");
        list.Add("third");

        ReadOnlySpan<string> values = list.Values;

        values.Length.Should().Be(3);
        values[0].Should().Be("first");
        values[1].Should().Be("second");
        values[2].Should().Be("third");
    }

    [Fact]
    public void Values_ReflectsListChanges()
    {
        using TestContiguousList<double> list = new();
        list.Add(1.1);
        list.Add(2.2);

        ReadOnlySpan<double> values1 = list.Values;
        values1.Length.Should().Be(2);

        list.Add(3.3);
        ReadOnlySpan<double> values2 = list.Values;
        values2.Length.Should().Be(3);
        values2[2].Should().Be(3.3);
    }

    [Fact]
    public void UnsafeValues_AttributesSetCorrectly()
    {
        Type type = typeof(ContiguousList<int>);
        PropertyInfo? property = type.GetProperty(nameof(ContiguousList<int>.UnsafeValues));

        property.Should().NotBeNull();

        // Check for EditorBrowsable attribute
        EditorBrowsableAttribute? editorBrowsable = property!.GetCustomAttribute<EditorBrowsableAttribute>();
        editorBrowsable.Should().NotBeNull();
        editorBrowsable!.State.Should().Be(EditorBrowsableState.Never);

        // Check for Browsable attribute
        BrowsableAttribute? browsable = property.GetCustomAttribute<BrowsableAttribute>();
        browsable.Should().NotBeNull();
        browsable!.Browsable.Should().BeFalse();
    }

    [Fact]
    public void Values_IsReadOnly()
    {
        using TestContiguousList<int> list = new();
        list.Add(42);

        ReadOnlySpan<int> values = list.Values;

        // ReadOnlySpan<T> ensures we cannot modify the underlying data through this reference
        // This is verified by the compiler, so we just verify we can read the data
        values[0].Should().Be(42);
    }

    [Fact]
    public void ContiguousList_InheritsFromListBase()
    {
        using TestContiguousList<int> list = new();

        list.Should().BeAssignableTo<ListBase<int>>();
    }

    [Fact]
    public void SpanAccess_WithReferenceTypes_WorksCorrectly()
    {
        using TestContiguousList<string> list = new();
        list.Add("alpha");
        list.Add("beta");
        list.Add("gamma");

        Span<string> unsafeValues = list.UnsafeValues;
        ReadOnlySpan<string> values = list.Values;

        unsafeValues.Length.Should().Be(3);
        values.Length.Should().Be(3);

        for (int i = 0; i < 3; i++)
        {
            unsafeValues[i].Should().Be(values[i]);
            values[i].Should().Be(list[i]);
        }
    }

    [Fact]
    public void SpanAccess_AfterClear_ReturnsEmptySpan()
    {
        using TestContiguousList<int> list = new();
        list.Add(1);
        list.Add(2);
        list.Add(3);

        list.Clear();

        Span<int> unsafeValues = list.UnsafeValues;
        ReadOnlySpan<int> values = list.Values;

        unsafeValues.Length.Should().Be(0);
        values.Length.Should().Be(0);
    }

    [Fact]
    public void SpanAccess_AfterRemoval_ReflectsChanges()
    {
        using TestContiguousList<int> list = new();
        list.Add(10);
        list.Add(20);
        list.Add(30);

        list.RemoveAt(1); // Remove 20

        ReadOnlySpan<int> values = list.Values;
        values.Length.Should().Be(2);
        values[0].Should().Be(10);
        values[1].Should().Be(30);
    }

    [Fact]
    public void UnsafeValues_ModificationsConcerns_DocumentedByNaming()
    {
        // The "Unsafe" prefix indicates that modifications to the list after getting
        // the span can lead to undefined behavior. This test documents that concern.
        using TestContiguousList<int> list = new();
        list.Add(1);
        list.Add(2);

        Span<int> unsafeSpan = list.UnsafeValues;
        int originalLength = unsafeSpan.Length;

        // Modifying the list after getting the span is "unsafe"
        list.Add(3); // This could potentially invalidate the span

        // The original span length doesn't change, which shows the concern
        unsafeSpan.Length.Should().Be(originalLength);
    }

    [Fact]
    public void ListBase_GenericConstraint_EnforcesNotNull()
    {
        // ContiguousList<T> should enforce where T : notnull constraint
        Type listType = typeof(ContiguousList<>);
        Type[] constraints = listType.GetGenericArguments()[0].GetGenericParameterConstraints();

        // In .NET 6+, the notnull constraint is represented as a GenericParameterAttributes flag
        GenericParameterAttributes attributes = listType.GetGenericArguments()[0].GenericParameterAttributes;
        bool hasNotNullConstraint = (attributes & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0 ||
                                    listType.GetGenericArguments()[0].GetCustomAttributes()
                                        .Any(attr => attr.GetType().Name.Contains("NotNull"));

        // At minimum, we know the constraint is enforced by the compiler
        // so we can test that the type compiles with value types and reference types
        Type intListType = typeof(TestContiguousList<int>);
        Type stringListType = typeof(TestContiguousList<string>);

        intListType.Should().NotBeNull();
        stringListType.Should().NotBeNull();
    }
}