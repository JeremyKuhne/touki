// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Tracing.Providers;

namespace TraceQ.Tracing;

// Thread time is reconstructed from ETW context-switch events, which is a
// Windows-only capture, so these read an .etl and are Windows-guarded; they skip
// cleanly on the Linux CI leg.
[TestClass]
[OSCondition(OperatingSystems.Windows)]
public sealed class ThreadTimeProviderTests
{
    private static string EtwFixture => Path.Combine(AppContext.BaseDirectory, "Fixtures", "etw.etl");

    private static StackSampleSource Read(ProcessScope? scope = null) =>
        new ThreadTimeProvider().Read(EtwFixture, scope);

    private static bool HasLeaf(StackSampleSource source, string leaf) =>
        source.Samples.Any(s => s.Frames.Count > 0
            && s.Frames[^1].Contains(leaf, StringComparison.Ordinal));

    [TestMethod]
    public void Read_EtwFixture_ProducesThreadTimeSamples()
    {
        StackSampleSource source = Read();

        source.Metric.Should().Be(MetricInfo.ThreadTime);
        source.Metric.Unit.Should().Be("ms");
        source.Samples.Should().NotBeEmpty();
        source.Samples.Should().OnlyContain(s => s.Weight > 0.0);
    }

    [TestMethod]
    public void Read_EtwFixture_AccountsForBlockedAndRunningTime()
    {
        StackSampleSource source = Read();

        // The distinguishing property of thread time over CPU sampling: it captures
        // off-CPU (blocked) intervals as well as running ones. Both leaves appear,
        // because the workload interleaves hot CPU work with brief sleeps.
        HasLeaf(source, "BLOCKED_TIME").Should().BeTrue("the workload blocks on its sleeps");
        HasLeaf(source, "CPU_TIME").Should().BeTrue("the workload also runs on the CPU");
    }

    [TestMethod]
    public void Read_EtwFixture_RootsStacksAtProcessAndThread()
    {
        StackSampleSource source = Read();

        // Every stack is rooted at its process, then its thread, so a multi-process
        // capture is attributable; the per-process label is carried on the sample too.
        source.Samples.Should().OnlyContain(s => s.Process.Length > 0);
        source.Samples.Should().Contain(s => s.Frames.Count > 1
            && s.Frames[1].StartsWith("Thread (", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Read_ScopedToJobChild_NarrowsToThatProcess()
    {
        StackSampleSource all = Read();
        StackSampleSource scoped = Read(new ProcessScope("HotLoopBench-Job", IncludeChildren: false));

        scoped.Samples.Should().NotBeEmpty();
        scoped.Samples.Count.Should().BeLessThan(all.Samples.Count);
        scoped.Samples.Select(static s => s.Process).Distinct(StringComparer.Ordinal)
            .Should().ContainSingle()
            .Which.Should().Contain("Job");
    }

    [TestMethod]
    public void Read_ScopeMatchingNoProcess_YieldsNoSamples()
    {
        StackSampleSource scoped = Read(new ProcessScope("no-such-process-name"));

        scoped.Samples.Should().BeEmpty();
    }

    [TestMethod]
    public void Read_ScopedSource_RanksThroughTheEngine()
    {
        StackSampleSource source = Read(new ProcessScope("HotLoopBench-Job", IncludeChildren: false));
        FoldingAggregator aggregator = new(source);

        RankingResult ranking = aggregator.SelfTime("", FrameNames.DefaultFoldPatterns, 5);

        ranking.Rows.Should().NotBeEmpty();
        ranking.ScopeWeight.Should().BeGreaterThan(0.0);
    }

    [TestMethod]
    public void Read_MissingFile_ThrowsFileNotFound()
    {
        Action act = () => new ThreadTimeProvider().Read(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "does-not-exist.etl"));

        act.Should().Throw<FileNotFoundException>();
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(null)]
    public void Read_NullOrEmptyPath_ThrowsArgument(string? path)
    {
        Action act = () => new ThreadTimeProvider().Read(path!);

        act.Should().Throw<ArgumentException>();
    }
}
