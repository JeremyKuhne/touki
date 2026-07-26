// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Analyzers;

[TestClass]
public class PreferValueStringBuilderAnalyzerTests
{
    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source) =>
        await AnalyzerTestHarness.GetDiagnosticsAsync(new PreferValueStringBuilderAnalyzer(), source)
            .ConfigureAwait(false);

    private const string Usings = """
        using System.Collections.Generic;
        using System.IO;
        using System.Text;
        using System.Threading.Tasks;

        """;

    [TestMethod]
    public async Task AnalyzeOperationBlock_LocalBuilder_ReportsDiagnostic()
    {
        string source = Usings + """
            class Sample
            {
                string Build(string value)
                {
                    StringBuilder builder = new StringBuilder();
                    builder.Append(value);
                    return builder.ToString();
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(PreferValueStringBuilderAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task AnalyzeOperationBlock_TargetTypedNew_ReportsDiagnostic()
    {
        string source = Usings + """
            class Sample
            {
                string Build(string value)
                {
                    StringBuilder builder = new();
                    builder.Append(value);
                    return builder.ToString();
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(PreferValueStringBuilderAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task AnalyzeOperationBlock_StoredInWiderLocal_ReportsNothing()
    {
        string source = Usings + """
            class Sample
            {
                string Build()
                {
                    object builder = new StringBuilder();
                    return builder.ToString()!;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeOperationBlock_LocalBuilder_ReportsAtCreation()
    {
        string source = Usings + """
            class Sample
            {
                string Build(string value)
                {
                    StringBuilder builder = new StringBuilder(64);
                    builder.Append(value);
                    return builder.ToString();
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        Location location = diagnostics.Should().ContainSingle().Subject.Location;
        location.SourceTree!.GetText().ToString(location.SourceSpan).Should().Be("new StringBuilder(64)");
    }

    [TestMethod]
    public async Task AnalyzeOperationBlock_LocalBuilder_MessageRecommendsValueStringBuilder()
    {
        string source = Usings + """
            class Sample
            {
                string Build(string value)
                {
                    StringBuilder builder = new();
                    return builder.Append(value).ToString();
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Contain("ValueStringBuilder");
    }

    [TestMethod]
    public async Task AnalyzeOperationBlock_AssignedToExistingLocal_ReportsDiagnostic()
    {
        string source = Usings + """
            class Sample
            {
                string Build(string value)
                {
                    StringBuilder builder;
                    builder = new StringBuilder();
                    builder.Append(value);
                    return builder.ToString();
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(PreferValueStringBuilderAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task AnalyzeOperationBlock_FluentTemporary_ReportsDiagnostic()
    {
        string source = Usings + """
            class Sample
            {
                string Build(string value) => new StringBuilder().Append(value).ToString();
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(PreferValueStringBuilderAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task AnalyzeOperationBlock_FluentResultReturned_ReportsNothing()
    {
        string source = Usings + """
            class Sample
            {
                StringBuilder Build(string value) => new StringBuilder().Append(value);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeOperationBlock_FluentResultPassedAsArgument_ReportsNothing()
    {
        string source = Usings + """
            class Sample
            {
                void Build(string value, List<StringBuilder> builders)
                {
                    builders.Add(new StringBuilder().Append(value));
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeOperationBlock_FluentResultStoredInEscapingLocal_ReportsNothing()
    {
        string source = Usings + """
            class Sample
            {
                StringBuilder Build(string value)
                {
                    StringBuilder builder = new StringBuilder().Append(value);
                    return builder;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeOperationBlock_FluentResultStoredInLocalBuild_ReportsDiagnostic()
    {
        string source = Usings + """
            class Sample
            {
                string Build(string value)
                {
                    StringBuilder builder = new StringBuilder().Append(value);
                    return builder.ToString();
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(PreferValueStringBuilderAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task AnalyzeOperationBlock_FluentChainToProperty_ReportsDiagnostic()
    {
        string source = Usings + """
            class Sample
            {
                int Build(string value) => new StringBuilder().Append(value).Length;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(PreferValueStringBuilderAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task AnalyzeOperationBlock_TwoLocalBuilders_ReportsEach()
    {
        string source = Usings + """
            class Sample
            {
                string Build(string value)
                {
                    StringBuilder first = new();
                    StringBuilder second = new();
                    first.Append(value);
                    second.Append(value);
                    return first.ToString() + second.ToString();
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().HaveCount(2);
    }

    [TestMethod]
    public async Task AnalyzeOperationBlock_ReturnedBuilder_ReportsNothing()
    {
        string source = Usings + """
            class Sample
            {
                StringBuilder Build(string value)
                {
                    StringBuilder builder = new();
                    builder.Append(value);
                    return builder;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeOperationBlock_ReturnedDirectly_ReportsNothing()
    {
        string source = Usings + """
            class Sample
            {
                StringBuilder Build() => new StringBuilder();
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeOperationBlock_PassedAsArgument_ReportsNothing()
    {
        string source = Usings + """
            class Sample
            {
                string Build(string value)
                {
                    StringBuilder builder = new();
                    builder.Append(value);
                    using StringWriter writer = new(builder);
                    return builder.ToString();
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeOperationBlock_CreatedAsArgument_ReportsNothing()
    {
        string source = Usings + """
            class Sample
            {
                StringWriter Build() => new StringWriter(new StringBuilder());
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeOperationBlock_StoredInField_ReportsNothing()
    {
        string source = Usings + """
            class Sample
            {
                private StringBuilder? _builder;

                void Build()
                {
                    StringBuilder builder = new();
                    _builder = builder;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeOperationBlock_FieldInitializer_ReportsNothing()
    {
        string source = Usings + """
            class Sample
            {
                private readonly StringBuilder _builder = new StringBuilder();
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeOperationBlock_AddedToCollection_ReportsNothing()
    {
        string source = Usings + """
            class Sample
            {
                void Build(List<StringBuilder> builders)
                {
                    StringBuilder builder = new();
                    builders.Add(builder);
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeOperationBlock_CapturedByLambda_ReportsNothing()
    {
        string source = Usings + """
            class Sample
            {
                string Build(string value)
                {
                    StringBuilder builder = new();
                    System.Action append = () => builder.Append(value);
                    append();
                    return builder.ToString();
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeOperationBlock_AsyncMethod_ReportsNothing()
    {
        string source = Usings + """
            class Sample
            {
                async Task<string> BuildAsync(string value)
                {
                    StringBuilder builder = new();
                    builder.Append(value);
                    await Task.Yield();
                    return builder.ToString();
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeOperationBlock_Iterator_ReportsNothing()
    {
        string source = Usings + """
            class Sample
            {
                IEnumerable<string> Build(string value)
                {
                    StringBuilder builder = new();
                    builder.Append(value);
                    yield return builder.ToString();
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeOperationBlock_IteratorLocalFunction_ReportsDiagnostic()
    {
        string source = Usings + """
            class Sample
            {
                string Build(string value)
                {
                    StringBuilder builder = new();
                    builder.Append(value);
                    builder.Append(string.Join(",", Numbers()));
                    return builder.ToString();

                    IEnumerable<int> Numbers()
                    {
                        yield return 1;
                    }
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(PreferValueStringBuilderAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task AnalyzeOperationBlock_FluentTemporaryInAsyncLambda_ReportsNothing()
    {
        string source = Usings + """
            using System;

            class Sample
            {
                void Build(string value)
                {
                    Func<Task<string>> build = async () =>
                    {
                        await Task.Yield();
                        return new StringBuilder().Append(value).ToString();
                    };
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeOperationBlock_BuilderInLocalFunction_ReportsNothing()
    {
        string source = Usings + """
            class Sample
            {
                string Build(string value)
                {
                    return Make();

                    string Make()
                    {
                        StringBuilder builder = new();
                        builder.Append(value);
                        return builder.ToString();
                    }
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeOperationBlock_BuilderFromParameter_ReportsNothing()
    {
        string source = Usings + """
            class Sample
            {
                string Build(StringBuilder builder, string value)
                {
                    builder.Append(value);
                    return builder.ToString();
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeOperationBlock_StringBuilderArray_ReportsNothing()
    {
        string source = Usings + """
            class Sample
            {
                StringBuilder[] Build(int count) => new StringBuilder[count];
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeOperationBlock_OtherType_ReportsNothing()
    {
        string source = Usings + """
            class Sample
            {
                string Build(string value)
                {
                    StringWriter writer = new();
                    writer.Write(value);
                    return writer.ToString();
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeOperationBlock_AliasedIntoAnotherLocal_ReportsNothing()
    {
        string source = Usings + """
            class Sample
            {
                StringBuilder Build(string value)
                {
                    StringBuilder builder = new();
                    builder.Append(value);
                    StringBuilder other = builder;
                    return other;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeOperationBlock_AssignedToArrayElement_ReportsNothing()
    {
        string source = Usings + """
            class Sample
            {
                void Build(StringBuilder[] builders, string value)
                {
                    StringBuilder builder = new();
                    builder.Append(value);
                    builders[0] = builder;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeOperationBlock_ReturnedThroughExplicitCast_ReportsNothing()
    {
        string source = Usings + """
            class Sample
            {
                object Build(string value)
                {
                    StringBuilder builder = new();
                    builder.Append(value);
                    return (object)builder;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeOperationBlock_ReturnedThroughTernary_ReportsNothing()
    {
        string source = Usings + """
            class Sample
            {
                StringBuilder Build(bool flag, StringBuilder other)
                {
                    StringBuilder builder = new();
                    return flag ? builder : other;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeOperationBlock_InArrayInitializer_ReportsNothing()
    {
        string source = Usings + """
            class Sample
            {
                StringBuilder[] Build()
                {
                    StringBuilder builder = new();
                    return new[] { builder };
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeOperationBlock_PropertyRead_ReportsDiagnostic()
    {
        string source = Usings + """
            class Sample
            {
                int Build(string value)
                {
                    StringBuilder builder = new();
                    builder.Append(value);
                    return builder.Length;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(PreferValueStringBuilderAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task AnalyzeOperationBlock_GeneratedCode_ReportsNothing()
    {
        string source = "// <auto-generated/>\n" + Usings + """
            class Sample
            {
                string Build(string value)
                {
                    StringBuilder builder = new();
                    builder.Append(value);
                    return builder.ToString();
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }
}
