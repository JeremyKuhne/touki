// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Analyzers;

[TestClass]
public class FileNameMatchesTypeAnalyzerTests
{
    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        string source,
        string? fileName,
        string? separators = null)
    {
        Dictionary<string, string>? options = separators is null
            ? null
            : new Dictionary<string, string> { [FileNameMatchesTypeAnalyzer.DetailSeparatorsOption] = separators };

        return await AnalyzerTestHarness
            .GetDiagnosticsAsync(new FileNameMatchesTypeAnalyzer(), source, options, fileName)
            .ConfigureAwait(false);
    }

    private const string SimpleType = """
        class Foo
        {
        }
        """;

    private const string NestedType = """
        partial class Foo
        {
            class Bar
            {
            }
        }
        """;

    [TestMethod]
    public async Task AnalyzeSyntaxTree_NameMatches_ReportsNothing()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(SimpleType, "Foo.cs").ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_NameDiffers_ReportsDiagnostic()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(SimpleType, "Other.cs").ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(FileNameMatchesTypeAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_MessageNamesFileAndType()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(SimpleType, "Other.cs").ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Contain("Other").And.Contain("Foo");
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_CaseDiffers_ReportsDiagnostic()
    {
        // Comparison is ordinal so that casing is right even on a case-insensitive file system.
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(SimpleType, "foo.cs").ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
    }

    [TestMethod]
    [DataRow("Foo.Windows.cs")]
    [DataRow("Foo-Windows.cs")]
    [DataRow("Foo_Windows.cs")]
    public async Task AnalyzeSyntaxTree_DetailAfterDefaultSeparator_ReportsNothing(string fileName)
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(SimpleType, fileName).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_DetailWithoutSeparator_ReportsDiagnostic()
    {
        // 'FooWindows' is a different name, not 'Foo' with detail.
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(SimpleType, "FooWindows.cs").ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
    }

    [TestMethod]
    [DataRow("Foo.Bar.cs")]
    [DataRow("Bar.cs")]
    public async Task AnalyzeSyntaxTree_NestedType_AcceptsEitherName(string fileName)
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(NestedType, fileName).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("Foo.Middle.Leaf.cs")]
    [DataRow("Foo.Middle.cs")]
    [DataRow("Leaf.cs")]
    public async Task AnalyzeSyntaxTree_DeeplyNestedType_AcceptsAnyLevel(string fileName)
    {
        string source = """
            partial class Foo
            {
                partial class Middle
                {
                    class Leaf
                    {
                    }
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, fileName).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_NestedNameUnrelatedToFile_ReportsDiagnostic()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(NestedType, "Unrelated.cs").ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_TypeInNamespace_ReportsNothing()
    {
        string source = """
            namespace Some.Space;

            class Foo
            {
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, "Foo.cs").ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_GenericType_MatchesUnadornedName()
    {
        string source = """
            class Foo<T>
            {
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, "Foo.cs").ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("Foo{T}.cs")]
    [DataRow("Foo{T}.Windows.cs")]
    public async Task AnalyzeSyntaxTree_GenericType_BracedTypeParameterSuffix_Matches(string fileName)
    {
        string source = """
            class Foo<T>
            {
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, fileName).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_GenericType_MultipleTypeParametersBracedSuffix_Matches()
    {
        string source = """
            class Pair<TKey, TValue>
            {
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, "Pair{TKey,TValue}.cs").ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_GenericType_BracedTypeParameterSuffixDiffers_ReportsDiagnostic()
    {
        string source = """
            class Foo<T>
            {
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, "Foo{U}.cs").ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_NonGenericType_BracedTypeParameterSuffix_ReportsDiagnostic()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(SimpleType, "Foo{T}.cs").ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_Enum_ReportsNothing()
    {
        string source = """
            enum Color
            {
                Red
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, "Color.cs").ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_Delegate_ReportsNothing()
    {
        string source = "delegate void Handler();";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, "Handler.cs").ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_NoTypeDeclared_ReportsNothing()
    {
        string source = """
            using System;

            [assembly: CLSCompliant(false)]
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, "GlobalUsings.cs").ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_NoFilePath_ReportsNothing()
    {
        // An in-memory tree has no name to match against.
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(SimpleType, fileName: null).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_GeneratedCode_ReportsNothing()
    {
        string source = "// <auto-generated/>\n" + SimpleType;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, "Other.cs").ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_ConfiguredSeparatorsNarrowed_ReportsDiagnostic()
    {
        // Underscore is dropped from the approved set, so 'Foo_Windows' no longer reads as detail.
        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzeAsync(SimpleType, "Foo_Windows.cs", separators: ".-").ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_ConfiguredSeparatorsWidened_ReportsNothing()
    {
        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzeAsync(SimpleType, "Foo+Windows.cs", separators: "+").ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_ConfiguredSeparatorsPadded_IsHonored()
    {
        // Padding around the set must not silently drop the setting back to the default.
        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzeAsync(SimpleType, "Foo+Windows.cs", separators: "  +  ").ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public async Task AnalyzeSyntaxTree_UnusableConfiguredSeparators_FallsBackToDefault(string separators)
    {
        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzeAsync(SimpleType, "Foo.Windows.cs", separators).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_NestedDottedPath_MatchesWhenDotIsNotASeparator()
    {
        // With '.' dropped from the approved set, 'Foo.Bar' can no longer read as 'Foo' plus detail, so this
        // only passes because the nested type's dotted path is itself a candidate.
        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzeAsync(NestedType, "Foo.Bar.cs", separators: "-").ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_DetailAfterDroppedSeparator_ReportsDiagnostic()
    {
        // Same configuration, but 'Other' is not a nested type, so there is no candidate to match.
        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzeAsync(NestedType, "Foo.Other.cs", separators: "-").ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_PathWithDirectories_MatchesOnFileNameOnly()
    {
        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzeAsync(SimpleType, "/src/deep/path/Foo.cs").ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }
}
