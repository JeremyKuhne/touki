// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Text;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

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
    public void GetFixAllProvider_Default_IsSolutionAware()
    {
        FixAllProvider provider = new MakeMemberReadonlyCodeFixProvider().GetFixAllProvider();

        provider.Should().NotBeSameAs(WellKnownFixAllProviders.BatchFixer);
        provider.GetSupportedFixAllScopes().Should().BeEquivalentTo(
            [FixAllScope.Document, FixAllScope.Project, FixAllScope.Solution]);
    }

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

    [TestMethod]
    public async Task DefensiveCopy_CSharp73ReadonlyMemberUnavailable_OffersNoFix()
    {
        string source = Types + """
            class C
            {
                int M(in Pooled p) => p.Read();
            }
            """;

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new DefensiveCopyAnalyzer(),
            new MakeMemberReadonlyCodeFixProvider(),
            [("Test.cs", "Test.cs", source)],
            DefensiveCopyAnalyzer.NonCopyableDefensiveCopyId,
            fixAll: false,
            parseOptions: new CSharpParseOptions(LanguageVersion.CSharp7_3)).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(1);
        result.CodeFixActionOffered.Should().BeFalse();
    }

    [TestMethod]
    public async Task DefensiveCopy_LinkedDeclaration_OffersNoFix()
    {
        string source = Types + """
            class C
            {
                int M(in Pooled p) => p.Read();
            }
            """;

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new DefensiveCopyAnalyzer(),
            new MakeMemberReadonlyCodeFixProvider(),
            [("Test.cs", "Shared/Test.cs", source)],
            DefensiveCopyAnalyzer.NonCopyableDefensiveCopyId,
            fixAll: false,
            addLinkedProject: true,
            linkedProjectParseOptions: new CSharpParseOptions(LanguageVersion.CSharp7_3)).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(2);
        result.CodeFixActionOffered.Should().BeFalse();
    }

    [TestMethod]
    public async Task DefensiveCopy_FixAllRepeatedAccesses_UpdatesMemberOnce()
    {
        string source = Types + """
            class C
            {
                int First(in Pooled p) => p.Read();
                int Second(in Pooled p) => p.Read();
                int Third(in Pooled p) => p.Read();
            }
            """;

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new DefensiveCopyAnalyzer(),
            new MakeMemberReadonlyCodeFixProvider(),
            [("Test.cs", "Test.cs", source)],
            DefensiveCopyAnalyzer.NonCopyableDefensiveCopyId,
            fixAll: true).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(3);
        result.FixAllActionOffered.Should().BeTrue();
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().BeEmpty();
        string fixedSource = result.Documents.Should().ContainSingle().Which.Source;
        fixedSource.Should().Contain("public readonly int Read() => _value;");
        fixedSource.Split(["public readonly int Read()"], StringSplitOptions.None).Should().HaveCount(2);
    }

    [TestMethod]
    public async Task DefensiveCopy_FixAllPartialMethod_UpdatesEachDeclarationOnce()
    {
        const string marker = """
            using System;

            namespace Touki
            {
                [AttributeUsage(AttributeTargets.Struct)]
                sealed class NonCopyableAttribute : Attribute { }
            }
            """;
        const string definition = """
            using Touki;

            [NonCopyable]
            partial struct Pooled
            {
                public partial int Read();
            }
            """;
        const string implementation = """
            partial struct Pooled
            {
                private int _value;
                public partial int Read() => _value;
            }

            class C
            {
                int First(in Pooled p) => p.Read();
                int Second(in Pooled p) => p.Read();
            }
            """;

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new DefensiveCopyAnalyzer(),
            new MakeMemberReadonlyCodeFixProvider(),
            [
                ("Marker.cs", "Marker.cs", marker),
                ("Definition.cs", "Definition.cs", definition),
                ("Implementation.cs", "Implementation.cs", implementation)
            ],
            DefensiveCopyAnalyzer.NonCopyableDefensiveCopyId,
            fixAll: true).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(2);
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().BeEmpty();
        result.Documents.Single(document => document.Name == "Definition.cs").Source
            .Should().Contain("public readonly partial int Read();");
        result.Documents.Single(document => document.Name == "Implementation.cs").Source
            .Should().Contain("public readonly partial int Read() => _value;");
    }

    [TestMethod]
    [DataRow(FixAllScope.Document)]
    [DataRow(FixAllScope.Project)]
    [DataRow(FixAllScope.Solution)]
    public async Task DefensiveCopy_FixAllScope_ChangesMembersWithinSelectedScope(FixAllScope scope)
    {
        const string marker = """
            using System;

            namespace Touki
            {
                [AttributeUsage(AttributeTargets.Struct)]
                sealed class NonCopyableAttribute : Attribute { }
            }
            """;
        const string members = """
            using Touki;

            [NonCopyable]
            struct Pooled
            {
                public int First() => 1;
                public int Second() => 2;
            }
            """;
        const string firstUse = """
            class FirstUse
            {
                int M(in Pooled value) => value.First();
            }
            """;
        const string secondUse = """
            class SecondUse
            {
                int M(in Pooled value) => value.Second();
            }
            """;
        const string additional = """
            using System;
            using Touki;

            namespace Touki
            {
                [AttributeUsage(AttributeTargets.Struct)]
                sealed class NonCopyableAttribute : Attribute { }
            }

            [NonCopyable]
            struct AdditionalPooled
            {
                public int Read() => 3;
            }

            class AdditionalUse
            {
                int M(in AdditionalPooled value) => value.Read();
            }
            """;

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new DefensiveCopyAnalyzer(),
            new MakeMemberReadonlyCodeFixProvider(),
            [
                ("Marker.cs", "A-Marker.cs", marker),
                ("Members.cs", "B-Members.cs", members),
                ("FirstUse.cs", "C-FirstUse.cs", firstUse),
                ("SecondUse.cs", "D-SecondUse.cs", secondUse)
            ],
            DefensiveCopyAnalyzer.NonCopyableDefensiveCopyId,
            fixAll: true,
            fixAllScope: scope,
            additionalProjectSources: [("Additional.cs", "Z-Additional.cs", additional)]).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(3);
        result.FixAllActionOffered.Should().BeTrue();
        result.CompilerErrors.Should().BeEmpty();

        string fixedMembers = result.Documents.Single(document => document.Name == "Members.cs").Source;
        fixedMembers.Should().Contain("public readonly int First() => 1;");

        switch (scope)
        {
            case FixAllScope.Document:
                fixedMembers.Should().Contain("public int Second() => 2;");
                result.AnalyzerDiagnostics.Should().HaveCount(2);
                break;
            case FixAllScope.Project:
                fixedMembers.Should().Contain("public readonly int Second() => 2;");
                result.AnalyzerDiagnostics.Should().ContainSingle();
                break;
            case FixAllScope.Solution:
                fixedMembers.Should().Contain("public readonly int Second() => 2;");
                result.Documents.Single(document => document.Name == "Additional.cs").Source
                    .Should().Contain("public readonly int Read() => 3;");
                result.AnalyzerDiagnostics.Should().BeEmpty();
                break;
        }
    }

    [TestMethod]
    public async Task DefensiveCopy_FixAllSolution_SkipsLinkedMemberAndFixesEligibleMember()
    {
        string linkedSource = Types + """
            class LinkedUse
            {
                int M(in Pooled value) => value.Read();
            }
            """;
        const string eligibleSource = """
            using System;
            using Touki;

            namespace Touki
            {
                [AttributeUsage(AttributeTargets.Struct)]
                sealed class NonCopyableAttribute : Attribute { }
            }

            [NonCopyable]
            struct EligiblePooled
            {
                public int Read() => 1;
            }

            class EligibleUse
            {
                int M(in EligiblePooled value) => value.Read();
            }
            """;

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new DefensiveCopyAnalyzer(),
            new MakeMemberReadonlyCodeFixProvider(),
            [("Linked.cs", "Z-Linked.cs", linkedSource)],
            DefensiveCopyAnalyzer.NonCopyableDefensiveCopyId,
            fixAll: true,
            fixAllScope: FixAllScope.Solution,
            addLinkedProject: true,
            linkedProjectParseOptions: new CSharpParseOptions(LanguageVersion.CSharp7_3),
            additionalProjectSources: [("Eligible.cs", "A-Eligible.cs", eligibleSource)]).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(3);
        result.CompilerErrors.Should().BeEmpty();
        result.Documents.Where(document => document.Name == "Linked.cs")
            .Should().OnlyContain(document => document.Source == linkedSource);
        result.Documents.Single(document => document.Name == "Eligible.cs").Source
            .Should().Contain("public readonly int Read() => 1;");
        result.AnalyzerDiagnostics.Should().HaveCount(2);
    }

    [TestMethod]
    public async Task DefensiveCopy_FixAllLinkedUse_UpdatesEachProjectsOwnMember()
    {
        const string use = """
            class Use
            {
                int M(in Pooled value) => value.Read();
            }
            """;
        const string primaryDeclaration = """
            struct Pooled
            {
                public int Read() => 1;
            }
            """;
        const string linkedDeclaration = """
            struct Pooled
            {
                public int Read() => 2;
            }
            """;

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new DefensiveCopyAnalyzer(),
            new MakeMemberReadonlyCodeFixProvider(),
            [("Use.cs", "Shared/Use.cs", use)],
            DefensiveCopyAnalyzer.DefensiveCopyId,
            fixAll: true,
            fixAllScope: FixAllScope.Solution,
            addLinkedProject: true,
            primaryProjectSources: [("PrimaryPooled.cs", "PrimaryPooled.cs", primaryDeclaration)],
            linkedProjectSources: [("LinkedPooled.cs", "LinkedPooled.cs", linkedDeclaration)]).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(2);
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().BeEmpty();
        result.Documents.Single(document => document.Name == "PrimaryPooled.cs").Source
            .Should().Contain("public readonly int Read() => 1;");
        result.Documents.Single(document => document.Name == "LinkedPooled.cs").Source
            .Should().Contain("public readonly int Read() => 2;");
    }

    [TestMethod]
    public async Task DefensiveCopy_GenericPartialMethod_UpdatesDefinitionAndImplementation()
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
            partial struct Pooled<T>
            {
                private int _value;
                public partial int Read<U>();
                public partial int Read<U>() => _value;
            }

            class C
            {
                int M(in Pooled<int> p) => p.Read<string>();
            }
            """;

        string fixedSource = await CodeFixTestHarness.ApplyFixAsync(
            new DefensiveCopyAnalyzer(),
            new MakeMemberReadonlyCodeFixProvider(),
            source,
            DefensiveCopyAnalyzer.NonCopyableDefensiveCopyId).ConfigureAwait(false);

        fixedSource.Should().Contain("public readonly partial int Read<U>();");
        fixedSource.Should().Contain("public readonly partial int Read<U>() => _value;");
    }

    [TestMethod]
    public async Task DefensiveCopy_GenericPartialMethodInReferencedProject_UpdatesBothDeclarations()
    {
        const string declarations = """
            using System;

            namespace Touki
            {
                [AttributeUsage(AttributeTargets.Struct)]
                public sealed class NonCopyableAttribute : Attribute { }
            }

            [Touki.NonCopyable]
            public partial struct Pooled<T>
            {
                private int _value;
                public partial int Read<U>();
                public partial int Read<U>() => _value;
            }
            """;
        const string use = """
            class C
            {
                int M(in Pooled<int> value) => value.Read<string>();
            }
            """;

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new DefensiveCopyAnalyzer(),
            new MakeMemberReadonlyCodeFixProvider(),
            [("Use.cs", "Z-Use.cs", use)],
            DefensiveCopyAnalyzer.NonCopyableDefensiveCopyId,
            fixAll: false,
            additionalProjectSources: [("Declarations.cs", "A-Declarations.cs", declarations)],
            referenceAdditionalProject: true).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(1);
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().BeEmpty();
        result.Documents.Single(document => document.Name == "Declarations.cs").Source
            .Should().Contain("public readonly partial int Read<U>();")
            .And.Contain("public readonly partial int Read<U>() => _value;");
    }

    [TestMethod]
    public async Task DefensiveCopy_DelegateValuedPropertyInvocation_AddsReadonly()
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
            struct Pooled
            {
                public Func<int> Callback => static () => 1;
            }

            class C
            {
                int M(in Pooled value) => value.Callback();
            }
            """;

        string fixedSource = await CodeFixTestHarness.ApplyFixAsync(
            new DefensiveCopyAnalyzer(),
            new MakeMemberReadonlyCodeFixProvider(),
            source,
            DefensiveCopyAnalyzer.NonCopyableDefensiveCopyId).ConfigureAwait(false);

        fixedSource.Should().Contain("public readonly Func<int> Callback => static () => 1;");
    }

    [TestMethod]
    public async Task DefensiveCopy_RefReadonlyInvocationReceiver_AddsReadonlyToOuterMember()
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
            struct Pooled
            {
                private int _value;
                public int Read() => _value;
            }

            static class Source
            {
                private static Pooled s_value;
                public static ref readonly Pooled Get() => ref s_value;
            }

            class C
            {
                int M() => Source.Get().Read();
            }
            """;

        string fixedSource = await CodeFixTestHarness.ApplyFixAsync(
            new DefensiveCopyAnalyzer(),
            new MakeMemberReadonlyCodeFixProvider(),
            source,
            DefensiveCopyAnalyzer.NonCopyableDefensiveCopyId).ConfigureAwait(false);

        fixedSource.Should().Contain("public readonly int Read() => _value;");
        fixedSource.Should().Contain("public static ref readonly Pooled Get() => ref s_value;");
    }

    [TestMethod]
    public async Task DefensiveCopy_NestedDefensiveCopyDiagnostic_UpdatesSelectedOuterMember()
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
            struct Pooled
            {
                private static Pooled s_value;
                private int _value;
                public ref readonly Pooled GetRef() => ref s_value;
                public int Read() => _value;
            }

            class C
            {
                int M(in Pooled value) => value.GetRef().Read();
            }
            """;

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new DefensiveCopyAnalyzer(),
            new MakeMemberReadonlyCodeFixProvider(),
            [("Test.cs", "Test.cs", source)],
            DefensiveCopyAnalyzer.NonCopyableDefensiveCopyId,
            fixAll: false,
            transformDiagnostics: diagnostics =>
            [
                diagnostics.OrderByDescending(diagnostic => diagnostic.Location.SourceSpan.Length).First()
            ]).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(1);
        result.CompilerErrors.Should().BeEmpty();
        string fixedSource = result.Documents.Should().ContainSingle().Which.Source;
        fixedSource.Should().Contain("public ref readonly Pooled GetRef() => ref s_value;");
        fixedSource.Should().Contain("public readonly int Read() => _value;");
    }

    [TestMethod]
    public async Task DefensiveCopy_FixAllRefReadonlyReceivers_UpdatesOuterMembers()
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
            struct Pooled
            {
                private int _value;
                public int Read() => _value;
                public int Value => _value;
                public int this[int index] => _value + index;
            }

            static class Source
            {
                private static Pooled s_value;
                public static ref readonly Pooled Get() => ref s_value;
                public static ref readonly Pooled Current => ref s_value;
            }

            class Holder
            {
                private Pooled _value;
                public ref readonly Pooled this[int index] => ref _value;
            }

            class C
            {
                int FromInvocation() => Source.Get().Read();
                int FromProperty() => Source.Current.Value;
                int FromIndexer(Holder holder) => holder[0][1];
            }
            """;

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new DefensiveCopyAnalyzer(),
            new MakeMemberReadonlyCodeFixProvider(),
            [("Test.cs", "Test.cs", source)],
            DefensiveCopyAnalyzer.NonCopyableDefensiveCopyId,
            fixAll: true).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(3);
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().BeEmpty();
        string fixedSource = result.Documents.Should().ContainSingle().Which.Source;
        fixedSource.Should().Contain("public readonly int Read() => _value;");
        fixedSource.Should().Contain("public readonly int Value => _value;");
        fixedSource.Should().Contain("public readonly int this[int index] => _value + index;");
    }

    [TestMethod]
    public async Task DefensiveCopy_StaleDiagnosticOnClassMember_OffersNoFix()
    {
        const string source = """
            class Pooled
            {
                public int Read() => 0;
            }

            class C
            {
                int M(Pooled p) => p.Read();
            }
            """;

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new ForcedDefensiveCopyAnalyzer(),
            new MakeMemberReadonlyCodeFixProvider(),
            [("Test.cs", "Test.cs", source)],
            DefensiveCopyAnalyzer.NonCopyableDefensiveCopyId,
            fixAll: false).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(1);
        result.CodeFixActionOffered.Should().BeFalse();
    }

    [TestMethod]
    public async Task DefensiveCopy_StaleDiagnosticOnWritableStructReceiver_OffersNoFix()
    {
        const string source = """
            struct Pooled
            {
                public int Read() => 0;
            }

            class C
            {
                int M(Pooled p) => p.Read();
            }
            """;

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new ForcedDefensiveCopyAnalyzer(),
            new MakeMemberReadonlyCodeFixProvider(),
            [("Test.cs", "Test.cs", source)],
            DefensiveCopyAnalyzer.NonCopyableDefensiveCopyId,
            fixAll: false).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(1);
        result.CodeFixActionOffered.Should().BeFalse();
    }

    [TestMethod]
    public async Task DefensiveCopy_StaleDiagnosticOnNestedWrapper_DoesNotUpdateOuterMember()
    {
        const string source = """
            struct Pooled
            {
                public int Read() => 0;
            }

            class Wrapper
            {
                private Pooled _value;
                public ref readonly Pooled GetRef() => ref _value;
            }

            class C
            {
                int M(Wrapper wrapper) => wrapper.GetRef().Read();
            }
            """;

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new ForcedDefensiveCopyAnalyzer(),
            new MakeMemberReadonlyCodeFixProvider(),
            [("Test.cs", "Test.cs", source)],
            DefensiveCopyAnalyzer.NonCopyableDefensiveCopyId,
            fixAll: false,
            transformDiagnostics: diagnostics =>
            [
                diagnostics.OrderBy(diagnostic => diagnostic.Location.SourceSpan.Length).First()
            ]).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(1);
        result.CodeFixActionOffered.Should().BeFalse();
        result.Documents.Should().ContainSingle().Which.Source.Should().Be(source);
    }

    [TestMethod]
    public async Task DefensiveCopy_GeneratedMember_OffersNoFix()
    {
        const string source = """
            class C
            {
                int M(in Pooled value) => value.Read();
            }
            """;
        const string generated = """
            struct Pooled
            {
                public int Read() => 1;
            }
            """;

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new DefensiveCopyAnalyzer(),
            new MakeMemberReadonlyCodeFixProvider(),
            [("Use.cs", "Use.cs", source)],
            DefensiveCopyAnalyzer.DefensiveCopyId,
            fixAll: false,
            analyzerReferences: [new TestGeneratorReference(new SourceGenerator(generated))]).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(1);
        result.CodeFixActionOffered.Should().BeFalse();
    }

    [TestMethod]
    public async Task DefensiveCopy_MixedOrdinaryAndGeneratedPartialMember_OffersNoFix()
    {
        const string source = """
            partial struct Pooled
            {
                public partial int Read();
            }

            class C
            {
                int M(in Pooled value) => value.Read();
            }
            """;
        const string generated = """
            partial struct Pooled
            {
                public partial int Read() => 1;
            }
            """;

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new DefensiveCopyAnalyzer(),
            new MakeMemberReadonlyCodeFixProvider(),
            [("Use.cs", "Use.cs", source)],
            DefensiveCopyAnalyzer.DefensiveCopyId,
            fixAll: false,
            analyzerReferences: [new TestGeneratorReference(new SourceGenerator(generated))]).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(1);
        result.CodeFixActionOffered.Should().BeFalse();
        result.Documents.Should().ContainSingle().Which.Source.Should().Be(source);
    }

    [TestMethod]
    public async Task DefensiveCopy_FixAllCanceled_ThrowsOperationCanceledException()
    {
        string source = Types + """
            class C
            {
                int M(in Pooled p) => p.Read();
            }
            """;
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        Func<Task> action = async () => await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new DefensiveCopyAnalyzer(),
            new MakeMemberReadonlyCodeFixProvider(),
            [("Test.cs", "Test.cs", source)],
            DefensiveCopyAnalyzer.NonCopyableDefensiveCopyId,
            fixAll: true,
            fixAllCancellationToken: cancellation.Token).ConfigureAwait(false);

        await action.Should().ThrowAsync<OperationCanceledException>().ConfigureAwait(false);
    }

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    private sealed class ForcedDefensiveCopyAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor s_rule = new(
            DefensiveCopyAnalyzer.NonCopyableDefensiveCopyId,
            "Forced defensive copy",
            "Forced defensive copy",
            "Test",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        private static readonly ImmutableArray<DiagnosticDescriptor> s_supportedDiagnostics = [s_rule];

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => s_supportedDiagnostics;

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(
                static syntaxContext =>
                {
                    InvocationExpressionSyntax invocation = (InvocationExpressionSyntax)syntaxContext.Node;
                    if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
                    {
                        syntaxContext.ReportDiagnostic(
                            Diagnostic.Create(s_rule, memberAccess.Expression.GetLocation()));
                    }
                },
                SyntaxKind.InvocationExpression);
        }
    }

#pragma warning disable RS1042 // This test-only generator is passed directly through an in-memory AnalyzerReference.
    private sealed class SourceGenerator(string source) : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
        }

        public void Execute(GeneratorExecutionContext context) =>
            context.AddSource("Generated.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    private sealed class TestGeneratorReference(ISourceGenerator generator) : AnalyzerReference
    {
        public override string FullPath => nameof(TestGeneratorReference);

        public override object Id { get; } = new();

        public override ImmutableArray<DiagnosticAnalyzer> GetAnalyzers(string language) => [];

        public override ImmutableArray<DiagnosticAnalyzer> GetAnalyzersForAllLanguages() => [];

        public override ImmutableArray<ISourceGenerator> GetGenerators(string language) =>
            language == LanguageNames.CSharp ? [generator] : [];

        public override ImmutableArray<ISourceGenerator> GetGeneratorsForAllLanguages() => [generator];
    }
#pragma warning restore RS1042
}
