// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Analyzers;

[TestClass]
public class XmlDocumentationFormattingCodeFixTests
{
    private static readonly Dictionary<string, ReportDiagnostic> s_enabled =
        new() { [XmlDocumentationFormattingAnalyzer.DiagnosticId] = ReportDiagnostic.Warn };

    private static async Task<string> ApplyFixAsync(
        string source,
        Dictionary<string, string>? options = null)
        => await CodeFixTestHarness.ApplyFixAsync(
            new XmlDocumentationFormattingAnalyzer(),
            new FormatXmlDocumentationCodeFixProvider(),
            source,
            XmlDocumentationFormattingAnalyzer.DiagnosticId,
            options,
            s_enabled).ConfigureAwait(false);

    [TestMethod]
    public async Task Format_SingleLineSummary_ExpandsIt()
    {
        string source =
            "class Sample\n{\n    /// <summary>The name.</summary>\n    string Name => \"name\";\n}\n";

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(
            "class Sample\n{\n    /// <summary>\n    ///  The name.\n    /// </summary>\n"
            + "    string Name => \"name\";\n}\n");
    }

    [TestMethod]
    public async Task Format_ExpandedFirstContentLineWithHangingContinuation_PreservesRelativeIndentation()
    {
        string source =
            "/// <summary>First line\n"
            + "///     hanging continuation.\n"
            + "/// </summary>\nclass Sample { }\n";

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);
        string fixedAgain = await ApplyFixAsync(fixedSource).ConfigureAwait(false);

