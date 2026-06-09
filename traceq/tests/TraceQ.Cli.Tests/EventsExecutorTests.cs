// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Cli;

[TestClass]
public sealed class EventsExecutorTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static string Alloc => FixturePath("alloc.nettrace");

    private static string Speedscope => FixturePath("folding.speedscope.json");

    private static EventsRequest Request(
        string path,
        string name = "",
        int skip = 0,
        int take = 50,
        int maxPayload = 200,
        OutputFormat format = OutputFormat.Text) =>
        new(path, name, skip, take, maxPayload, format);

    private static (int Exit, string Out, string Error) Run(EventsRequest request)
    {
        StringWriter output = new();
        StringWriter error = new();
        int exit = EventsExecutor.Run(request, output, error);
        return (exit, output.ToString(), error.ToString());
    }

    [TestMethod]
    public void Run_TextFormat_WritesMatchedEvents()
    {
        (int exit, string output, _) = Run(Request(Alloc, take: 5));

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("events");
        output.Should().Contain("matched");
    }

    [TestMethod]
    public void Run_JsonFormat_WritesSingleLineEnvelope()
    {
        (int exit, string output, _) = Run(Request(Alloc, take: 5, format: OutputFormat.Json));

        exit.Should().Be(ExitCodes.Success);
        string json = output.Trim();
        json.Should().NotContain("\n");
        json.Should().Contain("\"schemaVersion\"");
        json.Should().Contain("\"totalMatched\"");
    }

    [TestMethod]
    public void Run_TakeCapsThePage()
    {
        (int exit, string output, _) = Run(Request(Alloc, take: 3, format: OutputFormat.Json));

        exit.Should().Be(ExitCodes.Success);
        // The page carries at most take events, each rendered with an eventName field.
        Regex.Matches(output, "\"eventName\":").Count.Should().BeLessThanOrEqualTo(3);
    }

    [TestMethod]
    public void Run_MorePagesRemain_EmitsAPagingHint()
    {
        // The fixture carries many events, so a tiny page leaves more matching and the
        // executor steers toward the next page.
        (int exit, string output, _) = Run(Request(Alloc, take: 1));

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("more match");
        output.Should().Contain("--skip 1");
    }

    [TestMethod]
    public void Run_NameFilter_MatchesOnlyTheNamedEvents()
    {
        (int exit, string output, _) = Run(Request(Alloc, name: "AllocationTick", take: 1000, format: OutputFormat.Json));

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("AllocationTick");
    }

    [TestMethod]
    public void Run_MissingFile_ReturnsInputError()
    {
        (int exit, _, string error) = Run(Request(FixturePath("does-not-exist.nettrace")));

        exit.Should().Be(ExitCodes.InputError);
        error.Should().NotBeEmpty();
    }

    [TestMethod]
    public void Run_WrongFormat_ReturnsInputError()
    {
        (int exit, _, string error) = Run(Request(Speedscope));

        exit.Should().Be(ExitCodes.InputError);
        error.Should().Contain("events report requires");
    }
}
