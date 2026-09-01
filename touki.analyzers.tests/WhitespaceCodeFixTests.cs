// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.CodeAnalysis.CodeFixes;

namespace Touki.Analyzers;

[TestClass]
public class WhitespaceCodeFixTests
{
    // TOUKI0022 ships disabled, so the fix tests have to turn it on the way a project would.
    private static readonly Dictionary<string, ReportDiagnostic> s_tabsEnabled =
        new() { [NoTabsAnalyzer.DiagnosticId] = ReportDiagnostic.Warn };

    private static async Task<string> FixTabsAsync(string source, Dictionary<string, string>? options = null)
        => await CodeFixTestHarness.ApplyFixAsync(
            new NoTabsAnalyzer(),
            new ReplaceTabsWithSpacesCodeFixProvider(),
            source,
            NoTabsAnalyzer.DiagnosticId,
            options,
            s_tabsEnabled).ConfigureAwait(false);

    private static async Task<string> FixTrailingAsync(string source)
        => await CodeFixTestHarness.ApplyFixAsync(
            new TrailingWhitespaceAnalyzer(),
            new RemoveTrailingWhitespaceCodeFixProvider(),
            source,
            TrailingWhitespaceAnalyzer.DiagnosticId).ConfigureAwait(false);

    [TestMethod]
    public async Task ReplaceTabs_IndentedLine_UsesDefaultWidth()
    {
        string source = "class Sample\n{\n\tint Value;\n}\n";

        string fixedSource = await FixTabsAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be("class Sample\n{\n    int Value;\n}\n");
    }

    [TestMethod]
    public async Task ReplaceTabs_ConfiguredWidth_UsesConfiguredWidth()
    {
        string source = "class Sample\n{\n\tint Value;\n}\n";
        Dictionary<string, string> options = new() { [NoTabsAnalyzer.SpacesPerTabOption] = "2" };

        string fixedSource = await FixTabsAsync(source, options).ConfigureAwait(false);

        fixedSource.Should().Be("class Sample\n{\n  int Value;\n}\n");
    }

    [TestMethod]
    public async Task ReplaceTabs_MidLineTab_KeepsAlignment()
    {
        // The tab sits at column 5 and must land the '=' on column 8, so it becomes three spaces.
        string source = "class Sample\n{\n    int x\t= 1;\n}\n";

        string fixedSource = await FixTabsAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be("class Sample\n{\n    int x   = 1;\n}\n");
    }

    [TestMethod]
    public async Task ReplaceTabs_TabInStringLiteral_LeavesSourceUnchanged()
    {
        string source = "class Sample\n{\n    string Value = \"a\tb\";\n}\n";

        string fixedSource = await FixTabsAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(source);
    }

    [TestMethod]
    public async Task ReplaceTabs_LinkedDocument_OffersNoFix()
    {
        const string source = "class Sample\n{\n\tint Value;\n}\n";

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new NoTabsAnalyzer(),
            new ReplaceTabsWithSpacesCodeFixProvider(),
            [("Shared.cs", "Shared.cs", source)],
            NoTabsAnalyzer.DiagnosticId,
            fixAll: false,
            diagnosticOptions: s_tabsEnabled,
            addLinkedProject: true).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(2);
        result.CodeFixActionOffered.Should().BeFalse();
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().HaveCount(2);
        result.Documents.Should().HaveCount(2).And.OnlyContain(document => document.Source == source);
    }

    [TestMethod]
    public async Task ReplaceTabs_FixAllSolution_SkipsDivergentLinkedDocumentAndFixesEligibleDocument()
    {
        const string linkedSource = "class Linked\n{\n\tint Value;\n}\n";
        const string eligibleSource = "class Eligible\n{\n\tint Value;\n}\n";
        Dictionary<string, string> options = new()
        {
            [NoTabsAnalyzer.SpacesPerTabOption] = "2"
        };
        Dictionary<string, string> linkedOptions = new()
        {
            [NoTabsAnalyzer.SpacesPerTabOption] = "4"
        };

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new NoTabsAnalyzer(),
            new ReplaceTabsWithSpacesCodeFixProvider(),
            [("Linked.cs", "Z-Linked.cs", linkedSource)],
            NoTabsAnalyzer.DiagnosticId,
            fixAll: true,
            options,
            s_tabsEnabled,
            fixAllScope: FixAllScope.Solution,
            addLinkedProject: true,
            additionalProjectSources: [("Eligible.cs", "A-Eligible.cs", eligibleSource)],
            linkedProjectOptions: linkedOptions).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(3);
        result.CodeFixActionOffered.Should().BeTrue();
        result.FixAllActionOffered.Should().BeTrue();
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().HaveCount(2);
        result.Documents.Where(document => document.Name == "Linked.cs")
            .Should().HaveCount(2).And.OnlyContain(document => document.Source == linkedSource);
        result.Documents.Single(document => document.Name == "Eligible.cs").Source
            .Should().Be("class Eligible\n{\n  int Value;\n}\n");
    }

    [TestMethod]
    public async Task ReplaceTabs_CaseVariantPaths_UsesPlatformPathIdentity()
    {
        const string source = "class Sample\n{\n\tint Value;\n}\n";

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new NoTabsAnalyzer(),
            new ReplaceTabsWithSpacesCodeFixProvider(),
            [("Upper.cs", "Case.cs", source)],
            NoTabsAnalyzer.DiagnosticId,
            fixAll: false,
            diagnosticOptions: s_tabsEnabled,
            additionalProjectSources: [("Lower.cs", "case.cs", source)]).ConfigureAwait(false);

        bool pathsAreShared = Path.DirectorySeparatorChar == '\\';
        result.CodeFixActionOffered.Should().Be(!pathsAreShared);
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().HaveCount(pathsAreShared ? 2 : 1);
        result.Documents.Single(document => document.Name == "Upper.cs").Source.Should().Be(
            pathsAreShared ? source : "class Sample\n{\n    int Value;\n}\n");
        result.Documents.Single(document => document.Name == "Lower.cs").Source.Should().Be(source);
    }

    [TestMethod]
    public async Task RemoveTrailingWhitespace_TrailingSpaces_RemovesThem()
    {
        string source = "class Sample\n{\n    int Value;   \n}\n";

        string fixedSource = await FixTrailingAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be("class Sample\n{\n    int Value;\n}\n");
    }

    [TestMethod]
    public async Task RemoveTrailingWhitespace_WhitespaceOnlyLine_LeavesLineEmpty()
    {
        string source = "class Sample\n{\n    \n    int Value;\n}\n";

        string fixedSource = await FixTrailingAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be("class Sample\n{\n\n    int Value;\n}\n");
    }

    [TestMethod]
    public async Task RemoveTrailingWhitespace_InsideRawString_LeavesSourceUnchanged()
    {
        string source = "class Sample\n{\n    string Value = \"\"\"\n        a   \n        b\n        \"\"\";\n}\n";

        string fixedSource = await FixTrailingAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(source);
    }
}