        fixedSource.Should().Be(
            "/// <summary>\n"
            + "///  First line\n"
            + "///     hanging continuation.\n"
            + "/// </summary>\nclass Sample { }\n");
        fixedAgain.Should().Be(fixedSource);
    }

    [TestMethod]
    public async Task Format_FirstProseRunBeforeNestedBlock_DoesNotLeakIntoLaterProse()
    {
        string source =
            "/// <remarks>First line\n"
            + "///     hanging before child.<para>Nested.</para>\n"
            + "/// Trailing block.\n"
            + "/// </remarks>\nclass Sample { }\n";

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);
        string fixedAgain = await ApplyFixAsync(fixedSource).ConfigureAwait(false);

        fixedSource.Should().Be(
            "/// <remarks>\n"
            + "///  First line\n"
            + "///     hanging before child.\n"
            + "///  <para>\n"
            + "///   Nested.\n"
            + "///  </para>\n"
            + "///  Trailing block.\n"
            + "/// </remarks>\nclass Sample { }\n");
        fixedAgain.Should().Be(fixedSource);
    }

    [TestMethod]
    public async Task Format_ProseGeneratedAfterNestedBlock_PreservesHangingContinuation()
    {
        string source =
            "/// <remarks><para>Nested.</para>Trailing first\n"
            + "///      hanging continuation.\n"
            + "/// </remarks>\nclass Sample { }\n";

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);
        string fixedAgain = await ApplyFixAsync(fixedSource).ConfigureAwait(false);

        fixedSource.Should().Be(
            "/// <remarks>\n"
            + "///  <para>\n"
            + "///   Nested.\n"
            + "///  </para>\n"
            + "///  Trailing first\n"
            + "///      hanging continuation.\n"
            + "/// </remarks>\nclass Sample { }\n");
        fixedAgain.Should().Be(fixedSource);
    }

    [TestMethod]
    public async Task Format_ThreeLineTopLevelElement_CompactsIt()
    {
        string source =
            "class Sample\n{\n    /// <returns>\n    ///  The name.\n    /// </returns>\n"
            + "    string Name() => \"name\";\n}\n";

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(
            "class Sample\n{\n    /// <returns>The name.</returns>\n"
            + "    string Name() => \"name\";\n}\n");
    }

    [TestMethod]
    public async Task Format_ConfiguredIndentSize_UsesConfiguredIndentation()
    {
        string source =
            "/// <remarks><para>Text.</para></remarks>\nclass Sample { }\n";
        Dictionary<string, string> options = new()
        {
            [XmlDocumentationFormattingAnalyzer.IndentSizeOption] = "2"
        };

        string fixedSource = await ApplyFixAsync(source, options).ConfigureAwait(false);

        fixedSource.Should().Be(
            "/// <remarks>\n///   <para>\n///     Text.\n///   </para>\n/// </remarks>\n"
            + "class Sample { }\n");
    }

    [TestMethod]
    public async Task Format_CrlfSource_PreservesLineEndings()
    {
        string source =
            "/// <summary>Text.</summary>\r\nclass Sample { }\r\n";

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(
            "/// <summary>\r\n///  Text.\r\n/// </summary>\r\nclass Sample { }\r\n");
    }

    [TestMethod]
    public async Task Format_CodePayload_PreservesPayloadIndentation()
    {
        string source =
            "/// <example>\n/// <code>\n///       if (ready)\n///       {\n///           Run();\n///       }\n"
            + "/// </code>\n/// </example>\nclass Sample { }\n";

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(
            "/// <example>\n///  <code>\n///       if (ready)\n///       {\n///           Run();\n///       }\n"
            + "///  </code>\n/// </example>\nclass Sample { }\n");
    }

    [TestMethod]
    public async Task Format_AlreadyFixedSource_IsIdempotent()
    {
        string source = "/// <summary>Text.</summary>\nclass Sample { }\n";

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);
        string fixedAgain = await ApplyFixAsync(fixedSource).ConfigureAwait(false);

        fixedAgain.Should().Be(fixedSource);
    }

    [TestMethod]
    public async Task Format_OverLimitSingleLineWithEdgeWhitespace_IsIdempotent()
    {
        string source = "/// <returns> The name. </returns>\nclass Sample { }\n";
        Dictionary<string, string> options = new()
        {
            [XmlDocumentationFormattingAnalyzer.MaxLineLengthOption] = "32"
        };

        string fixedSource = await ApplyFixAsync(source, options).ConfigureAwait(false);
        string fixedAgain = await ApplyFixAsync(fixedSource, options).ConfigureAwait(false);

        fixedSource.Should().Be("/// <returns>The name.</returns>\nclass Sample { }\n");
        fixedAgain.Should().Be(fixedSource);
    }

    [TestMethod]
    public async Task Format_CompactedElementWithExtraPrefixSpaces_IsIdempotent()
    {
        string source = "///    <returns> The name. </returns>\nclass Sample { }\n";
        Dictionary<string, string> options = new()
        {
            [XmlDocumentationFormattingAnalyzer.MaxLineLengthOption] = "32"
        };

        string fixedSource = await ApplyFixAsync(source, options).ConfigureAwait(false);
        string fixedAgain = await ApplyFixAsync(fixedSource, options).ConfigureAwait(false);

        fixedSource.Should().Be("/// <returns>The name.</returns>\nclass Sample { }\n");
        fixedAgain.Should().Be(fixedSource);
    }

    [TestMethod]
    public async Task Format_ExactLimitCompactElementWithTrailingSuffix_IsIdempotent()
    {
        string source = "/// <returns> The name. </returns> \nclass Sample { }\n";
        Dictionary<string, string> options = new()
        {
            [XmlDocumentationFormattingAnalyzer.MaxLineLengthOption] = "32"
        };

        string fixedSource = await ApplyFixAsync(source, options).ConfigureAwait(false);
        string fixedAgain = await ApplyFixAsync(fixedSource, options).ConfigureAwait(false);

        fixedSource.Should().Be("/// <returns>\n///  The name.\n/// </returns> \nclass Sample { }\n");
        fixedAgain.Should().Be(fixedSource);
    }

    [TestMethod]
    public async Task Format_NeighboringCompactElementsOnOverLimitLine_IsIdempotent()
    {
        string source =
            "/// <param name=\"value\">The value.</param><returns>The result.</returns>\n"
            + "class Sample { string Method(string value) => value; }\n";
        Dictionary<string, string> options = new()
        {
            [XmlDocumentationFormattingAnalyzer.MaxLineLengthOption] = "50"
        };

        string fixedSource = await ApplyFixAsync(source, options).ConfigureAwait(false);
        string fixedAgain = await ApplyFixAsync(fixedSource, options).ConfigureAwait(false);

        fixedSource.Should().Be(
            "/// <param name=\"value\">The value.</param>\n"
            + "/// <returns>The result.</returns>\n"
            + "class Sample { string Method(string value) => value; }\n");
        fixedAgain.Should().Be(fixedSource);
    }

    [TestMethod]
    public async Task Format_LastCrowdedElementWithTrailingSuffix_IsIdempotent()
    {
        string source =
            "/// <param name=\"v\">x</param><returns>x</returns>          \n"
            + "class Sample { string Method(string v) => v; }\n";
        Dictionary<string, string> options = new()
        {
            [XmlDocumentationFormattingAnalyzer.MaxLineLengthOption] = "30"
        };

        string fixedSource = await ApplyFixAsync(source, options).ConfigureAwait(false);
        string fixedAgain = await ApplyFixAsync(fixedSource, options).ConfigureAwait(false);

        fixedSource.Should().Be(
            "/// <param name=\"v\">x</param>\n"
            + "/// <returns>\n///  x\n/// </returns>          \n"
            + "class Sample { string Method(string v) => v; }\n");
        fixedAgain.Should().Be(fixedSource);
    }

    [TestMethod]
    public async Task Format_LaterCrowdedElementRequiringNormalization_IsIdempotent()
    {
        string source =
            "/// <param name=\"v\">x</param><returns> x </returns>\n"
            + "class Sample { string Method(string v) => v; }\n";
        Dictionary<string, string> options = new()
        {
            [XmlDocumentationFormattingAnalyzer.MaxLineLengthOption] = "35"
        };

        string fixedSource = await ApplyFixAsync(source, options).ConfigureAwait(false);
        string fixedAgain = await ApplyFixAsync(fixedSource, options).ConfigureAwait(false);

        fixedSource.Should().Be(
            "/// <param name=\"v\">x</param>\n"
            + "/// <returns>x</returns>\n"
            + "class Sample { string Method(string v) => v; }\n");
        fixedAgain.Should().Be(fixedSource);
    }

    [TestMethod]
    public async Task Format_PairedElementCrowdedBySelfClosingSibling_IsIdempotent()
    {
        string source = "/// <returns>x</returns><inheritdoc/>\nclass Sample { }\n";
        Dictionary<string, string> options = new()
        {
            [XmlDocumentationFormattingAnalyzer.MaxLineLengthOption] = "30"
        };

        string fixedSource = await ApplyFixAsync(source, options).ConfigureAwait(false);
        string fixedAgain = await ApplyFixAsync(fixedSource, options).ConfigureAwait(false);

        fixedSource.Should().Be(
            "/// <returns>x</returns>\n/// <inheritdoc/>\nclass Sample { }\n");
        fixedAgain.Should().Be(fixedSource);
    }

    [TestMethod]
    public async Task FormatAll_MultipleDocuments_FixesEveryComment()
    {
        (string Name, string FilePath, string Source)[] sources =
        [
            ("One.cs", "One.cs", "/// <summary>One.</summary>\nclass One { }\n"),
            ("Two.cs", "Two.cs", "/// <summary>Two.</summary>\nclass Two { }\n")
        ];

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new XmlDocumentationFormattingAnalyzer(),
            new FormatXmlDocumentationCodeFixProvider(),
            sources,
            XmlDocumentationFormattingAnalyzer.DiagnosticId,
            fixAll: true,
            diagnosticOptions: s_enabled).ConfigureAwait(false);

        result.FixAllActionOffered.Should().BeTrue();
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().BeEmpty();
        result.Documents.Should().OnlyContain(
            document => document.Source.Contains("///  ", StringComparison.Ordinal)
                && !document.Source.Contains("<summary>One.</summary>", StringComparison.Ordinal)
                && !document.Source.Contains("<summary>Two.</summary>", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task FormatAll_MultipleCommentsInOneDocument_FixesEveryComment()
    {
        (string Name, string FilePath, string Source)[] sources =
        [
            (
                "Both.cs",
                "Both.cs",
                "/// <summary>One.</summary>\nclass One { }\n\n"
                + "/// <summary>Two.</summary>\nclass Two { }\n")
        ];

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new XmlDocumentationFormattingAnalyzer(),
            new FormatXmlDocumentationCodeFixProvider(),
            sources,
            XmlDocumentationFormattingAnalyzer.DiagnosticId,
            fixAll: true,
            diagnosticOptions: s_enabled).ConfigureAwait(false);

        result.FixAllActionOffered.Should().BeTrue();
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().BeEmpty();
        result.Documents.Should().ContainSingle()
            .Which.Source.Should().Be(
                "/// <summary>\n///  One.\n/// </summary>\nclass One { }\n\n"
                + "/// <summary>\n///  Two.\n/// </summary>\nclass Two { }\n");
    }

}
