// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Analyzers;

public partial class MemberXmlDocumentationAnalyzerTests
{
    private const int MaximumMetadataDocumentationLength = 1024 * 1024;
    private const int MaximumMetadataDocumentationNodes = 4096;
    private const int MaximumMetadataDocumentationDepth = 128;
    private const int MaximumDocumentationIdLength = 4096;
    private const int MaximumDocumentationIdDepth = 128;
    private const int MaximumDocumentationIdContexts = 4;
    private const int MaximumDocumentationIdDelimiters = 256;

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
    public void TryHasMetadataDocumentation_NestedMemberSummary_ParsesAsUndocumented()
    {
        const string xml = "<member><member><summary>Nested.</summary></member></member>";

        bool parsed = MemberXmlDocumentationAnalyzer.TryHasMetadataDocumentation(
            xml,
            CancellationToken.None,
            out bool hasDocumentation);

        parsed.Should().BeTrue();
        hasDocumentation.Should().BeFalse();
    }

    [TestMethod]
    public void TryHasMetadataDocumentation_MemberNestedInRemarks_ParsesAsUndocumented()
    {
        const string xml = "<remarks><member><summary>Nested.</summary></member></remarks>";

        bool parsed = MemberXmlDocumentationAnalyzer.TryHasMetadataDocumentation(
            xml,
            CancellationToken.None,
            out bool hasDocumentation);

        parsed.Should().BeTrue();
        hasDocumentation.Should().BeFalse();
    }

    [TestMethod]
    public void TryHasMetadataDocumentation_DefaultNamespaceSummary_ParsesDocumentation()
    {
        const string xml = "<member xmlns=\"urn:test\"><summary>Documentation.</summary></member>";

        bool parsed = MemberXmlDocumentationAnalyzer.TryHasMetadataDocumentation(
            xml,
            CancellationToken.None,
            out bool hasDocumentation);

        parsed.Should().BeTrue();
        hasDocumentation.Should().BeTrue();
    }

    [TestMethod]
    public void TryHasMetadataDocumentation_PrefixedSummary_ParsesAsUndocumented()
    {
        const string xml =
            "<member xmlns:doc=\"urn:test\"><doc:summary>Documentation.</doc:summary></member>";

        bool parsed = MemberXmlDocumentationAnalyzer.TryHasMetadataDocumentation(
            xml,
            CancellationToken.None,
            out bool hasDocumentation);

        parsed.Should().BeTrue();
        hasDocumentation.Should().BeFalse();
    }

    [TestMethod]
    public void IsSafeDocumentationId_ExactlyAtLengthLimit_ReturnsTrue()
    {
        string documentationId = new('x', MaximumDocumentationIdLength);

        bool result = DocumentationInheritanceResolver.IsSafeDocumentationId(documentationId);

        result.Should().BeTrue();
    }

    [TestMethod]
    public void IsSafeDocumentationId_OverLengthLimit_ReturnsFalse()
    {
        string documentationId = new('x', MaximumDocumentationIdLength + 1);

        bool result = DocumentationInheritanceResolver.IsSafeDocumentationId(documentationId);

        result.Should().BeFalse();
    }

    [TestMethod]
    public void IsSafeDocumentationId_ExactlyAtDepthLimit_ReturnsTrue()
    {
        string documentationId = string.Concat(
            new string('(', MaximumDocumentationIdDepth),
            new string(')', MaximumDocumentationIdDepth));

        bool result = DocumentationInheritanceResolver.IsSafeDocumentationId(documentationId);

        result.Should().BeTrue();
    }

    [TestMethod]
    public void IsSafeDocumentationId_OverDepthLimit_ReturnsFalse()
    {
        string documentationId = string.Concat(
            new string('(', MaximumDocumentationIdDepth + 1),
            new string(')', MaximumDocumentationIdDepth + 1));

        bool result = DocumentationInheritanceResolver.IsSafeDocumentationId(documentationId);

        result.Should().BeFalse();
    }

    [TestMethod]
    public void IsSafeDocumentationId_ExactlyAtContextLimit_ReturnsTrue()
    {
        string prefixes = string.Concat(Enumerable.Repeat("T:", MaximumDocumentationIdContexts));
        string documentationId = $"{prefixes}System.String";

        bool result = DocumentationInheritanceResolver.IsSafeDocumentationId(documentationId);

        result.Should().BeTrue();
    }

    [TestMethod]
    public void IsSafeDocumentationId_OverContextLimit_ReturnsFalse()
    {
        string prefixes = string.Concat(Enumerable.Repeat("T:", MaximumDocumentationIdContexts + 1));
        string documentationId = $"{prefixes}System.String";

        bool result = DocumentationInheritanceResolver.IsSafeDocumentationId(documentationId);

        result.Should().BeFalse();
    }

    [TestMethod]
    public void IsSafeDocumentationId_ExactlyAtDelimiterLimit_ReturnsTrue()
    {
        string documentationId = $"T:{new string('.', MaximumDocumentationIdDelimiters - 1)}A";

        bool result = DocumentationInheritanceResolver.IsSafeDocumentationId(documentationId);

        result.Should().BeTrue();
    }

    [TestMethod]
    public void IsSafeDocumentationId_OverDelimiterLimit_ReturnsFalse()
    {
        string documentationId = $"T:{new string('.', MaximumDocumentationIdDelimiters)}A";

        bool result = DocumentationInheritanceResolver.IsSafeDocumentationId(documentationId);

        result.Should().BeFalse();
    }

    [TestMethod]
    public async Task AnalyzeMember_MetadataInheritdocOverIdDepthLimit_IsTreatedAsUnknown()
    {
        string unsafeDocumentationId = string.Concat(
            "M:",
            new string('(', MaximumDocumentationIdDepth + 1),
            new string(')', MaximumDocumentationIdDepth + 1));
        MetadataReference metadata = CreateMetadataReference(
            "public static class External { public static void Run() { } }",
            new Dictionary<string, string>
            {
                ["M:External.Run"] = $"<member><inheritdoc cref=\"{unsafeDocumentationId}\"/></member>"
            });
        const string source = """
            public class Sample
            {
                /// <inheritdoc cref="External.Run()"/>
                public void Run() { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new MemberXmlDocumentationAnalyzer(),
            source,
            additionalReferences: [metadata]).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_ManyDuplicateBareInheritdocsAndInterfaces_ReportsNothing()
    {
        const int interfaceCount = 128;
        const int inheritdocCount = 4096;
        string interfaceDeclarations = string.Concat(
            Enumerable.Range(0, interfaceCount).Select(index => $$"""
                public interface I{{index}}
                {
                    /// <summary>Runs contract {{index}}.</summary>
                    void Run();
                }

                """));
        string interfaceList = string.Join(", ", Enumerable.Range(0, interfaceCount).Select(index => $"I{index}"));
        string inheritdocs = string.Concat(Enumerable.Repeat("    /// <inheritdoc/>\n", inheritdocCount));
        string source = $"{interfaceDeclarations}public class Sample : {interfaceList}\n{{\n"
            + inheritdocs
            + "    public void Run() { }\n}";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
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