// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Collections;

public class EmptyListTests
{
    [Test]
    public void Instance_RequestedMultipleTimes_ReturnsSameInstance()
    {
        EmptyList<int>.Instance.Should().BeSameAs(EmptyList<int>.Instance);
        EmptyList<string>.Instance.Should().BeSameAs(EmptyList<string>.Instance);
    }

    [Test]
    public void Indexer_Get_ThrowsArgumentOutOfRangeException()
    {
        EmptyList<int> list = EmptyList<int>.Instance;

        Action act = () => _ = list[0];
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("index");
    }

    [Test]
    public void Indexer_Set_ThrowsArgumentOutOfRangeException()
    {
        EmptyList<int> list = EmptyList<int>.Instance;

        Action act = () => list[0] = 42;
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("index");
    }

    [Test]
    public void IsReadOnly_Get_ReturnsTrue()
    {
        EmptyList<int>.Instance.IsReadOnly.Should().BeTrue();
    }

    [Test]
    public void UnsafeValues_Get_ReturnsEmptySpan()
    {
        EmptyList<int>.Instance.UnsafeValues.IsEmpty.Should().BeTrue();
    }

    [Test]
    public void Values_Get_ReturnsEmptySpan()
    {
        EmptyList<int>.Instance.Values.IsEmpty.Should().BeTrue();
    }

    [Test]
    public void Count_Get_ReturnsZero()
    {
        EmptyList<int>.Instance.Count.Should().Be(0);
    }

    [Test]
    public void Add_AnyItem_ThrowsNotImplementedException()
    {
        Action act = () => EmptyList<int>.Instance.Add(42);
        act.Should().Throw<NotImplementedException>();
    }

    [Test]
    public void Clear_Called_DoesNotThrow()
    {
        Action act = EmptyList<int>.Instance.Clear;
        act.Should().NotThrow();
    }

    [Test]
    public void CopyTo_ArrayWithNonZeroIndex_ThrowsArgumentOutOfRangeException()
    {
        int[] array = new int[1];
        Action act = () => EmptyList<int>.Instance.CopyTo(array, 1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void CopyTo_ArrayWithZeroIndex_DoesNotThrow()
    {
        int[] array = [];
        Action act = () => EmptyList<int>.Instance.CopyTo(array, 0);
        act.Should().NotThrow();
    }

    [Test]
    public void CopyTo_SystemArrayWithNonZeroIndex_ThrowsArgumentOutOfRangeException()
    {
        Array array = new int[1];
        Action act = () => EmptyList<int>.Instance.CopyTo(array, 1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void CopyTo_SystemArrayWithZeroIndex_DoesNotThrow()
    {
        Array array = Array.Empty<int>();
        Action act = () => EmptyList<int>.Instance.CopyTo(array, 0);
        act.Should().NotThrow();
    }

    [Test]
    public void IndexOf_AnyItem_ReturnsMinusOne()
    {
        EmptyList<int>.Instance.IndexOf(42).Should().Be(-1);
    }

    [Test]
    public void Insert_AnyItemAtAnyIndex_ThrowsInvalidOperationException()
    {
        Action act = () => EmptyList<int>.Instance.Insert(0, 42);
        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void RemoveAt_AnyIndex_ThrowsInvalidOperationException()
    {
        Action act = () => EmptyList<int>.Instance.RemoveAt(0);
        act.Should().Throw<InvalidOperationException>();
    }
}
