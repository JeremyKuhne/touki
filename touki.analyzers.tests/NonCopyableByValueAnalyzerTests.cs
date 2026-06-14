// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Analyzers;

[TestClass]
public class NonCopyableByValueAnalyzerTests
{
    // A non-ref [NonCopyable] struct so boxing, fields, and by-value passing are all legal (not blocked by the
    // ref-struct rules), letting each by-value path be exercised directly. The marker attribute is declared as
    // Touki.NonCopyableAttribute so it matches the fully qualified name the analyzer resolves.
    private const string Types = """
        using System;
        using Touki;

        namespace Touki
        {
            [AttributeUsage(AttributeTargets.Struct)]
            sealed class NonCopyableAttribute : Attribute { }
        }

        [NonCopyable]
        struct Pooled
        {
            public int Value;
        }

        struct Plain
        {
            public int Value;
        }

        """;

    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source) =>
        AnalyzerTestHarness.GetDiagnosticsAsync(new NonCopyableByValueAnalyzer(), source);

    [TestMethod]
    public async Task ByValueArgument_OfNonCopyable_Reports()
    {
        string source = Types + """
            class C
            {
                static void Take(Pooled p) { }
                static void M(Pooled p) => Take(p);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(NonCopyableByValueAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task RefArgument_OfNonCopyable_ReportsNothing()
    {
        string source = Types + """
            class C
            {
                static void Take(ref Pooled p) { }
                static void M(ref Pooled p) => Take(ref p);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task FreshlyCreatedArgument_IsMoveNotCopy_ReportsNothing()
    {
        string source = Types + """
            class C
            {
                static void Take(Pooled p) { }
                static void M() => Take(new Pooled());
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ByValueReturn_OfNonCopyable_Reports()
    {
        string source = Types + """
            class C
            {
                static Pooled M(Pooled p) => p;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(NonCopyableByValueAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task RefReadonlyReturn_OfNonCopyable_ReportsNothing()
    {
        // A 'ref readonly' return hands back the original location, not a copy.
        string source = Types + """
            class C
            {
                static Pooled s_pooled;
                static ref readonly Pooled M() => ref s_pooled;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task RefReturn_OfNonCopyable_ReportsNothing()
    {
        string source = Types + """
            class C
            {
                static Pooled s_pooled;
                static ref Pooled M() => ref s_pooled;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AssignmentFromLocation_OfNonCopyable_Reports()
    {
        string source = Types + """
            class C
            {
                static void M(Pooled a)
                {
                    Pooled b = default;
                    b = a;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(NonCopyableByValueAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task DeclarationFromLocation_OfNonCopyable_Reports()
    {
        string source = Types + """
            class C
            {
                static void M(Pooled a)
                {
                    Pooled b = a;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(NonCopyableByValueAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task RefLocalAlias_OfNonCopyable_ReportsNothing()
    {
        string source = Types + """
            class C
            {
                static void M(ref Pooled a)
                {
                    ref Pooled b = ref a;
                    b.Value = 1;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Boxing_OfNonCopyable_Reports()
    {
        string source = Types + """
            class C
            {
                static object M(Pooled p) => p;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(NonCopyableByValueAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task NonCopyableField_InCopyableStruct_Reports()
    {
        string source = Types + """
            struct Container
            {
                public Pooled Buffer;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Id.Should().Be(NonCopyableByValueAnalyzer.DiagnosticId);

        // The field case must not advise "pass by ref/in" (you cannot pass a field by ref to fix it); it advises
        // marking the containing type [NonCopyable] instead.
        string message = diagnostic.GetMessage();
        message.Should().Contain("[NonCopyable]");
        message.Should().NotContain("pass it by 'ref' or 'in'");
    }

    [TestMethod]
    public async Task ByValueArgument_OfPlainStruct_ReportsNothing()
    {
        string source = Types + """
            class C
            {
                static void Take(Plain p) { }
                static void M(Plain p) => Take(p);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }
}
