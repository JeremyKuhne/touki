// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Analyzers;

[TestClass]
public class OneTypePerFileAnalyzerTests
{
    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        string source,
        string? excludeNestedTypes = null)
    {
        Dictionary<string, string>? options = excludeNestedTypes is null
            ? null
            : new Dictionary<string, string>
            {
                [OneTypePerFileAnalyzer.ExcludeNestedTypesOption] = excludeNestedTypes
            };

        return await AnalyzerTestHarness
            .GetDiagnosticsAsync(new OneTypePerFileAnalyzer(), source, options)
            .ConfigureAwait(false);
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_SingleType_ReportsNothing()
    {
        const string source = """
            namespace Sample;

            public class Only
            {
                private int _value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_TwoTypes_ReportsSecond()
    {
        const string source = """
            namespace Sample;

            public class First
            {
            }

            public class Second
            {
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(OneTypePerFileAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_TwoTypes_ReportsAtIdentifier()
    {
        const string source = """
            namespace Sample;

            public class First
            {
            }

            public class Second
            {
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        Location location = diagnostics.Should().ContainSingle().Subject.Location;
        location.SourceTree!.GetText().ToString(location.SourceSpan).Should().Be("Second");
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_TwoTypes_MessageNamesBothTypes()
    {
        const string source = """
            namespace Sample;

            public class First
            {
            }

            public class Second
            {
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Be("Move 'Second' to its own file, 'First' is already declared in this file");
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_ThreeTypes_ReportsAllButFirst()
    {
        const string source = """
            namespace Sample;

            public class First
            {
            }

            public class Second
            {
            }

            public class Third
            {
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().HaveCount(2);
        diagnostics.Should().OnlyContain(diagnostic => diagnostic.Id == OneTypePerFileAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_NestedTypes_ReportsEachNestedType()
    {
        const string source = """
            namespace Sample;

            public class Outer
            {
                private struct Nested
                {
                }

                public enum Kind
                {
                    None
                }

                public delegate void Handler();
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().HaveCount(3);
        diagnostics.Should().OnlyContain(diagnostic => diagnostic.Id == OneTypePerFileAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_NestedTypesExcluded_ReportsNothing()
    {
        const string source = """
            namespace Sample;

            public class Outer
            {
                private struct Nested
                {
                }

                public enum Kind
                {
                    None
                }

                public delegate void Handler();
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, "true").ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_NestedTypesExcluded_StillReportsSecondTopLevelType()
    {
        const string source = """
            namespace Sample;

            public class First
            {
                private struct Nested
                {
                }
            }

            public class Second
            {
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, "true").ConfigureAwait(false);

        Location location = diagnostics.Should().ContainSingle().Subject.Location;
        location.SourceTree!.GetText().ToString(location.SourceSpan).Should().Be("Second");
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_NestedType_ReportsAtNestedIdentifier()
    {
        const string source = """
            namespace Sample;

            public class Outer
            {
                private int _value;

                private struct Nested
                {
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        Location location = diagnostics.Should().ContainSingle().Subject.Location;
        location.SourceTree!.GetText().ToString(location.SourceSpan).Should().Be("Nested");
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_PartialShellHostingOneNestedType_ReportsNothing()
    {
        const string source = """
            namespace Sample;

            public partial class Outer
            {
                public ref struct Enumerator
                {
                    private int _index;

                    public bool MoveNext() => _index++ < 0;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_DoublyNestedTypes_ReportsEachNestedType()
    {
        const string source = """
            namespace Sample;

            public class Outer
            {
                public class Middle
                {
                    public struct Leaf
                    {
                    }
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().HaveCount(2);
        diagnostics.Select(diagnostic => diagnostic.GetMessage()).Should().Equal(
            "Move 'Middle' to its own file, 'Outer' is already declared in this file",
            "Move 'Leaf' to its own file, 'Outer' is already declared in this file");
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_PartialShellHostingDoublyNestedType_ReportsLeaf()
    {
        const string source = """
            namespace Sample;

            public partial class Outer
            {
                public class Middle
                {
                    private int _value;

                    public struct Leaf
                    {
                    }
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Be("Move 'Leaf' to its own file, 'Middle' is already declared in this file");
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_NestedPartialShells_ReportsNothing()
    {
        const string source = """
            namespace Sample;

            public partial class Outer
            {
                public partial class Middle
                {
                    public struct Leaf
                    {
                    }
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_EmptyPartialBesideAnotherType_ReportsSecond()
    {
        const string source = """
            namespace Sample;

            public partial class Empty
            {
            }

            public class Other
            {
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        Location location = diagnostics.Should().ContainSingle().Subject.Location;
        location.SourceTree!.GetText().ToString(location.SourceSpan).Should().Be("Other");
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_PartialShellHostingTwoNestedTypes_ReportsSecond()
    {
        const string source = """
            namespace Sample;

            public partial class Outer
            {
                public struct First
                {
                }

                public struct Second
                {
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        Location location = diagnostics.Should().ContainSingle().Subject.Location;
        location.SourceTree!.GetText().ToString(location.SourceSpan).Should().Be("Second");
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_PartialWithOwnMemberAndNestedType_ReportsNestedType()
    {
        const string source = """
            namespace Sample;

            public partial class Outer
            {
                private int _value;

                public struct Nested
                {
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        Location location = diagnostics.Should().ContainSingle().Subject.Location;
        location.SourceTree!.GetText().ToString(location.SourceSpan).Should().Be("Nested");
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_PartialDeclarationsOfSameType_ReportsNothing()
    {
        const string source = """
            namespace Sample;

            public partial class Split
            {
                private int _first;
            }

            public partial class Split
            {
                private int _second;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_PartialTypesOfDifferentArity_ReportsSecond()
    {
        const string source = """
            namespace Sample;

            public partial class Split
            {
                private int _first;
            }

            public partial class Split<T>
            {
                private int _second;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Be("Move 'Split<T>' to its own file, 'Split' is already declared in this file");
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_PartialClassAndPartialStructOfSameName_ReportsSecond()
    {
        const string source = """
            namespace Sample;

            public partial class Shape
            {
                private int _first;
            }

            public partial struct Shape
            {
                private int _second;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new OneTypePerFileAnalyzer(),
            source,
            expectedCompilerDiagnosticIds: ["CS0261"]).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(OneTypePerFileAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_EnumBesideClass_ReportsEnum()
    {
        const string source = """
            namespace Sample;

            public class Owner
            {
            }

            public enum Kind
            {
                None
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        Location location = diagnostics.Should().ContainSingle().Subject.Location;
        location.SourceTree!.GetText().ToString(location.SourceSpan).Should().Be("Kind");
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_DelegateBesideClass_ReportsDelegate()
    {
        const string source = """
            namespace Sample;

            public class Owner
            {
            }

            public delegate void Handler();
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        Location location = diagnostics.Should().ContainSingle().Subject.Location;
        location.SourceTree!.GetText().ToString(location.SourceSpan).Should().Be("Handler");
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_RecordBesideInterface_ReportsRecord()
    {
        const string source = """
            namespace Sample;

            public interface IOwner
            {
            }

            public record Owner(int Value);
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        Location location = diagnostics.Should().ContainSingle().Subject.Location;
        location.SourceTree!.GetText().ToString(location.SourceSpan).Should().Be("Owner");
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_TypesInSeparateNamespaceBlocks_ReportsSecond()
    {
        const string source = """
            namespace First
            {
                public class One
                {
                }
            }

            namespace Second
            {
                public class Two
                {
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        Location location = diagnostics.Should().ContainSingle().Subject.Location;
        location.SourceTree!.GetText().ToString(location.SourceSpan).Should().Be("Two");
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_TypeOutsideAndInsideNamespace_ReportsSecond()
    {
        const string source = """
            public class Global
            {
            }

            namespace Sample
            {
                public class Scoped
                {
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        Location location = diagnostics.Should().ContainSingle().Subject.Location;
        location.SourceTree!.GetText().ToString(location.SourceSpan).Should().Be("Scoped");
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_NoTypes_ReportsNothing()
    {
        const string source = """
            using System;

            [assembly: CLSCompliant(false)]
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_GeneratedCode_ReportsNothing()
    {
        const string source = """
            // <auto-generated/>
            namespace Sample;

            public class First
            {
            }

            public class Second
            {
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }
}
