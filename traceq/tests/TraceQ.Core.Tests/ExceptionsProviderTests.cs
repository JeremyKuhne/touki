// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Tracing;

namespace TraceQ.Tracing.Providers;

[TestClass]
public sealed class ExceptionsProviderTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static StackSampleSource LoadExceptions() =>
        new ExceptionsProvider().Read(FixturePath("exceptions.nettrace"));

    [TestMethod]
    public void Read_ExceptionFixture_CarriesTheExceptionsMetric()
    {
        StackSampleSource source = LoadExceptions();

        source.Metric.Should().Be(MetricInfo.Exceptions);
        source.Metric.Unit.Should().Be("count");
        source.Samples.Should().NotBeEmpty("the trace carries Exception/Start stacks");
    }

    [TestMethod]
    public void Read_ExceptionFixture_WeightsEachThrowAsOneCount()
    {
        StackSampleSource source = LoadExceptions();

        // Unlike CPU (ms) or allocation (bytes), the exceptions metric is a pure
        // count - each throw contributes exactly one.
        source.Samples.Should().OnlyContain(s => s.Weight == 1.0);
    }

    [TestMethod]
    public void Read_ExceptionFixture_CountsEveryThrow()
    {
        StackSampleSource source = LoadExceptions();

        // The benchmark throws exactly 2,000 exceptions, each carrying a throw-site
        // stack, so there is one sample per throw.
        source.Samples.Should().HaveCount(2000);
    }

    [TestMethod]
    public void InclusiveTime_ExceptionFixture_RanksTheThrowSites()
    {
        FoldingAggregator aggregator = new(LoadExceptions());

        RankingResult result = aggregator.InclusiveTime("", FrameNames.DefaultFoldPatterns, 50);

        // Every throw is one count, so the scope total is the number of throws.
        result.ScopeWeight.Should().Be(2000);

        // Both throwing methods are on their throw stacks, so both appear in the
        // inclusive ranking.
        result.Rows.Should().Contain(r => r.Frame.Contains("ThrowInvalidOperation", StringComparison.Ordinal));
        result.Rows.Should().Contain(r => r.Frame.Contains("ThrowArgument", StringComparison.Ordinal));
    }

    [TestMethod]
    public void InclusiveTime_ExceptionFixture_ReflectsTheThrowRatio()
    {
        FoldingAggregator aggregator = new(LoadExceptions());

        RankingResult result = aggregator.InclusiveTime("", FrameNames.DefaultFoldPatterns, 50);

        RankRow invalidOp = result.Rows.First(r => r.Frame.Contains("ThrowInvalidOperation", StringComparison.Ordinal));
        RankRow argument = result.Rows.First(r => r.Frame.Contains("ThrowArgument", StringComparison.Ordinal));

        // The benchmark throws InvalidOperationException about twice as often as
        // ArgumentException (1,333 vs 667), and the count ranking reflects that.
        (invalidOp.Weight / argument.Weight).Should().BeInRange(1.7, 2.3);
    }

    [TestMethod]
    public void SelfTime_ExceptionFixture_RanksByCountDescending()
    {
        FoldingAggregator aggregator = new(LoadExceptions());

        RankingResult result = aggregator.SelfTime("", FrameNames.DefaultFoldPatterns, 25);

        result.Rows.Should().NotBeEmpty();
        result.Rows[0].Weight.Should().BeGreaterThan(0);
        for (int i = 1; i < result.Rows.Count; i++)
        {
            result.Rows[i].Weight.Should().BeLessThanOrEqualTo(result.Rows[i - 1].Weight);
        }
    }

    [TestMethod]
    public void Read_MissingFile_ThrowsFileNotFound()
    {
        ExceptionsProvider provider = new();

        Action act = () => provider.Read(FixturePath("does-not-exist.nettrace"));

        act.Should().Throw<FileNotFoundException>();
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(null)]
    public void Read_NullOrEmptyPath_ThrowsArgument(string? path)
    {
        ExceptionsProvider provider = new();

        Action act = () => provider.Read(path!);

        act.Should().Throw<ArgumentException>();
    }
}
