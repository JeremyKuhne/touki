// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

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
