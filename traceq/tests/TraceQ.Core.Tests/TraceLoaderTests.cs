// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Tracing;

[TestClass]
public sealed class TraceLoaderTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [TestMethod]
    public void Load_MissingFile_ThrowsFileNotFound()
    {
        TraceLoader loader = new();
        string missing = Path.Combine(AppContext.BaseDirectory, "Fixtures", "missing.speedscope.json");

        Action act = () => loader.Load(missing);

        act.Should().Throw<FileNotFoundException>();
    }

    [TestMethod]
    public void Load_UnsupportedExtension_ThrowsNotSupported()
    {
        TraceLoader loader = new();
        string temp = Path.GetTempFileName();
        try
        {
            Action act = () => loader.Load(temp);

            act.Should().Throw<NotSupportedException>();
        }
        finally
        {
            File.Delete(temp);
        }
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(null)]
    public void Load_NullOrEmptyPath_ThrowsArgument(string? path)
    {
        TraceLoader loader = new();

        Action act = () => loader.Load(path!);

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void Load_AllocationMetric_BuildsAllocationSource()
    {
        TraceLoader loader = new();

        LoadedTrace trace = loader.Load(FixturePath("alloc.nettrace"), TraceMetric.Allocations);

        // The loader's provider seam hands the aggregator the allocation source, so the
        // whole engine ranks bytes through the same path it ranks CPU milliseconds.
        trace.Source.Metric.Should().Be(MetricInfo.Allocations);
        trace.Aggregator.Metric.Should().Be(MetricInfo.Allocations);
        trace.Info.Format.Should().Be(TraceFormat.NetTrace);
        trace.Info.SampleCount.Should().BeGreaterThan(0);
        // TotalWeight carries the sum of the sample weights in the metric's unit, which
        // for allocation is total bytes - a large positive number, not a CPU duration.
        trace.Info.TotalWeight.Should().BeGreaterThan(0);
    }

    [TestMethod]
    public void Load_AllocationMetric_ReportsResolvedSymbolsAndNoWarning()
    {
        TraceLoader loader = new();

        LoadedTrace trace = loader.Load(FixturePath("alloc.nettrace"), TraceMetric.Allocations);

        // Managed allocation frames resolve from the trace's CLR rundown, so the
        // synthesized info reports full resolution and the --strict gate cannot trip.
        trace.Info.SymbolResolutionRate.Should().Be(1.0);
        trace.Info.Warnings.Should().BeEmpty();
    }

    [TestMethod]
    public void Load_NetTrace_ResolvesDotNetRuntimeManagedMethods()
    {
        TraceLoader loader = new();

        LoadedTrace trace = loader.Load(FixturePath("alloc.nettrace"), TraceMetric.Allocations);

        // The .NET runtime/framework managed methods on the stacks are ReadyToRun
        // (R2R) precompiled images on modern .NET, not JITted - so this pins that
        // precompiled managed frames resolve to qualified names from the EventPipe
        // CLR rundown, with no symbol server and no local PDBs. A regression here
        // (an unresolved BCL frame surfacing as "module!?" or "?") would silently
        // degrade every managed ranking, so it is asserted directly rather than
        // inferred from the aggregate resolution rate.
        IEnumerable<string> allFrames = trace.Source.Samples.SelectMany(static s => s.Frames);

        // The BCL namespace is `System.`; a resolved frame is a qualified
        // `Namespace.Type.Method`, never the `?` / `module!?` unresolved placeholders.
        allFrames.Should().Contain(
            f => f.StartsWith("System.", StringComparison.Ordinal) && !f.EndsWith("!?", StringComparison.Ordinal),
            because: "precompiled .NET runtime managed methods must resolve to names from the CLR rundown");

        // A concrete BCL type the AllocLoop fixture exercises (it allocates strings),
        // pinning that a specific runtime type resolves by name and not just that
        // some `System.` frame happens to be present.
        allFrames.Should().Contain(
            f => f.Contains("System.String", StringComparison.Ordinal),
            because: "the string allocations in the fixture must attribute to a named System.String method");
    }

    [TestMethod]
    public void Load_AllocationMetricOnSpeedscope_ThrowsNotSupported()
    {
        TraceLoader loader = new();

        // A speedscope export carries no allocation events, so the format guardrail
        // rejects the allocation metric rather than failing deep in a reader.
        Action act = () => loader.Load(FixturePath("folding.speedscope.json"), TraceMetric.Allocations);

        act.Should().Throw<NotSupportedException>().WithMessage("*allocation metric requires*");
    }

    [TestMethod]
    public void Load_CpuMetric_BuildsCpuSource()
    {
        TraceLoader loader = new();

        // The default and the explicit CPU metric resolve to the same CPU view.
        LoadedTrace trace = loader.Load(FixturePath("folding.speedscope.json"), TraceMetric.Cpu);

        trace.Source.Metric.Should().Be(MetricInfo.Cpu);
        trace.Info.Format.Should().Be(TraceFormat.Speedscope);
    }

    [TestMethod]
    public void Load_ExceptionsMetric_BuildsExceptionsSource()
    {
        TraceLoader loader = new();

        LoadedTrace trace = loader.Load(FixturePath("exceptions.nettrace"), TraceMetric.Exceptions);

        trace.Source.Metric.Should().Be(MetricInfo.Exceptions);
        trace.Aggregator.Metric.Should().Be(MetricInfo.Exceptions);
        trace.Info.Format.Should().Be(TraceFormat.NetTrace);
        trace.Info.SampleCount.Should().BeGreaterThan(0);
        // Throw-site frames resolve from the CLR rundown, so resolution is reported
        // complete and the --strict gate cannot trip.
        trace.Info.SymbolResolutionRate.Should().Be(1.0);
        trace.Info.Warnings.Should().BeEmpty();
    }

    [TestMethod]
    public void Load_ExceptionsMetricOnSpeedscope_ThrowsNotSupported()
    {
        TraceLoader loader = new();

        // A speedscope export carries no exception events, so the format guardrail
        // rejects the exceptions metric rather than failing deep in a reader.
        Action act = () => loader.Load(FixturePath("folding.speedscope.json"), TraceMetric.Exceptions);

        act.Should().Throw<NotSupportedException>().WithMessage("*exceptions metric requires*");
    }

    [TestMethod]
    public void Load_ThreadTimeMetricOnNetTrace_ThrowsNotSupported()
    {
        TraceLoader loader = new();

        // The thread-time guardrail checks the format (an extension test) before any
        // .etl conversion, so this rejection is platform-agnostic and runs everywhere.
        Action act = () => loader.Load(FixturePath("alloc.nettrace"), TraceMetric.ThreadTime);

        act.Should().Throw<NotSupportedException>().WithMessage("*thread-time metric requires*");
    }

    [TestMethod]
    public void Load_UndefinedMetric_ThrowsArgumentOutOfRange()
    {
        TraceLoader loader = new();

        // Enums can be cast from any int, so an undefined value must fail fast rather
        // than silently fall back to a CPU load and mask the bad input.
        Action act = () => loader.Load(FixturePath("folding.speedscope.json"), (TraceMetric)999);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}

// Loading the thread-time view reads an .etl, whose ETW conversion is Windows-only,
// so these are Windows-guarded and skip cleanly on the Linux CI leg.
[TestClass]
[OSCondition(OperatingSystems.Windows)]
public sealed class TraceLoaderThreadTimeTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [TestMethod]
    public void Load_ThreadTimeMetric_BuildsThreadTimeSource()
    {
        TraceLoader loader = new();

        LoadedTrace trace = loader.Load(FixturePath("etw.etl"), TraceMetric.ThreadTime);

        // The loader's provider seam hands the aggregator the thread-time source, so
        // the engine ranks elapsed milliseconds through the same path it ranks CPU.
        trace.Source.Metric.Should().Be(MetricInfo.ThreadTime);
        trace.Aggregator.Metric.Should().Be(MetricInfo.ThreadTime);
        trace.Info.Format.Should().Be(TraceFormat.Etl);
        trace.Info.SampleCount.Should().BeGreaterThan(0);
        trace.Info.TotalWeight.Should().BeGreaterThan(0);
    }

    [TestMethod]
    public void Load_ThreadTimeScopeMatchingNoProcess_WarnsAboutTheScopeNotTheCapture()
    {
        TraceLoader loader = new();

        LoadedTrace trace = loader.Load(
            FixturePath("etw.etl"),
            TraceMetric.ThreadTime,
            scope: ScopeRequest.ForProcess("no-such-process-name"));

        // When the scope drops every sample the warning must blame the scope, not imply
        // the capture lacks the context-switch keywords (the capture is fine).
        trace.Info.Warnings.Should().Contain(w => w.Contains("after scoping to", StringComparison.Ordinal));
        trace.Info.Warnings.Should().NotContain(w =>
            w.Contains("context-switch keywords", StringComparison.Ordinal));
    }
}
