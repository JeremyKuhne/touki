// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Analyzers;

[TestClass]
public class UseDisposableBaseAnalyzerTests
{
    private const string DisposableBase = """
        using System;

        namespace Touki
        {
            public abstract class DisposableBase : IDisposable
            {
                public void Dispose() { }
            }
        }

        """;

    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source, string? fileName = null) =>
        AnalyzerTestHarness.GetDiagnosticsAsync(
            new UseDisposableBaseAnalyzer(),
            source,
            fileName: fileName,
            diagnosticOptions: new Dictionary<string, ReportDiagnostic>
            {
                [UseDisposableBaseAnalyzer.DiagnosticId] = ReportDiagnostic.Warn
            });

    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        IReadOnlyList<(string Source, string FileName)> sources) =>
        AnalyzerTestHarness.GetDiagnosticsAsync(
            new UseDisposableBaseAnalyzer(),
            sources,
            diagnosticOptions: new Dictionary<string, ReportDiagnostic>
            {
                [UseDisposableBaseAnalyzer.DiagnosticId] = ReportDiagnostic.Warn
            });

    [TestMethod]
    public async Task AnalyzeNamedType_DirectIDisposableClass_ReportsAtIdentifier()
    {
        string source = DisposableBase + """
            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Id.Should().Be(UseDisposableBaseAnalyzer.DiagnosticId);
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).Should().Be("Resource");
    }

    [TestMethod]
    public async Task AnalyzeNamedType_ExplicitIDisposableImplementation_Reports()
    {
        string source = DisposableBase + """
            sealed class Resource : IDisposable
            {
                void IDisposable.Dispose() { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
    }

    [TestMethod]
    public async Task AnalyzeNamedType_AbstractIDisposableClass_Reports()
    {
        string source = DisposableBase + """
            abstract class Resource : IDisposable
            {
                public abstract void Dispose();
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
    }

    [TestMethod]
    public async Task AnalyzeNamedType_ClassWithAnotherBase_Reports()
    {
        string source = DisposableBase + """
            class Parent { }

            sealed class Resource : Parent, IDisposable
            {
                public void Dispose() { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
    }

    [TestMethod]
    public async Task AnalyzeNamedType_PartialClass_ReportsOnce()
    {
        string source = DisposableBase + """
            partial class Resource : IDisposable
            {
                public void Dispose() { }
            }

            partial class Resource { }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
    }

    [TestMethod]
    public async Task AnalyzeNamedType_PartialClassDeclaresIDisposableTwice_ReportsOnce()
    {
        string source = DisposableBase + """
            partial class Resource : IDisposable
            {
                public void Dispose() { }
            }

            partial class Resource : IDisposable { }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
    }

    [TestMethod]
    public async Task AnalyzeNamedType_RecordClass_Reports()
    {
        string source = DisposableBase + """
            sealed record Resource : IDisposable
            {
                public void Dispose() { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
    }

    [TestMethod]
    public async Task AnalyzeNamedType_RecordStruct_ReportsNothing()
    {
        string source = DisposableBase + """
            readonly record struct Resource : IDisposable
            {
                public void Dispose() { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeNamedType_UserPartialDeclaresIDisposable_ReportsUserDeclaration()
    {
        IReadOnlyList<(string Source, string FileName)> sources =
        [
            (DisposableBase, "DisposableBase.cs"),
            ("// <auto-generated/>\npartial class Resource { }", "Resource.g.cs"),
            ("partial class Resource : System.IDisposable { public void Dispose() { } }", "Resource.cs")
        ];

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(sources).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Location.SourceTree!.FilePath.Should().Be("Resource.cs");
    }

    [TestMethod]
    public async Task AnalyzeNamedType_OnlyGeneratedPartialDeclaresIDisposable_ReportsNothing()
    {
        IReadOnlyList<(string Source, string FileName)> sources =
        [
            (DisposableBase, "DisposableBase.cs"),
            (
                "// <auto-generated/>\npartial class Resource : System.IDisposable { public void Dispose() { } }",
                "Resource.g.cs"),
            ("partial class Resource { }", "Resource.cs")
        ];

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(sources).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeNamedType_DisposableBase_ReportsNothing()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(DisposableBase).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeNamedType_DerivedFromDisposableBase_ReportsNothing()
    {
        string source = DisposableBase + """
            sealed class Resource : Touki.DisposableBase
            {
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeNamedType_IndirectlyDerivedFromDisposableBase_ReportsNothing()
    {
        string source = DisposableBase + """
            abstract class ResourceBase : Touki.DisposableBase
            {
            }

            sealed class Resource : ResourceBase, IDisposable
            {
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeNamedType_DerivedFromDisposableBaseAndRedundantInterface_ReportsNothing()
    {
        string source = DisposableBase + """
            sealed class Resource : Touki.DisposableBase, IDisposable
            {
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeNamedType_BaseClassDirectlyImplementsIDisposable_ReportsBaseClass()
    {
        string source = DisposableBase + """
            class Parent : IDisposable
            {
                public void Dispose() { }
            }

            sealed class Resource : Parent
            {
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Contain("Parent");
    }

    [TestMethod]
    public async Task AnalyzeNamedType_ImplementsInterfaceDerivedFromIDisposable_ReportsNothing()
    {
        string source = DisposableBase + """
            interface IResource : IDisposable
            {
            }

            sealed class Resource : IResource
            {
                public void Dispose() { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeNamedType_IDisposableStruct_ReportsNothing()
    {
        string source = DisposableBase + """
            struct Resource : IDisposable
            {
                public void Dispose() { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeNamedType_NoDisposableBaseInCompilation_ReportsNothing()
    {
        const string source = """
            using System;

            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeNamedType_GeneratedClass_ReportsNothing()
    {
        string source = "// <auto-generated/>\n" + DisposableBase + """
            sealed class Resource : IDisposable
            {
                public void Dispose() { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, "Resource.g.cs").ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }
}
