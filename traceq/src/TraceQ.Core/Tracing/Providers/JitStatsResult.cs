// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Tracing.Providers;

/// <summary>
///  One jitted method's structured record in a <see cref="JitStatsResult"/>.
/// </summary>
/// <param name="MethodName">The fully qualified method name.</param>
/// <param name="ModuleILPath">The path of the module the method was jitted from.</param>
/// <param name="ILSize">The method's IL size, in bytes.</param>
/// <param name="NativeSize">The size of the jitted native code, in bytes.</param>
/// <param name="CompileMs">How long the method took to compile, in milliseconds.</param>
/// <param name="OptimizationTier">The optimization tier the method was compiled at.</param>
public sealed record JitMethodRecord(
    string MethodName,
    string ModuleILPath,
    int ILSize,
    int NativeSize,
    double CompileMs,
    string OptimizationTier);

/// <summary>
///  The just-in-time compilation report for a trace: the per-method records plus
///  the aggregate counts and compile-time summary an agent reads to judge JIT
///  cost and startup pressure.
/// </summary>
/// <remarks>
///  <para>
///   Like the GC-stats report, JIT behavior is a series of structured per-method
///   records rather than weighted call stacks, so it does not flow through the
///   folding aggregator; this is its own result shape.
///  </para>
/// </remarks>
/// <param name="MethodCount">The total number of methods jitted.</param>
/// <param name="TotalCompileMs">The summed compile time across all methods, in milliseconds.</param>
/// <param name="MaxCompileMs">The longest single compile, in milliseconds.</param>
/// <param name="MeanCompileMs">The mean compile time, in milliseconds.</param>
/// <param name="TotalILSize">The summed IL size across all methods, in bytes.</param>
/// <param name="TotalNativeSize">The summed native-code size across all methods, in bytes.</param>
/// <param name="Methods">The per-method records, in trace order.</param>
public sealed record JitStatsResult(
    int MethodCount,
    double TotalCompileMs,
    double MaxCompileMs,
    double MeanCompileMs,
    long TotalILSize,
    long TotalNativeSize,
    IReadOnlyList<JitMethodRecord> Methods);
