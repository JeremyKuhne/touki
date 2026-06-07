// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Tracing.Providers;

[TestClass]
public sealed class JitStatsProviderTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    // The JIT smoke trace is captured under the JIT profile, so it carries the
    // method-jitting events this provider reads.
    private static JitStatsResult LoadJitStats() =>
        new JitStatsProvider().Read(FixturePath("jit.nettrace"));

    [TestMethod]
    public void Read_JitFixture_ReportsCompiledMethods()
    {
        JitStatsResult result = LoadJitStats();

        result.MethodCount.Should().BeGreaterThan(0, "the JIT workload compiles methods on first call");
        result.Methods.Should().HaveCount(result.MethodCount);
    }

    [TestMethod]
    public void Read_JitFixture_CompileSummaryIsConsistent()
    {
        JitStatsResult result = LoadJitStats();

        result.TotalCompileMs.Should().BeGreaterThan(0.0);
        result.MaxCompileMs.Should().BeGreaterThan(0.0);
        // The mean lies on the total/count line, and the max never exceeds the total.
        result.MeanCompileMs.Should().BeApproximately(result.TotalCompileMs / result.MethodCount, 0.001);
        result.MaxCompileMs.Should().BeLessThanOrEqualTo(result.TotalCompileMs);
    }

    [TestMethod]
    public void Read_JitFixture_SizeTotalsMatchTheRecords()
    {
        JitStatsResult result = LoadJitStats();

        result.TotalILSize.Should().Be(result.Methods.Sum(static m => (long)m.ILSize));
        result.TotalNativeSize.Should().Be(result.Methods.Sum(static m => (long)m.NativeSize));
    }

    [TestMethod]
    public void Read_JitFixture_EveryRecordIsWellFormed()
    {
        JitStatsResult result = LoadJitStats();

        result.Methods.Should().OnlyContain(m => m.MethodName.Length > 0);
        result.Methods.Should().OnlyContain(m => m.ILSize >= 0 && m.NativeSize >= 0);
        result.Methods.Should().OnlyContain(m => m.CompileMs >= 0.0);
    }

    [TestMethod]
    public void Read_JitFixture_IncludesTheBenchmarkMethods()
    {
        JitStatsResult result = LoadJitStats();

        // The JitLoop benchmark's deliberately named methods are jitted once each.
        result.Methods.Should().Contain(m => m.MethodName.Contains("JitMethod", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Read_MissingFile_ThrowsFileNotFound()
    {
        JitStatsProvider provider = new();

        Action act = () => provider.Read(FixturePath("does-not-exist.nettrace"));

        act.Should().Throw<FileNotFoundException>();
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(null)]
    public void Read_NullOrEmptyPath_ThrowsArgument(string? path)
    {
        JitStatsProvider provider = new();

        Action act = () => provider.Read(path!);

        act.Should().Throw<ArgumentException>();
    }
}
