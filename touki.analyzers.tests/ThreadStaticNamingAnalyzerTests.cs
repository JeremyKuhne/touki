// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Analyzers;

[TestClass]
public class ThreadStaticNamingAnalyzerTests
{
    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        string source,
        string? prefix = null,
        string? additionalAttributes = null)
    {
        Dictionary<string, string> options = new();

        if (prefix is not null)
        {
            options[ThreadStaticNamingAnalyzer.PrefixOption] = prefix;
        }

        if (additionalAttributes is not null)
        {
            options[ThreadStaticNamingAnalyzer.AdditionalAttributesOption] = additionalAttributes;
        }

        return await AnalyzerTestHarness.GetDiagnosticsAsync(
            new ThreadStaticNamingAnalyzer(),
            source,
            options.Count == 0 ? null : options).ConfigureAwait(false);
    }

    private const string Usings = """
        using System;

        """;

    [TestMethod]
    public async Task AnalyzeField_ThreadStaticWithPrefix_ReportsNothing()
    {
        string source = Usings + """
            class Sample
            {
                [ThreadStatic]
                private static int t_value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeField_ThreadStaticWithStaticPrefix_ReportsDiagnostic()
    {
        string source = Usings + """
            class Sample
            {
                [ThreadStatic]
                private static int s_value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(ThreadStaticNamingAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task AnalyzeField_ThreadStaticWithStaticPrefix_SuggestsThreadStaticName()
    {
        string source = Usings + """
            class Sample
            {
                [ThreadStatic]
                private static int s_value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Contain("'s_value'").And.Contain("'t_value'");
    }

    [TestMethod]
    public async Task AnalyzeField_ThreadStaticWithInstancePrefix_SuggestsThreadStaticName()
    {
        string source = Usings + """
            class Sample
            {
                [ThreadStatic]
                private static int _value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Contain("'t_value'");
    }

    [TestMethod]
    public async Task AnalyzeField_ThreadStaticPascalCased_SuggestsCamelCasedName()
    {
        string source = Usings + """
            class Sample
            {
                [ThreadStatic]
                private static int Value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Contain("'t_value'");
    }

    [TestMethod]
    public async Task AnalyzeField_ThreadStaticPascalCasedAfterPrefix_ReportsDiagnostic()
    {
        // The prefix alone is not enough; 't_Value' is still not the camel-cased name.
        string source = Usings + """
            class Sample
            {
                [ThreadStatic]
                private static int t_Value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Contain("'t_value'");
    }

    [TestMethod]
    public async Task AnalyzeField_ThreadStaticWithUnrelatedUnderscoreName_KeepsBothParts()
    {
        // 'x_' is not a prefix this rule knows, so it is part of the name rather than something to replace.
        string source = Usings + """
            class Sample
            {
                [ThreadStatic]
                private static int x_ray;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Contain("'t_x_ray'");
    }

    [TestMethod]
    public async Task AnalyzeField_OrdinaryStaticField_ReportsNothing()
    {
        string source = Usings + """
            class Sample
            {
                private static int s_value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeField_ThreadStaticOnInstanceField_ReportsNothing()
    {
        // The attribute does nothing on an instance field, which CA2259 reports. It is still named '_value'.
        string source = Usings + """
            class Sample
            {
                [ThreadStatic]
                private int _value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeField_UnrelatedAttribute_ReportsNothing()
    {
        string source = Usings + """
            class Sample
            {
                [Obsolete]
                private static int s_value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeField_MultipleDeclarators_ReportsEachSeparately()
    {
        string source = Usings + """
            class Sample
            {
                [ThreadStatic]
                private static int s_first, s_second;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().HaveCount(2);
    }

    [TestMethod]
    public async Task AnalyzeField_ConfiguredPrefix_RequiresConfiguredPrefix()
    {
        string source = Usings + """
            class Sample
            {
                [ThreadStatic]
                private static int t_value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, prefix: "tl_").ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Contain("'tl_value'");
    }

    [TestMethod]
    public async Task AnalyzeField_ConfiguredPrefixMatched_ReportsNothing()
    {
        string source = Usings + """
            class Sample
            {
                [ThreadStatic]
                private static int tl_value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, prefix: "tl_").ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeField_EmptyConfiguredPrefix_FallsBackToDefault()
    {
        string source = Usings + """
            class Sample
            {
                [ThreadStatic]
                private static int s_value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, prefix: "  ").ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Contain("'t_value'");
    }

    private const string CustomAttribute = """
        class MyThreadLocalAttribute : Attribute
        {
        }

        """;

    [TestMethod]
    public async Task AnalyzeField_AdditionalAttributeConfigured_ReportsDiagnostic()
    {
        string source = Usings + CustomAttribute + """
            class Sample
            {
                [MyThreadLocal]
                private static int s_value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzeAsync(source, additionalAttributes: "MyThreadLocal").ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Contain("'t_value'");
    }

    [TestMethod]
    public async Task AnalyzeField_AdditionalAttributeWithSuffix_ReportsDiagnostic()
    {
        string source = Usings + CustomAttribute + """
            class Sample
            {
                [MyThreadLocal]
                private static int s_value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzeAsync(source, additionalAttributes: "Other, MyThreadLocalAttribute").ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(ThreadStaticNamingAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task AnalyzeField_AdditionalAttributeNotConfigured_ReportsNothing()
    {
        string source = Usings + CustomAttribute + """
            class Sample
            {
                [MyThreadLocal]
                private static int s_value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    private const string SuffixlessAttribute = """
        class ThreadBound : Attribute
        {
        }

        """;

    [TestMethod]
    public async Task AnalyzeField_AdditionalAttributeConfiguredWithSuffixTypeWithout_ReportsDiagnostic()
    {
        // An attribute class does not have to carry the 'Attribute' suffix, so the suffix has to be
        // optional on the configured side too.
        string source = Usings + SuffixlessAttribute + """
            class Sample
            {
                [ThreadBound]
                private static int s_value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzeAsync(source, additionalAttributes: "ThreadBoundAttribute").ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Contain("'t_value'");
    }

    [TestMethod]
    public async Task AnalyzeField_AdditionalAttributeSuffixOnNeitherSide_ReportsDiagnostic()
    {
        string source = Usings + SuffixlessAttribute + """
            class Sample
            {
                [ThreadBound]
                private static int s_value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzeAsync(source, additionalAttributes: "ThreadBound").ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(ThreadStaticNamingAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task AnalyzeField_AdditionalAttributeDifferentName_ReportsNothing()
    {
        // Comparing on the suffix-stripped core must not make unrelated names match.
        string source = Usings + CustomAttribute + """
            class Sample
            {
                [MyThreadLocal]
                private static int s_value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzeAsync(source, additionalAttributes: "OtherAttribute, Third").ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeField_AdditionalAttributesTrailingComma_ReportsDiagnostic()
    {
        string source = Usings + CustomAttribute + """
            class Sample
            {
                [MyThreadLocal]
                private static int s_value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzeAsync(source, additionalAttributes: "MyThreadLocal,").ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(ThreadStaticNamingAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task AnalyzeField_ThreadStaticConst_ReportsNothing()
    {
        // A constant has no per-thread slot, and constants are Pascal cased.
        string source = Usings + """
            class Sample
            {
                [ThreadStatic]
                private const int Value = 1;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeField_ThreadStaticUnderscoreOnly_SuggestsAConformingName()
    {
        // A name that is nothing but the prefix has no core to camel case, so the suggestion falls back to
        // a placeholder rather than doubling the prefix.
        string source = Usings + """
            class Sample
            {
                [ThreadStatic]
                private static int t_;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Contain("'t_value'").And.NotContain("'t_t_'");
    }

    [TestMethod]
    public async Task AnalyzeField_ThreadStaticDigitAfterPrefix_DoesNotSuggestTheSameName()
    {
        // 't_1' is not conforming, so it is reported - but the suggestion must not be 't_1' again.
        string source = Usings + """
            class Sample
            {
                [ThreadStatic]
                private static int t_1;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Contain("'t_value1'");
    }

    [TestMethod]
    public async Task AnalyzeField_StaticDigitName_DoesNotSuggestANameItWouldReport()
    {
        // Stripping 's_' leaves '1', which would produce 't_1' - a name this same rule reports.
        string source = Usings + """
            class Sample
            {
                [ThreadStatic]
                private static int s_1;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Contain("'t_value1'");
    }

    [TestMethod]
    public async Task AnalyzeField_GeneratedCode_ReportsNothing()
    {
        string source = """
            // <auto-generated/>
            using System;

            class Sample
            {
                [ThreadStatic]
                private static int s_value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }
}
