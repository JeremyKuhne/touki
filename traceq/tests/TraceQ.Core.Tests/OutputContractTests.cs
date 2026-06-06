// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Text.Json;
using TraceQ.Tracing;

namespace TraceQ.Output;

[TestClass]
public sealed class OutputContractTests
{
    private static AnalysisResult<RankingResult> SampleEnvelope()
    {
        RankingResult payload = new(
            25.0,
            "",
            [
                new RankRow("MyApp.Inner", 16.0, 64.0),
                new RankRow("MyApp.Work", 4.0, 16.0)
            ]);

        return new AnalysisResult<RankingResult>(
            payload,
            warnings: ["symbol resolution 50% (< 80%); pass --symbols <dir>"],
            hints: ["drill into the hot frame with: callers MyApp.Inner"]);
    }

    [TestMethod]
    public void Serialize_Envelope_IsSingleLineCompactJson()
    {
        string json = OutputJson.Serialize(SampleEnvelope());

        json.Should().NotContain("\n");
        json.Should().NotContain("\r");
        // No pretty-printing indentation.
        json.Should().NotContain("  ");
    }

    [TestMethod]
    public void Serialize_Envelope_CarriesSchemaVersionWarningsAndHints()
    {
        string json = OutputJson.Serialize(SampleEnvelope());

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        root.GetProperty("schemaVersion").GetInt32().Should().Be(AnalysisResult<RankingResult>.CurrentSchemaVersion);
        root.GetProperty("warnings").EnumerateArray().Should().ContainSingle();
        root.GetProperty("hints").EnumerateArray().Should().ContainSingle();
        root.GetProperty("result").GetProperty("rows").EnumerateArray().Should().HaveCount(2);
    }

    [TestMethod]
    public void Serialize_EmptyWarningsAndHints_AreEmptyArraysNotNull()
    {
        RankingResult payload = new(0.0, "", []);
        AnalysisResult<RankingResult> envelope = new(payload);

        string json = OutputJson.Serialize(envelope);

        using JsonDocument doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("warnings").GetArrayLength().Should().Be(0);
        doc.RootElement.GetProperty("hints").GetArrayLength().Should().Be(0);
    }

    [TestMethod]
    public void Serialize_Doubles_RoundedToTwoDecimals()
    {
        RankingResult payload = new(
            100.0,
            "",
            [new RankRow("A", 63.8567, 33.3333)]);
        AnalysisResult<RankingResult> envelope = new(payload);

        string json = OutputJson.Serialize(envelope);

        json.Should().Contain("\"milliseconds\":63.86");
        json.Should().Contain("\"percentOfScope\":33.33");
    }

    [TestMethod]
    public void Serialize_FrameNamesWithAngleBrackets_AreNotOverEscaped()
    {
        RankingResult payload = new(5.0, "", [new RankRow("<root>", 5.0, 100.0)]);
        AnalysisResult<RankingResult> envelope = new(payload);

        string json = OutputJson.Serialize(envelope);

        json.Should().Contain("<root>");
        json.Should().NotContain("\\u003C");
    }

    [TestMethod]
    public void Serialize_Envelope_MatchesGolden()
    {
        string goldenPath = Path.Combine(AppContext.BaseDirectory, "Goldens", "ranking-envelope.golden.json");
        string expected = File.ReadAllText(goldenPath).Trim();

        string json = OutputJson.Serialize(SampleEnvelope());

        json.Should().Be(expected);
    }
}
