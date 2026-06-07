// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Caching;

[TestClass]
public sealed class LruCacheTests
{
    [TestMethod]
    public void GetOrAdd_Miss_RunsFactoryOnceAndCaches()
    {
        LruCache<string, int> cache = new(capacity: 4);
        int calls = 0;

        int first = cache.GetOrAdd("a", _ => { calls++; return 1; });
        int second = cache.GetOrAdd("a", _ => { calls++; return 2; });

        first.Should().Be(1);
        // The second call hits the cache, so the factory does not run again.
        second.Should().Be(1);
        calls.Should().Be(1);
        cache.Count.Should().Be(1);
    }

    [TestMethod]
    public void GetOrAdd_ConcurrentSameKeyMisses_ShareOneStoredInstance()
    {
        LruCache<string, object> cache = new(capacity: 4);
        using ManualResetEventSlim bothInside = new();
        int started = 0;

        // Both tasks must enter the factory before either returns, guaranteeing both
        // miss the cache; the LruCache then has to converge them on one stored object.
        object Factory(string _)
        {
            if (Interlocked.Increment(ref started) == 2)
            {
                bothInside.Set();
            }

            bothInside.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();
            return new object();
        }

        Task<object> a = Task.Run(() => cache.GetOrAdd("k", Factory));
        Task<object> b = Task.Run(() => cache.GetOrAdd("k", Factory));
        Task.WaitAll(a, b);

        // Both factories ran, but both callers observe the single instance the cache kept.
        started.Should().Be(2);
        a.Result.Should().BeSameAs(b.Result);
        cache.Count.Should().Be(1);
    }

    [TestMethod]
    public void GetOrAdd_OverCapacity_EvictsLeastRecentlyUsed()
    {
        LruCache<string, int> cache = new(capacity: 2);
        cache.GetOrAdd("a", _ => 1);
        cache.GetOrAdd("b", _ => 2);
        // Adding a third entry evicts "a", the least-recently-used.
        cache.GetOrAdd("c", _ => 3);

        cache.Count.Should().Be(2);

        int calls = 0;
        cache.GetOrAdd("a", _ => { calls++; return 10; });
        // "a" was evicted, so its factory runs again.
        calls.Should().Be(1);
    }

    [TestMethod]
    public void GetOrAdd_AccessRefreshesRecency()
    {
        LruCache<string, int> cache = new(capacity: 2);
        cache.GetOrAdd("a", _ => 1);
        cache.GetOrAdd("b", _ => 2);
        // Touch "a" so "b" becomes the least-recently-used.
        cache.GetOrAdd("a", _ => 99);
        // Adding "c" now evicts "b", not "a".
        cache.GetOrAdd("c", _ => 3);

        int aCalls = 0;
        cache.GetOrAdd("a", _ => { aCalls++; return 1; });
        aCalls.Should().Be(0);

        int bCalls = 0;
        cache.GetOrAdd("b", _ => { bCalls++; return 2; });
        bCalls.Should().Be(1);
    }

    [TestMethod]
    public void GetOrAdd_HonorsKeyComparer()
    {
        LruCache<string, int> cache = new(capacity: 4, StringComparer.OrdinalIgnoreCase);
        cache.GetOrAdd("Key", _ => 1);

        int calls = 0;
        int value = cache.GetOrAdd("KEY", _ => { calls++; return 2; });

        value.Should().Be(1);
        calls.Should().Be(0);
        cache.Count.Should().Be(1);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void Ctor_NonPositiveCapacity_Throws(int capacity)
    {
        Action act = () => new LruCache<string, int>(capacity);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
