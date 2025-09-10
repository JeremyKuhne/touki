// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Framework.Touki;

public class InterlockTests
{
    [Fact]
    public void Increment_UInt32_ShouldIncrementValue()
    {
        uint value = 0;
        Interlock.Increment(ref value).Should().Be(1);
        value.Should().Be(1);
    }

    [Fact]
    public void Increment_UInt64_ShouldIncrementValue()
    {
        ulong value = 0;
        Interlock.Increment(ref value).Should().Be(1);
        value.Should().Be(1);
    }

    [Fact]
    public void Decrement_UInt32_ShouldDecrementValue()
    {
        uint value = 2;
        Interlock.Decrement(ref value).Should().Be(1);
        value.Should().Be(1);
    }

    [Fact]
    public void Decrement_UInt64_ShouldDecrementValue()
    {
        ulong value = 2;
        Interlock.Decrement(ref value).Should().Be(1);
        value.Should().Be(1);
    }

    [Fact]
    public void Exchange_UInt32_ShouldExchangeValues()
    {
        uint value = 1;
        Interlock.Exchange(ref value, 2).Should().Be(1);
        value.Should().Be(2);
    }

    [Fact]
    public void Exchange_UInt64_ShouldExchangeValues()
    {
        ulong value = 1;
        Interlock.Exchange(ref value, 2).Should().Be(1);
        value.Should().Be(2);
    }

    [Fact]
    public void CompareExchange_UInt32_WhenValuesMatch_ShouldExchange()
    {
        uint value = 1;
        Interlock.CompareExchange(ref value, 2, 1).Should().Be(1);
        value.Should().Be(2);
    }

    [Fact]
    public void CompareExchange_UInt32_WhenValuesDontMatch_ShouldNotExchange()
    {
        uint value = 1;
        Interlock.CompareExchange(ref value, 2, 3).Should().Be(1);
        value.Should().Be(1);
    }

    [Fact]
    public void CompareExchange_UInt64_WhenValuesMatch_ShouldExchange()
    {
        ulong value = 1;
        Interlock.CompareExchange(ref value, 2, 1).Should().Be(1);
        value.Should().Be(2);
    }

    [Fact]
    public void CompareExchange_UInt64_WhenValuesDontMatch_ShouldNotExchange()
    {
        ulong value = 1;
        Interlock.CompareExchange(ref value, 2, 3).Should().Be(1);
        value.Should().Be(1);
    }

    [Fact]
    public void Add_UInt32_ShouldAddValues()
    {
        uint value = 1;
        Interlock.Add(ref value, 2).Should().Be(3);
        value.Should().Be(3);
    }

    [Fact]
    public void Add_UInt64_ShouldAddValues()
    {
        ulong value = 1;
        Interlock.Add(ref value, 2).Should().Be(3);
        value.Should().Be(3);
    }

    [Fact]
    public void Read_UInt64_ShouldReturnValue()
    {
        ulong value = 42;
        Interlock.Read(ref value).Should().Be(42);
    }

    [Fact]
    public void And_Int32_ShouldPerformBitwiseAnd()
    {
        int value = 0b1111;
        Interlock.And(ref value, 0b1010).Should().Be(0b1111);
        value.Should().Be(0b1010);
    }

    [Fact]
    public void And_UInt32_ShouldPerformBitwiseAnd()
    {
        uint value = 0b1111u;
        Interlock.And(ref value, 0b1010u).Should().Be(0b1111u);
        value.Should().Be(0b1010u);
    }

    [Fact]
    public void And_Int64_ShouldPerformBitwiseAnd()
    {
        long value = 0b1111L;
        Interlock.And(ref value, 0b1010L).Should().Be(0b1111L);
        value.Should().Be(0b1010L);
    }

    [Fact]
    public void And_UInt64_ShouldPerformBitwiseAnd()
    {
        ulong value = 0b1111UL;
        Interlock.And(ref value, 0b1010UL).Should().Be(0b1111UL);
        value.Should().Be(0b1010UL);
    }

    [Fact]
    public void Or_Int32_ShouldPerformBitwiseOr()
    {
        int value = 0b1010;
        Interlock.Or(ref value, 0b0101).Should().Be(0b1010);
        value.Should().Be(0b1111);
    }

    [Fact]
    public void Or_UInt32_ShouldPerformBitwiseOr()
    {
        uint value = 0b1010u;
        Interlock.Or(ref value, 0b0101u).Should().Be(0b1010u);
        value.Should().Be(0b1111u);
    }

    [Fact]
    public void Or_Int64_ShouldPerformBitwiseOr()
    {
        long value = 0b1010L;
        Interlock.Or(ref value, 0b0101L).Should().Be(0b1010L);
        value.Should().Be(0b1111L);
    }

    [Fact]
    public void Or_UInt64_ShouldPerformBitwiseOr()
    {
        ulong value = 0b1010UL;
        Interlock.Or(ref value, 0b0101UL).Should().Be(0b1010UL);
        value.Should().Be(0b1111UL);
    }

    [Fact]
    public async Task Increment_UInt32_IsAtomic_WhenCalledFromMultipleThreads()
    {
        uint value = 0;
        const int iterations = 1000;
        const int taskCount = 10;

        Task[] tasks = new Task[taskCount];
        for (int i = 0; i < taskCount; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                for (int j = 0; j < iterations; j++)
                {
                    Interlock.Increment(ref value);
                }
            },
            TestContext.Current.CancellationToken);
        }

        await Task.WhenAll(tasks);
        value.Should().Be((uint)(iterations * taskCount));
    }

    [Fact]
    public async Task Add_UInt64_IsAtomic_WhenCalledFromMultipleThreads()
    {
        ulong value = 0;
        const int iterations = 1000;
        const int taskCount = 10;

        Task[] tasks = new Task[taskCount];
        for (int i = 0; i < taskCount; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                for (int j = 0; j < iterations; j++)
                {
                    Interlock.Add(ref value, 1);
                }
            },
            TestContext.Current.CancellationToken);
        }

        await Task.WhenAll(tasks);
        value.Should().Be((ulong)(iterations * taskCount));
    }

    [Fact]
    public async Task CompareExchange_UInt32_IsAtomic_WhenCalledFromMultipleThreads()
    {
        uint value = 0;
        const int iterations = 1000;
        const int taskCount = 10;

        Task[] tasks = new Task[taskCount];
        for (int i = 0; i < taskCount; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                for (int j = 0; j < iterations; j++)
                {
                    uint current;
                    do
                    {
                        current = value;
                    } while (Interlock.CompareExchange(ref value, current + 1, current) != current);
                }
            },
            TestContext.Current.CancellationToken);
        }

        await Task.WhenAll(tasks);
        value.Should().Be((uint)(iterations * taskCount));
    }

    [Fact]
    public async Task And_Or_Operations_AreAtomic_WhenCalledFromMultipleThreads()
    {
        const int iterations = 100;
        const int taskCount = 8;

        uint andValue = uint.MaxValue;
        uint orValue = 0;

        Task[] tasks = new Task[taskCount];
        for (int i = 0; i < taskCount; i++)
        {
            uint mask = (uint)(1 << i);
            tasks[i] = Task.Run(() =>
            {
                for (int j = 0; j < iterations; j++)
                {
                    Interlock.And(ref andValue, ~mask);
                    Interlock.Or(ref orValue, mask);
                }
            },
            TestContext.Current.CancellationToken);
        }

        await Task.WhenAll(tasks);
        andValue.Should().Be(uint.MaxValue - ((1u << taskCount) - 1));
        orValue.Should().Be((1u << taskCount) - 1);
    }
}
