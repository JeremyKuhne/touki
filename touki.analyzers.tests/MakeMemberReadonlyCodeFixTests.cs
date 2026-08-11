// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Analyzers;

[TestClass]
public class MakeMemberReadonlyCodeFixTests
{
    private const string Types = """
        using System;
        using Touki;

        namespace Touki
        {
            [AttributeUsage(AttributeTargets.Struct)]
            sealed class NonCopyableAttribute : Attribute { }
        }

        [NonCopyable]
        struct Pooled
        {
            private int _value;
            public int Prop => _value;
            public int Read() => _value;
        }

        """;

    [TestMethod]
    public async Task DefensiveCopy_OnNonMutatingProperty_AddsReadonly()
    {
        string source = Types + """
            class C
            {
                int M(in Pooled p) => p.Prop;
            }
            """;

        string fixedSource = await CodeFixTestHarness.ApplyFixAsync(
            new DefensiveCopyAnalyzer(),
            new MakeMemberReadonlyCodeFixProvider(),
            source,
            DefensiveCopyAnalyzer.NonCopyableDefensiveCopyId).ConfigureAwait(false);

        fixedSource.Should().Contain("public readonly int Prop => _value;");
    }

    [TestMethod]
    public async Task DefensiveCopy_OnNonMutatingMethod_AddsReadonly()
    {
        string source = Types + """
            class C
            {
                int M(in Pooled p) => p.Read();
            }
            """;

        string fixedSource = await CodeFixTestHarness.ApplyFixAsync(
            new DefensiveCopyAnalyzer(),
            new MakeMemberReadonlyCodeFixProvider(),
            source,
            DefensiveCopyAnalyzer.NonCopyableDefensiveCopyId).ConfigureAwait(false);

        fixedSource.Should().Contain("public readonly int Read() => _value;");
    }

    [TestMethod]
    public async Task DefensiveCopy_OnPartialMethod_AddsReadonlyToBothDeclarations()
    {
        const string source = """
            using System;
            using Touki;

            namespace Touki
            {
                [AttributeUsage(AttributeTargets.Struct)]
                sealed class NonCopyableAttribute : Attribute { }
            }

            [NonCopyable]
            partial struct Pooled
            {
                private int _value;
                public partial int Read();
                public partial int Read() => _value;
            }

            class C
            {
                int M(in Pooled p) => p.Read();
            }
            """;

        string fixedSource = await CodeFixTestHarness.ApplyFixAsync(
            new DefensiveCopyAnalyzer(),
            new MakeMemberReadonlyCodeFixProvider(),
            source,
            DefensiveCopyAnalyzer.NonCopyableDefensiveCopyId).ConfigureAwait(false);

        fixedSource.Should().Contain("public readonly partial int Read();");
        fixedSource.Should().Contain("public readonly partial int Read() => _value;");
    }

    [TestMethod]
    public async Task DefensiveCopy_OnPartialMethodAcrossDocuments_AddsReadonlyToBothDeclarations()
    {
        const string Marker = """
            using System;

            namespace Touki
            {
                [AttributeUsage(AttributeTargets.Struct)]
                sealed class NonCopyableAttribute : Attribute { }
            }
            """;
        const string Definition = """
            using Touki;

            [NonCopyable]
            partial struct Pooled
            {
                public partial int Read();
            }
            """;
        const string Implementation = """
            partial struct Pooled
            {
                private int _value;
                public partial int Read() => _value;
            }

            class C
            {
                int M(in Pooled p) => p.Read();
            }
            """;

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new DefensiveCopyAnalyzer(),
            new MakeMemberReadonlyCodeFixProvider(),
            [
                ("Marker.cs", "C:\\src\\Marker.cs", Marker),
                ("Definition.cs", "C:\\src\\Definition.cs", Definition),
                ("Implementation.cs", "C:\\src\\Implementation.cs", Implementation)
            ],
            DefensiveCopyAnalyzer.NonCopyableDefensiveCopyId,
            fixAll: false).ConfigureAwait(false);

