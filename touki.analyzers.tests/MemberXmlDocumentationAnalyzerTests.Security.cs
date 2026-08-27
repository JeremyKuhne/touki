// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Analyzers;

public partial class MemberXmlDocumentationAnalyzerTests
{
    private const int MaximumMetadataDocumentationLength = 1024 * 1024;
    private const int MaximumMetadataDocumentationNodes = 4096;
    private const int MaximumMetadataDocumentationDepth = 128;

    [TestMethod]
    public void TryHasMetadataDocumentation_ExactlyAtLengthLimit_ParsesDocumentation()
    {
        const string prefix = "<member name=\"M:Base.Run\"><summary>";
        const string suffix = "</summary></member>";
        string xml = string.Concat(
            prefix,
            new string('x', MaximumMetadataDocumentationLength - prefix.Length - suffix.Length),
            suffix);

        bool parsed = MemberXmlDocumentationAnalyzer.TryHasMetadataDocumentation(
            xml,
            CancellationToken.None,
            out bool hasDocumentation);

        parsed.Should().BeTrue();
        hasDocumentation.Should().BeTrue();
    }

    [TestMethod]
    public void TryHasMetadataDocumentation_OverLengthLimit_ReturnsUnknown()
    {
        string xml = new('x', MaximumMetadataDocumentationLength + 1);

        bool parsed = MemberXmlDocumentationAnalyzer.TryHasMetadataDocumentation(
            xml,
            CancellationToken.None,
            out bool hasDocumentation);

        parsed.Should().BeFalse();
        hasDocumentation.Should().BeFalse();
    }

    [TestMethod]
    public void TryHasMetadataDocumentation_ExactlyAtDepthLimit_ParsesAsUndocumented()
    {
        string xml = CreateNestedMetadataXml(MaximumMetadataDocumentationDepth - 2);

        bool parsed = MemberXmlDocumentationAnalyzer.TryHasMetadataDocumentation(
            xml,
            CancellationToken.None,
            out bool hasDocumentation);

        parsed.Should().BeTrue();
        hasDocumentation.Should().BeFalse();
    }

    [TestMethod]
    public void TryHasMetadataDocumentation_OverDepthLimit_ReturnsUnknown()
    {
        string xml = CreateNestedMetadataXml(MaximumMetadataDocumentationDepth - 1);

        bool parsed = MemberXmlDocumentationAnalyzer.TryHasMetadataDocumentation(
            xml,
            CancellationToken.None,
            out bool hasDocumentation);

        parsed.Should().BeFalse();
        hasDocumentation.Should().BeFalse();
    }

    [TestMethod]
    public void TryHasMetadataDocumentation_ExactlyAtNodeLimit_ParsesAsUndocumented()
    {
        string xml = CreateNodeCountMetadataXml(MaximumMetadataDocumentationNodes - 2);

        bool parsed = MemberXmlDocumentationAnalyzer.TryHasMetadataDocumentation(
            xml,
            CancellationToken.None,
            out bool hasDocumentation);

        parsed.Should().BeTrue();
        hasDocumentation.Should().BeFalse();
    }

    [TestMethod]
    public void TryHasMetadataDocumentation_OverNodeLimit_ReturnsUnknown()
    {
        string xml = CreateNodeCountMetadataXml(MaximumMetadataDocumentationNodes - 1);

        bool parsed = MemberXmlDocumentationAnalyzer.TryHasMetadataDocumentation(
            xml,
            CancellationToken.None,
            out bool hasDocumentation);

        parsed.Should().BeFalse();
        hasDocumentation.Should().BeFalse();
    }

    [TestMethod]
    public void TryHasMetadataDocumentation_DocumentTypeDefinition_ReturnsUnknown()
    {
        const string xml =
            "<!DOCTYPE member [<!ENTITY content \"text\">]><member><summary>&content;</summary></member>";

        bool parsed = MemberXmlDocumentationAnalyzer.TryHasMetadataDocumentation(
            xml,
            CancellationToken.None,
            out bool hasDocumentation);

        parsed.Should().BeFalse();
        hasDocumentation.Should().BeFalse();
    }

    [TestMethod]
    public void TryHasMetadataDocumentation_ValidSummaryThenMalformedSuffix_ReturnsUnknown()
    {
        const string xml = "<member><summary>Documentation.</summary><broken>";

        bool parsed = MemberXmlDocumentationAnalyzer.TryHasMetadataDocumentation(
            xml,
            CancellationToken.None,
            out bool hasDocumentation);

        parsed.Should().BeFalse();
        hasDocumentation.Should().BeFalse();
    }

    [TestMethod]
    public void TryHasMetadataDocumentation_ValidSummaryThenOverNodeLimit_ReturnsUnknown()
    {
        string xml = string.Concat(
            "<member><summary>Documentation.</summary>",
            string.Concat(Enumerable.Repeat("<node/>", MaximumMetadataDocumentationNodes)),
            "</member>");

        bool parsed = MemberXmlDocumentationAnalyzer.TryHasMetadataDocumentation(
            xml,
            CancellationToken.None,
            out bool hasDocumentation);

        parsed.Should().BeFalse();
        hasDocumentation.Should().BeFalse();
    }

    [TestMethod]
    public void TryHasMetadataDocumentation_Canceled_ThrowsOperationCanceledException()
    {
        CancellationToken cancellationToken = new(canceled: true);

        Action action = () => MemberXmlDocumentationAnalyzer.TryHasMetadataDocumentation(
            "<member><summary>Documentation.</summary></member>",
            cancellationToken,
            out _);

        action.Should().Throw<OperationCanceledException>();
    }

    private static string CreateNestedMetadataXml(int depth) => string.Concat(
        "<member>",
        string.Concat(Enumerable.Repeat("<node>", depth)),
        "<summary>Documentation.</summary>",
        string.Concat(Enumerable.Repeat("</node>", depth)),
        "</member>");

    private static string CreateNodeCountMetadataXml(int nodeCount) => string.Concat(
        "<member>",
        string.Concat(Enumerable.Repeat("<node/>", nodeCount)),
        "</member>");
}