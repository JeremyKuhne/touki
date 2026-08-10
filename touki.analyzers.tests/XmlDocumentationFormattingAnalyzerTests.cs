// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Globalization;

namespace Touki.Analyzers;

[TestClass]
public partial class XmlDocumentationFormattingAnalyzerTests
{
    private const string ReplacementProperty = "Replacement";

    private static readonly Dictionary<string, ReportDiagnostic> s_enabled =
        new() { [XmlDocumentationFormattingAnalyzer.DiagnosticId] = ReportDiagnostic.Warn };

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        string source,
        Dictionary<string, string>? options = null)
        => await AnalyzerTestHarness
            .GetDiagnosticsAsync(
                new XmlDocumentationFormattingAnalyzer(),
                source,
                options,
                fileName: null,
                s_enabled)
            .ConfigureAwait(false);

    private static string Replacement(Diagnostic diagnostic) =>
        diagnostic.Properties[ReplacementProperty]!;

    [TestMethod]
    public async Task Analyze_SingleLineSummary_ReportsExpandedReplacement()
    {
        string source =
            "class Sample\n{\n    /// <summary>The name.</summary>\n    string Name => \"name\";\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(XmlDocumentationFormattingAnalyzer.DiagnosticId);
        Replacement(diagnostics[0]).Should().Be(
            "    /// <summary>\n    ///  The name.\n    /// </summary>");
    }

    [TestMethod]
    public async Task Analyze_SingleLineTopLevelElementWithinLimit_ReportsNothing()
    {
        string source =
            "class Sample\n{\n    /// <returns>The name.</returns>\n    string Name() => \"name\";\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_EmptySingleLineTopLevelElementWithinLimit_ReportsNothing()
    {
        string source =
            "class Sample\n{\n    /// <param name=\"value\"></param>\n    void Method(string value) { }\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_SingleLineTopLevelElementOverLimit_ReportsExpandedReplacement()
    {
        string source =
            "class Sample\n{\n    /// <returns>The requested display name.</returns>\n    string Name() => \"name\";\n}\n";
        Dictionary<string, string> options = new()
        {
            [XmlDocumentationFormattingAnalyzer.MaxLineLengthOption] = "40"
        };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, options).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        Replacement(diagnostics[0]).Should().Be(
            "    /// <returns>\n    ///  The requested display name.\n    /// </returns>");
    }

    [TestMethod]
    public async Task Analyze_SingleLineElementWithEdgeWhitespace_MeasuresActualLine()
    {
        string source =
            "/// <returns> The name. </returns>\nclass Sample { }\n";
        Dictionary<string, string> options = new()
        {
            [XmlDocumentationFormattingAnalyzer.MaxLineLengthOption] = "32"
        };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, options).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
    }

    [TestMethod]
    public async Task Analyze_ThreeLineTopLevelElementWithOneContentLine_ReportsCompactReplacement()
    {
        string source =
            "class Sample\n{\n    /// <returns>\n    ///  The name.\n    /// </returns>\n"
            + "    string Name() => \"name\";\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        Replacement(diagnostics[0]).Should().Be("    /// <returns>The name.</returns>");
    }

    [TestMethod]
    public async Task Analyze_Compaction_PreservesNonBreakingSpaces()
    {
        string source =
            "/// <returns>\n///  \u00a0The name.\u00a0\n/// </returns>\nclass Sample { }\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        Replacement(diagnostics[0]).Should().Be(
            "/// <returns>\u00a0The name.\u00a0</returns>");
    }

    [TestMethod]
    public async Task Analyze_XmlSpacePreserveContent_RemainsOpaque()
    {
        string source =
            "/// <returns xml:space=\"preserve\">\n///    The name.  \n/// </returns>\n"
            + "class Sample { }\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_InheritedXmlSpacePreserveContent_RemainsOpaque()
    {
        string source =
            "/// <remarks xml:space=\"preserve\">\n///  <para>  Text.  </para>\n/// </remarks>\n"
            + "class Sample { }\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_XmlSpaceDefault_ResetsInheritedPreservation()
    {
        string source =
            "/// <remarks xml:space=\"preserve\">\n"
            + "///  <para xml:space=\"default\">Text.</para>\n"
            + "/// </remarks>\nclass Sample { }\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        Replacement(diagnostics[0]).Should().Be(
            "/// <remarks xml:space=\"preserve\">\n"
            + "///  <para xml:space=\"default\">\n"
            + "///   Text.\n"
            + "///  </para>\n"
            + "/// </remarks>");
    }

    [TestMethod]
    public async Task Analyze_SameLineXmlSpaceDefault_PreservesParentOwnedWhitespace()
    {
        string source =
            "/// <remarks xml:space=\"preserve\">before <para xml:space=\"default\">Text.</para> after</remarks>\n"
            + "class Sample { }\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        Replacement(diagnostics[0]).Should().Be(
            "/// <remarks xml:space=\"preserve\">before <para xml:space=\"default\">\n"
            + "///   Text.\n"
            + "///  </para> after</remarks>");
    }

    [TestMethod]
    public async Task Analyze_SameLineDefaultCode_PreservesParentOwnedWhitespace()
    {
        string source =
            "/// <remarks xml:space=\"preserve\">before <code xml:space=\"default\">x</code> after</remarks>\n"
            + "class Sample { }\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_DefaultChildOpeningIndentation_IsOwnedByPreservedParent()
    {
        string source =
            "/// <remarks xml:space=\"preserve\">\n"
            + "///       <para xml:space=\"default\">\n"
            + "///   Text.\n"
            + "///  </para>\n"
            + "/// </remarks>\nclass Sample { }\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_EntityNormalizedXmlSpacePreserve_RemainsOpaque()
    {
        string source =
            "/// <remarks xml:space=\"pre&#x73;erve\">\n///       Text.  \n/// </remarks>\n"
            + "class Sample { }\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_NonBreakingSpaceOnlyContent_PreservesContentAndConverges()
    {
        string source = "/// <summary>\u00a0</summary>\nclass Sample { }\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        string replacement = Replacement(diagnostics[0]);
        replacement.Should().Be("/// <summary>\n///  \u00a0\n/// </summary>");

        ImmutableArray<Diagnostic> replacementDiagnostics = await AnalyzeAsync(
            $"{replacement}\nclass Sample {{ }}\n").ConfigureAwait(false);
        replacementDiagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_TopLevelElementWithTwoContentLines_ReportsNothing()
    {
        string source =
            "class Sample\n{\n    /// <returns>\n    ///  The requested name,\n"
            + "    ///  or null when unavailable.\n    /// </returns>\n"
            + "    string? Name() => null;\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_CorrectlyIndentedBlockWithHangingIndent_PreservesRelativeIndentation()
    {
        string source =
            "/// <remarks>\n///  <para>\n"
            + "///   - <c>Class</c> / <c>NegClass</c>: matches one character\n"
            + "///     against the class body.\n"
            + "///  </para>\n/// </remarks>\nclass Sample { }\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_ExpandedFirstContentLineWithHangingContinuation_PreservesRelativeIndentation()
    {
        string source =
            "/// <summary>First line\n"
            + "///     hanging continuation.\n"
            + "/// </summary>\nclass Sample { }\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        Replacement(diagnostics[0]).Should().Be(
            "/// <summary>\n"
            + "///  First line\n"
            + "///     hanging continuation.\n"
            + "/// </summary>");
    }

    [TestMethod]
    public async Task Analyze_ExpandedNestedFirstContentLineWithHangingContinuation_PreservesRelativeIndentation()
    {
        string source =
            "/// <remarks><para>First line\n"
            + "///      hanging continuation.\n"
            + "/// </para></remarks>\nclass Sample { }\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        Replacement(diagnostics[0]).Should().Be(
            "/// <remarks>\n"
            + "///  <para>\n"
            + "///   First line\n"
            + "///      hanging continuation.\n"
            + "///  </para>\n"
            + "/// </remarks>");
    }

    [TestMethod]
    public async Task Analyze_ProseAfterSameLineNestedBlock_ComputesNewBlockIndentation()
    {
        string source =
            "/// <remarks>Intro.<para>Nested.</para>\n"
            + "/// Trailing block.\n"
            + "/// </remarks>\nclass Sample { }\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        Replacement(diagnostics[0]).Should().Be(
            "/// <remarks>\n"
            + "///  Intro.\n"
            + "///  <para>\n"
            + "///   Nested.\n"
            + "///  </para>\n"
            + "///  Trailing block.\n"
            + "/// </remarks>");
    }

    [TestMethod]
    public async Task Analyze_FirstProseRunBeforeNestedBlock_DoesNotLeakIntoLaterProse()
    {
        string source =
            "/// <remarks>First line\n"
            + "///     hanging before child.<para>Nested.</para>\n"
            + "/// Trailing block.\n"
            + "/// </remarks>\nclass Sample { }\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        Replacement(diagnostics[0]).Should().Be(
            "/// <remarks>\n"
            + "///  First line\n"
            + "///     hanging before child.\n"
            + "///  <para>\n"
            + "///   Nested.\n"
            + "///  </para>\n"
            + "///  Trailing block.\n"
            + "/// </remarks>");
    }

    [TestMethod]
    public async Task Analyze_ProseGeneratedAfterNestedBlock_PreservesHangingContinuation()
    {
        string source =
            "/// <remarks><para>Nested.</para>Trailing first\n"
            + "///      hanging continuation.\n"
            + "/// </remarks>\nclass Sample { }\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        Replacement(diagnostics[0]).Should().Be(
            "/// <remarks>\n"
            + "///  <para>\n"
            + "///   Nested.\n"
            + "///  </para>\n"
            + "///  Trailing first\n"
            + "///      hanging continuation.\n"
            + "/// </remarks>");
    }

    [TestMethod]
    public async Task Analyze_UnderIndentedBlock_ShiftsEveryLineBySameAmount()
    {
        string source =
            "/// <summary>\n"
            + "/// Text starts one space too far left.\n"
            + "///   Its hanging continuation is two spaces deeper.\n"
            + "/// </summary>\nclass Sample { }\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        Replacement(diagnostics[0]).Should().Be(
            "/// <summary>\n"
            + "///  Text starts one space too far left.\n"
            + "///    Its hanging continuation is two spaces deeper.\n"
            + "/// </summary>");
    }

    [TestMethod]
    public async Task Analyze_OverLimitMultilineElementWithUnderIndentedContent_CorrectsIndentation()
    {
        string source =
            "/// <summary>Summary.</summary>\n/// <param name=\"value\">A deliberately long parameter description.</param>\n"
            + "/// <returns>\n/// The deliberately long return description.\n/// </returns>\n"
            + "class Sample { string Method(string value) => value; }\n";
        Dictionary<string, string> options = new()
        {
            [XmlDocumentationFormattingAnalyzer.MaxLineLengthOption] = "40"
        };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, options).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        Replacement(diagnostics[0]).Should().Contain("///  The deliberately long return description.");
    }

    [TestMethod]
    public async Task Analyze_OverLimitReturnsAfterLongParameter_CorrectsInlineXmlContentIndentation()
    {
        string source =
            "    /// <summary>\n    ///  Attempts to retrieve the value as the specified type.\n    /// </summary>\n"
            + "    /// <typeparam name=\"T\">The type to retrieve the value as.</typeparam>\n"
            + "    /// <param name=\"value\">When this method returns, contains the value if the conversion succeeded.</param>\n"
            + "    /// <returns>\n"
            + "    /// <see langword=\"true\"/> if the value was successfully retrieved; otherwise, <see langword=\"false\"/>.\n"
            + "    /// </returns>\n"
            + "    public bool TryGetValue<T>(out T value) { value = default!; return true; }\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        Replacement(diagnostics[0]).Should().Contain(
            "    ///  <see langword=\"true\"/> if the value was successfully retrieved;");
    }

    [TestMethod]
    public async Task Analyze_NestedElements_UsesConfiguredIndentSize()
    {
        string source =
            "/// <remarks>\n///  <para>\n///   Text.\n///  </para>\n/// </remarks>\nclass Sample { }\n";
        Dictionary<string, string> options = new()
        {
            [XmlDocumentationFormattingAnalyzer.IndentSizeOption] = "2"
        };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, options).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        Replacement(diagnostics[0]).Should().Be(
            "/// <remarks>\n///   <para>\n///     Text.\n///   </para>\n/// </remarks>");
    }

    [TestMethod]
    public async Task Analyze_CompactNestedBlockElements_ExpandsEveryLevel()
    {
        string source = "/// <remarks><para>Text.</para></remarks>\nclass Sample { }\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        Replacement(diagnostics[0]).Should().Be(
            "/// <remarks>\n///  <para>\n///   Text.\n///  </para>\n/// </remarks>");
    }

    [TestMethod]
    public async Task Analyze_StandardMaxLineLength_ControlsExpansion()
    {
        string source =
            "/// <returns>The requested display name.</returns>\nclass Sample { }\n";
        Dictionary<string, string> options = new() { ["max_line_length"] = "45" };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, options).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        Replacement(diagnostics[0]).Should().Contain("\n");
    }

    [TestMethod]
    public async Task Analyze_RuleMaxLineLength_OverridesStandardValue()
    {
        string source =
            "/// <returns>The requested display name.</returns>\nclass Sample { }\n";
        Dictionary<string, string> options = new()
        {
            [XmlDocumentationFormattingAnalyzer.MaxLineLengthOption] = "120",
            ["max_line_length"] = "20"
        };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, options).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_InvalidRuleMaxLineLength_FallsThroughToStandardValue()
    {
        string source =
            "/// <returns>The requested display name.</returns>\nclass Sample { }\n";
        Dictionary<string, string> options = new()
        {
            [XmlDocumentationFormattingAnalyzer.MaxLineLengthOption] = "invalid",
            ["max_line_length"] = "45"
        };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, options).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
    }

    [TestMethod]
    public async Task Analyze_MaximumIntegerLineLength_DoesNotOverflow()
    {
        string source =
            "/// <returns>The requested display name.</returns>\nclass Sample { }\n";
        Dictionary<string, string> options = new()
        {
            [XmlDocumentationFormattingAnalyzer.MaxLineLengthOption] = int.MaxValue.ToString(CultureInfo.InvariantCulture)
        };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, options).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_OversizedIndentSize_FallsBackToDefault()
    {
        string source =
            "/// <remarks>\n///  <para>\n///   Text.\n///  </para>\n/// </remarks>\nclass Sample { }\n";
        Dictionary<string, string> options = new()
        {
            [XmlDocumentationFormattingAnalyzer.IndentSizeOption] = int.MaxValue.ToString(CultureInfo.InvariantCulture)
        };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, options).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_InlineAndSelfClosingElements_PreservesTheirText()
    {
        string source =
            "/// <summary>Gets <see cref=\"string\"/> for <paramref name=\"value\"/>.</summary>\n"
            + "class Sample { }\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        Replacement(diagnostics[0]).Should().Be(
            "/// <summary>\n///  Gets <see cref=\"string\"/> for <paramref name=\"value\"/>.\n"
            + "/// </summary>");
    }

    [TestMethod]
    public async Task Analyze_EmphasisElement_RemainsInline()
    {
        string source =
            "/// <summary>Uses the <em>requested name</em>.</summary>\nclass Sample { }\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        Replacement(diagnostics[0]).Should().Be(
            "/// <summary>\n///  Uses the <em>requested name</em>.\n/// </summary>");
    }

    [TestMethod]
    public async Task Analyze_WrappedInlineElement_DoesNotAddNestingIndent()
    {
        string source =
            "/// <summary>\n///  Uses <b>bold text that wraps\n///  across source lines</b>.\n"
            + "/// </summary>\nclass Sample { }\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_CodePayload_PreservesRelativeIndentation()
    {
        string source =
            "/// <example>\n/// <code>\n///       if (ready)\n///       {\n///           Run();\n///       }\n"
            + "/// </code>\n/// </example>\nclass Sample { }\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        Replacement(diagnostics[0]).Should().Be(
            "/// <example>\n///  <code>\n///       if (ready)\n///       {\n///           Run();\n///       }\n"
            + "///  </code>\n/// </example>");
    }

    [TestMethod]
    public async Task Analyze_XmlLookingCodePayload_RemainsOpaque()
    {
        string source =
            "/// <example><code><summary>literal text</summary></code></example>\nclass Sample { }\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        Replacement(diagnostics[0]).Should().Be(
            "/// <example>\n///  <code><summary>literal text</summary></code>\n/// </example>");
    }

    [TestMethod]
    public async Task Analyze_CDataPayload_RemainsOpaque()
    {
        string source =
            "/// <summary><![CDATA[  <tag> value </tag>  ]]></summary>\nclass Sample { }\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        Replacement(diagnostics[0]).Should().Be(
            "/// <summary>\n///  <![CDATA[  <tag> value </tag>  ]]>\n/// </summary>");
    }

    [TestMethod]
    public async Task Analyze_MultilineCData_AlignsDelimitersAndPreservesPayloadIndentation()
    {
        string source =
            "/// <summary>\n/// <![CDATA[\n///       <tag> value </tag>\n/// ]]>\n/// </summary>\n"
            + "class Sample { }\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        Replacement(diagnostics[0]).Should().Be(
            "/// <summary>\n///  <![CDATA[\n///       <tag> value </tag>\n///  ]]>\n/// </summary>");
    }

    [TestMethod]
    public async Task Analyze_CDataInsideCode_RemainsOpaque()
    {
        string source =
            "/// <example><code><![CDATA[  <tag> value </tag>  ]]></code></example>\nclass Sample { }\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        Replacement(diagnostics[0]).Should().Be(
            "/// <example>\n///  <code><![CDATA[  <tag> value </tag>  ]]></code>\n/// </example>");
    }

    [TestMethod]
    public async Task Analyze_MultipleCDataSectionsInsideCode_KeepInterveningPayloadOpaque()
    {
        string source =
            "/// <example>\n///  <code>\n///       <![CDATA[first]]>\n///       middle <tag>text</tag>\n"
            + "///       <![CDATA[second]]>\n///  </code>\n/// </example>\nclass Sample { }\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_MultilineCDataInsideCode_AlignsDelimitersAndPreservesBody()
    {
        string source =
            "/// <example>\n///  <code>\n/// <![CDATA[\n///       payload\n/// ]]>\n///  </code>\n"
            + "/// </example>\nclass Sample { }\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        Replacement(diagnostics[0]).Should().Be(
            "/// <example>\n///  <code>\n///   <![CDATA[\n///       payload\n///   ]]>\n///  </code>\n"
            + "/// </example>");
    }

    [TestMethod]
    public async Task Analyze_CDataClosingDelimiterAfterPayload_PreservesPayloadIndentation()
    {
        string source =
            "/// <summary>\n///  <![CDATA[\n///       payload]]>\n/// </summary>\nclass Sample { }\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_MultilineStartTag_PreservesAttributeIndentationAndFormatsContent()
    {
        string source =
            "class Sample\n{\n    /// <exception\n    /// cref=\"ArgumentException\">Invalid.</exception>\n"
            + "    void Method() { }\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        Replacement(diagnostics[0]).Should().Be(
            "    /// <exception\n    /// cref=\"ArgumentException\">\n"
            + "    ///  Invalid.\n    /// </exception>");
    }

    [TestMethod]
    public async Task Analyze_MultilineAttributeValue_PreservesLeadingWhitespaceData()
    {
        string source =
            "/// <summary>\n///  <a href=\"first\n///       second\">Text.</a>\n/// </summary>\n"
            + "class Sample { }\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_WhitespaceOnlyAttributeValueContinuation_PreservesWhitespaceData()
    {
        string source =
            "/// <summary>\n///  <a href=\"first\n///       \n///       second\">Text.</a>\n"
            + "/// </summary>\nclass Sample { }\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_MultilineSelfClosingElementWithCorrectFirstLine_PreservesBlock()
    {
        string source =
            "class Sample\n{\n    /// <inheritdoc\n    /// cref=\"Sample.Method()\"/>\n"
            + "    void Method() { }\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_UnderIndentedMultilineSelfClosingElement_ShiftsWholeBlock()
    {
        string source =
            "class Sample\n{\n    ///<inheritdoc\n    ///  cref=\"Sample.Method()\"/>\n"
            + "    void Method() { }\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        Replacement(diagnostics[0]).Should().Be(
            "    /// <inheritdoc\n    ///   cref=\"Sample.Method()\"/>");
    }

    [TestMethod]
    public async Task Analyze_MalformedXml_ReportsNothing()
    {
        string source = "/// <summary>Missing close tag.\nclass Sample { }\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_DelimitedDocumentationComment_ReportsNothing()
    {
        string source = "/** <summary>Text.</summary> */\nclass Sample { }\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_GeneratedCode_ReportsNothing()
    {
        string source = "// <auto-generated/>\n/// <summary>Text.</summary>\nclass Sample { }\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }
}
