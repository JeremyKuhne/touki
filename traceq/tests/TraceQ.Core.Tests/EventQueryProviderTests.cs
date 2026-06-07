// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Tracing.Providers;

[TestClass]
public sealed class EventQueryProviderTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static string AllocTrace => FixturePath("alloc.nettrace");

    [TestMethod]
    public void Query_NoFilter_MatchesEveryEvent()
    {
        EventQueryResult result = new EventQueryProvider().Query(AllocTrace, take: 5);

        // The fixture carries hundreds of events; the total reflects all of them.
        result.TotalMatched.Should().BeGreaterThan(5);
        result.Events.Should().HaveCount(5, "take caps the page");
    }

    [TestMethod]
    public void Query_NameFilter_MatchesOnlyTheNamedEvents()
    {
        EventQueryResult result = new EventQueryProvider().Query(AllocTrace, "AllocationTick", take: 1000);

        result.TotalMatched.Should().BeGreaterThan(0);
        result.Events.Should().OnlyContain(e => e.EventName.Contains("AllocationTick", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Query_Pagination_SkipAndTakeWalkDistinctPages()
    {
        EventQueryProvider provider = new();
        EventQueryResult firstPage = provider.Query(AllocTrace, "AllocationTick", skip: 0, take: 3);
        EventQueryResult secondPage = provider.Query(AllocTrace, "AllocationTick", skip: 3, take: 3);

        firstPage.Events.Should().HaveCount(3);
        secondPage.Events.Should().HaveCount(3);
        secondPage.Skipped.Should().Be(3);
        // The two pages cover different events (the totals agree, the times differ).
        firstPage.TotalMatched.Should().Be(secondPage.TotalMatched);
        secondPage.Events[0].TimestampMs.Should().BeGreaterThan(firstPage.Events[0].TimestampMs);
    }

    [TestMethod]
    public void Query_TruncatesPayloadToTheCap()
    {
        EventQueryResult result = new EventQueryProvider().Query(AllocTrace, "AllocationTick", take: 5, maxPayloadChars: 16);

        result.Events.Should().OnlyContain(e => e.Payload.Length <= 16);
        result.Events.Should().Contain(e => e.Payload.Length > 0, "AllocationTick carries named fields");
    }

    [TestMethod]
    public void Query_ZeroPayloadCap_ReturnsEmptyPayloads()
    {
        EventQueryResult result = new EventQueryProvider().Query(AllocTrace, "AllocationTick", take: 5, maxPayloadChars: 0);

        result.Events.Should().OnlyContain(e => e.Payload.Length == 0);
    }

    [TestMethod]
    public void Query_RecordsCarryProviderAndThread()
    {
        EventQueryResult result = new EventQueryProvider().Query(AllocTrace, "AllocationTick", take: 1);

        EventRecord first = result.Events.Single();
        first.Provider.Should().Contain("DotNETRuntime");
        first.EventName.Should().Contain("AllocationTick");
        first.TimestampMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [TestMethod]
    public void Query_MissingFile_ThrowsFileNotFound()
    {
        Action act = () => new EventQueryProvider().Query(FixturePath("nope.nettrace"));

        act.Should().Throw<FileNotFoundException>();
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(null)]
    public void Query_NullOrEmptyPath_ThrowsArgument(string? path)
    {
        Action act = () => new EventQueryProvider().Query(path!);

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void Query_NegativeSkip_ThrowsArgumentOutOfRange()
    {
        Action act = () => new EventQueryProvider().Query(AllocTrace, skip: -1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [TestMethod]
    public void Query_SkipBeyondTotal_ReportsTheActualSkipped()
    {
        EventQueryResult result = new EventQueryProvider().Query(AllocTrace, "AllocationTick", skip: 1_000_000);

        // Nothing is returned, and Skipped reflects the matches actually passed over
        // (the total), not the larger requested skip.
        result.Events.Should().BeEmpty();
        result.Skipped.Should().Be(result.TotalMatched);
    }

    [TestMethod]
    public void AppendCapped_OversizedValue_NeverGrowsPastTheCap()
    {
        System.Text.StringBuilder builder = new();
        string huge = new('x', 100_000);

        EventQueryProvider.AppendCapped(builder, huge, 64);

        // A single degenerately large value must not grow the builder past the cap.
        builder.Length.Should().Be(64);
    }

    [TestMethod]
    public void AppendCapped_AtCapacity_AppendsNothing()
    {
        System.Text.StringBuilder builder = new();
        builder.Append(new string('a', 10));

        EventQueryProvider.AppendCapped(builder, "more", 10);

        builder.Length.Should().Be(10);
    }
}
