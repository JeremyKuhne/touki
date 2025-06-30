// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections;

namespace Touki.Collections;

public class ListBaseTests
{
    // Concrete implementation of ListBase for testing
    private class TestList<T> : ListBase<T> where T : notnull
    {
        private readonly List<T> _items = [];

        public override T this[int index]
        {
            get => _items[index];
            set => _items[index] = value;
        }

        public override int Count => _items.Count;

        public override void Add(T item) => _items.Add(item);

        public override void Clear() => _items.Clear();

        public override bool Contains(T item) => _items.Contains(item);

        public override void CopyTo(T[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

        public override void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);

        protected override IEnumerator<T> GetIEnumerableEnumerator() => _items.GetEnumerator();

        public override int IndexOf(T item) => _items.IndexOf(item);

        public override void Insert(int index, T item) => _items.Insert(index, item);

        public override bool Remove(T item) => _items.Remove(item);

        public override void RemoveAt(int index) => _items.RemoveAt(index);

        protected override void Dispose(bool disposing)
        {
            // No resources to dispose in this test implementation
        }
    }

    [Fact]
    public void Can_Add_And_Retrieve_Items()
    {
        TestList<string> list = new()
        {
            "Item1",
            "Item2"
        };

        list.Count.Should().Be(2);
        list[0].Should().Be("Item1");
        list[1].Should().Be("Item2");
    }

    [Fact]
    public void Can_Insert_Items()
    {
        TestList<string> list = new()
        {
            "Item1",
            "Item3"
        };

        list.Insert(1, "Item2");

        list.Count.Should().Be(3);
        list[0].Should().Be("Item1");
        list[1].Should().Be("Item2");
        list[2].Should().Be("Item3");
    }

    [Fact]
    public void Can_Remove_Items()
    {
        TestList<string> list = new()
        {
            "Item1",
            "Item2",
            "Item3"
        };

        bool removed = list.Remove("Item2");

        removed.Should().BeTrue();
        list.Count.Should().Be(2);
        list[0].Should().Be("Item1");
        list[1].Should().Be("Item3");
    }

    [Fact]
    public void Remove_Returns_False_When_Item_Not_Found()
    {
        TestList<string> list = new()
        {
            "Item1"
        };

        bool removed = list.Remove("NonExistent");

        removed.Should().BeFalse();
        list.Count.Should().Be(1);
    }

    [Fact]
    public void Can_RemoveAt_Index()
    {
        TestList<string> list = new()
        {
            "Item1",
            "Item2",
            "Item3"
        };

        list.RemoveAt(1);

        list.Count.Should().Be(2);
        list[0].Should().Be("Item1");
        list[1].Should().Be("Item3");
    }

