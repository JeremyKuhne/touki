// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Fuzz;

/// <summary>
///  Entry point for the SharpFuzz coverage-guided fuzzing harness.
/// </summary>
/// <remarks>
///  <para>
///   The fuzz target is selected with the <c>FUZZ_TARGET</c> environment variable so that the same
///   instrumented executable can drive any of the registered targets. The libFuzzer driver
///   (<c>libfuzzer-dotnet</c>) supplies its own command-line arguments, so an environment variable
///   is used instead of <c>args</c>.
///  </para>
///  <para>
///   Setting <c>FUZZ_MODE=sweep</c> runs a deterministic, in-process random sweep instead of the
///   libFuzzer driver. This needs no native tooling and is used for quick local smoke runs and as a
///   fixed-seed regression check. See <c>README.md</c> for both workflows.
///  </para>
/// </remarks>
internal static class Program
{
    private static int Main()
    {
        string target = Environment.GetEnvironmentVariable("FUZZ_TARGET") ?? "SpanReader";

        ReadOnlySpanAction action = target switch
        {
            "SpanReader" => SpanReaderTarget.Run,
            "SpanWriter" => SpanWriterTarget.Run,
            "RunLength" => RunLengthTarget.Run,
            "StringSegment" => StringSegmentTarget.Run,
            "ValueStringBuilder" => ValueStringBuilderTarget.Run,
            "GlobSpecification" => GlobSpecificationTarget.Run,
            _ => throw new ArgumentException($"Unknown fuzz target '{target}'. Set FUZZ_TARGET to 'SpanReader', 'SpanWriter', 'RunLength', 'StringSegment', 'ValueStringBuilder', or 'GlobSpecification'.")
        };

        if (string.Equals(Environment.GetEnvironmentVariable("FUZZ_MODE"), "sweep", StringComparison.OrdinalIgnoreCase))
        {
            return SweepRunner.Run(target, action);
        }

        Fuzzer.LibFuzzer.Run(action);
        return 0;
    }
}
