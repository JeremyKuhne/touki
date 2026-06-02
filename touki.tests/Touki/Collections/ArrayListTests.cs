// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Collections;

public class ArrayListTests
{
    [Test]
    public void Ctor_Default_HasZeroCount()
    {
        using ArrayList<int> list = new();
        list.Count.Should().Be(0);
    }

    [Test]
    public void Ctor_Default_AcceptsAdds()
    {
        using ArrayList<string> list = new();
        for (int i = 0; i < 32; i++)
        {
            list.Add(i.ToString());
        }

        list.Count.Should().Be(32);
        list[0].Should().Be("0");
        list[31].Should().Be("31");
    }

    [Test]
    public void Ctor_PositiveCapacity_HasZeroCount()
    {
        using ArrayList<int> list = new(16);
        list.Count.Should().Be(0);
    }

    [Test]
    public void Ctor_ZeroCapacity_StillUsable()
    {
        using ArrayList<int> list = new(0);
        list.Count.Should().Be(0);
        list.Add(1);
        list.Add(2);
        list.Count.Should().Be(2);
        list[0].Should().Be(1);
        list[1].Should().Be(2);
    }

    [Test]
    public void Ctor_NegativeCapacity_ThrowsArgumentOutOfRangeException()
    {
        Action act = () => _ = new ArrayList<int>(-1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void Add_NullItem_ThrowsArgumentNullException()
    {
        using ArrayList<string> list = new();
        Action act = () => list.Add(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
