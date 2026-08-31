// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Analyzers;

[TestClass]
public class RequireNamedArgumentsForLiteralsAnalyzerTests
{
    private static readonly IReadOnlyDictionary<string, ReportDiagnostic> s_enabled =
        new Dictionary<string, ReportDiagnostic>
        {
            [RequireNamedArgumentsForLiteralsAnalyzer.DiagnosticId] = ReportDiagnostic.Warn
        };

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        string source,
        string? literals = null,
        IReadOnlyCollection<string>? expectedCompilerDiagnosticIds = null)
    {
        Dictionary<string, string>? options = literals is null
            ? null
            : new Dictionary<string, string>
            {
                [RequireNamedArgumentsForLiteralsAnalyzer.LiteralsOption] = literals
            };

        return await AnalyzerTestHarness.GetDiagnosticsAsync(
            new RequireNamedArgumentsForLiteralsAnalyzer(),
            source,
            options,
            diagnosticOptions: s_enabled,
            expectedCompilerDiagnosticIds: expectedCompilerDiagnosticIds).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task AnalyzeArgument_DefaultLiterals_ReportsEachParameterName()
    {
        const string source = """
            class Sample
            {
                void Target(bool enabled, bool visible, object value, int count) { }

                void Use() => Target(true, false, null, default);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().HaveCount(4);
        diagnostics.Select(diagnostic => diagnostic.GetMessage()).Should().Equal(
            "Use the parameter name 'enabled:' for this literal argument",
            "Use the parameter name 'visible:' for this literal argument",
            "Use the parameter name 'value:' for this literal argument",
            "Use the parameter name 'count:' for this literal argument");
    }

    [TestMethod]
    public async Task AnalyzeArgument_NamedLiterals_ReportsNoDiagnostic()
    {
        const string source = """
            class Sample
            {
                void Target(bool enabled, object value) { }

                void Use() => Target(enabled: true, value: null);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeArgument_NamedConstants_ReportsNoDiagnostic()
    {
        const string source = """
            class Sample
            {
                private const bool Enabled = true;
                private const string Value = null;

                void Target(bool enabled, string value) { }

                void Use() => Target(Enabled, Value);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeArgument_ConfiguredLiteralKinds_ReplacesDefaults()
    {
        const string source = """
            class Sample
            {
                void Target(int count, double ratio, char marker, string text, bool enabled, bool visible) { }

                void Use() => Target(42, 1.5, 'x', "value", true, false);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            source,
            "integer, floating_point, character, string").ConfigureAwait(false);

        diagnostics.Should().HaveCount(4);
        diagnostics.Select(diagnostic => diagnostic.GetMessage()).Should().Equal(
            "Use the parameter name 'count:' for this literal argument",
            "Use the parameter name 'ratio:' for this literal argument",
            "Use the parameter name 'marker:' for this literal argument",
            "Use the parameter name 'text:' for this literal argument");
    }

    [TestMethod]
    public async Task AnalyzeArgument_IntegerConfiguration_ReportsOnlyIntegerForms()
    {
        const string source = """
            class Sample
            {
                void Target(int decimalValue, int hexadecimal, int binary, double floatingPoint) { }

                void Use() => Target(42, 0x2A, 0b101010, 3.14);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, "integer").ConfigureAwait(false);

        diagnostics.Should().HaveCount(3);
        diagnostics.Select(diagnostic => diagnostic.GetMessage()).Should().OnlyContain(
            message => !message.Contains("floatingPoint", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task AnalyzeArgument_FloatingPointConfiguration_ReportsAllFloatingPointTypes()
    {
        const string source = """
            class Sample
            {
                void Target(float single, double doubleValue, decimal decimalValue, int integer) { }

                void Use() => Target(1.5f, 2.5, 3.5m, 4);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, "floating_point").ConfigureAwait(false);

        diagnostics.Should().HaveCount(3);
        diagnostics.Select(diagnostic => diagnostic.GetMessage()).Should().OnlyContain(
            message => !message.Contains("integer", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task AnalyzeArgument_BooleanConfiguration_ReportsTrueAndFalse()
    {
        const string source = """
            class Sample
            {
                void Target(bool enabled, bool visible) { }

                void Use() => Target(true, false);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, " BoOlEaN ").ConfigureAwait(false);

        diagnostics.Should().HaveCount(2);
    }

    [TestMethod]
    public async Task AnalyzeArgument_AllLiteralKinds_ReportsSupportedSyntaxes()
    {
        const string source = """"
            #nullable enable
            using System;

            class Sample
            {
                void Target(
                    int integer,
                    double floatingPoint,
                    char character,
                    string text,
                    string interpolated,
                    ReadOnlySpan<byte> utf8,
                    bool boolean,
                    object? nullable,
                    int defaultValue) { }

                void Use(int value) => Target(
                    -42,
                    1.5e2,
                    '\n',
                    """raw""",
                    $"{value}",
                    "utf8"u8,
                    false,
                    null!,
                    default(int));
            }
            """";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            source,
            "integer, floating_point, character, string, boolean, null, default").ConfigureAwait(false);

        diagnostics.Should().HaveCount(9);
    }

    [TestMethod]
    public async Task AnalyzeArgument_ParenthesizedAndCastLiterals_ReportsDiagnostics()
    {
        const string source = """
            class Sample
            {
                void Target(bool enabled, object value) { }

                void Use() => Target((true), (object)null);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().HaveCount(2);
    }

    [TestMethod]
    public async Task AnalyzeArgument_CheckedAndSignedLiterals_ReportsOnlyDirectLiterals()
    {
        const string source = """
            class Sample
            {
                private const int Named = 42;

                void Target(int checkedValue, int uncheckedValue, int named) { }

                void Use() => Target(checked(+42), unchecked(-1), checked(+Named));
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, "integer").ConfigureAwait(false);

        diagnostics.Should().HaveCount(2);
        diagnostics.Select(diagnostic => diagnostic.GetMessage()).Should().NotContain(
            message => message.Contains("named:", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task AnalyzeArgument_ConstantExpressions_ReportsNoDiagnostic()
    {
        const string source = """
            class Sample
            {
                void Target(bool enabled, int count, string text) { }

                void Use() => Target(true && false, 40 + 2, "a" + "b");
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            source,
            "integer, string, boolean").ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeArgument_ExpandedParamsLiteral_ReportsNoDiagnostic()
    {
        const string source = """
            class Sample
            {
                void Target(params bool[] values) { }

                void Use() => Target(true, false);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeArgument_ExpandedParamsCollectionLiteral_ReportsNoDiagnostic()
    {
        const string source = """
            using System;

            class Sample
            {
                void Target(params ReadOnlySpan<bool> values) { }

                void Use() => Target(true, false);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeArgument_MalformedConfiguration_UsesDefaults()
    {
        const string source = """
            class Sample
            {
                void Target(int count, bool enabled) { }

                void Use() => Target(42, true);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, "integer, typo").ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Contain("enabled:");
    }

    [TestMethod]
    public async Task AnalyzeArgument_LargeValidConfiguration_RemainsSupported()
    {
        const string source = """
            class Sample
            {
                void Target(bool enabled, int count) { }

                void Use() => Target(true, 42);
            }
            """;
        string literals = string.Join(",", Enumerable.Repeat("integer", 4096));

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, literals).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Contain("count:");
    }

    [TestMethod]
    public async Task AnalyzeArgument_DiagnosticLocation_IsLiteralExpression()
    {
        const string source = """
            class Sample
            {
                void Target(bool enabled) { }

                void Use() => Target((bool)true);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        Location location = diagnostics[0].Location;
        location.SourceTree!.GetText().ToString(location.SourceSpan).Should().Be("(bool)true");
    }

    [TestMethod]
    public async Task AnalyzeArgument_GeneratedCode_ReportsNoDiagnostic()
    {
        const string source = """
            // <auto-generated/>
            class Sample
            {
                void Target(bool enabled) { }

                void Use() => Target(true);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeArgument_InterpolatedStringHandler_ReportsOnlySourceArgument()
    {
        const string source = """
            using System.Runtime.CompilerServices;

            [InterpolatedStringHandler]
            struct SampleHandler
            {
                public SampleHandler(int literalLength, int formattedCount) { }
                public void AppendLiteral(string value) { }
                public void AppendFormatted<T>(T value) { }
            }

            class Sample
            {
                void Target(SampleHandler handler) { }

                void Use(int value) => Target($"Value: {value}");
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, "string").ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Contain("handler:");
    }

    [TestMethod]
    public async Task AnalyzeArgument_InterpolatedStringWithOmittedOptionalArgument_ReportsOnlySourceArgument()
    {
        const string source = """
            class Sample
            {
                string Format(bool enabled = true) => enabled.ToString();
                void Target(string text) { }

                void Use() => Target($"{Format()}");
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, "string").ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Contain("text:");
    }

    [TestMethod]
    public async Task AnalyzeArgument_ReducedExtensionReceiver_ReportsNoDiagnostic()
    {
        const string source = """
            static class Extensions
            {
                public static int Next(this int value) => value;
            }

            class Sample
            {
                void Use()
                {
                    _ = 0.Next().Next().Next().Next().Next().Next().Next().Next();
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, "integer").ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeArgument_InvalidConversion_ReportsNoDiagnostic()
    {
        const string source = """
            class Sample
            {
                void Target(int count) { }

                void Use() => Target(true);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            source,
            expectedCompilerDiagnosticIds: ["CS1503"]).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeArgument_InvalidCast_ReportsNoDiagnostic()
    {
        const string source = """
            class Sample
            {
                void Target(int count) { }

                void Use() => Target((int)true);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            source,
            expectedCompilerDiagnosticIds: ["CS0030"]).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeArgument_OutOfRangeConstantCast_ReportsNoDiagnostic()
    {
        const string source = """
            class Sample
            {
                void Target(byte count) { }

                void Use() => Target((byte)256);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            source,
            "integer",
            expectedCompilerDiagnosticIds: ["CS0221"]).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeArgument_ManyUnrelatedCompilerErrors_ReportsValidLiteral()
    {
        string invalidFields = string.Join(
            Environment.NewLine,
            Enumerable.Range(0, 256).Select(index => $"    MissingType{index} Field{index};"));
        string source = $$"""
            class Sample
            {
                void Target(bool enabled) { }

                void Use() => Target(true);
            {{invalidFields}}
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            source,
            expectedCompilerDiagnosticIds: Enumerable.Repeat("CS0246", 256).ToArray()).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Contain("enabled:");
    }

    [TestMethod]
    public async Task AnalyzeArgument_AttributeConstructorLiteral_ReportsOnlyPositionalArgument()
    {
        const string source = """
            using System;

            sealed class FlagAttribute : Attribute
            {
                public FlagAttribute(bool enabled) { }
            }

            [Flag(true)]
            class Positional { }

            [Flag(enabled: true)]
            class Named { }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Contain("enabled:");
    }

    [TestMethod]
    public async Task AnalyzeArgument_SupportedCallShapes_ReportParameterNames()
    {
        const string source = """
            delegate void Callback(bool enabled);

            class Base
            {
                protected Base(bool enabled) { }
            }

            class Sample : Base
            {
                public Sample() : this(true, 0) { }
                private Sample(bool enabled, int count) : base(false) { }

                public int this[bool enabled] => 0;

                void Use(Callback callback)
                {
                    Sample value = new(true, 0);
                    callback(false);
                    _ = value[true];
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().HaveCount(5);
        diagnostics.Select(diagnostic => diagnostic.GetMessage()).Should().OnlyContain(
            message => message.Contains("enabled:", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task AnalyzeArgument_PerFileConfiguration_KeepsCachedOptionsSeparate()
    {
        (string Source, string FileName)[] sources =
        [
            (
                """
                partial class Sample
                {
                    void Target(bool enabled, int count) { }
                    void UseBoolean() => Target(true, 1);
                }
                """,
                "Boolean.cs"),
            (
                """
                partial class Sample
                {
                    void UseInteger() => Target(false, 2);
                }
                """,
                "Integer.cs")
        ];
        Dictionary<string, IReadOnlyDictionary<string, string>> optionsByFile = new()
        {
            ["Boolean.cs"] = new Dictionary<string, string>
            {
                [RequireNamedArgumentsForLiteralsAnalyzer.LiteralsOption] = "boolean"
            },
            ["Integer.cs"] = new Dictionary<string, string>
            {
                [RequireNamedArgumentsForLiteralsAnalyzer.LiteralsOption] = "integer"
            }
        };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new RequireNamedArgumentsForLiteralsAnalyzer(),
            sources,
            diagnosticOptions: s_enabled,
            optionsByFile: optionsByFile).ConfigureAwait(false);

        diagnostics.Should().HaveCount(2);
        Dictionary<string, string> messagesByFile = diagnostics.ToDictionary(
            diagnostic => diagnostic.Location.SourceTree!.FilePath,
            diagnostic => diagnostic.GetMessage());
        messagesByFile["Boolean.cs"].Should().Contain("enabled:");
        messagesByFile["Integer.cs"].Should().Contain("count:");
    }
}
