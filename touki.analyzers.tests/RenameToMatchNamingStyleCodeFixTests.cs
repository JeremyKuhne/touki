// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Analyzers;

[TestClass]
public class RenameToMatchNamingStyleCodeFixTests
{
    private static readonly Dictionary<string, ReportDiagnostic> s_enabled =
        new() { [NamingStyleAnalyzer.DiagnosticId] = ReportDiagnostic.Warn };

    private static async Task<string> ApplyFixAsync(string source, Dictionary<string, string>? options = null)
        => await CodeFixTestHarness.ApplyFixAsync(
            new NamingStyleAnalyzer(),
            new RenameToMatchNamingStyleCodeFixProvider(),
            source,
            NamingStyleAnalyzer.DiagnosticId,
            options,
            s_enabled).ConfigureAwait(false);

    private static Dictionary<string, string> PrivateFieldRule() => new()
    {
        ["touki_naming_rule.private_fields.severity"] = "warning",
        ["touki_naming_rule.private_fields.symbols"] = "private_fields",
        ["touki_naming_rule.private_fields.style"] = "underscore_camel_case",
        ["touki_naming_symbols.private_fields.applicable_kinds"] = "field",
        ["touki_naming_symbols.private_fields.applicable_accessibilities"] = "private",
        ["touki_naming_style.underscore_camel_case.required_prefix"] = "_",
        ["touki_naming_style.underscore_camel_case.capitalization"] = "camel_case"
    };

    [TestMethod]
    public async Task ApplyFix_Field_RenamesDeclarationAndReferences()
    {
        string fixedSource = await ApplyFixAsync(
            """
            class Thing
            {
                private int value;

                public int Read() => value;
            }
            """,
            PrivateFieldRule()).ConfigureAwait(false);

        fixedSource.Should().Contain("private int _value;");
        fixedSource.Should().Contain("=> _value;");
    }

    [TestMethod]
    public async Task ApplyFix_Interface_AddsThePrefix()
    {
        string fixedSource = await ApplyFixAsync(
            """
            interface Thing
            {
            }

            class Implementation : Thing
            {
            }
            """).ConfigureAwait(false);

        fixedSource.Should().Contain("interface IThing");
        fixedSource.Should().Contain(": IThing");
    }

    [TestMethod]
    public async Task ApplyFix_TypeParameter_AddsThePrefix()
    {
        string fixedSource = await ApplyFixAsync(
            """
            class Thing<Item>
            {
                public Item Value { get; set; }
            }
            """).ConfigureAwait(false);

        fixedSource.Should().Contain("class Thing<TItem>");
        fixedSource.Should().Contain("public TItem Value");
    }

    [TestMethod]
    public async Task ApplyFix_Method_PascalCasesTheName()
    {
        string fixedSource = await ApplyFixAsync(
            """
            class Thing
            {
                public void doWork() { }

                public void Call() => doWork();
            }
            """).ConfigureAwait(false);

        fixedSource.Should().Contain("public void DoWork()");
        fixedSource.Should().Contain("=> DoWork();");
    }

    [TestMethod]
    public async Task ApplyFix_LocalFunction_PascalCasesTheName()
    {
        string fixedSource = await ApplyFixAsync(
            """
            class Thing
            {
                public void Work()
                {
                    void doMore() { }
                    doMore();
                }
            }
            """,
            new Dictionary<string, string>
            {
                ["touki_naming_rule.local_functions.severity"] = "warning",
                ["touki_naming_rule.local_functions.symbols"] = "local_functions",
                ["touki_naming_rule.local_functions.style"] = "pascal_case_style",
                ["touki_naming_symbols.local_functions.applicable_kinds"] = "local_function",
                ["touki_naming_style.pascal_case_style.capitalization"] = "pascal_case"
            }).ConfigureAwait(false);

        fixedSource.Should().Contain("void DoMore() { }");
        fixedSource.Should().Contain("DoMore();");
    }

    [TestMethod]
    public async Task ApplyFix_ParameterUnderAConfiguredRule_RenamesIt()
    {
        string fixedSource = await ApplyFixAsync(
            """
            class Thing
            {
                public int Work(int Value) => Value;
            }
            """,
            new Dictionary<string, string>
            {
                ["touki_naming_rule.parameters.severity"] = "warning",
                ["touki_naming_rule.parameters.symbols"] = "parameters",
                ["touki_naming_rule.parameters.style"] = "camel_case_style",
                ["touki_naming_symbols.parameters.applicable_kinds"] = "parameter",
                ["touki_naming_style.camel_case_style.capitalization"] = "camel_case"
            }).ConfigureAwait(false);

        fixedSource.Should().Contain("Work(int value)");
        fixedSource.Should().Contain("=> value;");
    }

    [TestMethod]
    public async Task ApplyFix_NoSuggestedName_LeavesSourceUnchanged()
    {
        // A single underscore is reported under the built-in PascalCase type rule but has no compliant form,
        // so the analyzer attaches no suggestion and the fix must not be offered.
        const string Source = """
            class _
            {
            }
            """;

        string fixedSource = await ApplyFixAsync(Source).ConfigureAwait(false);

        fixedSource.Should().Be(Source);
    }
}
