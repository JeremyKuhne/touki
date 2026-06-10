// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Mcp;

[TestClass]
public sealed class TraceServerInstructionsTests
{
    [TestMethod]
    public void Text_NamesTheToolsInWorkflowOrder()
    {
        string text = TraceServerInstructions.Text;

        text.Should().NotBeNullOrWhiteSpace();
        text.IndexOf("trace_info", StringComparison.Ordinal).Should().BeGreaterThanOrEqualTo(0);

        // The workflow nudges the model to load before ranking, so trace_info is named
        // before trace_rank.
        text.IndexOf("trace_info", StringComparison.Ordinal)
            .Should().BeLessThan(text.IndexOf("trace_rank", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Text_CallsOutTheSymbolResolutionThreshold()
    {
        TraceServerInstructions.Text.Should().Contain("0.8");
    }
}
