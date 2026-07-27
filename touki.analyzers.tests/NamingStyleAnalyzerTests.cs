// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Analyzers;

[TestClass]
public class NamingStyleAnalyzerTests
{
    /// <summary>
    ///  TOUKI0041 ships disabled, so every test has to enable it the way an <c>.editorconfig</c> would.
    /// </summary>
    private static readonly Dictionary<string, ReportDiagnostic> s_enabled =
        new() { [NamingStyleAnalyzer.DiagnosticId] = ReportDiagnostic.Warn };

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        string source,
        Dictionary<string, string>? options = null)
        => await AnalyzerTestHarness
            .GetDiagnosticsAsync(new NamingStyleAnalyzer(), source, options, fileName: null, s_enabled)
            .ConfigureAwait(false);

    /// <summary>
    ///  A private-field rule, matching the shape a project would write to get <c>_camelCase</c> fields.
    /// </summary>
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

    // The built-in rules stay in force alongside configured ones. See dotnet/roslyn#71414.

    [TestMethod]
    public async Task Analyze_CustomFieldRuleConfigured_BuiltInTypeRuleStillApplies()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            """
            class lowerCaseType
            {
                private int _value;
            }
            """,
            PrivateFieldRule()).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Location.GetLineSpan().StartLinePosition.Line.Should().Be(0);
    }

    [TestMethod]
    public async Task Analyze_CustomFieldRuleConfigured_FieldRuleAlsoApplies()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            """
            class Thing
            {
                private int value;
            }
            """,
            PrivateFieldRule()).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Contain("Missing prefix: '_'");
    }

    [TestMethod]
    public async Task Analyze_NoConfiguration_InterfaceWithoutIPrefix_Reports()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("interface Thing { }").ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Contain("Missing prefix: 'I'");
    }

    [TestMethod]
    public async Task Analyze_NoConfiguration_TypeParameterWithoutTPrefix_Reports()
    {
        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzeAsync("class Thing<Item> { }").ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Contain("Missing prefix: 'T'");
    }

    [TestMethod]
    public async Task Analyze_NoConfiguration_ConformingNames_ReportsNothing()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            """
            interface IThing { }
            class Thing<TItem> : IThing
            {
                public int Value { get; set; }
                public void Work() { }
            }
            """).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    // Symbol groups can match on attributes. See dotnet/roslyn#32955.

    private static Dictionary<string, string> ThreadStaticAndStaticRules()
    {
        Dictionary<string, string> options = new()
        {
            ["touki_naming_rule.thread_static_fields.severity"] = "warning",
            ["touki_naming_rule.thread_static_fields.symbols"] = "thread_static_fields",
            ["touki_naming_rule.thread_static_fields.style"] = "thread_static_prefix",
            ["touki_naming_symbols.thread_static_fields.applicable_kinds"] = "field",
            ["touki_naming_symbols.thread_static_fields.required_modifiers"] = "static",
            ["touki_naming_symbols.thread_static_fields.required_attributes"] = "System.ThreadStaticAttribute",
            ["touki_naming_style.thread_static_prefix.required_prefix"] = "t_",
            ["touki_naming_style.thread_static_prefix.capitalization"] = "camel_case",

            ["touki_naming_rule.static_fields.severity"] = "warning",
            ["touki_naming_rule.static_fields.symbols"] = "static_fields",
            ["touki_naming_rule.static_fields.style"] = "static_prefix",
            ["touki_naming_symbols.static_fields.applicable_kinds"] = "field",
            ["touki_naming_symbols.static_fields.required_modifiers"] = "static",
            ["touki_naming_style.static_prefix.required_prefix"] = "s_",
            ["touki_naming_style.static_prefix.capitalization"] = "camel_case"
        };

        return options;
    }

    [TestMethod]
    public async Task Analyze_ThreadStaticFieldWithoutPrefix_ReportsThreadStaticRule()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            """
            class Thing
            {
                [System.ThreadStatic]
                private static int values;
            }
            """,
            ThreadStaticAndStaticRules()).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Contain("Missing prefix: 't_'");
    }

    [TestMethod]
    public async Task Analyze_ThreadStaticFieldWithPrefix_ReportsNothing()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            """
            class Thing
            {
                [System.ThreadStatic]
                private static int t_values;
            }
            """,
            ThreadStaticAndStaticRules()).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_StaticFieldWithoutThreadStaticAttribute_ReportsStaticRule()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            """
            class Thing
            {
                private static int values;
            }
            """,
            ThreadStaticAndStaticRules()).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Contain("Missing prefix: 's_'");
    }

    [TestMethod]
    public async Task Analyze_AttributeWrittenWithoutSuffixOrNamespace_StillMatches()
    {
        Dictionary<string, string> options = ThreadStaticAndStaticRules();
        options["touki_naming_symbols.thread_static_fields.required_attributes"] = "ThreadStatic";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            """
            class Thing
            {
                [System.ThreadStatic]
                private static int values;
            }
            """,
            options).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Contain("Missing prefix: 't_'");
    }

    [TestMethod]
    public async Task Analyze_AttributeTypeWithoutSuffixConfiguredWithSuffix_StillMatches()
    {
        // The Attribute suffix is a convention, not a requirement, so the class may omit the suffix the
        // configured name carries. The match has to work in both directions.
        Dictionary<string, string> options = ThreadStaticAndStaticRules();
        options["touki_naming_symbols.thread_static_fields.required_attributes"] = "MyThreadLocalAttribute";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            """
            class MyThreadLocal : System.Attribute
            {
            }

            class Thing
            {
                [MyThreadLocal]
                private static int values;
            }
            """,
            options).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Contain("Missing prefix: 't_'");
    }

    [TestMethod]
    public async Task Analyze_NamespacedAttributeTypeWithoutSuffixConfiguredWithSuffix_StillMatches()
    {
        Dictionary<string, string> options = ThreadStaticAndStaticRules();
        options["touki_naming_symbols.thread_static_fields.required_attributes"] =
            "Custom.Markers.MyThreadLocalAttribute";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            """
            namespace Custom.Markers
            {
                class MyThreadLocal : System.Attribute
                {
                }
            }

            class Thing
            {
                [Custom.Markers.MyThreadLocal]
                private static int values;
            }
            """,
            options).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Contain("Missing prefix: 't_'");
    }

    [TestMethod]
    public async Task Analyze_AttributeNameThatOnlySharesAPrefix_DoesNotMatch()
    {
        Dictionary<string, string> options = ThreadStaticAndStaticRules();
        options["touki_naming_symbols.thread_static_fields.required_attributes"] = "MyThreadLocal";

        // MyThreadLocalMarker shares a prefix with the configured name but is a different attribute, so the
        // thread-static rule must not claim this field. It falls to the static rule instead.
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            """
            class MyThreadLocalMarker : System.Attribute
            {
            }

            class Thing
            {
                [MyThreadLocalMarker]
                private static int values;
            }
            """,
            options).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Contain("Missing prefix: 's_'");
    }

    // const does not satisfy a `static` requirement even though the language treats it as static.
    // See dotnet/roslyn#23884, dotnet/roslyn#15428 and dotnet/roslyn#23391.

    [TestMethod]
    public async Task Analyze_ConstField_StaticRuleDoesNotApply()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            """
            class Thing
            {
                private const int MaxValue = 1;
            }
            """,
            ThreadStaticAndStaticRules()).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    // A group can exclude a modifier rather than only require one. See dotnet/roslyn#18354.

    [TestMethod]
    public async Task Analyze_ExcludedModifier_RuleSkipsMatchingSymbol()
    {
        Dictionary<string, string> options = PrivateFieldRule();
        options["touki_naming_symbols.private_fields.excluded_modifiers"] = "static";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            """
            class Thing
            {
                private static int value;
                private int other;
            }
            """,
            options).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Location.GetLineSpan().StartLinePosition.Line.Should().Be(3);
    }

    // A rule can name more than one symbol group. See dotnet/roslyn#20891.

    [TestMethod]
    public async Task Analyze_RuleWithTwoSymbolGroups_AppliesToBoth()
    {
        Dictionary<string, string> options = new()
        {
            ["touki_naming_rule.underscored.severity"] = "warning",
            ["touki_naming_rule.underscored.symbols"] = "private_fields, private_events",
            ["touki_naming_rule.underscored.style"] = "underscore_camel_case",
            ["touki_naming_symbols.private_fields.applicable_kinds"] = "field",
            ["touki_naming_symbols.private_fields.applicable_accessibilities"] = "private",
            ["touki_naming_symbols.private_events.applicable_kinds"] = "event",
            ["touki_naming_symbols.private_events.applicable_accessibilities"] = "private",
            ["touki_naming_style.underscore_camel_case.required_prefix"] = "_",
            ["touki_naming_style.underscore_camel_case.capitalization"] = "camel_case"
        };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            """
            class Thing
            {
                private int value;
                private event System.Action changed;
            }
            """,
            options).ConfigureAwait(false);

        diagnostics.Should().HaveCount(2);
    }

    // pascal_case checks the whole name, not only its first character. See dotnet/roslyn#70709.

    [TestMethod]
    public async Task Analyze_PascalCaseNameContainingUnderscore_Reports()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            """
            class Thing
            {
                public void Do_Work() { }
            }
            """).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Contain("must not contain word separators");
    }

    // all_upper is not rejected just because the name starts with a well known prefix.
    // See dotnet/roslyn#57706 and dotnet/roslyn#55845.

    [TestMethod]
    public async Task Analyze_AllUpperConstantWithUnderscore_ReportsNothing()
    {
        Dictionary<string, string> options = new()
        {
            ["touki_naming_rule.constants.severity"] = "warning",
            ["touki_naming_rule.constants.symbols"] = "constants",
            ["touki_naming_rule.constants.style"] = "all_upper_style",
            ["touki_naming_symbols.constants.applicable_kinds"] = "field",
            ["touki_naming_symbols.constants.required_modifiers"] = "const",
            ["touki_naming_style.all_upper_style.capitalization"] = "all_upper"
        };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            """
            class Thing
            {
                private const int S_MAX = 1;
                private const int MAX_VALUE = 2;
            }
            """,
            options).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_RuleSeverityNone_ReportsNothing()
    {
        Dictionary<string, string> options = PrivateFieldRule();
        options["touki_naming_rule.private_fields.severity"] = "none";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            """
            class Thing
            {
                private int value;
            }
            """,
            options).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_Indexer_ReportsNothing()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            """
            class Thing
            {
                public int this[int index] => index;
            }
            """).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_Override_ReportsOnlyTheDeclaration()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            """
            class Base
            {
                public virtual void doWork() { }
            }

            class Derived : Base
            {
                public override void doWork() { }
            }
            """).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Location.GetLineSpan().StartLinePosition.Line.Should().Be(2);
    }

    [TestMethod]
    public async Task Analyze_ExplicitInterfaceImplementation_ReportsOnlyTheInterfaceMember()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            """
            interface IThing
            {
                void doWork();
            }

            class Thing : IThing
            {
                void IThing.doWork() { }
            }
            """).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Location.GetLineSpan().StartLinePosition.Line.Should().Be(2);
    }

    [TestMethod]
    public async Task Analyze_Violation_CarriesSuggestedName()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            """
            class Thing
            {
                private int value;
            }
            """,
            PrivateFieldRule()).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Properties["SuggestedName"].Should().Be("_value");
    }

    [TestMethod]
    public async Task Analyze_LocalFunction_IsChecked()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
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

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Contain("These words must begin with upper case characters: doMore");
    }

    [TestMethod]
    public async Task Analyze_UnparseableRule_IsIgnoredWithoutLosingBuiltInRules()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            "interface Thing { }",
            new Dictionary<string, string>
            {
                ["touki_naming_rule.broken.severity"] = "warning",
                ["touki_naming_rule.broken.symbols"] = "missing_group",
                ["touki_naming_rule.broken.style"] = "missing_style"
            }).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Contain("Missing prefix: 'I'");
    }

    [TestMethod]
    public async Task Analyze_RuleNamingAnUndefinedSymbolGroup_DoesNotBecomeACatchAll()
    {
        // Every list on an undefined group would be empty, and an empty list matches everything. A misspelled
        // group name has to drop the rule rather than apply its style to every symbol in the compilation.
        Dictionary<string, string> options = PrivateFieldRule();
        options["touki_naming_rule.private_fields.symbols"] = "private_feilds";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            """
            class Thing
            {
                private int value;

                public void Work() { }
            }
            """,
            options).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_RuleNamingAnUndefinedStyle_IsIgnored()
    {
        Dictionary<string, string> options = PrivateFieldRule();
        options["touki_naming_rule.private_fields.style"] = "no_such_style";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            """
            class Thing
            {
                private int value;
            }
            """,
            options).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_NameWithNoCompliantForm_ReportsWithoutASuggestion()
    {
        // "_" is reported under the built-in PascalCase type rule, but every candidate MakeCompliant produces
        // is still "_", which the rule would report again. A suggestion the fix cannot clear is worse than none.
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("class _ { }").ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Which;
        diagnostic.Properties.ContainsKey("SuggestedName").Should().BeFalse();
    }

    [TestMethod]
    public async Task Analyze_SuggestedName_AlwaysSatisfiesTheRuleThatProducedIt()
    {
        Dictionary<string, string> options = ThreadStaticAndStaticRules();

        foreach (string name in new[] { "value", "_value", "s_value", "t_", "x_ray", "__", "V" })
        {
            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
                $$"""
                class Thing
                {
                    [System.ThreadStatic]
                    private static int {{name}};
                }
                """,
                options).ConfigureAwait(false);

            foreach (Diagnostic diagnostic in diagnostics)
            {
                if (diagnostic.Properties.TryGetValue("SuggestedName", out string? suggested))
                {
                    suggested.Should().NotBe(name);
                    suggested.Should().StartWith("t_");
                }
            }
        }
    }

    [TestMethod]
    public async Task Analyze_NoPrefixRequired_SuggestionDoesNotDeleteALeadingCommonPrefix()
    {
        // Upstream strips a leading s_/m_/t_/_ before rebuilding the name, so s_max would be "fixed" to MAX -
        // silently dropping a word the author wrote. A style that requires no prefix has no business removing
        // one. See dotnet/roslyn#57706. The underscore itself is not preserved because the style configures no
        // word_separator; adding one yields S_MAX.
        Dictionary<string, string> options = new()
        {
            ["touki_naming_rule.constants.severity"] = "warning",
            ["touki_naming_rule.constants.symbols"] = "constants",
            ["touki_naming_rule.constants.style"] = "all_upper_style",
            ["touki_naming_symbols.constants.applicable_kinds"] = "field",
            ["touki_naming_symbols.constants.required_modifiers"] = "const",
            ["touki_naming_style.all_upper_style.capitalization"] = "all_upper"
        };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            """
            class Thing
            {
                private const int s_max = 1;
            }
            """,
            options).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Properties["SuggestedName"].Should().Be("SMAX");
    }

    [TestMethod]
    public async Task Analyze_WordSeparatorConfigured_SuggestionKeepsTheSeparator()
    {
        Dictionary<string, string> options = new()
        {
            ["touki_naming_rule.constants.severity"] = "warning",
            ["touki_naming_rule.constants.symbols"] = "constants",
            ["touki_naming_rule.constants.style"] = "all_upper_style",
            ["touki_naming_symbols.constants.applicable_kinds"] = "field",
            ["touki_naming_symbols.constants.required_modifiers"] = "const",
            ["touki_naming_style.all_upper_style.capitalization"] = "all_upper",
            ["touki_naming_style.all_upper_style.word_separator"] = "_"
        };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            """
            class Thing
            {
                private const int maxValue = 1;
            }
            """,
            options).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Properties["SuggestedName"].Should().Be("MAX_VALUE");
    }
}
