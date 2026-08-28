// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Touki.Analyzers;

[TestClass]
public class TypeXmlSummaryAnalyzerTests
{
    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        string source,
        string? apiSurface = null) =>
        AnalyzerTestHarness.GetDiagnosticsAsync(
            new TypeXmlSummaryAnalyzer(),
            source,
            options: CreateOptions(apiSurface));

    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        IReadOnlyList<(string Source, string FileName)> sources,
        string? apiSurface = null) =>
        AnalyzerTestHarness.GetDiagnosticsAsync(
            new TypeXmlSummaryAnalyzer(),
            sources,
            options: CreateOptions(apiSurface));

    private static Dictionary<string, string>? CreateOptions(string? apiSurface) =>
        apiSurface is null
            ? null
            : new Dictionary<string, string> { [TypeXmlSummaryAnalyzer.ApiSurfaceOption] = apiSurface };

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeSemanticAsync(string source)
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText(source);
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "Touki.Analyzers.SemanticTestCompilation",
            syntaxTrees: [tree],
            references: RoslynTestEnvironment.References,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        CompilationWithAnalyzers compilationWithAnalyzers =
            compilation.WithAnalyzers([new TypeXmlSummaryAnalyzer()]);
        SemanticModel semanticModel = compilation.GetSemanticModel(tree);

        return await compilationWithAnalyzers.GetAnalyzerSemanticDiagnosticsAsync(
            semanticModel,
            filterSpan: null,
            cancellationToken: default).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task AnalyzeNamedType_AllTypeKindsHaveSummary_ReportsNothing()
    {
        const string source = """
            /// <summary>A class.</summary>
            public class ClassSample { }

            /// <summary>A struct.</summary>
            public struct StructSample { }

            /// <summary>An interface.</summary>
            public interface IInterfaceSample { }

            /// <summary>A record.</summary>
            public record RecordSample;

            /// <summary>A record struct.</summary>
            public record struct RecordStructSample;

            /// <summary>An enum.</summary>
            public enum EnumSample { None }

            /// <summary>A delegate.</summary>
            public delegate void DelegateSample();
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeNamedType_AllTypeKindsMissingSummary_ReportEachType()
    {
        const string source = """
            public class ClassSample { }
            public struct StructSample { }
            public interface IInterfaceSample { }
            public record RecordSample;
            public record struct RecordStructSample;
            public enum EnumSample { None }
            public delegate void DelegateSample();
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().HaveCount(7);
        diagnostics.Select(diagnostic => diagnostic.Location.SourceTree!.GetText()
            .ToString(diagnostic.Location.SourceSpan)).Should().BeEquivalentTo(
                "ClassSample",
                "StructSample",
                "IInterfaceSample",
                "RecordSample",
                "RecordStructSample",
                "EnumSample",
                "DelegateSample");
    }

    [TestMethod]
    public async Task AnalyzeNamedType_MissingSummary_ReportsAtIdentifier()
    {
        const string source = "class Sample { }";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Id.Should().Be(TypeXmlSummaryAnalyzer.DiagnosticId);
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).Should().Be("Sample");
        diagnostic.GetMessage().Should().Be(
            "Type 'Sample' must declare one XML <summary> element or an <inheritdoc> element; found 0 summaries");
    }

    [TestMethod]
    public async Task AnalyzeNamedType_MissingSummary_ReportsDuringSemanticAnalysis()
    {
        const string source = "class Sample { }";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeSemanticAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(TypeXmlSummaryAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task AnalyzeNamedType_NestedTypeMissingSummary_ReportsNestedType()
    {
        const string source = """
            /// <summary>The outer type.</summary>
            public class Outer
            {
                public class Nested { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        Location location = diagnostics.Should().ContainSingle().Subject.Location;
        location.SourceTree!.GetText().ToString(location.SourceSpan).Should().Be("Nested");
    }

    [TestMethod]
    public async Task AnalyzeNamedType_SummaryBeforeAttribute_ReportsNothing()
    {
        const string source = """
            using System;

            /// <summary>A sample.</summary>
            [Obsolete]
            class Sample { }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeNamedType_RegularCommentBetweenSummaryAndDeclaration_ReportsNothing()
    {
        const string source = """
            /// <summary>A sample.</summary>
            // The implementation intentionally follows the summary.
            class Sample { }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeNamedType_SummarySeparatedFromAttachedDocumentation_ReportsMissingSummary()
    {
        const string source = """
            /// <summary>Unprocessed summary.</summary>
            // The regular comment separates the documentation blocks.
            /// <remarks>Attached remarks.</remarks>
            class Sample { }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().EndWith("found 0 summaries");
    }

    [TestMethod]
    public async Task AnalyzeNamedType_MalformedSummary_ReportsMissingSummary()
    {
        const string source = """
            /// <summary>Unclosed summary.
            class Sample { }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().EndWith("found 0 summaries");
    }

    [TestMethod]
    public async Task AnalyzeNamedType_InheritdocWithoutSummary_ReportsNothing()
    {
        const string source = """
            /// <inheritdoc/>
            class Sample { }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeNamedType_InheritdocWithCref_ReportsNothing()
    {
        const string source = """
            /// <inheritdoc cref="Base"/>
            class Sample { }

            /// <summary>A base type.</summary>
            class Base { }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeNamedType_SummaryAndInheritdoc_ReportsNothing()
    {
        const string source = """
            /// <summary>A sample.</summary>
            /// <inheritdoc/>
            class Sample { }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeNamedType_TwoSummariesAndInheritdoc_ReportsDuplicateSummaries()
    {
        const string source = """
            /// <summary>First.</summary>
            /// <summary>Second.</summary>
            /// <inheritdoc/>
            class Sample { }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().EndWith("found 2 summaries");
    }

    [TestMethod]
    public async Task AnalyzeNamedType_InheritdocNestedInRemarks_ReportsMissingDocumentation()
    {
        const string source = """
            /// <remarks><inheritdoc/></remarks>
            class Sample { }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
    }

    [TestMethod]
    public async Task AnalyzeNamedType_MalformedInheritdoc_ReportsMissingDocumentation()
    {
        const string source = """
            /// <inheritdoc
            class Sample { }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
    }

    [TestMethod]
    public async Task AnalyzeNamedType_UnprocessedInheritdoc_ReportsMissingDocumentation()
    {
        const string source = """
            /// <inheritdoc/>
            // This separates the documentation blocks.
            /// <remarks>Attached remarks.</remarks>
            class Sample { }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
    }

    [TestMethod]
    public async Task AnalyzeNamedType_SummaryNestedInRemarks_ReportsMissingSummary()
    {
        const string source = """
            /// <remarks>
            ///  <summary>Not a top-level summary.</summary>
            /// </remarks>
            class Sample { }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().EndWith("found 0 summaries");
    }

    [TestMethod]
    public async Task AnalyzeNamedType_TwoSummariesOnOneDeclaration_ReportsDuplicate()
    {
        const string source = """
            /// <summary>First.</summary>
            /// <summary>Second.</summary>
            class Sample { }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().EndWith("found 2 summaries");
    }

    [TestMethod]
    public async Task AnalyzeNamedType_PartialTypeWithOneSummary_ReportsNothing()
    {
        IReadOnlyList<(string Source, string FileName)> sources =
        [
            ("/// <summary>A sample.</summary>\npartial class Sample { }", "Sample.cs"),
            ("partial class Sample { }", "Sample.Other.cs")
        ];

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(sources).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeNamedType_PartialTypeWithoutSummary_ReportsOnceOnEarliestDeclaration()
    {
        IReadOnlyList<(string Source, string FileName)> sources =
        [
            ("partial class Sample { }", "B.cs"),
            ("partial class Sample { }", "A.cs")
        ];

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(sources).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Location.SourceTree!.FilePath.Should().Be("A.cs");
        diagnostic.GetMessage().Should().EndWith("found 0 summaries");
    }

    [TestMethod]
    public async Task AnalyzeNamedType_PartialTypeSummarizedInTwoFiles_ReportsOnce()
    {
        IReadOnlyList<(string Source, string FileName)> sources =
        [
            ("/// <summary>First.</summary>\npartial class Sample { }", "A.cs"),
            ("/// <summary>Second.</summary>\npartial class Sample { }", "B.cs")
        ];

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(sources).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Location.SourceTree!.FilePath.Should().Be("A.cs");
        diagnostic.GetMessage().Should().EndWith("found 2 summaries");
    }

    [TestMethod]
    public async Task AnalyzeNamedType_PartialTypeSummarizedOnTwoSameFileDeclarations_ReportsOnce()
    {
        const string source = """
            /// <summary>First.</summary>
            partial class Sample { }

            /// <summary>Second.</summary>
            partial class Sample { }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().EndWith("found 2 summaries");
    }

    [TestMethod]
    public async Task AnalyzeNamedType_PartialTypeIncludedByOneFileConfiguration_Reports()
    {
        IReadOnlyList<(string Source, string FileName)> sources =
        [
            ("partial class Sample { }", "A.cs"),
            ("partial class Sample { }", "B.cs")
        ];
        Dictionary<string, IReadOnlyDictionary<string, string>> optionsByFile = new(StringComparer.Ordinal)
        {
            ["A.cs"] = new Dictionary<string, string>
            {
                [TypeXmlSummaryAnalyzer.ApiSurfaceOption] = "public"
            },
            ["B.cs"] = new Dictionary<string, string>
            {
                [TypeXmlSummaryAnalyzer.ApiSurfaceOption] = "internal"
            }
        };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new TypeXmlSummaryAnalyzer(),
            sources,
            optionsByFile: optionsByFile).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
    }

    [TestMethod]
    public async Task AnalyzeNamedType_PartialTypeExcludedByEveryFileConfiguration_ReportsNothing()
    {
        IReadOnlyList<(string Source, string FileName)> sources =
        [
            ("partial class Sample { }", "A.cs"),
            ("partial class Sample { }", "B.cs")
        ];
        Dictionary<string, IReadOnlyDictionary<string, string>> optionsByFile = new(StringComparer.Ordinal)
        {
            ["A.cs"] = new Dictionary<string, string>
            {
                [TypeXmlSummaryAnalyzer.ApiSurfaceOption] = "public"
            },
            ["B.cs"] = new Dictionary<string, string>
            {
                [TypeXmlSummaryAnalyzer.ApiSurfaceOption] = "public"
            }
        };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new TypeXmlSummaryAnalyzer(),
            sources,
            optionsByFile: optionsByFile).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeNamedType_GeneratedPartialSummarySatisfiesUserDeclaration_ReportsNothing()
    {
        IReadOnlyList<(string Source, string FileName)> sources =
        [
            ("partial class Sample { }", "Sample.cs"),
            ("// <auto-generated/>\n/// <summary>Generated.</summary>\npartial class Sample { }", "Sample.g.cs")
        ];

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(sources).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeNamedType_GeneratedPartialSummaryDuplicatesUserSummary_ReportsUserDeclaration()
    {
        IReadOnlyList<(string Source, string FileName)> sources =
        [
            ("/// <summary>User.</summary>\npartial class Sample { }", "Sample.cs"),
            ("// <auto-generated/>\n/// <summary>Generated.</summary>\npartial class Sample { }", "Sample.g.cs")
        ];

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(sources).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Location.SourceTree!.FilePath.Should().Be("Sample.cs");
        diagnostic.GetMessage().Should().EndWith("found 2 summaries");
    }

    [TestMethod]
    public async Task AnalyzeNamedType_GeneratedCodeAttributeOnPartial_ReportsUserDeclaration()
    {
        IReadOnlyList<(string Source, string FileName)> sources =
        [
            (
                "[System.CodeDom.Compiler.GeneratedCode(\"test\", \"1.0\")]\npartial class Sample { }",
                "A.cs"),
            ("partial class Sample { }", "B.cs")
        ];

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(sources).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Location.SourceTree!.FilePath.Should().Be("B.cs");
    }

    [TestMethod]
    public async Task AnalyzeNamedType_CompilerGeneratedAttributeOnPartial_ReportsUserDeclaration()
    {
        IReadOnlyList<(string Source, string FileName)> sources =
        [
            (
                "[System.Runtime.CompilerServices.CompilerGenerated]\npartial class Sample { }",
                "A.cs"),
            ("partial class Sample { }", "B.cs")
        ];

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(sources).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Location.SourceTree!.FilePath.Should().Be("B.cs");
    }

    [TestMethod]
    public async Task AnalyzeNamedType_GeneratedCodeOptionTrue_ReportsNothing()
    {
        Dictionary<string, string> options = new()
        {
            ["generated_code"] = "true"
        };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new TypeXmlSummaryAnalyzer(),
            "class Sample { }",
            options).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeNamedType_GeneratedCodeOptionFalseOverridesGeneratedFileName_Reports()
    {
        Dictionary<string, string> options = new()
        {
            ["generated_code"] = "false"
        };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new TypeXmlSummaryAnalyzer(),
            "class Sample { }",
            options,
            fileName: "Sample.g.cs").ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
    }

    [TestMethod]
    public async Task AnalyzeNamedType_GeneratedFileName_ReportsNothing()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new TypeXmlSummaryAnalyzer(),
            "class Sample { }",
            fileName: "Sample.g.cs").ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeNamedType_HiddenLine_ReportsNothing()
    {
        const string source = """
            #line hidden
            class Sample { }
            #line default
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeNamedType_NestedInGeneratedType_ReportsNothing()
    {
        const string source = """
            [System.CodeDom.Compiler.GeneratedCode("test", "1.0")]
            class Outer
            {
                class Nested { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeNamedType_NestedInUserPartialWithGeneratedOtherPart_ReportsUserNestedType()
    {
        IReadOnlyList<(string Source, string FileName)> sources =
        [
            (
                "[System.CodeDom.Compiler.GeneratedCode(\"test\", \"1.0\")]\npartial class Outer { }",
                "Outer.g.cs"),
            (
                "/// <summary>An outer type.</summary>\npartial class Outer { class Nested { } }",
                "Outer.cs")
        ];

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(sources).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Location.SourceTree!.FilePath.Should().Be("Outer.cs");
        diagnostic.Location.SourceTree.GetText().ToString(diagnostic.Location.SourceSpan).Should().Be("Nested");
    }

    [TestMethod]
    public async Task AnalyzeNamedType_OnlyGeneratedDeclaration_ReportsNothing()
    {
        const string source = "// <auto-generated/>\nclass Sample { }";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeNamedType_PublicSurface_ReportsOnlyEffectivelyPublicType()
    {
        const string source = """
            public class PublicSample { }
            internal class InternalSample { }

            /// <summary>An outer type.</summary>
            internal class Outer
            {
                public class Nested { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, "public").ConfigureAwait(false);

        Location location = diagnostics.Should().ContainSingle().Subject.Location;
        location.SourceTree!.GetText().ToString(location.SourceSpan).Should().Be("PublicSample");
    }

    [TestMethod]
    public async Task AnalyzeNamedType_InternalSurface_IncludesPublicTypeNestedInInternalType()
    {
        const string source = """
            /// <summary>An outer type.</summary>
            internal class Outer
            {
                public class Nested { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, "internal").ConfigureAwait(false);

        Location location = diagnostics.Should().ContainSingle().Subject.Location;
        location.SourceTree!.GetText().ToString(location.SourceSpan).Should().Be("Nested");
    }

    [TestMethod]
    public async Task AnalyzeNamedType_PrivateSurface_ReportsPrivateNestedType()
    {
        const string source = """
            /// <summary>An outer type.</summary>
            public class Outer
            {
                private class Nested { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, "private").ConfigureAwait(false);

        Location location = diagnostics.Should().ContainSingle().Subject.Location;
        location.SourceTree!.GetText().ToString(location.SourceSpan).Should().Be("Nested");
    }

    [TestMethod]
    public async Task AnalyzeNamedType_FileSurface_ReportsOnlyFileLocalType()
    {
        const string source = """
            file class FileSample { }
            internal class InternalSample { }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, "file").ConfigureAwait(false);

        Location location = diagnostics.Should().ContainSingle().Subject.Location;
        location.SourceTree!.GetText().ToString(location.SourceSpan).Should().Be("FileSample");
    }

    [TestMethod]
    public async Task AnalyzeNamedType_FileSurface_IncludesPublicTypeNestedInFileLocalType()
    {
        const string source = """
            /// <summary>A file-local outer type.</summary>
            file class Outer
            {
                public class Nested { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, "file").ConfigureAwait(false);

        Location location = diagnostics.Should().ContainSingle().Subject.Location;
        location.SourceTree!.GetText().ToString(location.SourceSpan).Should().Be("Nested");
    }

    [TestMethod]
    public async Task AnalyzeNamedType_FileSurface_ExcludesPrivateTypeNestedInFileLocalType()
    {
        const string source = """
            /// <summary>A file-local outer type.</summary>
            file class Outer
            {
                private class Nested { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, "file").ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeNamedType_CombinedSurfaces_AreCaseInsensitiveAndTrimmed()
    {
        const string source = """
            public class PublicSample { }
            internal class InternalSample { }
            file class FileSample { }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzeAsync(source, " PUBLIC, file ").ConfigureAwait(false);

        diagnostics.Should().HaveCount(2);
        diagnostics.Select(diagnostic => diagnostic.Location.SourceTree!.GetText()
            .ToString(diagnostic.Location.SourceSpan)).Should().BeEquivalentTo("PublicSample", "FileSample");
    }

    [TestMethod]
    public async Task AnalyzeNamedType_InvalidSurface_FallsBackToAll()
    {
        const string source = "class Sample { }";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, "unknown").ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
    }
}