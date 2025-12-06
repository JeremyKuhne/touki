// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Collections;

public class RefCountedCacheTests
{
    private class TestCache : RefCountedCache<string, string, string>
    {
        public TestCache(int softLimit = 20, int hardLimit = 40) : base(softLimit, hardLimit)
        {
        }

        protected override CacheEntry CreateEntry(string key, bool cached)
        {
            return new TestCacheEntry(key, cached);
        }

        protected override bool IsMatch(string key, CacheEntry entry)
        {
            return key == entry.Data;
        }

        public int Count => this.TestAccessor.Dynamic._list.Count;
    }

    private class TestCacheEntry : RefCountedCache<string, string, string>.CacheEntry
    {
        public TestCacheEntry(string data, bool cached) : base(data, cached)
        {
        }

        public override string Object => Data;
    }

    private class DisposableTestCache : RefCountedCache<DisposableValue, DisposableValue, string>
    {
        public DisposableTestCache(int softLimit = 20, int hardLimit = 40) : base(softLimit, hardLimit)
        {
        }

        protected override CacheEntry CreateEntry(string key, bool cached)
        {
            return new DisposableTestCacheEntry(new DisposableValue(key), cached);
        }

        protected override bool IsMatch(string key, CacheEntry entry)
        {
            return key == entry.Data.Value;
        }
    }

    private class DisposableTestCacheEntry : RefCountedCache<DisposableValue, DisposableValue, string>.CacheEntry
    {
        public DisposableTestCacheEntry(DisposableValue data, bool cached) : base(data, cached)
        {
        }

        public override DisposableValue Object => Data;
    }

    private class DisposableValue : IDisposable
    {
        public string Value { get; }
        public bool IsDisposed { get; private set; }

