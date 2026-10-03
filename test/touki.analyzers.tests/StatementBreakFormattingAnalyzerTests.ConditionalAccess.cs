// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Analyzers;

public partial class StatementBreakFormattingAnalyzerTests
{
    [TestMethod]
    public async Task Analyze_NestedConditionalAccessInFluentLambda_CompletesWithoutAnalyzerFailure()
    {
        const string sourceTemplate = """
            using System.Linq;
            using System.Xml.Linq;

            class Sample
            {
                static XElement[] Read(XElement root)
                {
                    XElement[] nodes = root?
                        .Element("packages")?
                        .Elements("package")
                        .SelectMany(package => package
                            .Element("classes")?
                            .Elements("class") ?? [])
                        .ToArray() ?? [];
                    return nodes;
                }
            }
            """;

        string source = sourceTemplate.ReplaceLineEndings("\n");
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source)
            .ConfigureAwait(continueOnCapturedContext: false);

        diagnostics.Should().OnlyContain(diagnostic =>
            diagnostic.Id == StatementBreakFormattingAnalyzer.DiagnosticId);

        diagnostics.Should().HaveCount(5);
        diagnostics.Select(diagnostic =>
            diagnostic.Location.GetRequiredSourceTree().GetText().ToString(diagnostic.Location.SourceSpan))
            .Should().BeEquivalentTo(
                ["?\n            .", "?\n            .", ".", ".", "?\n                ."]);
    }

    [TestMethod]
    public async Task Analyze_NestedConditionalElementAccessInFluentLambda_CompletesWithoutAnalyzerFailure()
    {
        const string sourceTemplate = """
            using System.Linq;

            class Sample
            {
                public Sample this[int index] => this;

                public Sample[] Children => [];

                static Sample[] Read(Sample root)
                {
                    Sample[] nodes = root?
                        [0]?
                        [1]
                        .Children
                        .SelectMany(child => child?
                            [0]
                            .Children ?? [])
                        .ToArray() ?? [];
                    return nodes;
                }
            }
            """;

        string source = sourceTemplate.ReplaceLineEndings("\n");
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source)
            .ConfigureAwait(continueOnCapturedContext: false);

        diagnostics.Should().OnlyContain(diagnostic =>
            diagnostic.Id == StatementBreakFormattingAnalyzer.DiagnosticId);

        diagnostics.Should().Contain(diagnostic =>
            diagnostic.Location.GetRequiredSourceTree().GetText().ToString(diagnostic.Location.SourceSpan)
                == "?\n            [");
    }
}