    [Fact]
    public void RemoveAt_Out_Of_Range_Throws()
    {
        TestList<string> list = new()
        {
            "Item1"
        };

        Action action = () => list.RemoveAt(1);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Can_Clear_List()
    {
        TestList<string> list = new()
        {
            "Item1",
            "Item2"
        };

        list.Clear();
        list.Count.Should().Be(0);
    }

    [Fact]
    public void Can_Check_Contains()
    {
        TestList<string> list = new()
        {
            "Item1",
            "Item2"
        };

        list.Contains("Item1").Should().BeTrue();
        list.Contains("Item3").Should().BeFalse();
    }

    [Fact]
    public void Can_Find_IndexOf_Item()
    {
        TestList<string> list = new()
        {
            "Item1",
            "Item2",
            "Item3"
        };

        list.IndexOf("Item2").Should().Be(1);
        list.IndexOf("Item4").Should().Be(-1);
    }

    [Fact]
    public void Can_CopyTo_Array()
    {
        TestList<string> list = new()
        {
            "Item1",
            "Item2"
        };
        string[] array = new string[3];

        list.CopyTo(array, 1);

        array[0].Should().BeNull();
        array[1].Should().Be("Item1");
        array[2].Should().Be("Item2");
    }

    [Fact]
    public void Access_Out_Of_Range_Index_Throws()
    {
        TestList<string> list = new()
        {
            "Item1"
        };

        Action action = () => _ = list[1];

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Set_Out_Of_Range_Index_Throws()
    {
        TestList<string> list = new()
        {
            "Item1"
        };

        Action action = () => list[1] = "Item2";
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Non_Generic_Interface_Works_Correctly()
    {
        IList list = new TestList<string>();

        int index = list.Add("Item1");
        list.Insert(0, "Item0");

        index.Should().Be(0);
        list.Count.Should().Be(2);
        list[0].Should().Be("Item0");
        list[1].Should().Be("Item1");
        list.Contains("Item0").Should().BeTrue();
        list.IndexOf("Item1").Should().Be(1);
        list.IndexOf("NonExistent").Should().Be(-1);
    }

    [Fact]
    public void Non_Generic_Remove_Ignores_Wrong_Type()
    {
        IList list = new TestList<string>
        {
            "Item1"
        };

        list.Remove(42); // Integer, not string

        list.Count.Should().Be(1);
    }

    [Fact]
    public void Setting_Via_NonGeneric_Interface_Validates_Type()
    {
        IList list = new TestList<string>
        {
            "Item1"
        };

        Action action = () => list[0] = 42; // Integer, not string

        action.Should().Throw<ArgumentException>()
            .WithMessage("*Invalid item type*");
    }

    [Fact]
    public void Adding_Via_NonGeneric_Interface_Validates_Type()
    {
        IList list = new TestList<string>();

        Action action = () => list.Add(42); // Integer, not string

        action.Should().Throw<ArgumentException>()
            .WithMessage("*Invalid item type*");
    }

    [Fact]
    public void Inserting_Via_NonGeneric_Interface_Validates_Type()
    {
        IList list = new TestList<string>();

        Action action = () => list.Insert(0, 42); // Integer, not string

        action.Should().Throw<ArgumentException>()
            .WithMessage("*Invalid item type*");
    }

    [Fact]
    public void Adding_Null_Via_NonGeneric_Interface_Throws()
    {
        IList list = new TestList<string>();

        Action action = () => list.Add(null);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Inserting_Null_Via_NonGeneric_Interface_Throws()
    {
        IList list = new TestList<string>();

        Action action = () => list.Insert(0, null);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void IsSynchronized_Returns_False()
    {
        ICollection list = new TestList<string>();

        ((ICollection)list).IsSynchronized.Should().BeFalse();
    }

    [Fact]
    public void SyncRoot_Returns_This()
    {
        TestList<string> testList = new();
        ICollection list = testList;

        list.SyncRoot.Should().BeSameAs(testList);
    }

    [Fact]
    public void IsFixedSize_Returns_False()
    {
        IList list = new TestList<string>();

        list.IsFixedSize.Should().BeFalse();
    }

    [Fact]
    public void IsReadOnly_Returns_False_By_Default()
    {
        TestList<string> list = new();

        list.IsReadOnly.Should().BeFalse();
    }

    [Fact]
    public void Can_Enumerate_Items()
    {
        TestList<string> list = new()
        {
            "Item1",
            "Item2"
        };

        List<string> enumerated = [.. list];

        enumerated.Count.Should().Be(2);
        enumerated[0].Should().Be("Item1");
        enumerated[1].Should().Be("Item2");
    }

    [Fact]
    public void Can_Enumerate_Via_NonGeneric_Interface()
    {
        IEnumerable list = new TestList<string>();
        ((IList)list).Add("Item1");
        ((IList)list).Add("Item2");

        List<object> enumerated = [.. list];

        enumerated.Count.Should().Be(2);
        enumerated[0].Should().Be("Item1");
        enumerated[1].Should().Be("Item2");
    }

    [Fact]
    public void Dispose_Can_Be_Called_Multiple_Times()
    {
        TestList<string> list = new();

        list.Dispose();
        list.Dispose(); // Second dispose should be no-op
    }
}
