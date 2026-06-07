// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Tracing;

// The ProcessScope constructor validation is platform-agnostic (no trace read), so
// it runs everywhere - unlike the ETL-backed scoping tests below.
[TestClass]
public sealed class ProcessScopeValidationTests
{
    [TestMethod]
    [DataRow("")]
    [DataRow(null)]
    public void Ctor_NullOrEmptyNameSubstring_ThrowsArgument(string? name)
    {
        Action act = () => _ = new ProcessScope(name!);

        act.Should().Throw<ArgumentException>().WithParameterName("NameSubstring");
    }

    [TestMethod]
    public void Ctor_NonEmptyNameSubstring_Succeeds()
    {
        ProcessScope scope = new("HotLoopBench");

        scope.NameSubstring.Should().Be("HotLoopBench");
        scope.IncludeChildren.Should().BeTrue("children are followed by default");
    }
}

// Reading an ETW (.etl) trace uses the Windows-only ETW conversion, so these
// tests are restricted to Windows; on other platforms they are skipped. The
// process-tree scoping logic they cover is platform-agnostic - only the .etl
// read path underneath is not.
[TestClass]
[OSCondition(OperatingSystems.Windows)]
public sealed class ProcessScopeTests
{
    private static string EtwFixture => Path.Combine(AppContext.BaseDirectory, "Fixtures", "etw.etl");

    private static IReadOnlyList<SampleStack> Load(ProcessScope? scope) =>
        new TraceLoader().Load(EtwFixture, processScope: scope).Source.Samples;

    private static int DistinctProcesses(IReadOnlyList<SampleStack> samples) =>
        samples.Select(static s => s.Process).Distinct(StringComparer.Ordinal).Count();

    [TestMethod]
    public void Read_EtlFixture_ProducesCpuSamples()
    {
        IReadOnlyList<SampleStack> samples = Load(scope: null);

        // The ETW fixture is a CPU-sampled capture, so the reader yields weighted
        // stacks; at least some carry a resolved "module!method" frame.
        samples.Should().NotBeEmpty();
        samples.Should().Contain(s => s.Frames.Any(f => f.Contains('!', StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Read_EtlFixture_SpansMultipleProcesses()
    {
        IReadOnlyList<SampleStack> samples = Load(scope: null);

        // The unscoped capture is the BenchmarkDotNet process tree: the host, the job
        // child, and its console host - more than one process.
        DistinctProcesses(samples).Should().BeGreaterThan(1);
        samples.Should().OnlyContain(s => s.Process.Length > 0);
    }

    [TestMethod]
    public void Read_ScopedToJobChild_KeepsOnlyThatProcessAndIsNarrower()
    {
        IReadOnlyList<SampleStack> all = Load(scope: null);
        IReadOnlyList<SampleStack> scoped = Load(new ProcessScope("HotLoopBench-Job", IncludeChildren: false));

        // Scoping to the job process alone drops the host and console-host samples.
        scoped.Should().NotBeEmpty();
        scoped.Count.Should().BeLessThan(all.Count);
        DistinctProcesses(scoped).Should().Be(1);
        scoped.Should().OnlyContain(s => s.Process.Contains("Job", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Read_ScopedToTree_IsASubsetOfTheWholeCapture()
    {
        IReadOnlyList<SampleStack> all = Load(scope: null);
        IReadOnlyList<SampleStack> jobOnly = Load(new ProcessScope("HotLoopBench-Job", IncludeChildren: false));
        IReadOnlyList<SampleStack> tree = Load(new ProcessScope("HotLoopBench"));

        // The "HotLoopBench" tree (host + job + descendants) is at least the job child
        // and never more than the whole capture.
        tree.Count.Should().BeGreaterThanOrEqualTo(jobOnly.Count);
        tree.Count.Should().BeLessThanOrEqualTo(all.Count);
        tree.Should().NotBeEmpty();
    }

    [TestMethod]
    public void Read_ScopeIncludingChildren_IsNoNarrowerThanExcludingThem()
    {
        IReadOnlyList<SampleStack> withChildren = Load(new ProcessScope("HotLoopBench", IncludeChildren: true));
        IReadOnlyList<SampleStack> withoutChildren = Load(new ProcessScope("HotLoopBench", IncludeChildren: false));

        // Following children can only ever keep more samples, never fewer.
        withChildren.Count.Should().BeGreaterThanOrEqualTo(withoutChildren.Count);
    }

    [TestMethod]
    public void Read_ScopeMatchingNoProcess_YieldsNoSamples()
    {
        IReadOnlyList<SampleStack> scoped = Load(new ProcessScope("no-such-process-name"));

        scoped.Should().BeEmpty();
    }

    [TestMethod]
    public void Read_ScopedTree_RanksThroughTheEngineUnchanged()
    {
        LoadedTrace loaded = new TraceLoader().Load(
            EtwFixture,
            processScope: new ProcessScope("HotLoopBench-Job", IncludeChildren: false));

        RankingResult ranking = loaded.Aggregator.SelfTime("", FrameNames.DefaultFoldPatterns, 5);

        // A scoped source flows through the folding aggregator like any other.
        ranking.Rows.Should().NotBeEmpty();
        ranking.ScopeWeight.Should().BeGreaterThan(0.0);
    }
}
