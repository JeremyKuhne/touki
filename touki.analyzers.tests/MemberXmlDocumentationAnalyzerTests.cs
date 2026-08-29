// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Touki.Analyzers;

[TestClass]
public partial class MemberXmlDocumentationAnalyzerTests
{
    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        string source,
        string? apiSurface = null,
        bool? requireParameters = null,
        bool? requireReturns = null,
        string? fileName = null)
    {
        Dictionary<string, string>? options = CreateOptions(apiSurface, requireParameters, requireReturns);
        return AnalyzerTestHarness.GetDiagnosticsAsync(
            new MemberXmlDocumentationAnalyzer(),
            source,
            options,
            fileName);
    }

    private static Task<ImmutableArray<Diagnostic>> AnalyzePreviewAsync(
        string source,
        string? apiSurface = null) =>
        AnalyzerTestHarness.GetDiagnosticsAsync(
            new MemberXmlDocumentationAnalyzer(),
            source,
            options: CreateOptions(apiSurface, requireParameters: null, requireReturns: null),
            parseOptions: new CSharpParseOptions(LanguageVersion.Preview));

    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        IReadOnlyList<(string Source, string FileName)> sources,
        string? apiSurface = null,
        bool? requireParameters = null,
        bool? requireReturns = null)
    {
        Dictionary<string, string>? options = CreateOptions(apiSurface, requireParameters, requireReturns);
        return AnalyzerTestHarness.GetDiagnosticsAsync(
            new MemberXmlDocumentationAnalyzer(),
            sources,
            options);
    }

    private static Dictionary<string, string>? CreateOptions(
        string? apiSurface,
        bool? requireParameters,
        bool? requireReturns)
    {
        if (apiSurface is null && requireParameters is null && requireReturns is null)
        {
            return null;
        }

        Dictionary<string, string> options = new();
        if (apiSurface is not null)
        {
            options.Add(MemberXmlDocumentationAnalyzer.ApiSurfaceOption, apiSurface);
        }

        if (requireParameters is bool parameters)
        {
            options.Add(
                MemberXmlDocumentationAnalyzer.RequireParameterDocumentationOption,
                parameters.ToString());
        }

        if (requireReturns is bool returns)
        {
            options.Add(
                MemberXmlDocumentationAnalyzer.RequireReturnDocumentationOption,
                returns.ToString());
        }

        return options;
    }

    private static bool IsMemberDocumentationDiagnostic(Diagnostic diagnostic) =>
        diagnostic.Id == MemberXmlDocumentationAnalyzer.DiagnosticId
        && diagnostic.GetMessage().Contains("missing <summary>", StringComparison.Ordinal);

    private static bool IsParameterDocumentationDiagnostic(Diagnostic diagnostic) =>
        diagnostic.Id == MemberXmlDocumentationAnalyzer.DiagnosticId
        && diagnostic.GetMessage().Contains("missing <param>", StringComparison.Ordinal);

    private static bool IsReturnDocumentationDiagnostic(Diagnostic diagnostic) =>
        diagnostic.Id == MemberXmlDocumentationAnalyzer.DiagnosticId
        && diagnostic.GetMessage().Contains("missing <returns>", StringComparison.Ordinal);

    private static PortableExecutableReference CreateMetadataReference(
        string source,
        IReadOnlyDictionary<string, string> documentation,
        string assemblyName = "MemberDocumentation.Metadata")
    {
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName,
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references: RoslynTestEnvironment.References,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using MemoryStream peStream = new();
        Microsoft.CodeAnalysis.Emit.EmitResult result = compilation.Emit(peStream);
        if (!result.Success)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics));
        }

        return MetadataReference.CreateFromImage(
            peStream.ToArray(),
            documentation: new TestDocumentationProvider(documentation),
            filePath: "MemberDocumentation.Metadata.dll");
    }

    private static CompilationReference CreateCompilationReference(
        string source,
        string assemblyName = "MemberDocumentation.ProjectReference",
        IReadOnlyCollection<MetadataReference>? additionalReferences = null)
    {
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName,
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references: RoslynTestEnvironment.GetReferences(additionalReferences),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        return compilation.ToMetadataReference();
    }

    [TestMethod]
    public async Task AnalyzeMember_PublicMethodWithoutDocumentation_ReportsMember()
    {
        const string source = "public class Sample { public void Run() { } }";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Id.Should().Be(MemberXmlDocumentationAnalyzer.DiagnosticId);
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).Should().Be("Run");
    }

    [TestMethod]
    public async Task AnalyzeMember_Summary_ReportsNothing()
    {
        const string source = """
            public class Sample
            {
                /// <summary>Runs the sample.</summary>
                public void Run() { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_BareInheritdocWithoutTarget_ReportsAllRequirements()
    {
        const string source = """
            public class Sample
            {
                /// <inheritdoc/>
                public int Transform(int value) => value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().HaveCount(3);
        diagnostics.Should().ContainSingle(
            diagnostic => diagnostic.GetMessage().Contains(
                "<inheritdoc> does not resolve to a top-level <summary>",
                StringComparison.Ordinal));
        diagnostics.Should().ContainSingle(diagnostic => IsParameterDocumentationDiagnostic(diagnostic));
        diagnostics.Should().ContainSingle(diagnostic => IsReturnDocumentationDiagnostic(diagnostic));
    }

    [TestMethod]
    public async Task AnalyzeMember_InheritdocCrefToSynthesizedConstructor_ReportsMember()
    {
        const string source = """
            internal class Target { }

            public class Sample
            {
                /// <inheritdoc cref="Target.Target()"/>
                public void Run() { }
            }
            """;
        SyntaxTree tree = CSharpSyntaxTree.ParseText(source);
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "SynthesizedTarget",
            syntaxTrees: [tree],
            references: RoslynTestEnvironment.References,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        XmlCrefAttributeSyntax cref = tree.GetRoot().DescendantNodes(descendIntoTrivia: true)
            .OfType<XmlCrefAttributeSyntax>()
            .Single();
        ISymbol? target = compilation.GetSemanticModel(tree).GetSymbolInfo(cref.Cref).Symbol;
        target.Should().BeAssignableTo<IMethodSymbol>();
        target!.IsImplicitlyDeclared.Should().BeTrue();

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, apiSurface: "public")
            .ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
    }

    [TestMethod]
    public async Task AnalyzeMember_InheritdocCrefToAssemblylessFunctionPointer_ReportsMember()
    {
        const string source = """
            using unsafe Callback = delegate* unmanaged<void>;

            public class Sample
            {
                /// <inheritdoc cref="Callback"/>
                public void Run() { }
            }
            """;
        SyntaxTree tree = CSharpSyntaxTree.ParseText(source);
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "AssemblylessTarget",
            syntaxTrees: [tree],
            references: RoslynTestEnvironment.References,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        XmlCrefAttributeSyntax cref = tree.GetRoot().DescendantNodes(descendIntoTrivia: true)
            .OfType<XmlCrefAttributeSyntax>()
            .Single();
        ISymbol? target = compilation.GetSemanticModel(tree).GetSymbolInfo(cref.Cref).Symbol;
        target.Should().BeAssignableTo<IFunctionPointerTypeSymbol>();
        target!.ContainingAssembly.Should().BeNull();

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
    }

    [TestMethod]
    public async Task AnalyzeMember_InheritdocCrefToUndocumentedProjectEnumValue_ReportsMember()
    {
        MetadataReference projectReference = CreateCompilationReference("public enum ExternalKind { None }");
        const string source = """
            public enum LocalKind
            {
                /// <inheritdoc cref="ExternalKind.None"/>
                None
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new MemberXmlDocumentationAnalyzer(),
            source,
            additionalReferences: [projectReference]).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.GetMessage().Should().Contain("<inheritdoc> does not resolve to a top-level <summary>");
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).Should().Be("None");
    }

    [TestMethod]
    public async Task AnalyzeMember_InheritdocCrefToDocumentedProjectEnumValue_ReportsNothing()
    {
        MetadataReference projectReference = CreateCompilationReference(
            """
            public enum ExternalKind
            {
                /// <summary>The default value.</summary>
                None
            }
            """);
        const string source = """
            public enum LocalKind
            {
                /// <inheritdoc cref="ExternalKind.None"/>
                None
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new MemberXmlDocumentationAnalyzer(),
            source,
            additionalReferences: [projectReference]).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_InheritdocCrefChainInProjectReference_ReportsNothing()
    {
        MetadataReference projectReference = CreateCompilationReference(
            """
            public enum ExternalKind
            {
                /// <summary>The documented value.</summary>
                Documented,

                /// <inheritdoc cref="Documented"/>
                Alias
            }
            """);
        const string source = """
            public enum LocalKind
            {
                /// <inheritdoc cref="ExternalKind.Alias"/>
                None
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new MemberXmlDocumentationAnalyzer(),
            source,
            additionalReferences: [projectReference]).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_InheritdocCrefChainAcrossTransitiveProjectReference_ReportsNothing()
    {
        CompilationReference rootReference = CreateCompilationReference(
            """
            public enum RootKind
            {
                /// <summary>The documented value.</summary>
                Documented
            }
            """,
            assemblyName: "RootProject");
        CompilationReference middleReference = CreateCompilationReference(
            """
            public enum MiddleKind
            {
                /// <inheritdoc cref="RootKind.Documented"/>
                Alias
            }
            """,
            assemblyName: "MiddleProject",
            additionalReferences: [rootReference]);
        const string source = """
            public enum LocalKind
            {
                /// <inheritdoc cref="MiddleKind.Alias"/>
                None
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new MemberXmlDocumentationAnalyzer(),
            source,
            additionalReferences: [middleReference]).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_InheritdocCrefChainThroughMetadataDeclarationId_ReportsNothing()
    {
        MetadataReference metadata = CreateMetadataReference(
            "public enum ExternalKind { Documented, Alias }",
            new Dictionary<string, string>
            {
                ["F:ExternalKind.Documented"] =
                    "<member name=\"F:ExternalKind.Documented\"><summary>The documented value.</summary></member>",
                ["F:ExternalKind.Alias"] =
                    "<member name=\"F:ExternalKind.Alias\"><inheritdoc cref=\"F:ExternalKind.Documented\"/></member>"
            });
        const string source = """
            public enum LocalKind
            {
                /// <inheritdoc cref="ExternalKind.Alias"/>
                None
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new MemberXmlDocumentationAnalyzer(),
            source,
            additionalReferences: [metadata]).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_InheritdocCrefChainsThroughMetadataMemberDeclarationIds_ReportNothing()
    {
        MetadataReference metadata = CreateMetadataReference(
            """
            public static class External
            {
                public static int RootField;
                public static int AliasField;
                public static int RootProperty { get; }
                public static int AliasProperty { get; }
                public static event System.Action RootEvent;
                public static event System.Action AliasEvent;
                public static void RootMethod() { }
                public static void AliasMethod() { }
            }
            """,
            new Dictionary<string, string>
            {
                ["F:External.RootField"] = "<member><summary>Field documentation.</summary></member>",
                ["F:External.AliasField"] = "<member><inheritdoc cref=\"F:External.RootField\"/></member>",
                ["P:External.RootProperty"] = "<member><summary>Property documentation.</summary></member>",
                ["P:External.AliasProperty"] =
                    "<member><inheritdoc cref=\"P:External.RootProperty\"/></member>",
                ["E:External.RootEvent"] = "<member><summary>Event documentation.</summary></member>",
                ["E:External.AliasEvent"] = "<member><inheritdoc cref=\"E:External.RootEvent\"/></member>",
                ["M:External.RootMethod"] = "<member><summary>Method documentation.</summary></member>",
                ["M:External.AliasMethod"] = "<member><inheritdoc cref=\"M:External.RootMethod\"/></member>"
            });
        const string source = """
            public class Sample
            {
                /// <inheritdoc cref="External.AliasField"/>
                public int Field;

                /// <inheritdoc cref="External.AliasProperty"/>
                public int Property { get; }

                /// <inheritdoc cref="External.AliasEvent"/>
                public event System.Action? Event;

                /// <inheritdoc cref="External.AliasMethod()"/>
                public void Method() { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new MemberXmlDocumentationAnalyzer(),
            source,
            additionalReferences: [metadata]).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_MetadataInheritdocDeclarationId_ResolvesWithinDeclaringAssembly()
    {
        PortableExecutableReference documentedReference = CreateMetadataReference(
            "public enum External { Root, Alias }",
            new Dictionary<string, string>
            {
                ["F:External.Root"] = "<member><summary>Root documentation.</summary></member>",
                ["F:External.Alias"] = "<member><inheritdoc cref=\"F:External.Root\"/></member>"
            },
            assemblyName: "DocumentedAssembly").WithAliases(["Documented"]);
        PortableExecutableReference undocumentedReference = CreateMetadataReference(
            "public enum External { Root, Alias }",
            new Dictionary<string, string>
            {
                ["F:External.Root"] = "<member><remarks>No summary.</remarks></member>"
            },
            assemblyName: "UndocumentedAssembly").WithAliases(["Undocumented"]);
        const string source = """
            extern alias Documented;

            public enum Local
            {
                /// <inheritdoc cref="Documented::External.Alias"/>
                None
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new MemberXmlDocumentationAnalyzer(),
            source,
            additionalReferences: [undocumentedReference, documentedReference]).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_MaximumDuplicateMetadataInheritdocReferences_ReportsNothing()
    {
        const int inheritdocCount = 4094;
        string inheritdocElements = string.Concat(
            Enumerable.Repeat("<inheritdoc cref=\"F:External.Root\"/>", inheritdocCount));
        PortableExecutableReference metadata = CreateMetadataReference(
            "public enum External { Root, Alias }",
            new Dictionary<string, string>
            {
                ["F:External.Root"] = "<member><summary>Root documentation.</summary></member>",
                ["F:External.Alias"] = $"<member>{inheritdocElements}</member>"
            }).WithAliases(["ExternalAlias"]);
        const string source = """
            extern alias ExternalAlias;

            public enum Local
            {
                /// <inheritdoc cref="ExternalAlias::External.Alias"/>
                None
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new MemberXmlDocumentationAnalyzer(),
            source,
            additionalReferences: [metadata]).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_MetadataInheritdocWithPath_DoesNotSatisfyDocumentation()
    {
        MetadataReference metadata = CreateMetadataReference(
            "public enum ExternalKind { Documented, Alias }",
            new Dictionary<string, string>
            {
                ["F:ExternalKind.Documented"] =
                    "<member name=\"F:ExternalKind.Documented\"><summary>The documented value.</summary></member>",
                ["F:ExternalKind.Alias"] =
                    "<member name=\"F:ExternalKind.Alias\"><inheritdoc cref=\"F:ExternalKind.Documented\" path=\"/summary\"/></member>"
            });
        const string source = """
            public enum LocalKind
            {
                /// <inheritdoc cref="ExternalKind.Alias"/>
                None
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new MemberXmlDocumentationAnalyzer(),
            source,
            additionalReferences: [metadata]).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
    }

    [TestMethod]
    public async Task AnalyzeMember_SourceInheritdocWithPath_DoesNotSatisfyDocumentation()
    {
        const string source = """
            internal static class Targets
            {
                /// <summary>Target documentation.</summary>
                public static void Target() { }
            }

            public class Sample
            {
                /// <inheritdoc cref="Targets.Target()" path="/summary"/>
                public void Run() { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, apiSurface: "public")
            .ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
    }

    [TestMethod]
    public async Task AnalyzeMember_InheritdocCrefToMetadataWithoutXml_ReportsNothing()
    {
        MetadataReference metadata = CreateMetadataReference(
            "public static class External { public static void Run() { } }",
            new Dictionary<string, string>());
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
    public async Task AnalyzeMember_AllMemberKindsInheritdocCrefToUndocumentedTarget_ReportEachMember()
    {
        const string source = """
            internal class UndocumentedTarget { }

            public class Sample
            {
                /// <inheritdoc cref="UndocumentedTarget"/>
                public int Field;

                /// <inheritdoc cref="UndocumentedTarget"/>
                public int Property { get; }

                /// <inheritdoc cref="UndocumentedTarget"/>
                public int this[int index] => index;

                /// <inheritdoc cref="UndocumentedTarget"/>
                public event System.Action? Changed;

                /// <inheritdoc cref="UndocumentedTarget"/>
                public Sample() { }

                /// <inheritdoc cref="UndocumentedTarget"/>
                static Sample() { }

                /// <inheritdoc cref="UndocumentedTarget"/>
                public void Run() { }

                /// <inheritdoc cref="UndocumentedTarget"/>
                public static Sample operator +(Sample left, Sample right) => left;

                /// <inheritdoc cref="UndocumentedTarget"/>
                public static implicit operator int(Sample value) => 0;
            }

            public enum Kind
            {
                /// <inheritdoc cref="UndocumentedTarget"/>
                None
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            source,
            apiSurface: "all",
            requireParameters: false,
            requireReturns: false).ConfigureAwait(false);

        diagnostics.Should().HaveCount(10);
        diagnostics.Should().OnlyContain(
            diagnostic => diagnostic.GetMessage().Contains(
                "<inheritdoc> does not resolve to a top-level <summary>",
                StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task AnalyzeMember_AllMemberKindsInheritdocCrefToDocumentedTarget_ReportNothing()
    {
        const string source = """
            /// <summary>Shared documentation.</summary>
            internal class DocumentedTarget { }

            public class Sample
            {
                /// <inheritdoc cref="DocumentedTarget"/>
                public int Field;

                /// <inheritdoc cref="DocumentedTarget"/>
                public int Property { get; }

                /// <inheritdoc cref="DocumentedTarget"/>
                public int this[int index] => index;

                /// <inheritdoc cref="DocumentedTarget"/>
                public event System.Action? Changed;

                /// <inheritdoc cref="DocumentedTarget"/>
                public Sample() { }

                /// <inheritdoc cref="DocumentedTarget"/>
                static Sample() { }

                /// <inheritdoc cref="DocumentedTarget"/>
                public void Run() { }

                /// <inheritdoc cref="DocumentedTarget"/>
                public static Sample operator +(Sample left, Sample right) => left;

                /// <inheritdoc cref="DocumentedTarget"/>
                public static implicit operator int(Sample value) => 0;
            }

            public enum Kind
            {
                /// <inheritdoc cref="DocumentedTarget"/>
                None
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            source,
            apiSurface: "all",
            requireParameters: false,
            requireReturns: false).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_AllSignatureShapesInheritdocCrefToUndocumentedTarget_ReportRequirements()
    {
        const string source = """
            internal class UndocumentedTarget { }

            /// <inheritdoc cref="UndocumentedTarget"/>
            public delegate int Transformer(int value);

            /// <inheritdoc cref="UndocumentedTarget"/>
            public class PrimarySample(int value);

            /// <inheritdoc cref="UndocumentedTarget"/>
            public record RecordSample(int Value);

            public static class Extensions
            {
                /// <inheritdoc cref="UndocumentedTarget"/>
                extension(string receiver)
                {
                    /// <summary>Gets the receiver length.</summary>
                    /// <returns>The receiver length.</returns>
                    public int GetLength() => receiver.Length;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzePreviewAsync(source).ConfigureAwait(false);

        diagnostics.Should().HaveCount(5);
        diagnostics.Count(IsParameterDocumentationDiagnostic).Should().Be(4);
        diagnostics.Should().ContainSingle(diagnostic => IsReturnDocumentationDiagnostic(diagnostic));
    }

    [TestMethod]
    public async Task AnalyzeMember_AllSignatureShapesInheritdocCrefToDocumentedTarget_ReportNothing()
    {
        const string source = """
            /// <summary>Shared documentation.</summary>
            internal class DocumentedTarget { }

            /// <inheritdoc cref="DocumentedTarget"/>
            public delegate int Transformer(int value);

            /// <inheritdoc cref="DocumentedTarget"/>
            public class PrimarySample(int value);

            /// <inheritdoc cref="DocumentedTarget"/>
            public record RecordSample(int Value);

            public static class Extensions
            {
                /// <inheritdoc cref="DocumentedTarget"/>
                extension(string receiver)
                {
                    public int GetLength() => receiver.Length;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzePreviewAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_BareInheritdocWithDocumentedOverrideOrInterfaceTarget_ReportsNothing()
    {
        const string source = """
            public abstract class Base
            {
                /// <summary>Runs the operation.</summary>
                public abstract void Run();
            }

            public sealed class Derived : Base
            {
                /// <inheritdoc/>
                public override void Run() { }
            }

            public interface IService
            {
                /// <summary>Executes the service.</summary>
                void Execute();
            }

            public sealed class Service : IService
            {
                /// <inheritdoc/>
                public void Execute() { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_InheritdocCrefChainEndingInSummary_ReportsNothing()
    {
        const string source = """
            internal static class Targets
            {
                /// <summary>Root documentation.</summary>
                public static void Root() { }

                /// <inheritdoc cref="Root()"/>
                public static void Middle() { }
            }

            public class Sample
            {
                /// <inheritdoc cref="Targets.Middle()"/>
                public void Run() { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, apiSurface: "public")
            .ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_CyclicInheritdocCrefs_ReportMember()
    {
        const string source = """
            internal static class Targets
            {
                /// <inheritdoc cref="Second()"/>
                public static void First() { }

                /// <inheritdoc cref="First()"/>
                public static void Second() { }
            }

            public class Sample
            {
                /// <inheritdoc cref="Targets.First()"/>
                public void Run() { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, apiSurface: "public")
            .ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.GetMessage().Should().Contain("<inheritdoc> does not resolve to a top-level <summary>");
    }

    [TestMethod]
    public async Task AnalyzeMember_ParametersAndReturnMissing_ReportsEach()
    {
        const string source = """
            public class Sample
            {
                /// <summary>Adds two values.</summary>
                public int Add(int left, int right) => left + right;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().HaveCount(3);
        diagnostics.Should().OnlyContain(diagnostic => diagnostic.Id == MemberXmlDocumentationAnalyzer.DiagnosticId);
        diagnostics.Count(IsParameterDocumentationDiagnostic).Should().Be(2);
        diagnostics.Should().ContainSingle(diagnostic => IsReturnDocumentationDiagnostic(diagnostic));
    }

    [TestMethod]
    public async Task AnalyzeMember_ParametersAndReturnDocumented_ReportsNothing()
    {
        const string source = """
            public class Sample
            {
                /// <summary>Adds two values.</summary>
                /// <param name="left">The left value.</param>
                /// <param name="right">The right value.</param>
                /// <returns>The sum.</returns>
                public int Add(int left, int right) => left + right;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_ParameterNameDoesNotMatch_ReportsActualParameter()
    {
        const string source = """
            public class Sample
            {
                /// <summary>Uses a value.</summary>
                /// <param name="other">The wrong parameter.</param>
                public void Use(int value) { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Id.Should().Be(MemberXmlDocumentationAnalyzer.DiagnosticId);
        IsParameterDocumentationDiagnostic(diagnostic).Should().BeTrue();
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).Should().Be("value");
    }

    [TestMethod]
    public async Task AnalyzeMember_ParameterRequirementDisabled_ReportsOnlyReturn()
    {
        const string source = """
            public class Sample
            {
                /// <summary>Gets a value.</summary>
                public int Get(int value) => value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            source,
            requireParameters: false).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        IsReturnDocumentationDiagnostic(diagnostic).Should().BeTrue();
    }

    [TestMethod]
    public async Task AnalyzeMember_ReturnRequirementDisabled_ReportsOnlyParameter()
    {
        const string source = """
            public class Sample
            {
                /// <summary>Gets a value.</summary>
                public int Get(int value) => value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            source,
            requireReturns: false).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        IsParameterDocumentationDiagnostic(diagnostic).Should().BeTrue();
    }

    [TestMethod]
    public async Task AnalyzeMember_BothSignatureRequirementsDisabled_ReportsNothing()
    {
        const string source = """
            public class Sample
            {
                /// <summary>Gets a value.</summary>
                public int Get(int value) => value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            source,
            requireParameters: false,
            requireReturns: false).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_DefaultVisibility_ReportsEveryNonPrivateAccessibility()
    {
        const string source = """
            public class Sample
            {
                public void PublicMethod() { }
                protected void ProtectedMethod() { }
                internal void InternalMethod() { }
                protected internal void ProtectedInternalMethod() { }
                private protected void PrivateProtectedMethod() { }
                private void PrivateMethod() { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().HaveCount(5);
        diagnostics.Should().OnlyContain(
            diagnostic => diagnostic.Id == MemberXmlDocumentationAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task AnalyzeMember_PrivateSurface_ReportsOnlyPrivateMember()
    {
        const string source = """
            public class Sample
            {
                public void PublicMethod() { }
                private void PrivateMethod() { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            source,
            apiSurface: "private").ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan)
            .Should().Be("PrivateMethod");
    }

    [TestMethod]
    public async Task AnalyzeMember_EffectivePublicAndInternalSurfaces_ReportSelectedMembers()
    {
        const string source = """
            public class Sample
            {
                public void PublicMethod() { }
                protected void ProtectedMethod() { }
                private protected void PrivateProtectedMethod() { }
                private void PrivateMethod() { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            source,
            apiSurface: " PUBLIC, internal ").ConfigureAwait(false);

        diagnostics.Should().HaveCount(3);
        diagnostics.Select(diagnostic => diagnostic.Location.SourceTree!.GetText()
            .ToString(diagnostic.Location.SourceSpan)).Should().BeEquivalentTo(
                "PublicMethod",
                "ProtectedMethod",
                "PrivateProtectedMethod");
    }

    [TestMethod]
    public async Task AnalyzeMember_PublicMemberInInternalType_UsesInternalSurface()
    {
        const string source = "internal class Sample { public void Run() { } }";

        ImmutableArray<Diagnostic> publicDiagnostics = await AnalyzeAsync(
            source,
            apiSurface: "public").ConfigureAwait(false);
        ImmutableArray<Diagnostic> internalDiagnostics = await AnalyzeAsync(
            source,
            apiSurface: "internal").ConfigureAwait(false);

        publicDiagnostics.Should().BeEmpty();
        internalDiagnostics.Should().ContainSingle();
    }

    [TestMethod]
    public async Task AnalyzeMember_PublicMemberInPrivateNestedType_UsesPrivateSurface()
    {
        const string source = """
            public class Outer
            {
                private class Inner
                {
                    public void Run() { }
                }
            }
            """;

        ImmutableArray<Diagnostic> defaultDiagnostics = await AnalyzeAsync(source).ConfigureAwait(false);
        ImmutableArray<Diagnostic> privateDiagnostics = await AnalyzeAsync(
            source,
            apiSurface: "private").ConfigureAwait(false);

        defaultDiagnostics.Should().BeEmpty();
        privateDiagnostics.Should().ContainSingle();
    }

    [TestMethod]
    public async Task AnalyzeMember_FileSurfaceIsInvalid_FallsBackToDefault()
    {
        const string source = """
            file class Sample
            {
                public void PublicMethod() { }
                private void PrivateMethod() { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            source,
            apiSurface: "file").ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan)
            .Should().Be("PublicMethod");
    }

    [TestMethod]
    public async Task AnalyzeMember_FieldsPropertiesEventsConstructorsAndEnumValues_Report()
    {
        const string source = """
            public class Sample
            {
                public int Field;
                public int Property { get; }
                public event System.Action? Changed;
                public Sample() { }
            }

            public enum Kind
            {
                None
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            source,
            requireParameters: false,
            requireReturns: false).ConfigureAwait(false);

        diagnostics.Should().HaveCount(5);
        diagnostics.Select(diagnostic => diagnostic.Location.SourceTree!.GetText()
            .ToString(diagnostic.Location.SourceSpan)).Should().BeEquivalentTo(
                "Field",
                "Property",
                "Changed",
                "Sample",
                "None");
    }

    [TestMethod]
    public async Task AnalyzeMember_Destructor_ReportsNothing()
    {
        const string source = "public class Sample { ~Sample() { } }";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_MultiVariableFieldSummary_AppliesToEachField()
    {
        const string source = """
            public class Sample
            {
                /// <summary>Stored values.</summary>
                public int First, Second;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_MultiVariableFieldWithoutSummary_ReportsEachField()
    {
        const string source = "public class Sample { public int First, Second; }";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().HaveCount(2);
    }

    [TestMethod]
    public async Task AnalyzeMember_AutoPropertyBackingField_IsNotReported()
    {
        const string source = """
            public class Sample
            {
                /// <summary>A value.</summary>
                public int Value { get; set; }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_IndexerRequiresParameterButNotReturns()
    {
        const string source = """
            public class Sample
            {
                /// <summary>Gets a value by index.</summary>
                public int this[int index] => index;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        IsParameterDocumentationDiagnostic(diagnostic).Should().BeTrue();
    }

    [TestMethod]
    public async Task AnalyzeMember_ValueElementDoesNotDocumentMethodReturn()
    {
        const string source = """
            public class Sample
            {
                /// <summary>Gets a value.</summary>
                /// <value>The value.</value>
                public int Get() => 0;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        IsReturnDocumentationDiagnostic(diagnostic).Should().BeTrue();
    }

    [TestMethod]
    public async Task AnalyzeMember_OperatorRequiresMemberParametersAndReturnDocumentation()
    {
        const string source = """
            public readonly struct Number
            {
                public static Number operator +(Number left, Number right) => left;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().HaveCount(4);
        diagnostics.Should().OnlyContain(diagnostic => diagnostic.Id == MemberXmlDocumentationAnalyzer.DiagnosticId);
        diagnostics.Should().ContainSingle(diagnostic => IsMemberDocumentationDiagnostic(diagnostic));
        diagnostics.Count(IsParameterDocumentationDiagnostic).Should().Be(2);
        diagnostics.Should().ContainSingle(diagnostic => IsReturnDocumentationDiagnostic(diagnostic));
    }

    [TestMethod]
    public async Task AnalyzeMember_ConversionOperatorRequiresDocumentation()
    {
        const string source = """
            public readonly struct Number
            {
                public static implicit operator int(Number value) => 0;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().HaveCount(3);
        diagnostics.Should().OnlyContain(diagnostic => diagnostic.Id == MemberXmlDocumentationAnalyzer.DiagnosticId);
        diagnostics.Should().ContainSingle(diagnostic => IsMemberDocumentationDiagnostic(diagnostic));
        diagnostics.Should().ContainSingle(diagnostic => IsParameterDocumentationDiagnostic(diagnostic));
        diagnostics.Should().ContainSingle(diagnostic => IsReturnDocumentationDiagnostic(diagnostic));
    }

    [TestMethod]
    public async Task AnalyzeMember_DelegateSignatureDocumented_ReportsNothing()
    {
        const string source = """
            /// <summary>Transforms a value.</summary>
            /// <param name="value">The value.</param>
            /// <returns>The transformed value.</returns>
            public delegate int Transformer(int value);
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_DelegateMissingSignatureTags_ReportsParameterAndReturnOnly()
    {
        const string source = """
            /// <summary>Transforms a value.</summary>
            public delegate int Transformer(int value);
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().HaveCount(2);
        diagnostics.Should().OnlyContain(diagnostic => diagnostic.Id == MemberXmlDocumentationAnalyzer.DiagnosticId);
        diagnostics.Should().ContainSingle(diagnostic => IsParameterDocumentationDiagnostic(diagnostic));
        diagnostics.Should().ContainSingle(diagnostic => IsReturnDocumentationDiagnostic(diagnostic));
    }

    [TestMethod]
    public async Task AnalyzeMember_PrimaryConstructorParameterDocumentedOnType_ReportsNothing()
    {
        const string source = """
            /// <summary>A sample.</summary>
            /// <param name="value">The value.</param>
            public class Sample(int value);
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_PrimaryConstructorMissingParameterTag_ReportsParameterOnly()
    {
        const string source = """
            /// <summary>A sample.</summary>
            public class Sample(int value);
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        IsParameterDocumentationDiagnostic(diagnostic).Should().BeTrue();
    }

    [TestMethod]
    public async Task AnalyzeMember_RecordPrimaryConstructorParameterDocumentedOnType_ReportsNothing()
    {
        const string source = """
            /// <summary>A sample.</summary>
            /// <param name="Value">The value.</param>
            public record Sample(int Value);
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_DerivedPrimaryConstructorsWithBareInheritdoc_ReportNothing()
    {
        const string source = """
            /// <summary>A base class.</summary>
            public class Base { }

            /// <inheritdoc/>
            public class Derived(int value) : Base { }

            /// <summary>A base record.</summary>
            /// <param name="Value">The value.</param>
            public record BaseRecord(int Value);

            /// <inheritdoc/>
            public record DerivedRecord(int Value) : BaseRecord(Value);
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_InheritdocCrefChainThroughPrimaryConstructor_ReportsNothing()
    {
        const string source = """
            /// <summary>A documented base.</summary>
            public class Base { }

            /// <inheritdoc/>
            public class Middle(int value) : Base { }

            public class Sample
            {
                /// <inheritdoc cref="Middle.Middle(int)"/>
                public void Run() { }
            }
            """;
        SyntaxTree tree = CSharpSyntaxTree.ParseText(source);
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "PrimaryConstructorTarget",
            syntaxTrees: [tree],
            references: RoslynTestEnvironment.References,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        XmlCrefAttributeSyntax cref = tree.GetRoot().DescendantNodes(descendIntoTrivia: true)
            .OfType<XmlCrefAttributeSyntax>()
            .Single();
        IMethodSymbol target = compilation.GetSemanticModel(tree).GetSymbolInfo(cref.Cref).Symbol
            .Should().BeAssignableTo<IMethodSymbol>().Subject;
        target.MethodKind.Should().Be(MethodKind.Constructor);
        target.DeclaringSyntaxReferences.Should().ContainSingle()
            .Which.GetSyntax().Should().BeAssignableTo<TypeDeclarationSyntax>();

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_PartialMethodDocumentationOnImplementation_ReportsNothing()
    {
        IReadOnlyList<(string Source, string FileName)> sources =
        [
            (
                "public partial class Sample { public partial int Transform(int value); }",
                "Sample.cs"),
            (
                """
                public partial class Sample
                {
                    /// <summary>Transforms a value.</summary>
                    /// <param name="value">The value.</param>
                    /// <returns>The transformed value.</returns>
                    public partial int Transform(int value) => value;
                }
                """,
                "Sample.Transform.cs")
        ];

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(sources).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_UndocumentedPartialMethod_ReportsOncePerRequirement()
    {
        const string source = """
            public partial class Sample
            {
                public partial int Transform(int value);
                public partial int Transform(int value) => value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().HaveCount(3);
        diagnostics.Should().OnlyContain(diagnostic => diagnostic.Id == MemberXmlDocumentationAnalyzer.DiagnosticId);
        diagnostics.Should().ContainSingle(diagnostic => IsMemberDocumentationDiagnostic(diagnostic));
        diagnostics.Should().ContainSingle(diagnostic => IsParameterDocumentationDiagnostic(diagnostic));
        diagnostics.Should().ContainSingle(diagnostic => IsReturnDocumentationDiagnostic(diagnostic));
    }

    [TestMethod]
    public async Task AnalyzeMember_PartialMethodImplementationParameterName_IsMatchedByOrdinal()
    {
        const string source = """
            public partial class Sample
            {
                public partial void Transform(int value);

                /// <summary>Transforms a value.</summary>
                /// <param name="input">The input value.</param>
                public partial void Transform(int input) { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_CrossedPartialParameterNames_OneTagDoesNotDocumentBothOrdinals()
    {
        const string source = """
            public partial class Sample
            {
                public partial void Transform(int left, int right);

                /// <summary>Transforms values.</summary>
                /// <param name="right">The first implementation parameter.</param>
                public partial void Transform(int right, int left) { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        IsParameterDocumentationDiagnostic(diagnostic).Should().BeTrue();
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).Should().Be("right");
    }

    [TestMethod]
    public async Task AnalyzeMember_PartialPropertyDocumentationOnImplementation_ReportsNothing()
    {
        const string source = """
            public partial class Sample
            {
                public partial int Value { get; }

                /// <summary>The value.</summary>
                public partial int Value => 0;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_GeneratedPartialImplementationDocumentationDoesNotSatisfyUserDeclaration()
    {
        IReadOnlyList<(string Source, string FileName)> sources =
        [
            (
                "public partial class Sample { public partial void Run(); }",
                "Sample.cs"),
            (
                """
                // <auto-generated/>
                public partial class Sample
                {
                    /// <summary>Generated documentation.</summary>
                    public partial void Run() { }
                }
                """,
                "Sample.g.cs")
        ];

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(sources).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(MemberXmlDocumentationAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task AnalyzeMember_GeneratedPartialDefinitionDoesNotHideUserImplementation()
    {
        IReadOnlyList<(string Source, string FileName)> sources =
        [
            (
                """
                // <auto-generated/>
                public partial class Sample
                {
                    public partial void Run();
                }
                """,
                "Sample.g.cs"),
            (
                """
                public partial class Sample
                {
                    /// <summary>Runs the operation.</summary>
                    public partial void Run() { }
                }
                """,
                "Sample.cs")
        ];

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(sources).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_GeneratedPartialDefinition_ReportsParameterOnUserImplementation()
    {
        IReadOnlyList<(string Source, string FileName)> sources =
        [
            (
                """
                // <auto-generated/>
                public partial class Sample
                {
                    public partial void Run(int generatedValue);
                }
                """,
                "Sample.g.cs"),
            (
                """
                public partial class Sample
                {
                    /// <summary>Runs the operation.</summary>
                    public partial void Run(int input) { }
                }
                """,
                "Sample.cs")
        ];

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(sources).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        IsParameterDocumentationDiagnostic(diagnostic).Should().BeTrue();
        diagnostic.Location.SourceTree!.FilePath.Should().Be("Sample.cs");
        diagnostic.Location.SourceTree.GetText().ToString(diagnostic.Location.SourceSpan).Should().Be("input");
        diagnostic.GetMessage().Should().Contain("parameter 'input'");
    }

    [TestMethod]
    public async Task AnalyzeMember_DocumentedSourceBase_ExemptsOverride()
    {
        const string source = """
            public abstract class Base
            {
                /// <summary>Runs the operation.</summary>
                public abstract void Run();
            }

            public sealed class Derived : Base
            {
                public override void Run() { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_DocumentationOnGrandbase_ExemptsOverrideChain()
    {
        const string source = """
            public abstract class Base
            {
                /// <summary>Runs the operation.</summary>
                public abstract void Run();
            }

            public abstract class Middle : Base
            {
                public override void Run() { }
            }

            public sealed class Derived : Middle
            {
                public override void Run() { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_DocumentationOnPartialBaseImplementation_ExemptsOverride()
    {
        const string source = """
            public abstract partial class Base
            {
                public virtual partial void Run();

                /// <summary>Runs the operation.</summary>
                public virtual partial void Run() { }
            }

            public sealed class Derived : Base
            {
                public override void Run() { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            source,
            requireParameters: false,
            requireReturns: false).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_FullyUndocumentedSourceHierarchy_ReportsBaseAndOverride()
    {
        const string source = """
            public abstract class Base
            {
                public abstract void Run();
            }

            public sealed class Derived : Base
            {
                public override void Run() { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().HaveCount(2);
    }

    [TestMethod]
    public async Task AnalyzeMember_GeneratedIntermediateDocumentationDoesNotHideUndocumentedGrandbase()
    {
        const string source = """
            public abstract class Base
            {
                public abstract void Run();
            }

            [System.CodeDom.Compiler.GeneratedCode("test", "1.0")]
            public class Middle : Base
            {
                /// <summary>Generated documentation.</summary>
                public override void Run() { }
            }

            public sealed class Derived : Middle
            {
                public override void Run() { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().HaveCount(2);
        diagnostics.Should().OnlyContain(
            diagnostic => diagnostic.Id == MemberXmlDocumentationAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task AnalyzeMember_GeneratedHierarchyRootDocumentationDoesNotExemptOverride()
    {
        const string source = """
            [System.CodeDom.Compiler.GeneratedCode("test", "1.0")]
            public abstract class Base
            {
                /// <summary>Generated documentation.</summary>
                public abstract void Run();
            }

            public sealed class Derived : Base
            {
                public override void Run() { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(MemberXmlDocumentationAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task AnalyzeMember_MetadataHierarchyWithoutXml_IsSkipped()
    {
        const string source = "public sealed class Sample { public override string ToString() => string.Empty; }";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_MetadataHierarchyWithXml_ExemptsOverride()
    {
        MetadataReference metadata = CreateMetadataReference(
            "public abstract class Base { public abstract void Run(); }",
            new Dictionary<string, string>
            {
                ["M:Base.Run"] = "<member name=\"M:Base.Run\"><summary>Runs the operation.</summary></member>"
            });
        const string source = "public sealed class Derived : Base { public override void Run() { } }";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new MemberXmlDocumentationAnalyzer(),
            source,
            additionalReferences: [metadata]).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_OversizedMetadataDocumentation_IsTreatedAsUnknown()
    {
        string oversizedDocumentation =
            $"<member name=\"M:Base.Run\"><summary>{new string('x', 1024 * 1024)}</summary></member>";
        MetadataReference metadata = CreateMetadataReference(
            "public abstract class Base { public abstract void Run(); }",
            new Dictionary<string, string> { ["M:Base.Run"] = oversizedDocumentation });
        const string source = "public sealed class Derived : Base { public override void Run() { } }";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new MemberXmlDocumentationAnalyzer(),
            source,
            additionalReferences: [metadata]).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_OverrideWithLocalSummaryStillRequiresReturnTag()
    {
        const string source = """
            public sealed class Sample
            {
                /// <summary>Returns this instance as text.</summary>
                public override string ToString() => string.Empty;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        IsReturnDocumentationDiagnostic(diagnostic).Should().BeTrue();
    }

    [TestMethod]
    public async Task AnalyzeMember_DocumentedInterfaceExemptsExplicitImplementation()
    {
        const string source = """
            public interface IService
            {
                /// <summary>Runs the service.</summary>
                void Run();
            }

            public sealed class Service : IService
            {
                void IService.Run() { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            source,
            apiSurface: "private").ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_UndocumentedInterfaceRequiresExplicitImplementationDocumentation()
    {
        const string source = """
            public interface IService
            {
                void Run();
            }

            public sealed class Service : IService
            {
                void IService.Run() { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            source,
            apiSurface: "private").ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(MemberXmlDocumentationAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task AnalyzeMember_DocumentedInterfaceExemptsExplicitPropertyAndEvent()
    {
        const string source = """
            public interface IService
            {
                /// <summary>The value.</summary>
                int Value { get; }

                /// <summary>Raised when the value changes.</summary>
                event System.Action Changed;
            }

            public sealed class Service : IService
            {
                int IService.Value => 0;
                event System.Action IService.Changed { add { } remove { } }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            source,
            apiSurface: "private").ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_ImplicitInterfaceImplementationStillRequiresDocumentation()
    {
        const string source = """
            public interface IService
            {
                /// <summary>Runs the service.</summary>
                void Run();
            }

            public sealed class Service : IService
            {
                public void Run() { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(MemberXmlDocumentationAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task AnalyzeMember_MalformedSummary_DoesNotSatisfyMemberDocumentation()
    {
        const string source = """
            public class Sample
            {
                /// <summary>Unclosed summary.
                public void Run() { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(MemberXmlDocumentationAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task AnalyzeMember_GeneratedFile_ReportsNothing()
    {
        const string source = "public class Sample { public int Transform(int value) => value; }";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            source,
            fileName: "Sample.g.cs").ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_GeneratedCodeAttribute_ReportsNothing()
    {
        const string source = """
            public class Sample
            {
                [System.CodeDom.Compiler.GeneratedCode("test", "1.0")]
                public int Transform(int value) => value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_ExtensionMemberInGeneratedOuterType_ReportsNothing()
    {
        const string source = """
            [System.CodeDom.Compiler.GeneratedCode("test", "1.0")]
            public static class Extensions
            {
                extension(string receiver)
                {
                    public int GetLength() => receiver.Length;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzePreviewAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_EnumMemberInGeneratedEnum_ReportsNothing()
    {
        const string source = """
            [System.CodeDom.Compiler.GeneratedCode("test", "1.0")]
            public enum Kind
            {
                None
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_EnumMemberInGeneratedOuterType_ReportsNothing()
    {
        const string source = """
            [System.CodeDom.Compiler.GeneratedCode("test", "1.0")]
            public class Outer
            {
                public enum Kind
                {
                    None
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_GeneratedCodeOption_ReportsNothing()
    {
        const string source = "public class Sample { public int Transform(int value) => value; }";
        Dictionary<string, string> options = new()
        {
            ["generated_code"] = "true"
        };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new MemberXmlDocumentationAnalyzer(),
            source,
            options).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_ExtensionBlockReceiverWithoutParamTag_ReportsParameter()
    {
        const string source = """
            public static class Extensions
            {
                extension(string receiver)
                {
                    /// <summary>Gets the receiver length.</summary>
                    /// <returns>The receiver length.</returns>
                    public int GetLength() => receiver.Length;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzePreviewAsync(source).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        IsParameterDocumentationDiagnostic(diagnostic).Should().BeTrue();
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).Should().Be("receiver");
        diagnostic.GetMessage().Should().Contain("Member 'extension(receiver)'");
    }

    [TestMethod]
    public async Task AnalyzeMember_ExtensionBlockDocumentationCopiedToContainedMember_ReportsNothing()
    {
        const string source = """
            public static class Extensions
            {
                /// <summary>Provides receiver information.</summary>
                /// <param name="receiver">The receiver.</param>
                extension(string receiver)
                {
                    /// <returns>The receiver length.</returns>
                    public int GetLength() => receiver.Length;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzePreviewAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_PrivateOnlyExtensionBlock_UsesPrivateSurface()
    {
        const string source = """
            public static class Extensions
            {
                extension(string receiver)
                {
                    /// <summary>Gets the receiver length.</summary>
                    /// <returns>The receiver length.</returns>
                    private int GetLength() => receiver.Length;
                }
            }
            """;

        ImmutableArray<Diagnostic> defaultDiagnostics = await AnalyzePreviewAsync(source).ConfigureAwait(false);
        ImmutableArray<Diagnostic> privateDiagnostics = await AnalyzePreviewAsync(
            source,
            apiSurface: "private").ConfigureAwait(false);

        defaultDiagnostics.Should().BeEmpty();
        Diagnostic diagnostic = privateDiagnostics.Should().ContainSingle().Subject;
        IsParameterDocumentationDiagnostic(diagnostic).Should().BeTrue();
    }

    [TestMethod]
    public async Task AnalyzeMember_UnnamedExtensionReceiver_ReportsNothing()
    {
        const string source = """
            public static class Extensions
            {
                extension(System.ArgumentException)
                {
                    /// <summary>Throws an argument exception.</summary>
                    public static void Throw() { }
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzePreviewAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_DeepGeneratedContainingTypes_DoesNotCrash()
    {
        const int depth = 256;
        string prefixes = string.Concat(Enumerable.Range(0, depth).Select(index => $"public class C{index} {{"));
        string source = $"[System.CodeDom.Compiler.GeneratedCode(\"test\", \"1.0\")] public class Root {{"
            + prefixes
            + "public void Run() { }"
            + new string('}', depth + 1);

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_MemberNestedInGeneratedType_ReportsNothing()
    {
        const string source = """
            [System.CodeDom.Compiler.GeneratedCode("test", "1.0")]
            public class Sample
            {
                public int Transform(int value) => value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeMember_HiddenLine_ReportsNothing()
    {
        const string source = """
            public class Sample
            {
            #line hidden
                public int Transform(int value) => value;
            #line default
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    private sealed class TestDocumentationProvider(IReadOnlyDictionary<string, string> documentation)
        : DocumentationProvider
    {
        protected override string GetDocumentationForSymbol(
            string documentationMemberId,
            CultureInfo? preferredCulture,
            CancellationToken cancellationToken) =>
            documentation.TryGetValue(documentationMemberId, out string? xml) ? xml : string.Empty;

        public override bool Equals(object? obj) => ReferenceEquals(this, obj);

        public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
    }

    [TestMethod]
    public void SupportedDiagnostics_Always_ContainsSingleRule()
    {
        MemberXmlDocumentationAnalyzer analyzer = new();

        DiagnosticDescriptor descriptor = analyzer.SupportedDiagnostics.Should().ContainSingle().Subject;
        descriptor.Id.Should().Be(MemberXmlDocumentationAnalyzer.DiagnosticId);
    }
}