// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.CodeAnalysis.CodeFixes;

namespace Touki.Analyzers;

[TestClass]
public class FileNameMatchesTypeCodeFixTests
{
    private static async Task<CodeFixTestResult> ApplyFixAsync(
        IReadOnlyList<(string Name, string FilePath, string Source)> sources,
        bool fixAll = false,
        IReadOnlyDictionary<string, string>? options = null)
    {
        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new FileNameMatchesTypeAnalyzer(),
            new RenameFileToMatchTypeCodeFixProvider(),
            sources,
            FileNameMatchesTypeAnalyzer.DiagnosticId,
            fixAll,
            options).ConfigureAwait(false);

        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().NotContain(
            diagnostic => diagnostic.Id == FileNameMatchesTypeAnalyzer.DiagnosticId);
        return result;
    }

    [TestMethod]
    public async Task ApplyFix_NameDiffers_RenamesFileAndPreservesSource()
    {
        const string Source = "class Foo { }";
        string directory = Path.Combine(Path.GetTempPath(), $"touki-rename-{Guid.NewGuid():N}");
        string sourcePath = Path.Combine(directory, "Other.cs");

        CodeFixTestResult result = await ApplyFixAsync(
            [("Other.cs", sourcePath, Source)]).ConfigureAwait(false);

        CodeFixTestDocument document = result.Documents.Should().ContainSingle().Subject;
        document.Name.Should().Be("Foo.cs");
        document.FilePath.Should().Be(Path.Combine(directory, "Foo.cs"));
        document.Source.Should().Be(Source);
    }

    [TestMethod]
    public async Task ApplyFix_CaseDiffers_PerformsCaseOnlyRename()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"touki-case-rename-{Guid.NewGuid():N}");
        string sourcePath = Path.Combine(directory, "foo.cs");

        CodeFixTestResult result = await ApplyFixAsync(
            [("foo.cs", sourcePath, "class Foo { }")]).ConfigureAwait(false);

        CodeFixTestDocument document = result.Documents.Should().ContainSingle().Subject;
        document.Name.Should().Be("Foo.cs");
        document.FilePath.Should().Be(Path.Combine(directory, "Foo.cs"));
    }

    [TestMethod]
    public async Task ApplyFix_UnrootedPath_NormalizesToIsolatedAbsolutePath()
    {
        CodeFixTestResult result = await ApplyFixAsync(
            [("Other.cs", Path.Combine("relative", "Other.cs"), "class Foo { }")]).ConfigureAwait(false);

        CodeFixTestDocument document = result.Documents.Should().ContainSingle().Subject;
        document.FilePath.Should().NotBeNull();
        Path.IsPathFullyQualified(document.FilePath!).Should().BeTrue();
        document.FilePath!.EndsWith(
            Path.Combine("relative", "Foo.cs"),
            StringComparison.Ordinal).Should().BeTrue();
        document.FilePath.StartsWith(
            Path.GetTempPath(),
            StringComparison.OrdinalIgnoreCase).Should().BeTrue();
    }

    [TestMethod]
    public async Task ApplyFix_DirectoryOccupiesDestination_UsesSuffix()
    {
        const string Source = "class Foo { }";
        string directory = Path.Combine(Path.GetTempPath(), $"touki-file-fix-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            string currentPath = Path.Combine(directory, "Other.cs");
            File.WriteAllText(currentPath, Source);
            Directory.CreateDirectory(Path.Combine(directory, "Foo.cs"));

            CodeFixTestResult result = await ApplyFixAsync(
                [("Other.cs", currentPath, Source)]).ConfigureAwait(false);

            result.Documents.Should().ContainSingle().Which.Name.Should().Be("Foo.2.cs");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ApplyFix_CaseSensitiveTwinOccupiesDestination_UsesSuffix()
    {
        const string Source = "class Foo { }";
        string directory = Path.Combine(Path.GetTempPath(), $"touki-case-fix-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            string currentPath = Path.Combine(directory, "foo.cs");
            string targetPath = Path.Combine(directory, "Foo.cs");
            File.WriteAllText(currentPath, Source);
            File.WriteAllText(targetPath, "excluded");

            int caseVariants = Directory.EnumerateFileSystemEntries(directory)
                .Count(path => string.Equals(Path.GetFileName(path), "foo.cs", StringComparison.Ordinal)
                    || string.Equals(Path.GetFileName(path), "Foo.cs", StringComparison.Ordinal));
            if (caseVariants < 2)
            {
                return;
            }

            CodeFixTestResult result = await ApplyFixAsync(
                [("foo.cs", currentPath, Source)]).ConfigureAwait(false);

            result.Documents.Should().ContainSingle().Which.Name.Should().Be("Foo.2.cs");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ApplyFixAll_SplitPartialType_UsesCurrentStemsAsDetail()
    {
        CodeFixTestResult result = await ApplyFixAsync(
            [
                ("FirstPart.cs", "C:\\src\\FirstPart.cs", "partial class Foo { int First; }"),
                ("SecondPart.cs", "C:\\src\\SecondPart.cs", "partial class Foo { int Second; }")
            ],
            fixAll: true).ConfigureAwait(false);

        result.Documents.Select(document => document.Name).Should().BeEquivalentTo(
            ["Foo.FirstPart.cs", "Foo.SecondPart.cs"]);
    }

    [TestMethod]
    public async Task ApplyFixAll_SplitPartialType_UsesConfiguredDetailSeparator()
    {
        CodeFixTestResult result = await ApplyFixAsync(
            [
                ("FirstPart.cs", "C:\\src\\FirstPart.cs", "partial class Foo { int First; }"),
                ("SecondPart.cs", "C:\\src\\SecondPart.cs", "partial class Foo { int Second; }")
            ],
            fixAll: true,
            new Dictionary<string, string>
            {
                [FileNameMatchesTypeAnalyzer.DetailSeparatorsOption] = "-"
            }).ConfigureAwait(false);

        result.Documents.Select(document => document.Name).Should().BeEquivalentTo(
            ["Foo-FirstPart.cs", "Foo-SecondPart.cs"]);
    }

    [TestMethod]
    public async Task ApplyFixAll_SplitPartialType_InvalidConfiguredSeparatorFallsBackToDefault()
    {
        CodeFixTestResult result = await ApplyFixAsync(
            [
                ("FirstPart.cs", "C:\\src\\FirstPart.cs", "partial class Foo { int First; }"),
                ("SecondPart.cs", "C:\\src\\SecondPart.cs", "partial class Foo { int Second; }")
            ],
            fixAll: true,
            new Dictionary<string, string>
            {
                [FileNameMatchesTypeAnalyzer.DetailSeparatorsOption] = "/"
            }).ConfigureAwait(false);

        result.Documents.Select(document => document.Name).Should().BeEquivalentTo(
            ["Foo.FirstPart.cs", "Foo.SecondPart.cs"]);
    }

    [TestMethod]
    public async Task ApplyFixAll_PreferredNameOccupied_UsesDetailAndRenamesOccupant()
    {
        CodeFixTestResult result = await ApplyFixAsync(
            [
                ("Unrelated.cs", "C:\\src\\Unrelated.cs", "class Foo { }"),
                ("Foo.cs", "C:\\src\\Foo.cs", "class Occupant { }")
            ],
            fixAll: true).ConfigureAwait(false);

        result.Documents.Select(document => document.Name).Should().BeEquivalentTo(
            ["Foo.Unrelated.cs", "Occupant.cs"]);
    }

    [TestMethod]
    public async Task ApplyFixAll_MsBuildWorkspace_LeavesSolutionUnchanged()
    {
        const string Source = "class Foo { }";

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new FileNameMatchesTypeAnalyzer(),
            new RenameFileToMatchTypeCodeFixProvider(),
            [("Other.cs", "C:\\src\\Other.cs", Source)],
            FileNameMatchesTypeAnalyzer.DiagnosticId,
            fixAll: true,
            workspaceKind: WorkspaceKind.MSBuild).ConfigureAwait(false);

        CodeFixTestDocument document = result.Documents.Should().ContainSingle().Subject;
        document.Name.Should().Be("Other.cs");
        document.Source.Should().Be(Source);
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().Contain(
            diagnostic => diagnostic.Id == FileNameMatchesTypeAnalyzer.DiagnosticId);
        result.FixAllActionOffered.Should().BeFalse();
    }

    [TestMethod]
    public async Task ApplyFix_MsBuildWorkspace_LeavesSolutionUnchanged()
    {
        const string Source = "class Foo { }";

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new FileNameMatchesTypeAnalyzer(),
            new RenameFileToMatchTypeCodeFixProvider(),
            [("Other.cs", "C:\\src\\Other.cs", Source)],
            FileNameMatchesTypeAnalyzer.DiagnosticId,
            fixAll: false,
            workspaceKind: WorkspaceKind.MSBuild).ConfigureAwait(false);

        CodeFixTestDocument document = result.Documents.Should().ContainSingle().Subject;
        document.Name.Should().Be("Other.cs");
        document.Source.Should().Be(Source);
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().Contain(
            diagnostic => diagnostic.Id == FileNameMatchesTypeAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public void GetFixAllProvider_DefaultScopes_ContainsSolution()
    {
        FixAllProvider provider = new RenameFileToMatchTypeCodeFixProvider().GetFixAllProvider();

        provider.GetSupportedFixAllScopes().Should().Contain(FixAllScope.Solution);
    }
}