        public DisposableValue(string value)
        {
            Value = value;
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    [Fact]
    public void Constructor_ValidParameters_CreatesCache()
    {
        TestCache cache = new(10, 20);
        cache.Should().NotBeNull();
        cache.Count.Should().Be(0);
    }

    [Fact]
    public void Constructor_DefaultParameters_CreatesCache()
    {
        TestCache cache = new();
        cache.Should().NotBeNull();
        cache.Count.Should().Be(0);
    }

    [Fact]
    public void GetEntry_NullKey_ThrowsArgumentNullException()
    {
        TestCache cache = new();
        Action action = () => cache.GetEntry(null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetEntry_NewKey_CreatesEntry()
    {
        TestCache cache = new();
        var entry = cache.GetEntry("test");

        entry.Should().NotBeNull();
        entry.Data.Should().Be("test");
        entry.Object.Should().Be("test");
        entry.RefCount.Should().Be(0);
        cache.Count.Should().Be(1);
    }

    [Fact]
    public void GetEntry_ExistingKey_ReturnsSameEntry()
    {
        TestCache cache = new();
        var entry1 = cache.GetEntry("test");
        var entry2 = cache.GetEntry("test");

        entry1.Should().BeSameAs(entry2);
        cache.Count.Should().Be(1);
    }

    [Fact]
    public void GetEntry_MultipleKeys_CreatesMultipleEntries()
    {
        TestCache cache = new();
        var entry1 = cache.GetEntry("test1");
        var entry2 = cache.GetEntry("test2");

        entry1.Should().NotBeSameAs(entry2);
        entry1.Data.Should().Be("test1");
        entry2.Data.Should().Be("test2");
        cache.Count.Should().Be(2);
    }

    [Fact]
    public void CacheEntry_AddRef_IncrementsRefCount()
    {
        TestCache cache = new();
        var entry = cache.GetEntry("test");

        entry.RefCount.Should().Be(0);
        entry.AddRef();
        entry.RefCount.Should().Be(1);
        entry.AddRef();
        entry.RefCount.Should().Be(2);
    }

    [Fact]
    public void CacheEntry_RemoveRef_DecrementsRefCount()
    {
        TestCache cache = new();
        var entry = cache.GetEntry("test");

        entry.AddRef();
        entry.AddRef();
        entry.RefCount.Should().Be(2);

        entry.RemoveRef();
        entry.RefCount.Should().Be(1);
        entry.RemoveRef();
        entry.RefCount.Should().Be(0);
    }

    [Fact]
    public void CacheEntry_CreateScope_AddsReference()
    {
        TestCache cache = new();
        var entry = cache.GetEntry("test");

        entry.RefCount.Should().Be(0);
        using var scope = entry.CreateScope();
        entry.RefCount.Should().Be(1);
        scope.RefCount.Should().Be(1);
        scope.Object.Should().Be("test");
    }

    [Fact]
    public void Scope_Dispose_RemovesReference()
    {
        TestCache cache = new();
        var entry = cache.GetEntry("test");

        entry.RefCount.Should().Be(0);

        {
            using var scope = entry.CreateScope();
            entry.RefCount.Should().Be(1);
        }

        entry.RefCount.Should().Be(0);
    }

    [Fact]
    public void Scope_ImplicitConversion_ReturnsObject()
    {
        TestCache cache = new();
        var entry = cache.GetEntry("test");

        using var scope = entry.CreateScope();
        string value = scope;
        value.Should().Be("test");
    }

    [Fact]
    public void Scope_TryGetCacheData_ReturnsData()
    {
        TestCache cache = new();
        var entry = cache.GetEntry("test");

        using var scope = entry.CreateScope();
        bool hasData = scope.TryGetCacheData(out string? data);

        hasData.Should().BeTrue();
        data.Should().Be("test");
    }

    [Fact]
    public void Scope_UncachedObject_TryGetCacheDataReturnsFalse()
    {
        using RefCountedCache<string, string, string>.Scope scope = new("uncached");
        bool hasData = scope.TryGetCacheData(out string? data);

        hasData.Should().BeFalse();
        data.Should().BeNull();
        scope.RefCount.Should().Be(-1);
        scope.Object.Should().Be("uncached");
    }

    [Fact]
    public void Cache_SoftLimitReached_CleansUnreferencedEntries()
    {
        using TestCache cache = new(softLimit: 3, hardLimit: 5);

        // Add entries up to soft limit
        var entry1 = cache.GetEntry("test1");
        var entry2 = cache.GetEntry("test2");
        _ = cache.GetEntry("test3");
        cache.Count.Should().Be(3);

        // Add references to first two entries
        entry1.AddRef();
        entry2.AddRef();

        // Add another entry, should trigger cleanup of the one unreferenced entry
        _ = cache.GetEntry("test4");
        cache.Count.Should().Be(3);

        // Add another entry, should clean the most recent one
        _ = cache.GetEntry("test5");
        cache.Count.Should().Be(3);
    }

    [Fact]
    public void Cache_HardLimitReached_DoesNotAddToCache()
    {
        using TestCache cache = new(softLimit: 2, hardLimit: 3);

        // Fill to hard limit
        var entry1 = cache.GetEntry("test1");
        var entry2 = cache.GetEntry("test2");

        // Add references to prevent cleanup
        entry1.AddRef();
        entry2.AddRef();

        var entry3 = cache.GetEntry("test3");
        cache.Count.Should().Be(3);

        // Add references to prevent cleanup
        entry3.AddRef();

        // This should create an uncached entry
        var entry4 = cache.GetEntry("test4");
        cache.Count.Should().Be(3); // Should not increase

        entry4.Should().NotBeNull();
        entry4.Data.Should().Be("test4");
    }

    [Fact]
    public void Cache_UncachedEntry_DisposesWhenUnreferenced()
    {
        DisposableTestCache cache = new(softLimit: 1, hardLimit: 1);

        // Fill cache
        var entry1 = cache.GetEntry("test1");
        entry1.AddRef(); // Keep it referenced

        // Create uncached entry (cache is full)
        var entry2 = cache.GetEntry("test2");
        DisposableValue value2 = entry2.Object;

        value2.IsDisposed.Should().BeFalse();

        // Add and remove reference - should dispose uncached entry
        entry2.AddRef();
        entry2.RemoveRef();

        value2.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void Cache_Dispose_DisposesAllEntries()
    {
        DisposableTestCache cache = new();

        var entry1 = cache.GetEntry("test1");
        var entry2 = cache.GetEntry("test2");

        DisposableValue value1 = entry1.Object;
        DisposableValue value2 = entry2.Object;

        value1.IsDisposed.Should().BeFalse();
        value2.IsDisposed.Should().BeFalse();

        cache.Dispose();

        value1.IsDisposed.Should().BeTrue();
        value2.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void CacheEntry_Dispose_DisposesObjectAndData()
    {
        DisposableTestCache cache = new();
        var entry = cache.GetEntry("test");

        DisposableValue value = entry.Object;
        value.IsDisposed.Should().BeFalse();

        entry.Dispose();
        value.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void Cache_MoveToFront_OptimizesAccess()
    {
        TestCache cache = new();

        // Create many entries to test move-to-front logic
        List<RefCountedCache<string, string, string>.CacheEntry> entries = [];
        for (int i = 0; i < 15; i++)
        {
            entries.Add(cache.GetEntry($"test{i}"));
        }

        // Access an entry that should be far in the list
        var lastEntry = cache.GetEntry("test14");
        lastEntry.Should().BeSameAs(entries[14]);

        // The entry should have been moved to front due to MoveToFront logic
        var firstAccess = cache.GetEntry("test14");
        firstAccess.Should().BeSameAs(lastEntry);
    }

    [Fact]
    public void Cache_MultipleScopes_TrackReferencesCorrectly()
    {
        TestCache cache = new();
        var entry = cache.GetEntry("test");

        entry.RefCount.Should().Be(0);

        {
            using var scope1 = entry.CreateScope();
            entry.RefCount.Should().Be(1);

            using var scope2 = entry.CreateScope();
            entry.RefCount.Should().Be(2);

            using var scope3 = entry.CreateScope();
            entry.RefCount.Should().Be(3);
        }

        // All scopes dispose automatically at end of using blocks
        // Final ref count should be 0
        entry.RefCount.Should().Be(0);
    }
}