        result.CompilerErrors.Should().BeEmpty();
        result.Documents.Single(document => document.Name == "Definition.cs").Source
            .Should().Contain("public readonly partial int Read();");
        result.Documents.Single(document => document.Name == "Implementation.cs").Source
            .Should().Contain("public readonly partial int Read() => _value;");
    }

    [TestMethod]
    public async Task DefensiveCopy_OnPartialProperty_AddsReadonlyToBothDeclarations()
    {
        const string source = """
            using System;
            using Touki;

            namespace Touki
            {
                [AttributeUsage(AttributeTargets.Struct)]
                sealed class NonCopyableAttribute : Attribute { }
            }

            [NonCopyable]
            partial struct Pooled
            {
                private int _value;
                public partial int Prop { get; }
                public partial int Prop => _value;
            }

            class C
            {
                int M(in Pooled p) => p.Prop;
            }
            """;

        string fixedSource = await CodeFixTestHarness.ApplyFixAsync(
            new DefensiveCopyAnalyzer(),
            new MakeMemberReadonlyCodeFixProvider(),
            source,
            DefensiveCopyAnalyzer.NonCopyableDefensiveCopyId).ConfigureAwait(false);

        fixedSource.Should().Contain("public readonly partial int Prop { get; }");
        fixedSource.Should().Contain("public readonly partial int Prop => _value;");
    }

    [TestMethod]
    public async Task DefensiveCopy_OnPartialPropertyAcrossDocuments_AddsReadonlyToBothDeclarations()
    {
        const string Marker = """
            using System;

            namespace Touki
            {
                [AttributeUsage(AttributeTargets.Struct)]
                sealed class NonCopyableAttribute : Attribute { }
            }
            """;
        const string Definition = """
            using Touki;

            [NonCopyable]
            partial struct Pooled
            {
                public partial int Prop { get; }
            }
            """;
        const string Implementation = """
            partial struct Pooled
            {
                private int _value;
                public partial int Prop => _value;
            }

            class C
            {
                int M(in Pooled p) => p.Prop;
            }
            """;

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new DefensiveCopyAnalyzer(),
            new MakeMemberReadonlyCodeFixProvider(),
            [
                ("Marker.cs", "C:\\src\\Marker.cs", Marker),
                ("Definition.cs", "C:\\src\\Definition.cs", Definition),
                ("Implementation.cs", "C:\\src\\Implementation.cs", Implementation)
            ],
            DefensiveCopyAnalyzer.NonCopyableDefensiveCopyId,
            fixAll: false).ConfigureAwait(false);

        result.CompilerErrors.Should().BeEmpty();
        result.Documents.Single(document => document.Name == "Definition.cs").Source
            .Should().Contain("public readonly partial int Prop { get; }");
        result.Documents.Single(document => document.Name == "Implementation.cs").Source
            .Should().Contain("public readonly partial int Prop => _value;");
    }

    [TestMethod]
    public async Task DefensiveCopy_OnPartialIndexerAcrossDocuments_AddsReadonlyToBothDeclarations()
    {
        const string Marker = """
            using System;

            namespace Touki
            {
                [AttributeUsage(AttributeTargets.Struct)]
                sealed class NonCopyableAttribute : Attribute { }
            }
            """;
        const string Definition = """
            using Touki;

            [NonCopyable]
            partial struct Pooled
            {
                public partial int this[int index] { get; }
            }
            """;
        const string Implementation = """
            partial struct Pooled
            {
                private int _value;
                public partial int this[int index] { get => _value + index; }
            }

            class C
            {
                int M(in Pooled p) => p[0];
            }
            """;

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new DefensiveCopyAnalyzer(),
            new MakeMemberReadonlyCodeFixProvider(),
            [
                ("Marker.cs", "C:\\src\\Marker.cs", Marker),
                ("Definition.cs", "C:\\src\\Definition.cs", Definition),
                ("Implementation.cs", "C:\\src\\Implementation.cs", Implementation)
            ],
            DefensiveCopyAnalyzer.NonCopyableDefensiveCopyId,
            fixAll: false).ConfigureAwait(false);

        result.CompilerErrors.Should().BeEmpty();
        result.Documents.Single(document => document.Name == "Definition.cs").Source
            .Should().Contain("public readonly partial int this[int index] { get; }");
        result.Documents.Single(document => document.Name == "Implementation.cs").Source
            .Should().Contain("public readonly partial int this[int index] { get => _value + index; }");
    }

    [TestMethod]
    public async Task DefensiveCopy_OnPropertyWithSetter_OffersNoFix()
    {
        // A member-level 'readonly' would also mark the setter readonly, which is a compiler error. The fix must
        // not be offered here, so ApplyFixAsync returns the source unchanged.
        const string source = """
            using System;
            using Touki;

            namespace Touki
            {
                [AttributeUsage(AttributeTargets.Struct)]
                sealed class NonCopyableAttribute : Attribute { }
            }

            [NonCopyable]
            struct Pooled
            {
                private int _value;
                public int Prop { get => _value; set => _value = value; }
            }

            class C
            {
                int M(in Pooled p) => p.Prop;
            }
            """;

        string fixedSource = await CodeFixTestHarness.ApplyFixAsync(
            new DefensiveCopyAnalyzer(),
            new MakeMemberReadonlyCodeFixProvider(),
            source,
            DefensiveCopyAnalyzer.NonCopyableDefensiveCopyId).ConfigureAwait(false);

        fixedSource.Should().Be(source);
    }
}
