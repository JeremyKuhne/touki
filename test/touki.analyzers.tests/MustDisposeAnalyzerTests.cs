// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Analyzers;

[TestClass]
public class MustDisposeAnalyzerTests
{
    // Shared sample types. The marker attribute is declared as Touki.MustDisposeAttribute so it matches the fully
    // qualified name the analyzer resolves. 'Scope' is the common ref-struct scope (pattern Dispose, no
    // IDisposable, an implicit conversion to exercise the "use is not disposal" path). 'Resource' is a
    // [MustDispose] class. 'Plain' owns nothing and must never be flagged.
    private const string Types = """
        using System;
        using Touki;

        namespace Touki
        {
            [AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
            sealed class MustDisposeAttribute : Attribute { }
        }

        [MustDispose]
        ref struct Scope
        {
            public void Dispose() { }
            public int Length => 0;
            public static implicit operator int(in Scope scope) => scope.Length;
        }

        [MustDispose]
        sealed class Resource : IDisposable
        {
            public void Dispose() { }
        }

        struct Plain
        {
            public void Dispose() { }
        }

        """;

    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source) =>
        AnalyzerTestHarness.GetDiagnosticsAsync(new MustDisposeAnalyzer(), source);

    [TestMethod]
    public async Task UndisposedLocal_Reports()
    {
        string source = Types + """
            class C
            {
                static void M()
                {
                    Scope s = new Scope();
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(MustDisposeAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task UsingDeclaration_ReportsNothing()
    {
        string source = Types + """
            class C
            {
                static void M()
                {
                    using Scope s = new Scope();
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task UsingStatement_OfExistingLocal_ReportsNothing()
    {
        string source = Types + """
            class C
            {
                static void M()
                {
                    Scope s = new Scope();
                    using (s) { }
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ExplicitDispose_ReportsNothing()
    {
        string source = Types + """
            class C
            {
                static void M()
                {
                    Scope s = new Scope();
                    s.Dispose();
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task TryFinallyDispose_ReportsNothing()
    {
        string source = Types + """
            class C
            {
                static void M()
                {
                    Scope s = new Scope();
                    try { }
                    finally { s.Dispose(); }
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Returned_ReportsNothing()
    {
        string source = Types + """
            class C
            {
                static Scope M()
                {
                    Scope s = new Scope();
                    return s;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ReturnedThroughTernary_ReportsNothing()
    {
        string source = Types + """
            class C
            {
                static Scope M(bool c)
                {
                    Scope s = new Scope();
                    Scope t = new Scope();
                    return c ? s : t;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task StoredInField_ReportsNothing()
    {
        string source = Types + """
            class C
            {
                private Resource _r;
                void M()
                {
                    Resource r = new Resource();
                    _r = r;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task PassedAsArgument_ReportsNothing()
    {
        string source = Types + """
            class C
            {
                static void Take(Scope s) { }
                static void M()
                {
                    Scope s = new Scope();
                    Take(s);
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task FactoryResultUndisposed_Reports()
    {
        string source = Types + """
            class C
            {
                static Scope Create() => new Scope();
                static void M()
                {
                    Scope s = Create();
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(MustDisposeAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task ImplicitConversionOnlyUse_Reports()
    {
        // A user-defined implicit conversion is a use of the scope, not a transfer of ownership, so the scope is
        // still leaked. This is the classic "implicit conversion leaks the scope" mistake.
        string source = Types + """
            class C
            {
                static void M()
                {
                    Scope s = new Scope();
                    int v = s;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(MustDisposeAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task AliasedAndOriginalDisposed_ReportsNothing()
    {
        string source = Types + """
            class C
            {
                static void M()
                {
                    Scope a = new Scope();
                    Scope b = a;
                    a.Dispose();
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ClassBareNew_Reports()
    {
        string source = Types + """
            class C
            {
                static void M()
                {
                    Resource r = new Resource();
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(MustDisposeAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task ClassConditionalDispose_ReportsNothing()
    {
        string source = Types + """
            class C
            {
                static void M()
                {
                    Resource r = new Resource();
                    r?.Dispose();
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task PlainTypeUndisposed_ReportsNothing()
    {
        string source = Types + """
            class C
            {
                static void M()
                {
                    Plain p = new Plain();
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task OutArgument_ReceivedAndUndisposed_Reports()
    {
        // Receiving an out scope hands ownership to the caller, who must dispose it.
        string source = Types + """
            class C
            {
                static bool TryCreate(out Scope s) { s = new Scope(); return true; }
                static void M()
                {
                    TryCreate(out Scope s);
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(MustDisposeAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task OutVarArgument_ReceivedAndUndisposed_Reports()
    {
        string source = Types + """
            class C
            {
                static bool TryCreate(out Scope s) { s = new Scope(); return true; }
                static void M()
                {
                    TryCreate(out var s);
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(MustDisposeAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task OutArgument_ReceivedAndDisposed_ReportsNothing()
    {
        // The canonical Try-pattern: receive by out, use, dispose.
        string source = Types + """
            class C
            {
                static bool TryCreate(out Scope s) { s = new Scope(); return true; }
                static void M()
                {
                    if (TryCreate(out var s))
                    {
                        s.Dispose();
                    }
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task OutArgument_Discarded_ReportsNothing()
    {
        string source = Types + """
            class C
            {
                static bool TryCreate(out Scope s) { s = new Scope(); return true; }
                static void M()
                {
                    TryCreate(out _);
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task OutParameter_ProducerAssignsFresh_ReportsNothing()
    {
        // The producer side: writing a fresh scope through an out parameter transfers it to the caller and must
        // not be flagged as a leak.
        string source = Types + """
            class C
            {
                static bool TryCreate(out Scope s)
                {
                    s = new Scope();
                    return true;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task OutParameter_ProducerAssignsOwnedLocal_ReportsNothing()
    {
        // Writing an owned local through the out parameter is a transfer, not a leak of the local.
        string source = Types + """
            class C
            {
                static bool TryCreate(out Scope s)
                {
                    Scope temp = new Scope();
                    s = temp;
                    return true;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ReturnedFromFactory_ProducerLocal_ReportsNothing()
    {
        // The producer side of "receiving a returned scope": building a scope in a local and returning it is a
        // transfer to the caller.
        string source = Types + """
            class C
            {
                static Scope Create()
                {
                    Scope s = new Scope();
                    return s;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }
}
