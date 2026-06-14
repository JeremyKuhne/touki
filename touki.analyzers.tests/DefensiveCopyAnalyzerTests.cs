// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Analyzers;

[TestClass]
public class DefensiveCopyAnalyzerTests
{
    // Sample types shared by the snippets below. The marker attribute is declared as Touki.NonCopyableAttribute so
    // it matches the fully qualified name the analyzer resolves (the test compilation has no reference to touki).
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
            private int _value;
            public void Mutate() => _value++;
            public readonly int Peek() => _value;
            public int Prop => _value;
        }

        struct Plain
        {
            private int _value;
            public void Mutate() => _value++;
        }

        readonly struct ReadOnlyPlain
        {
            public void Method() { }
        }

        """;

    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source) =>
        AnalyzerTestHarness.GetDiagnosticsAsync(new DefensiveCopyAnalyzer(), source);

    [TestMethod]
    public async Task NonReadonlyMethod_OnInParameterOfNonCopyable_ReportsTOUKI0003()
    {
        string source = Types + """
            class C
            {
                void M(in Pooled p) => p.Mutate();
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(DefensiveCopyAnalyzer.NonCopyableDefensiveCopyId);
    }

    [TestMethod]
    public async Task NonReadonlyMethod_OnInParameterOfPlainStruct_ReportsTOUKI0002()
    {
        string source = Types + """
            class C
            {
                void M(in Plain p) => p.Mutate();
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(DefensiveCopyAnalyzer.DefensiveCopyId);
    }

    [TestMethod]
    public async Task NonReadonlyMethod_OnReadonlyField_OutsideConstructor_Reports()
    {
        string source = Types + """
            struct Holder
            {
                private readonly Plain _p;
                public void Use() => _p.Mutate();
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(DefensiveCopyAnalyzer.DefensiveCopyId);
    }

    [TestMethod]
    public async Task NonReadonlyMethod_OnReadonlyField_InsideConstructor_ReportsNothing()
    {
        string source = Types + """
            struct Holder
            {
                private readonly Plain _p;
                public Holder(int x)
                {
                    _p = default;
                    _p.Mutate();
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task NonReadonlyProperty_OnInParameter_Reports()
    {
        string source = Types + """
            class C
            {
                int M(in Pooled p) => p.Prop;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(DefensiveCopyAnalyzer.NonCopyableDefensiveCopyId);
    }

    [TestMethod]
    public async Task ReadonlyMember_OnInParameter_ReportsNothing()
    {
        string source = Types + """
            class C
            {
                int M(in Pooled p) => p.Peek();
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ReadonlyStruct_ReportsNothing()
    {
        string source = Types + """
            class C
            {
                void M(in ReadOnlyPlain p) => p.Method();
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task NonReadonlyMethod_OnByValueParameter_ReportsNothing()
    {
        string source = Types + """
            class C
            {
                void M(Pooled p) => p.Mutate();
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task NonReadonlyMethod_OnRefReadonlyLocal_Reports()
    {
        string source = Types + """
            class C
            {
                static Pooled s_pooled;

                void M()
                {
                    ref readonly Pooled p = ref s_pooled;
                    p.Mutate();
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(DefensiveCopyAnalyzer.NonCopyableDefensiveCopyId);
    }

    [TestMethod]
    public async Task GeneratedCode_ReportsNothing()
    {
        string source = """
            // <auto-generated/>
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
                private int _value;
                public void Mutate() => _value++;
            }

            class C
            {
                void M(in Pooled p) => p.Mutate();
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }
}
