// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Tracing;

// SymbolOptions is the high-level native-symbol intent; its factories and cache-key
// fragment are platform-agnostic (no trace read or network), so they run everywhere.
// The actual native resolution is exercised manually against a real .etl with network;
// it is not pinned by a committed fixture (the trimmed etw.etl carries no rundown).
[TestClass]
public sealed class SymbolOptionsTests
{
    [TestMethod]
    public void None_IsManagedOnlyAndOffline()
    {
        SymbolOptions.None.ResolveNativeRuntime.Should().BeFalse();
        SymbolOptions.None.CacheDirectory.Should().BeNull();
    }

    [TestMethod]
    public void WithCache_DefaultsToTheSharedTempCache()
    {
        SymbolOptions options = SymbolOptions.WithCache();

        options.ResolveNativeRuntime.Should().BeTrue();
        // A null cache directory means "use the default"; the reader resolves it to
        // DefaultCacheDirectory, so the value stays null here by design.
        options.CacheDirectory.Should().BeNull();
    }

    [TestMethod]
    public void WithCache_CarriesAnExplicitCacheDirectory()
    {
        SymbolOptions options = SymbolOptions.WithCache(@"C:\sym");

        options.ResolveNativeRuntime.Should().BeTrue();
        options.CacheDirectory.Should().Be(@"C:\sym");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(null)]
    public void WithCache_EmptyDirectory_FallsBackToTheDefault(string? dir)
    {
        // An empty or null directory is normalized to null so the reader uses the
        // default cache, rather than treating "" as a real (cwd) path.
        SymbolOptions.WithCache(dir).CacheDirectory.Should().BeNull();
    }

    [TestMethod]
    [DataRow(@"C:\sym*evil")]
    [DataRow("*")]
    [DataRow(@"srv*c:\x*https://evil.example/symbols")]
    public void WithCache_DirectoryWithStar_Throws(string dir)
    {
        // The cache directory is interpolated into a SymSrv path element
        // (srv*<cache>*<server>), so a '*' would split into extra elements and could
        // redirect the effective symbol path; it is rejected up front.
        Action act = () => SymbolOptions.WithCache(dir);

        act.Should().Throw<ArgumentException>().WithParameterName("cacheDirectory");
    }

    [TestMethod]
    public void DefaultCacheDirectory_IsUnderTheTempPath()
    {
        SymbolOptions.DefaultCacheDirectory.Should().Be(Path.Combine(Path.GetTempPath(), "traceq-symbols"));
    }

    [TestMethod]
    public void CacheKeyFragment_None_IsManaged()
    {
        // The managed-only default and native resolution must produce different cache
        // keys so the same trace read both ways is cached separately.
        SymbolOptions.None.CacheKeyFragment().Should().Be("managed");
    }

    [TestMethod]
    public void CacheKeyFragment_Native_DiffersFromManagedAndCarriesTheCache()
    {
        string nativeKey = SymbolOptions.WithCache(@"C:\sym").CacheKeyFragment();

        nativeKey.Should().NotBe("managed");
        nativeKey.Should().Contain(@"C:\sym");
    }

    [TestMethod]
    public void CacheKeyFragment_NativeWithDifferentCaches_Differ()
    {
        // Two native reads with distinct cache directories are distinct cache entries.
        string a = SymbolOptions.WithCache(@"C:\a").CacheKeyFragment();
        string b = SymbolOptions.WithCache(@"C:\b").CacheKeyFragment();

        a.Should().NotBe(b);
    }
}
