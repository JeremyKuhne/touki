// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public class InterlockTests
{
    [Test]
    public void Increment_UInt32_ShouldIncrementValue()
    {
        uint value = 0;
        Interlocked.Increment(ref value).Should().Be(1);
        value.Should().Be(1);
    }

    [Test]
    public void Increment_UInt64_ShouldIncrementValue()
    {
        ulong value = 0;
        Interlocked.Increment(ref value).Should().Be(1);
        value.Should().Be(1);
    }

    [Test]
    public void Decrement_UInt32_ShouldDecrementValue()
    {
        uint value = 2;
        Interlocked.Decrement(ref value).Should().Be(1);
        value.Should().Be(1);
    }

    [Test]
    public void Decrement_UInt64_ShouldDecrementValue()
    {
        ulong value = 2;
        Interlocked.Decrement(ref value).Should().Be(1);
        value.Should().Be(1);
    }

    [Test]
    public void Exchange_UInt32_ShouldExchangeValues()
    {
        uint value = 1;
        Interlocked.Exchange(ref value, 2).Should().Be(1);
        value.Should().Be(2);
    }

    [Test]
    public void Exchange_UInt64_ShouldExchangeValues()
    {
        ulong value = 1;
        Interlocked.Exchange(ref value, 2).Should().Be(1);
        value.Should().Be(2);
    }

    [Test]
    public void CompareExchange_UInt32_WhenValuesMatch_ShouldExchange()
    {
        uint value = 1;
        Interlocked.CompareExchange(ref value, 2, 1).Should().Be(1);
        value.Should().Be(2);
    }

    [Test]
    public void CompareExchange_UInt32_WhenValuesDontMatch_ShouldNotExchange()
    {
        uint value = 1;
        Interlocked.CompareExchange(ref value, 2, 3).Should().Be(1);
        value.Should().Be(1);
    }

    [Test]
    public void CompareExchange_UInt64_WhenValuesMatch_ShouldExchange()
    {
        ulong value = 1;
        Interlocked.CompareExchange(ref value, 2, 1).Should().Be(1);
        value.Should().Be(2);
    }

    [Test]
    public void CompareExchange_UInt64_WhenValuesDontMatch_ShouldNotExchange()
    {
        ulong value = 1;
        Interlocked.CompareExchange(ref value, 2, 3).Should().Be(1);
        value.Should().Be(1);
    }

    [Test]
    public void Add_UInt32_ShouldAddValues()
    {
        uint value = 1;
        Interlocked.Add(ref value, 2).Should().Be(3);
        value.Should().Be(3);
    }

    [Test]
    public void Add_UInt64_ShouldAddValues()
    {
        ulong value = 1;
        Interlocked.Add(ref value, 2).Should().Be(3);
        value.Should().Be(3);
    }

    [Test]
    public void Read_UInt64_ShouldReturnValue()
    {
        ulong value = 42;
        Interlocked.Read(ref value).Should().Be(42);
    }

    [Test]
    public void And_Int32_ShouldPerformBitwiseAnd()
    {
        int value = 0b1111;
        Interlocked.And(ref value, 0b1010).Should().Be(0b1111);
        value.Should().Be(0b1010);
    }

    [Test]
    public void And_UInt32_ShouldPerformBitwiseAnd()
    {
        uint value = 0b1111u;
        Interlocked.And(ref value, 0b1010u).Should().Be(0b1111u);
        value.Should().Be(0b1010u);
    }

    [Test]
    public void And_Int64_ShouldPerformBitwiseAnd()
    {
        long value = 0b1111L;
        Interlocked.And(ref value, 0b1010L).Should().Be(0b1111L);
        value.Should().Be(0b1010L);
    }

    [Test]
    public void And_UInt64_ShouldPerformBitwiseAnd()
    {
        ulong value = 0b1111UL;
        Interlocked.And(ref value, 0b1010UL).Should().Be(0b1111UL);
        value.Should().Be(0b1010UL);
    }

    [Test]
    public void Or_Int32_ShouldPerformBitwiseOr()
    {
        int value = 0b1010;
        Interlocked.Or(ref value, 0b0101).Should().Be(0b1010);
        value.Should().Be(0b1111);
    }

    [Test]
    public void Or_UInt32_ShouldPerformBitwiseOr()
    {
        uint value = 0b1010u;
        Interlocked.Or(ref value, 0b0101u).Should().Be(0b1010u);
        value.Should().Be(0b1111u);
    }

    [Test]
    public void Or_Int64_ShouldPerformBitwiseOr()
    {
        long value = 0b1010L;
        Interlocked.Or(ref value, 0b0101L).Should().Be(0b1010L);
        value.Should().Be(0b1111L);
    }

    [Test]
    public void Or_UInt64_ShouldPerformBitwiseOr()
    {
        ulong value = 0b1010UL;
        Interlocked.Or(ref value, 0b0101UL).Should().Be(0b1010UL);
        value.Should().Be(0b1111UL);
    }

    [Test]
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
                    Interlocked.Increment(ref value);
                }
            },
            CancellationToken.None);
        }

        await Task.WhenAll(tasks);
        value.Should().Be((uint)(iterations * taskCount));
    }

    [Test]
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
                    Interlocked.Add(ref value, 1);
                }
            },
            CancellationToken.None);
        }

        await Task.WhenAll(tasks);
        value.Should().Be((ulong)(iterations * taskCount));
    }

    [Test]
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
                    } while (Interlocked.CompareExchange(ref value, current + 1, current) != current);
                }
            },
            CancellationToken.None);
        }

        await Task.WhenAll(tasks);
        value.Should().Be((uint)(iterations * taskCount));
    }

    [Test]
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
                    Interlocked.And(ref andValue, ~mask);
                    Interlocked.Or(ref orValue, mask);
                }
            },
            CancellationToken.None);
        }

        await Task.WhenAll(tasks);
        andValue.Should().Be(uint.MaxValue - ((1u << taskCount) - 1));
        orValue.Should().Be((1u << taskCount) - 1);
    }
}
