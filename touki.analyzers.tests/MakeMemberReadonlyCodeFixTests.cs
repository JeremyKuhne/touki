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
