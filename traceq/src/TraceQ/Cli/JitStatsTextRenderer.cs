// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Output;
using TraceQ.Tracing.Providers;

namespace TraceQ.Cli;

/// <summary>
///  Renders a JIT-stats result as the dense, fixed-width text view a human reads at
///  the terminal: a header, the aggregate counts and compile / size summary, then
///  the per-method detail in aligned columns, and finally any warnings.
/// </summary>
/// <remarks>
///  <para>
///   This is the text half of the JIT report; the JSON half is
///   <see cref="OutputJson"/>. Both render the same <see cref="AnalysisResult{T}"/>
///   envelope.
///  </para>
/// </remarks>
internal static class JitStatsTextRenderer
{
    /// <summary>
    ///  Renders the JIT-stats envelope to <paramref name="output"/>.
    /// </summary>
    /// <param name="envelope">The JIT report, with its warnings.</param>
    /// <param name="path">The trace path, for the header line.</param>
    /// <param name="output">The writer the text is rendered to.</param>
    public static void Render(AnalysisResult<JitStatsResult> envelope, string path, TextWriter output)
    {
        JitStatsResult report = envelope.Result;

        output.WriteLine($"JIT report  -  {path}");
        output.WriteLine();

        if (report.MethodCount == 0)
        {
            output.WriteLine("  (no methods; the trace carries no JIT events)");
            RenderWarnings(envelope, output);
            return;
        }

        output.WriteLine(
            $"  {report.MethodCount} methods   compile total {report.TotalCompileMs:N2} ms   "
            + $"max {report.MaxCompileMs:N2} ms   mean {report.MeanCompileMs:N2} ms");
        output.WriteLine(
            $"  size    IL {report.TotalILSize:N0} B   native {report.TotalNativeSize:N0} B");
        output.WriteLine();

        output.WriteLine($"  {"compile(ms)",12}  {"IL(B)",8}  {"native(B)",10}  {"tier",-16}  method");
        foreach (JitMethodRecord method in report.Methods)
        {
            output.WriteLine(
                $"  {method.CompileMs,12:N2}  {method.ILSize,8}  {method.NativeSize,10}  "
                + $"{method.OptimizationTier,-16}  {method.MethodName}");
        }

        RenderWarnings(envelope, output);
    }

    private static void RenderWarnings(AnalysisResult<JitStatsResult> envelope, TextWriter output)
    {
        foreach (string warning in envelope.Warnings)
        {
            output.WriteLine($"! {warning}");
        }
    }
}
