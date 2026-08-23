// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

[TestClass]
public class UsePathJoinCodeFixTests
{
    private static async Task<string> FixAsync(string source) =>
        await CodeFixTestHarness.ApplyFixAsync(
            new UsePathJoinAnalyzer(),
            new UsePathJoinCodeFixProvider(),
            source,
            UsePathJoinAnalyzer.DiagnosticId).ConfigureAwait(false);

    [TestMethod]
    public async Task UsePathJoin_PathCombine_RenamesMethod()
    {
        const string source = """
            using System.IO;

            class Sample
            {
                string Build(string first, string second) => Path.Combine(first, second);
            }
            """;
        string expected = source.Replace("Path.Combine", "Path.Join");

        string fixedSource = await FixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task UsePathJoin_FullyQualifiedPathCombine_RenamesMethod()
    {
        const string source = """
            class Sample
            {
                string Build(string first, string second) => System.IO.Path.Combine(first, second);
            }
            """;
        string expected = source.Replace("Path.Combine", "Path.Join");

        string fixedSource = await FixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task UsePathJoin_ModernPathWithInternalComment_PreservesComment()
    {
        const string source = """
            class Sample
            {
                string Build(string first, string second)
                    => System.IO.Path /* keep */ .Combine(first, second);
            }
            """;
        string expected = source.Replace(".Combine", ".Join");

        string fixedSource = await FixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task UsePathJoin_AliasedPathCombine_PreservesAlias()
    {
        const string source = """
            using FilePath = System.IO.Path;

            class Sample
            {
                string Build(string first, string second) => FilePath.Combine(first, second);
            }
            """;
        string expected = source.Replace("FilePath.Combine", "FilePath.Join");

        string fixedSource = await FixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task UsePathJoin_UsingStaticCombine_PreservesStaticImport()
    {
        const string source = """
            using static System.IO.Path;

            class Sample
            {
                string Build(string first, string second) => Combine(first, second);
            }
            """;
        string expected = source.Replace("Combine(first", "Join(first");

        string fixedSource = await FixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task UsePathJoin_FourArguments_RenamesMethod()
    {
        const string source = """
            using System.IO;

            class Sample
            {
                string Build(string first, string second, string third, string fourth)
                    => Path.Combine(first, second, third, fourth);
            }
            """;
        string expected = source.Replace("Path.Combine", "Path.Join");

        string fixedSource = await FixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task UsePathJoin_ArrayArgument_RenamesMethod()
    {
        const string source = """
            using System.IO;

            class Sample
            {
                string Build(string[] paths) => Path.Combine(paths);
            }
            """;
        string expected = source.Replace("Path.Combine", "Path.Join");

        string fixedSource = await FixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task UsePathJoin_SpanArgument_RenamesMethod()
    {
        const string source = """
            using System;
            using System.IO;

            class Sample
            {
                string Build(ReadOnlySpan<string> paths) => Path.Combine(paths);
            }
            """;
        string expected = source.Replace("Path.Combine", "Path.Join");

        string fixedSource = await FixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task UsePathJoin_NullSegment_RenamesMethod()
    {
        const string source = """
            using System.IO;

            class Sample
            {
                string Build(string root) => Path.Combine(root, null!);
            }
            """;
        string expected = source.Replace("Path.Combine", "Path.Join");

        string fixedSource = await FixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task UsePathJoin_DriveRelativeSegment_RenamesMethod()
    {
        const string source = """
            using System.IO;

            class Sample
            {
                string Build() => Path.Combine("C:", "child");
            }
            """;
        string expected = source.Replace("Path.Combine", "Path.Join");

        string fixedSource = await FixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task UsePathJoin_NamedArguments_RenamesMethod()
    {
        const string source = """
            using System.IO;

            class Sample
            {
                string Build(string first, string second)
                    => Path.Combine(path1: first, path2: second);
            }
            """;
        string expected = source.Replace("Path.Combine", "Path.Join");

        string fixedSource = await FixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task UsePathJoin_MicrosoftIoPathAvailableOnModernDotNet_UsesSystemPath()
    {
        const string source = """
            using System.IO;

            namespace Microsoft.IO
            {
                public static class Path
                {
                    public static string Join(string first, string second) => first + second;
                }
            }

            class Sample
            {
                string Build(string first, string second) => Path.Combine(first, second);
            }
            """;
        string expected = source.Replace("Path.Combine", "Path.Join");

        string fixedSource = await FixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task UsePathJoin_Net472_UsesMicrosoftIoRedistPath()
    {
        const string source = """
            using System.IO;

            class Sample
            {
                string Build(string first, string second) => Path.Combine(first, second);
            }
            """;
        string expected = source.Replace("Path.Combine", "global::Microsoft.IO.Path.Join");

        string fixedSource = await CodeFixTestHarness.ApplyFixAsync(
            new UsePathJoinAnalyzer(),
            new UsePathJoinCodeFixProvider(),
            source,
            UsePathJoinAnalyzer.DiagnosticId,
            metadataReferences: RoslynTestEnvironment.Net472References).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task UsePathJoin_Net472MicrosoftIoPathCombine_RenamesMethod()
    {
        const string source = """
            class Sample
            {
                string Build(string first, string second)
                    => Microsoft.IO.Path.Combine(first, second);
            }
            """;
        string expected = source.Replace("Path.Combine", "Path.Join");

        string fixedSource = await CodeFixTestHarness.ApplyFixAsync(
            new UsePathJoinAnalyzer(),
            new UsePathJoinCodeFixProvider(),
            source,
            UsePathJoinAnalyzer.DiagnosticId,
            metadataReferences: RoslynTestEnvironment.Net472References).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task UsePathJoin_Net472MicrosoftIoPathFixAll_PreservesEverySpelling()
    {
        const string source = """
            using Microsoft.IO;
            using RedistPath = Microsoft.IO.Path;
            using static Microsoft.IO.Path;

            class Sample
            {
                string Imported(string first, string second) => Path.Combine(first, second);
                string Qualified(string first, string second) => Microsoft.IO.Path.Combine(first, second);
                string Aliased(string first, string second) => RedistPath.Combine(first, second);
                string Static(string first, string second) => Combine(first, second);
            }
            """;
        string expected = source
            .Replace(".Combine", ".Join")
            .Replace("=> Combine(", "=> Join(");

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new UsePathJoinAnalyzer(),
            new UsePathJoinCodeFixProvider(),
            [("Test.cs", "Test.cs", source)],
            UsePathJoinAnalyzer.DiagnosticId,
            fixAll: true,
            metadataReferences: RoslynTestEnvironment.Net472References).ConfigureAwait(false);

        result.FixAllActionOffered.Should().BeTrue();
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().BeEmpty();
        string fixedSource = result.Documents.Should().ContainSingle().Subject.Source;
        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task UsePathJoin_Net472NamedArguments_UsesMicrosoftIoRedistPath()
    {
        const string source = """
            using System.IO;

            class Sample
            {
                string Build(string first, string second)
                    => Path.Combine(path1: first, path2: second);
            }
            """;
        string expected = source.Replace("Path.Combine", "global::Microsoft.IO.Path.Join");

        string fixedSource = await CodeFixTestHarness.ApplyFixAsync(
            new UsePathJoinAnalyzer(),
            new UsePathJoinCodeFixProvider(),
            source,
            UsePathJoinAnalyzer.DiagnosticId,
            metadataReferences: RoslynTestEnvironment.Net472References).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task UsePathJoin_Net472FixAll_RewritesEveryOverload()
    {
        const string source = """
            using System.IO;

            class Sample
            {
                string Two(string first, string second) => Path.Combine(first, second);
                string Three(string first, string second, string third) => Path.Combine(first, second, third);
                string Four(string first, string second, string third, string fourth)
                    => Path.Combine(first, second, third, fourth);
                string Array(string[] paths) => Path.Combine(paths);
            }
            """;

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new UsePathJoinAnalyzer(),
            new UsePathJoinCodeFixProvider(),
            [("Test.cs", "Test.cs", source)],
            UsePathJoinAnalyzer.DiagnosticId,
            fixAll: true,
            metadataReferences: RoslynTestEnvironment.Net472References).ConfigureAwait(false);

        result.FixAllActionOffered.Should().BeTrue();
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().BeEmpty();
        string fixedSource = result.Documents.Should().ContainSingle().Subject.Source;
        fixedSource.Should().NotContain("Path.Combine");
        fixedSource.Split(["global::Microsoft.IO.Path.Join"], StringSplitOptions.None).Should().HaveCount(5);
    }

    [TestMethod]
    public async Task UsePathJoin_Net472SourceMicrosoftIoPathCollision_WithholdsFix()
    {
        const string source = """
            using System.IO;

            namespace Microsoft.IO
            {
                public static class Path
                {
                    public static string Join(string first, string second) => first + second;
                }
            }

            class Sample
            {
                string Build(string first, string second) => Path.Combine(first, second);
            }
            """;

        string fixedSource = await CodeFixTestHarness.ApplyFixAsync(
            new UsePathJoinAnalyzer(),
            new UsePathJoinCodeFixProvider(),
            source,
            UsePathJoinAnalyzer.DiagnosticId,
            metadataReferences: RoslynTestEnvironment.Net472References).ConfigureAwait(false);

        fixedSource.Should().Be(source);
    }

    [TestMethod]
    public async Task UsePathJoin_Net472InvalidCharacter_RenamesMethod()
    {
        const string source = """
            using System.IO;

            class Sample
            {
                string Build(string root) => Path.Combine(root, "bad\0name");
            }
            """;
        string expected = source.Replace("Path.Combine", "global::Microsoft.IO.Path.Join");

        string fixedSource = await CodeFixTestHarness.ApplyFixAsync(
            new UsePathJoinAnalyzer(),
            new UsePathJoinCodeFixProvider(),
            source,
            UsePathJoinAnalyzer.DiagnosticId,
            metadataReferences: RoslynTestEnvironment.Net472References).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task UsePathJoin_Net472PathWithInternalComment_WithholdsFix()
    {
        const string source = """
            class Sample
            {
                string Build(string first, string second)
                    => System.IO.Path /* keep */ .Combine(first, second);
            }
            """;

        string fixedSource = await CodeFixTestHarness.ApplyFixAsync(
            new UsePathJoinAnalyzer(),
            new UsePathJoinCodeFixProvider(),
            source,
            UsePathJoinAnalyzer.DiagnosticId,
            metadataReferences: RoslynTestEnvironment.Net472References).ConfigureAwait(false);

        fixedSource.Should().Be(source);
    }

    [TestMethod]
    public async Task UsePathJoin_Net472PathWithDirective_WithholdsFix()
    {
        const string source = """
            class Sample
            {
                string Build(string first, string second) => System.IO.Path
            #if USE_FIRST
                    .Combine(first, second);
            #else
                    .Combine(first, second);
            #endif
            }
            """;

        string fixedSource = await CodeFixTestHarness.ApplyFixAsync(
            new UsePathJoinAnalyzer(),
            new UsePathJoinCodeFixProvider(),
            source,
            UsePathJoinAnalyzer.DiagnosticId,
            metadataReferences: RoslynTestEnvironment.Net472References).ConfigureAwait(false);

        fixedSource.Should().Be(source);
    }

    [TestMethod]
    public async Task UsePathJoin_FixAll_RewritesEveryCall()
    {
        const string source = """
            using System.IO;

            class Sample
            {
                string First(string root, string child) => Path.Combine(root, child);
                string Second(string root, string child) => Path.Combine(root, "nested", child);
            }
            """;

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new UsePathJoinAnalyzer(),
            new UsePathJoinCodeFixProvider(),
            [("Test.cs", "Test.cs", source)],
            UsePathJoinAnalyzer.DiagnosticId,
            fixAll: true).ConfigureAwait(false);

        result.FixAllActionOffered.Should().BeTrue();
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().BeEmpty();
        string fixedSource = result.Documents.Should().ContainSingle().Subject.Source;
        fixedSource.Should().NotContain("Path.Combine");
        fixedSource.Split(["Path.Join"], StringSplitOptions.None).Should().HaveCount(3);
    }

    [TestMethod]
    public async Task UsePathJoin_StaleUnrelatedPathDiagnostic_WithholdsFix()
    {
        const string source = """
            static class Path
            {
                public static string Combine(string first, string second) => first + second;
            }

            class Sample
            {
                string Build(string first, string second) => Path.Combine(first, second);
            }
            """;

        string fixedSource = await CodeFixTestHarness.ApplyFixAsync(
            new ForcedCombineAnalyzer(),
            new UsePathJoinCodeFixProvider(),
            source,
            UsePathJoinAnalyzer.DiagnosticId).ConfigureAwait(false);

        fixedSource.Should().Be(source);
    }

    [TestMethod]
    public async Task UsePathJoin_StaleJoinDiagnostic_WithholdsFix()
    {
        const string source = """
            using System.IO;

            class Sample
            {
                string Build(string first, string second) => Path.Join(first, second);
            }
            """;

        string fixedSource = await CodeFixTestHarness.ApplyFixAsync(
            new ForcedCombineAnalyzer(),
            new UsePathJoinCodeFixProvider(),
            source,
            UsePathJoinAnalyzer.DiagnosticId).ConfigureAwait(false);

        fixedSource.Should().Be(source);
    }

    [TestMethod]
    public async Task UsePathJoin_ShiftedDiagnosticInsideCombine_WithholdsFix()
    {
        const string source = """
            using System.IO;

            class Sample
            {
                string Build(string first, string second) => Path.Combine(first, second);
            }
            """;

        string fixedSource = await CodeFixTestHarness.ApplyFixAsync(
            new ForcedCombineAnalyzer(reportArgument: true),
            new UsePathJoinCodeFixProvider(),
            source,
            UsePathJoinAnalyzer.DiagnosticId).ConfigureAwait(false);

        fixedSource.Should().Be(source);
    }

    [TestMethod]
    public async Task UsePathJoin_MultiTargetedLinkedSource_ReportsBothAndWithholdsFix()
    {
        const string source = """
            using System.IO;

            class Sample
            {
                string Build(string first, string second) => Path.Combine(first, second);
            }
            """;
        using AdhocWorkspace workspace = new();
        Project firstProject = workspace.AddProject("First", LanguageNames.CSharp)
            .AddMetadataReferences(RoslynTestEnvironment.References)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        workspace.TryApplyChanges(firstProject.Solution).Should().BeTrue();
        Project secondProject = workspace.AddProject("Second", LanguageNames.CSharp)
            .AddMetadataReferences(RoslynTestEnvironment.Net472References)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        workspace.TryApplyChanges(secondProject.Solution).Should().BeTrue();

        string filePath = Path.Join(Path.GetTempPath(), "touki-linked", "Shared.cs");
        firstProject = workspace.CurrentSolution.GetProject(firstProject.Id)!;
        Document firstDocument = firstProject.AddDocument(
            "Shared.cs",
            SourceText.From(source),
            filePath: filePath);
        workspace.TryApplyChanges(firstDocument.Project.Solution).Should().BeTrue();
        secondProject = workspace.CurrentSolution.GetProject(secondProject.Id)!;
        Document secondDocument = secondProject.AddDocument(
            "Shared.cs",
            SourceText.From(source),
            filePath: filePath);
        workspace.TryApplyChanges(secondDocument.Project.Solution).Should().BeTrue();
        firstDocument = workspace.CurrentSolution.GetDocument(firstDocument.Id)!;
        secondDocument = workspace.CurrentSolution.GetDocument(secondDocument.Id)!;

        Compilation firstCompilation = (await firstDocument.Project.GetCompilationAsync().ConfigureAwait(false))!;
        Compilation secondCompilation = (await secondDocument.Project.GetCompilationAsync().ConfigureAwait(false))!;
        firstCompilation.GetDiagnostics().Should().NotContain(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        secondCompilation.GetDiagnostics().Should().NotContain(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        Diagnostic firstDiagnostic = (await firstCompilation
            .WithAnalyzers([new UsePathJoinAnalyzer()])
            .GetAnalyzerDiagnosticsAsync()
            .ConfigureAwait(false))
            .Should().ContainSingle().Subject;
        Diagnostic secondDiagnostic = (await secondCompilation
            .WithAnalyzers([new UsePathJoinAnalyzer()])
            .GetAnalyzerDiagnosticsAsync()
            .ConfigureAwait(false))
            .Should().ContainSingle().Subject;
        List<CodeAction> actions = [];
        CodeFixContext firstContext = new(
            firstDocument,
            firstDiagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None);

        UsePathJoinCodeFixProvider provider = new();
        await provider.RegisterCodeFixesAsync(firstContext).ConfigureAwait(false);

        actions.Should().BeEmpty();
        CodeFixContext secondContext = new(
            secondDocument,
            secondDiagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None);
        await provider.RegisterCodeFixesAsync(secondContext).ConfigureAwait(false);

        actions.Should().BeEmpty();
    }

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    private sealed class ForcedCombineAnalyzer(bool reportArgument = false) : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor s_rule = new(
            UsePathJoinAnalyzer.DiagnosticId,
            "Forced path diagnostic",
            "Forced path diagnostic",
            "Test",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        private static readonly ImmutableArray<DiagnosticDescriptor> s_supportedDiagnostics = [s_rule];

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => s_supportedDiagnostics;

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(
                syntaxContext =>
                {
                    if (syntaxContext.Node is InvocationExpressionSyntax invocation)
                    {
                        if (reportArgument && invocation.ArgumentList.Arguments.Count > 0)
                        {
                            syntaxContext.ReportDiagnostic(
                                Diagnostic.Create(s_rule, invocation.ArgumentList.Arguments[0].GetLocation()));
                            return;
                        }

                        SimpleNameSyntax? methodName = invocation.Expression switch
                        {
                            SimpleNameSyntax simpleName => simpleName,
                            MemberAccessExpressionSyntax memberAccess => memberAccess.Name,
                            _ => null
                        };
                        if (methodName is not null)
                        {
                            syntaxContext.ReportDiagnostic(
                                Diagnostic.Create(s_rule, methodName.GetLocation()));
                        }
                    }
                },
                SyntaxKind.InvocationExpression);
        }
    }
}