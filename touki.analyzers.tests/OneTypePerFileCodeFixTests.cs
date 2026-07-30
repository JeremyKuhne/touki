// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.CodeAnalysis.CodeFixes;

namespace Touki.Analyzers;

[TestClass]
public class OneTypePerFileCodeFixTests
{
    private static async Task<CodeFixTestResult> ApplyFixAsync(
        IReadOnlyList<(string Name, string FilePath, string Source)> sources,
        bool fixAll = false,
        bool expectFix = true)
    {
        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new OneTypePerFileAnalyzer(),
            new MoveTypeToFileCodeFixProvider(),
            sources,
            OneTypePerFileAnalyzer.DiagnosticId,
            fixAll).ConfigureAwait(false);

        result.CompilerErrors.Should().BeEmpty();
        if (expectFix)
        {
            result.AnalyzerDiagnostics.Should().NotContain(
                diagnostic => diagnostic.Id == OneTypePerFileAnalyzer.DiagnosticId);
        }
        else
        {
            result.AnalyzerDiagnostics.Should().Contain(
                diagnostic => diagnostic.Id == OneTypePerFileAnalyzer.DiagnosticId);
        }

        return result;
    }

    [TestMethod]
    public async Task ApplyFix_TopLevelDelegate_MovesDeclarationAndCopiesHeader()
    {
        const string Source = """
            // Copyright header

            namespace Sample;

            class Owner
            {
            }

            delegate void Handler();
            """;

        CodeFixTestResult result = await ApplyFixAsync(
            [("Owner.cs", "C:\\src\\Owner.cs", Source)]).ConfigureAwait(false);

        result.Documents.Should().HaveCount(2);
        CodeFixTestDocument source = result.Documents.Single(document => document.Name == "Owner.cs");
        CodeFixTestDocument destination = result.Documents.Single(document => document.Name == "Handler.cs");
        source.Source.Should().NotContain("delegate void Handler");
        destination.Source.Should().Contain("delegate void Handler();");
        destination.Source.Should().StartWith("// Copyright header");
    }

    [TestMethod]
    public async Task ApplyFix_TypeNameMatchesCurrentStem_UsesNumericSuffix()
    {
        const string Source = "class First { } class Bar { }";
        string directory = Path.Combine(Path.GetTempPath(), $"touki-duplicate-stem-{Guid.NewGuid():N}");
        string sourcePath = Path.Combine(directory, "Bar.cs");

        CodeFixTestResult result = await ApplyFixAsync(
            [("Bar.cs", sourcePath, Source)]).ConfigureAwait(false);

        result.Documents.Select(document => document.Name).Should().Contain("Bar.2.cs");
    }

    [TestMethod]
    public async Task ApplyFix_DirectoryOccupiesDestination_UsesDetailName()
    {
        const string Source = "class First { } class Second { }";
        string directory = Path.Combine(Path.GetTempPath(), $"touki-move-fix-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            string currentPath = Path.Combine(directory, "First.cs");
            File.WriteAllText(currentPath, Source);
            Directory.CreateDirectory(Path.Combine(directory, "Second.cs"));

            CodeFixTestResult result = await ApplyFixAsync(
                [("First.cs", currentPath, Source)]).ConfigureAwait(false);

            result.Documents.Select(document => document.Name).Should().Contain("Second.First.cs");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ApplyFix_CaseVariantOccupiesDestination_UsesDetailName()
    {
        const string Source = "class First { } class Second { }";
        string directory = Path.Combine(Path.GetTempPath(), $"touki-move-case-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            string currentPath = Path.Combine(directory, "First.cs");
            File.WriteAllText(currentPath, Source);
            File.WriteAllText(Path.Combine(directory, "second.cs"), "excluded");

            CodeFixTestResult result = await ApplyFixAsync(
                [("First.cs", currentPath, Source)]).ConfigureAwait(false);

            result.Documents.Select(document => document.Name).Should().Contain("Second.First.cs");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ApplyFix_NestedType_PreservesModifiersAndRemovesShellOnlySyntax()
    {
        const string Source = """
            [System.Obsolete]
            internal sealed unsafe class Outer<T>(int value) : Base where T : class
            {
                private int _value = value;

                private readonly record struct Nested;
            }
            """;

        CodeFixTestResult result = await ApplyFixAsync(
            [
                ("Base.cs", "C:\\src\\Base.cs", "class Base { }"),
                ("Outer.cs", "C:\\src\\Outer.cs", Source)
            ]).ConfigureAwait(false);

        CodeFixTestDocument source = result.Documents.Single(document => document.Name == "Outer.cs");
        CodeFixTestDocument destination = result.Documents.Single(document => document.Name == "Nested.cs");
        source.Source.Should().Contain("internal sealed unsafe partial class Outer<T>(int value) : Base where T : class");
        destination.Source.Should().Contain("internal sealed unsafe partial class Outer<T>");
        destination.Source.Should().Contain("private readonly record struct Nested;");
        destination.Source.Should().NotContain("System.Obsolete");
        destination.Source.Should().NotContain("(int value)");
        destination.Source.Should().NotContain(": Base");
        destination.Source.Should().NotContain("where T : class");
    }

    [TestMethod]
    public async Task ApplyFix_TypeWrappedInConditionalDirective_LeavesSolutionUnchanged()
    {
        const string Source = """
            class Owner
            {
            }

            #if true
            class WindowsOnly
            {
            }
            #endif
            """;

        CodeFixTestResult result = await ApplyFixAsync(
            [("Owner.cs", "C:\\src\\Owner.cs", Source)],
            expectFix: false).ConfigureAwait(false);

        CodeFixTestDocument document = result.Documents.Should().ContainSingle().Subject;
        document.Name.Should().Be("Owner.cs");
        document.Source.Should().Be(Source);
    }

    [TestMethod]
    public async Task ApplyFix_FileContainsConditionalUsing_LeavesSolutionUnchanged()
    {
        const string Source = """
            #if TARGET_WINDOWS
            using System;
            #endif

            class Owner
            {
            }

            class Other
            {
            }
            """;

        CodeFixTestResult result = await ApplyFixAsync(
            [("Owner.cs", "C:\\src\\Owner.cs", Source)],
            expectFix: false).ConfigureAwait(false);

        CodeFixTestDocument document = result.Documents.Should().ContainSingle().Subject;
        document.Source.Should().Be(Source);
    }

    [TestMethod]
    public async Task ApplyFix_FileLocalEnum_LeavesSolutionUnchanged()
    {
        const string Source = "class Owner { } file enum Hidden { None }";

        CodeFixTestResult result = await ApplyFixAsync(
            [("Owner.cs", "C:\\src\\Owner.cs", Source)],
            expectFix: false).ConfigureAwait(false);

        CodeFixTestDocument document = result.Documents.Should().ContainSingle().Subject;
        document.Source.Should().Be(Source);
    }

    [TestMethod]
    public async Task ApplyFix_FileLocalDelegate_LeavesSolutionUnchanged()
    {
        const string Source = "class Owner { } file delegate void Hidden();";

        CodeFixTestResult result = await ApplyFixAsync(
            [("Owner.cs", "C:\\src\\Owner.cs", Source)],
            expectFix: false).ConfigureAwait(false);

        CodeFixTestDocument document = result.Documents.Should().ContainSingle().Subject;
        document.Source.Should().Be(Source);
    }

    [TestMethod]
    public async Task ApplyFix_TypeReferencesFileLocalType_LeavesSolutionUnchanged()
    {
        const string Source = """
            class Owner
            {
            }

            class Consumer
            {
                public void UseHidden()
                {
                    Hidden hidden = new();
                    _ = hidden;
                }
            }

            file class Hidden
            {
            }
            """;

        CodeFixTestResult result = await ApplyFixAsync(
            [("Owner.cs", "C:\\src\\Owner.cs", Source)],
            expectFix: false).ConfigureAwait(false);

        CodeFixTestDocument document = result.Documents.Should().ContainSingle().Subject;
        document.Source.Should().Be(Source);
    }

    [TestMethod]
    public async Task ApplyFix_FileContainsUnrelatedFileLocalType_LeavesSolutionUnchanged()
    {
        const string Source = """
            class Owner
            {
            }

            class Other
            {
            }

            file class Hidden
            {
            }
            """;

        CodeFixTestResult result = await ApplyFixAsync(
            [("Owner.cs", "C:\\src\\Owner.cs", Source)],
            expectFix: false).ConfigureAwait(false);

        CodeFixTestDocument document = result.Documents.Should().ContainSingle().Subject;
        document.Source.Should().Be(Source);
    }

    [TestMethod]
    public async Task ApplyFix_AttributedTypeParameter_StripsAttributeFromPartialShell()
    {
        const string Source = """
            partial class Outer<[Marker] T>
            {
                private int _value;

                class Nested
                {
                }
            }
            """;

        CodeFixTestResult result = await ApplyFixAsync(
            [
                ("Marker.cs", "C:\\src\\Marker.cs", "class MarkerAttribute : System.Attribute { }"),
                ("Outer.cs", "C:\\src\\Outer.cs", Source)
            ]).ConfigureAwait(false);

        CodeFixTestDocument source = result.Documents.Single(document => document.Name == "Outer.cs");
        CodeFixTestDocument destination = result.Documents.Single(document => document.Name == "Nested.cs");
        source.Source.Should().Contain("Outer<[Marker] T>");
        destination.Source.Should().Contain("partial class Outer<T>");
        destination.Source.Should().NotContain("[Marker]");
    }

    [TestMethod]
    public async Task ApplyFix_RepeatedPartialDeclarations_MovesAllDeclarations()
    {
        const string Source = """
            class First
            {
            }

            partial class Second
            {
                private int _first;
            }

            partial class Second
            {
                private int _second;
            }
            """;

        CodeFixTestResult result = await ApplyFixAsync(
            [("First.cs", "C:\\src\\First.cs", Source)]).ConfigureAwait(false);

        CodeFixTestDocument source = result.Documents.Single(document => document.Name == "First.cs");
        CodeFixTestDocument destination = result.Documents.Single(document => document.Name == "Second.cs");
        source.Source.Should().NotContain("class Second");
        destination.Source.Should().Contain("private int _first;");
        destination.Source.Should().Contain("private int _second;");
    }

    [TestMethod]
    public async Task ApplyFix_SoleNestedType_RemovesEmptyHostingShell()
    {
        const string Source = """
            class First
            {
            }

            partial class Outer
            {
                class Nested
                {
                }
            }
            """;

        CodeFixTestResult result = await ApplyFixAsync(
            [("First.cs", "C:\\src\\First.cs", Source)]).ConfigureAwait(false);

        CodeFixTestDocument source = result.Documents.Single(document => document.Name == "First.cs");
        CodeFixTestDocument destination = result.Documents.Single(document => document.Name == "Nested.cs");
        source.Source.Should().NotContain("class Outer");
        destination.Source.Should().Contain("partial class Outer");
        destination.Source.Should().Contain("class Nested");
    }

    [TestMethod]
    public async Task ApplyFixAll_ThreeTopLevelTypes_MovesAllButFirst()
    {
        const string Source = """
            class First
            {
            }

            class Second
            {
            }

            delegate void Third();
            """;

        CodeFixTestResult result = await ApplyFixAsync(
            [("First.cs", "C:\\src\\First.cs", Source)],
            fixAll: true).ConfigureAwait(false);

        result.Documents.Select(document => document.Name).Should().BeEquivalentTo(
            ["First.cs", "Second.cs", "Third.cs"]);
        result.Documents.Single(document => document.Name == "First.cs").Source
            .Should().NotContain("class Second").And.NotContain("delegate void Third");
    }

    [TestMethod]
    public async Task ApplyFixAll_NestedHierarchy_MovesDeepestTypeFirst()
    {
        const string Source = """
            class Outer
            {
                class Middle
                {
                    class Leaf
                    {
                    }
                }
            }
            """;

        CodeFixTestResult result = await ApplyFixAsync(
            [("Outer.cs", "C:\\src\\Outer.cs", Source)],
            fixAll: true).ConfigureAwait(false);

        result.Documents.Select(document => document.Name).Should().BeEquivalentTo(
            ["Outer.cs", "Middle.cs", "Leaf.cs"]);
        CodeFixTestDocument source = result.Documents.Single(document => document.Name == "Outer.cs");
        source.Source.Should().Contain("partial class Outer");
        CodeFixTestDocument leaf = result.Documents.Single(document => document.Name == "Leaf.cs");
        leaf.Source.Should().Contain("partial class Outer");
        leaf.Source.Should().Contain("partial class Middle");
        leaf.Source.Should().Contain("class Leaf");
    }

    [TestMethod]
    public async Task ApplyFix_DocumentedPartialHost_PreservesHostAndDocumentation()
    {
        const string Source = """
            /// <summary>Owns nested types.</summary>
            partial class Outer
            {
                private int _value;

                class Nested
                {
                }
            }
            """;

        CodeFixTestResult result = await ApplyFixAsync(
            [("Outer.cs", "C:\\src\\Outer.cs", Source)]).ConfigureAwait(false);

        CodeFixTestDocument source = result.Documents.Single(document => document.Name == "Outer.cs");
        source.Source.Should().Contain("/// <summary>Owns nested types.</summary>");
        source.Source.Should().Contain("partial class Outer");
    }

    [TestMethod]
    public async Task ApplyFixAll_SameNestedName_UsesQualifiedFallback()
    {
        const string Source = """
            class First
            {
                class BitReader
                {
                }
            }

            class Second
            {
                class BitReader
                {
                }
            }
            """;

        CodeFixTestResult result = await ApplyFixAsync(
            [("First.cs", "C:\\src\\First.cs", Source)],
            fixAll: true).ConfigureAwait(false);

        result.Documents.Select(document => document.Name).Should().BeEquivalentTo(
            ["First.cs", "BitReader.cs", "Second.cs", "Second.BitReader.cs"]);
    }

    [TestMethod]
    public async Task ApplyFixAll_MsBuildWorkspace_LeavesSolutionUnchanged()
    {
        const string Source = "class First { } class Second { }";

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new OneTypePerFileAnalyzer(),
            new MoveTypeToFileCodeFixProvider(),
            [("First.cs", "C:\\src\\First.cs", Source)],
            OneTypePerFileAnalyzer.DiagnosticId,
            fixAll: true,
            workspaceKind: WorkspaceKind.MSBuild).ConfigureAwait(false);

        CodeFixTestDocument document = result.Documents.Should().ContainSingle().Subject;
        document.Name.Should().Be("First.cs");
        document.Source.Should().Be(Source);
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().Contain(
            diagnostic => diagnostic.Id == OneTypePerFileAnalyzer.DiagnosticId);
        result.FixAllActionOffered.Should().BeFalse();
    }

    [TestMethod]
    public async Task ApplyFix_MsBuildWorkspace_LeavesSolutionUnchanged()
    {
        const string Source = "class First { } class Second { }";

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new OneTypePerFileAnalyzer(),
            new MoveTypeToFileCodeFixProvider(),
            [("First.cs", "C:\\src\\First.cs", Source)],
            OneTypePerFileAnalyzer.DiagnosticId,
            fixAll: false,
            workspaceKind: WorkspaceKind.MSBuild).ConfigureAwait(false);

        CodeFixTestDocument document = result.Documents.Should().ContainSingle().Subject;
        document.Name.Should().Be("First.cs");
        document.Source.Should().Be(Source);
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().Contain(
            diagnostic => diagnostic.Id == OneTypePerFileAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public void GetFixAllProvider_DefaultScopes_ContainsSolution()
    {
        FixAllProvider provider = new MoveTypeToFileCodeFixProvider().GetFixAllProvider();

        provider.GetSupportedFixAllScopes().Should().Contain(FixAllScope.Solution);
    }
}