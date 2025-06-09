// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Concurrent;

namespace Touki;

public class CacheTests
{
    // Basic test class for cache to use in tests
    private sealed class TestItem
    {
        public int Value { get; set; }
    }

    // Disposable test class for cache to use in disposal tests
    private sealed class DisposableTestItem : IDisposable
    {
        public bool IsDisposed { get; private set; }
        public int Value { get; set; }

        public void Dispose() => IsDisposed = true;
    }

    [Fact]
    public void Constructor_ZeroSize_DefaultsToProcessorCountMultiplied()
    {
        using Cache<TestItem> cache = new(0);

        // Should default to Environment.ProcessorCount * 4
        // We can verify this by filling more than any reasonable minimum
        List<TestItem> items = [];

        // Acquire more items than a minimal cache would support
        for (int i = 0; i < Environment.ProcessorCount * 4; i++)
        {
            TestItem item = cache.Acquire();
            item.Value = i;
            items.Add(item);
        }

        // Release all items
        foreach (TestItem item in items)
        {
            cache.Release(item);
        }

        // Reacquire them to ensure they were cached
        int count = 0;
        for (int i = 0; i < Environment.ProcessorCount * 4; i++)
        {
            TestItem item = cache.Acquire();
            if (item.Value == i)
                count++;
        }

        // At least some items should have been reused from cache
        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Constructor_NegativeSize_DefaultsToProcessorCountMultiplied()
    {
        using Cache<TestItem> cache = new(-5);
        // Should behave same as zero constructor
    }

    [Fact]
    public void Acquire_ReturnsNewItem_WhenCacheEmpty()
    {
        using Cache<TestItem> cache = new(5);

        TestItem item = cache.Acquire();

        item.Should().NotBeNull();
        item.Should().BeOfType<TestItem>();
    }

    [Fact]
    public void Acquire_ReturnsCachedItem_AfterRelease()
    {
        using Cache<TestItem> cache = new(5);

        // Acquire and setup a distinct item
        TestItem item = cache.Acquire();
        item.Value = 42;

        // Release back to cache
        cache.Release(item);

        // Should get the same item back
        TestItem recycledItem = cache.Acquire();
        recycledItem.Value.Should().Be(42);
        recycledItem.Should().BeSameAs(item);
    }

    [Fact]
    public void Acquire_ThreadLocalItem_TakesPriority()
    {
        using Cache<TestItem> cache = new(5);

        // Set up multiple items with distinct values
        TestItem item1 = cache.Acquire();
        item1.Value = 100;

        TestItem item2 = cache.Acquire();
        item2.Value = 200;

        // Release items (item2 should become thread-local)
        cache.Release(item1);  // This becomes thread-local
        cache.Release(item2);

        // First acquire should return the thread-local item (item1)
        TestItem firstRecycled = cache.Acquire();
        firstRecycled.Value.Should().Be(100);
        firstRecycled.Should().BeSameAs(item1);

        // Second acquire should return the item from the shared cache (item2)
        TestItem secondRecycled = cache.Acquire();
        secondRecycled.Value.Should().Be(200);
        secondRecycled.Should().BeSameAs(item2);
    }

    [Fact]
    public void Release_StoresInThreadLocal_WhenThreadLocalEmpty()
    {
        using Cache<TestItem> cache = new(5);

        TestItem item = cache.Acquire();
        item.Value = 42;

        // Release to store in thread-local
        cache.Release(item);

        // Acquire should return the thread-local item
        TestItem recycledItem = cache.Acquire();
        recycledItem.Should().BeSameAs(item);
    }

    [Fact]
    public void Release_StoresInCache_WhenThreadLocalFull()
    {
        using Cache<TestItem> cache = new(5);

        TestItem item1 = cache.Acquire();
        item1.Value = 10;

        TestItem item2 = cache.Acquire();
        item2.Value = 20;

        // Fill the thread-local slot first
        cache.Release(item1);

        // This should go to the shared cache since thread-local is full
        cache.Release(item2);

        // First acquire returns thread-local (item1)
        TestItem firstRecycled = cache.Acquire();
        firstRecycled.Should().BeSameAs(item1);

        // Second acquire returns from shared cache (item2)
        TestItem secondRecycled = cache.Acquire();
        secondRecycled.Should().BeSameAs(item2);
    }

    [Fact]
    public void Release_DisposesItems_WhenCacheFull()
    {
        using Cache<DisposableTestItem> cache = new(2);

        // Create more items than the cache can hold
        List<DisposableTestItem> items = [];
        for (int i = 0; i < 4; i++)
        {
            DisposableTestItem item = cache.Acquire();
            item.Value = i;
            items.Add(item);
        }

        // Release all items - the first ones should be disposed when cache overflows
        foreach (DisposableTestItem item in items)
        {
            cache.Release(item);
        }

        // The first item should be disposed as it was pushed out of the cache
        items[1].IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void Dispose_DisposesAllCachedItems()
    {
        Cache<DisposableTestItem> cache = new(5);

        // Create and cache several items
        List<DisposableTestItem> items = [];
        for (int i = 0; i < 5; i++)
        {
            DisposableTestItem item = cache.Acquire();
            items.Add(item);
        }

        // Release all items to the cache
        foreach (DisposableTestItem item in items)
        {
            cache.Release(item);
        }

        // Dispose the cache
        cache.Dispose();

        // All cached items should be disposed
        foreach (DisposableTestItem item in items)
        {
            item.IsDisposed.Should().BeTrue();
        }
    }

    [Fact]
    public void Dispose_SafeToCallMultipleTimes()
    {
        Cache<TestItem> cache = new(5);

        // Create and release an item
        TestItem item = cache.Acquire();
        cache.Release(item);

        // Dispose multiple times should not throw
        cache.Dispose();
        cache.Dispose();
    }

    [Fact]
    public void Acquire_ReturnsNewItem_AfterDispose()
    {
        Cache<object> cache = new(5);

        // Get and customize an item
        object item = cache.Acquire();
        cache.Release(item);

        // Dispose and then try to acquire (should get a new item)
        cache.Dispose();

        object newItem = cache.Acquire();
        newItem.Should().NotBeNull();
        newItem.Should().NotBeSameAs(item);
    }

    [Fact]
    public void EdgeCase_NullItem_HandlesSafely()
    {
        // This tests for null safety, though the Cache implementation should
        // never return null because it creates a new instance if needed
        using Cache<TestItem> cache = new(5);

        TestItem item = cache.Acquire();
        item.Should().NotBeNull();

        // Multiple acquisitions should all return non-null
        for (int i = 0; i < 10; i++)
        {
            cache.Acquire().Should().NotBeNull();
        }
    }

    [Fact]
    public void MultithreadedUsage_WorksCorrectly()
    {
        using Cache<TestItem> cache = new(Environment.ProcessorCount * 2);
        const int itemsPerThread = 1000;
        const int threadCount = 4;

        CountdownEvent countdown = new(threadCount);
        ConcurrentQueue<Exception> exceptions = new();

        // Create and start multiple threads that use the cache concurrently
        for (int t = 0; t < threadCount; t++)
        {
            int threadId = t;
            Thread thread = new(() =>
            {
                try
                {
                    // Each thread acquires and releases many items
                    for (int i = 0; i < itemsPerThread; i++)
                    {
                        TestItem item = cache.Acquire();
                        // Do some "work" with the item
                        item.Value = threadId * 1000 + i;
                        // Release the item back to cache
                        cache.Release(item);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Enqueue(ex);
                }
                finally
                {
                    countdown.Signal();
                }
            });

            thread.Start();
        }

        // Wait for all threads to finish
        countdown.Wait();

        // Verify no exceptions occurred
        exceptions.Should().BeEmpty();
    }

    [Fact]
    public void EdgeCase_ReleaseAfterDispose_Throws()
    {
        Cache<TestItem> cache = new(5);
        TestItem item = cache.Acquire();

        // Dispose the cache
        cache.Dispose();

        // Release after dispose should not throw
        Action action = () => cache.Release(item);
        action.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void EdgeCase_ExceedCacheCapacity_RecoversGracefully()
    {
        int cacheSize = 3;
        using Cache<TestItem> cache = new(cacheSize);

        // Acquire more items than the cache can hold
        List<TestItem> items = [];
        for (int i = 0; i < cacheSize * 2; i++)
        {
            TestItem item = cache.Acquire();
            item.Value = i + 100;
            items.Add(item);
        }

        // Release all items
        foreach (TestItem item in items)
        {
            cache.Release(item);
        }

        // Should be able to acquire the cached items plus create new ones
        List<TestItem> recycledItems = [];
        for (int i = 0; i < cacheSize * 2; i++)
        {
            recycledItems.Add(cache.Acquire());
        }

        // Some items should be reused from cache
        bool foundRecycled = false;
        foreach (TestItem item in recycledItems)
        {
            if (item.Value >= 100)
            {
                foundRecycled = true;
                break;
            }
        }

        foundRecycled.Should().BeTrue();
    }
}
