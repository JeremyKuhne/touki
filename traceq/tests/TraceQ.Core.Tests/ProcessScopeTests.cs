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

// ScopeRequest is the high-level CLI intent; its factory validation is
// platform-agnostic (no trace read), so it runs everywhere.
[TestClass]
public sealed class ScopeRequestTests
{
    [TestMethod]
    public void Auto_HasNoNameAndIsNotAllProcesses()
    {
        ScopeRequest.Auto.ProcessName.Should().BeNull();
        ScopeRequest.Auto.IncludeAll.Should().BeFalse();
    }

    [TestMethod]
    public void AllProcesses_IsTheOptOut()
    {
        ScopeRequest.AllProcesses.IncludeAll.Should().BeTrue();
        ScopeRequest.AllProcesses.ProcessName.Should().BeNull();
    }

    [TestMethod]
    public void ForProcess_CarriesTheNameAndChildrenDefault()
    {
        ScopeRequest scope = ScopeRequest.ForProcess("MyApp");

        scope.ProcessName.Should().Be("MyApp");
        scope.IncludeAll.Should().BeFalse();
        scope.IncludeChildren.Should().BeTrue("children are followed by default");
    }

    [TestMethod]
    public void ForProcess_CanExcludeChildren()
    {
        ScopeRequest.ForProcess("MyApp", includeChildren: false).IncludeChildren.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(null)]
    public void ForProcess_NullOrEmptyName_ThrowsArgument(string? name)
    {
        Action act = () => ScopeRequest.ForProcess(name!);

        act.Should().Throw<ArgumentException>();
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

    // A null scope reads every process (the core loader only narrows when given a
    // request); the scoped overloads pass an explicit ScopeRequest.
    private static IReadOnlyList<SampleStack> Load(ScopeRequest? scope) =>
        new TraceLoader().Load(EtwFixture, scope: scope).Source.Samples;

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
        IReadOnlyList<SampleStack> scoped = Load(ScopeRequest.ForProcess("HotLoopBench-Job", includeChildren: false));

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
        IReadOnlyList<SampleStack> jobOnly = Load(ScopeRequest.ForProcess("HotLoopBench-Job", includeChildren: false));
        IReadOnlyList<SampleStack> tree = Load(ScopeRequest.ForProcess("HotLoopBench"));

        // The "HotLoopBench" tree (host + job + descendants) is at least the job child
        // and never more than the whole capture.
        tree.Count.Should().BeGreaterThanOrEqualTo(jobOnly.Count);
        tree.Count.Should().BeLessThanOrEqualTo(all.Count);
        tree.Should().NotBeEmpty();
    }

    [TestMethod]
    public void Read_ScopeIncludingChildren_IsNoNarrowerThanExcludingThem()
    {
        IReadOnlyList<SampleStack> withChildren = Load(ScopeRequest.ForProcess("HotLoopBench", includeChildren: true));
        IReadOnlyList<SampleStack> withoutChildren = Load(ScopeRequest.ForProcess("HotLoopBench", includeChildren: false));

        // Following children can only ever keep more samples, never fewer.
        withChildren.Count.Should().BeGreaterThanOrEqualTo(withoutChildren.Count);
    }

    [TestMethod]
    public void Read_ScopeMatchingNoProcess_YieldsNoSamples()
    {
        IReadOnlyList<SampleStack> scoped = Load(ScopeRequest.ForProcess("no-such-process-name"));

        scoped.Should().BeEmpty();
    }

    [TestMethod]
    public void Read_ScopedTree_RanksThroughTheEngineUnchanged()
    {
        LoadedTrace loaded = new TraceLoader().Load(
            EtwFixture,
            scope: ScopeRequest.ForProcess("HotLoopBench-Job", includeChildren: false));

        RankingResult ranking = loaded.Aggregator.SelfTime("", FrameNames.DefaultFoldPatterns, 5);

        // A scoped source flows through the folding aggregator like any other.
        ranking.Rows.Should().NotBeEmpty();
        ranking.ScopeWeight.Should().BeGreaterThan(0.0);
    }

    [TestMethod]
    public void Read_AutoScope_KeepsNoMoreThanTheWholeCapture()
    {
        LoadedTrace all = new TraceLoader().Load(EtwFixture, scope: ScopeRequest.AllProcesses);
        LoadedTrace auto = new TraceLoader().Load(EtwFixture, scope: ScopeRequest.Auto);

        // The automatic default scopes to the busiest process tree, which can never
        // keep more than the whole capture. (On this committed fixture - a tight BDN
        // process tree already trimmed to the workload - the busiest tree happens to be
        // the whole capture, so it does not narrow further; on a real machine-wide
        // capture it would.)
        auto.Source.Samples.Count.Should().BeLessThanOrEqualTo(all.Source.Samples.Count);
        auto.Source.Samples.Should().NotBeEmpty();
    }

    [TestMethod]
    public void Read_AutoScope_OnAnAlreadyScopedCapture_DoesNotWarn()
    {
        // The applied-scope notice is suppressed when the automatic scope did not
        // actually drop any process - emitting "Scoped to X; pass --all-processes" for
        // a no-op would be misleading. This fixture's busiest tree is the whole capture.
        LoadedTrace auto = new TraceLoader().Load(EtwFixture, scope: ScopeRequest.Auto);
        LoadedTrace all = new TraceLoader().Load(EtwFixture, scope: ScopeRequest.AllProcesses);

        if (auto.Source.Samples.Count == all.Source.Samples.Count)
        {
            auto.Info.Warnings.Should().NotContain(w => w.StartsWith("Scoped to the ", StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public void Read_ExplicitNarrowingScope_Warns()
    {
        // An explicit --process that drops part of the capture surfaces the scope
        // notice so the agent knows the ranking covers one tree and how to widen.
        LoadedTrace scoped = new TraceLoader().Load(
            EtwFixture,
            scope: ScopeRequest.ForProcess("HotLoopBench-Job", includeChildren: false));

        scoped.Info.Warnings.Should().Contain(w =>
            w.StartsWith("Scoped to the ", StringComparison.Ordinal)
            && w.Contains("--all-processes", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Read_AllProcesses_ReadsEveryProcessAndDoesNotWarn()
    {
        LoadedTrace all = new TraceLoader().Load(EtwFixture, scope: ScopeRequest.AllProcesses);

        // The opt-out reads the whole capture (more than one process) and emits no
        // scope notice.
        DistinctProcesses(all.Source.Samples).Should().BeGreaterThan(1);
        all.Info.Warnings.Should().NotContain(w => w.StartsWith("Scoped to the ", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Read_AutoScope_IsDeterministicAcrossReads()
    {
        // The busiest-process choice is a pure function of the trace, so two automatic
        // reads of the same capture resolve to the same process and keep the same
        // samples. (The exact process is the heaviest CPU consumer, which need not be a
        // touki-named process - the BDN host can dominate - so this asserts stability
        // rather than a specific name.)
        LoadedTrace first = new TraceLoader().Load(EtwFixture, scope: ScopeRequest.Auto);
        LoadedTrace second = new TraceLoader().Load(EtwFixture, scope: ScopeRequest.Auto);

        first.Source.Samples.Count.Should().Be(second.Source.Samples.Count);
        first.Info.Warnings.Should().BeEquivalentTo(second.Info.Warnings);
    }
}
