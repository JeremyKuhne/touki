// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Analyzers;

[TestClass]
public class StackAllocSizeAnalyzerTests
{
    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        string source,
        string? maxBytes = null)
    {
        Dictionary<string, string>? options = maxBytes is null
            ? null
            : new Dictionary<string, string> { [StackAllocSizeAnalyzer.MaxBytesOption] = maxBytes };

        return await AnalyzerTestHarness.GetDiagnosticsAsync(new StackAllocSizeAnalyzer(), source, options)
            .ConfigureAwait(false);
    }

    private const string Usings = """
        using System;

        """;

    [TestMethod]
    public async Task AnalyzeStackAlloc_ByteArrayOverDefault_ReportsDiagnostic()
    {
        string source = Usings + """
            class Sample
            {
                void Use()
                {
                    Span<byte> buffer = stackalloc byte[2048];
                    buffer[0] = 1;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(StackAllocSizeAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task AnalyzeStackAlloc_CharArray_UsesTwoBytesPerElement()
    {
        // 600 chars is 1200 bytes, which is over the 1024 default even though the count is not.
        string source = Usings + """
            class Sample
            {
                void Use()
                {
                    Span<char> buffer = stackalloc char[600];
                    buffer[0] = 'a';
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Contain("1200").And.Contain("1024");
    }

    [TestMethod]
    public async Task AnalyzeStackAlloc_ExactlyAtLimit_ReportsNothing()
    {
        string source = Usings + """
            class Sample
            {
                void Use()
                {
                    Span<byte> buffer = stackalloc byte[1024];
                    buffer[0] = 1;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeStackAlloc_TypicalSeedBuffer_ReportsNothing()
    {
        string source = Usings + """
            class Sample
            {
                void Use()
                {
                    Span<char> buffer = stackalloc char[256];
                    buffer[0] = 'a';
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeStackAlloc_ConstantLength_IsEvaluated()
    {
        string source = Usings + """
            class Sample
            {
                private const int Size = 4096;

                void Use()
                {
                    Span<byte> buffer = stackalloc byte[Size];
                    buffer[0] = 1;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Contain("4096");
    }

    [TestMethod]
    public async Task AnalyzeStackAlloc_RuntimeLength_ReportsNothing()
    {
        string source = Usings + """
            class Sample
            {
                void Use(int length)
                {
                    Span<byte> buffer = stackalloc byte[length];
                    buffer[0] = 1;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeStackAlloc_EnumElement_UsesUnderlyingTypeSize()
    {
        // DayOfWeek is backed by int, so 512 elements is 2048 bytes.
        string source = Usings + """
            class Sample
            {
                void Use()
                {
                    Span<DayOfWeek> buffer = stackalloc DayOfWeek[512];
                    buffer[0] = DayOfWeek.Monday;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Contain("2048");
    }

    [TestMethod]
    public async Task AnalyzeStackAlloc_CustomStructElement_ReportsNothing()
    {
        // The layout of a custom struct is not knowable from source, so it is left alone.
        string source = Usings + """
            struct Point
            {
                public int X;
                public int Y;
            }

            class Sample
            {
                void Use()
                {
                    Span<Point> buffer = stackalloc Point[4096];
                    buffer[0] = default;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeStackAlloc_PointerElement_UsesEightBytes()
    {
        string source = Usings + """
            class Sample
            {
                unsafe void Use()
                {
                    Span<byte> buffer = stackalloc byte[8];
                    byte** pointers = stackalloc byte*[256];
                    pointers[0] = null;
                    buffer[0] = 1;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Contain("2048");
    }

    [TestMethod]
    public async Task AnalyzeStackAlloc_ConfiguredMaxLowered_ReportsDiagnostic()
    {
        string source = Usings + """
            class Sample
            {
                void Use()
                {
                    Span<byte> buffer = stackalloc byte[512];
                    buffer[0] = 1;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, maxBytes: "256").ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Contain("512").And.Contain("256");
    }

    [TestMethod]
    public async Task AnalyzeStackAlloc_ConfiguredMaxRaised_ReportsNothing()
    {
        string source = Usings + """
            class Sample
            {
                void Use()
                {
                    Span<byte> buffer = stackalloc byte[2048];
                    buffer[0] = 1;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, maxBytes: "4096").ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("not-a-number")]
    [DataRow("0")]
    [DataRow("-1")]
    [DataRow("")]
    public async Task AnalyzeStackAlloc_UnusableConfiguredMax_FallsBackToDefault(string maxBytes)
    {
        string source = Usings + """
            class Sample
            {
                void Use()
                {
                    Span<byte> buffer = stackalloc byte[2048];
                    buffer[0] = 1;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, maxBytes).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Contain($"{StackAllocSizeAnalyzer.DefaultMaxBytes}");
    }

    [TestMethod]
    public async Task AnalyzeStackAlloc_ImplicitlyTyped_UsesInitializerCount()
    {
        // 'stackalloc[] { 1, 2, 3 }' is three ints, so 12 bytes.
        string source = Usings + """
            class Sample
            {
                void Use()
                {
                    Span<int> buffer = stackalloc[] { 1, 2, 3 };
                    buffer[0] = 1;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, maxBytes: "8").ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Contain("12");
    }

    [TestMethod]
    public async Task AnalyzeStackAlloc_OmittedLengthWithInitializer_UsesInitializerCount()
    {
        string source = Usings + """
            class Sample
            {
                void Use()
                {
                    Span<int> buffer = stackalloc int[] { 1, 2, 3, 4 };
                    buffer[0] = 1;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, maxBytes: "8").ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.GetMessage().Should().Contain("16");
    }

    [TestMethod]
    public async Task AnalyzeStackAlloc_MultipleOversizedAllocations_ReportsEach()
    {
        string source = Usings + """
            class Sample
            {
                void Use()
                {
                    Span<byte> first = stackalloc byte[2048];
                    Span<byte> second = stackalloc byte[4096];
                    first[0] = second[0];
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().HaveCount(2);
    }
}
