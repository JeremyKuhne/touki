// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.Diagnostics.Tracing.Analysis;
using Microsoft.Diagnostics.Tracing.Analysis.JIT;
using TraceLog = Microsoft.Diagnostics.Tracing.Etlx.TraceLog;
using TraceLogEventSource = Microsoft.Diagnostics.Tracing.Etlx.TraceLogEventSource;
using TraceLogOptions = Microsoft.Diagnostics.Tracing.Etlx.TraceLogOptions;
using TraceProcess = Microsoft.Diagnostics.Tracing.Analysis.TraceProcess;

namespace TraceQ.Tracing.Providers;

/// <summary>
///  The JIT-stats provider: reads the structured just-in-time compilation records
///  from a .NET EventPipe trace into a <see cref="JitStatsResult"/>.
/// </summary>
/// <remarks>
///  <para>
///   JIT activity is captured by the runtime's method events (a JIT EventPipe
///   profile), which TraceEvent's analysis layer assembles into per-method
///   <c>TraceJittedMethod</c> records. Unlike the stack-source families this is
///   structured data, not weighted stacks, so it returns its own result rather
///   than a <see cref="StackSampleSource"/>.
///  </para>
/// </remarks>
internal sealed class JitStatsProvider
{
    /// <summary>
    ///  Reads the JIT-stats report from the EventPipe trace at <paramref name="path"/>.
    /// </summary>
    /// <param name="path">The <c>.nettrace</c> file path.</param>
    /// <returns>The JIT report, or an empty report when the trace carries no JIT events.</returns>
    /// <exception cref="ArgumentException"><paramref name="path"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    public JitStatsResult Read(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Trace file not found: {fullPath}", fullPath);
        }

        string etlxPath = TraceLog.CreateFromEventPipeDataFile(
            fullPath,
            null,
            new TraceLogOptions { ContinueOnError = true });

        using TraceLog traceLog = new(etlxPath);

        // The JIT analysis layer reconstructs per-method records from the raw method
        // events as the source is processed; request it before draining the events.
        using TraceLogEventSource source = traceLog.Events.GetSource();
        source.NeedLoadedDotNetRuntimes();
        source.Process();

        List<JitMethodRecord> records = [];
        foreach (TraceProcess process in source.Processes())
        {
            TraceLoadedDotNetRuntime? runtime = process.LoadedDotNetRuntime();
            if (runtime is null)
            {
                continue;
            }

            foreach (TraceJittedMethod method in runtime.JIT.Methods)
            {
                records.Add(new JitMethodRecord(
                    method.MethodName ?? string.Empty,
                    method.ModuleILPath ?? string.Empty,
                    method.ILSize,
                    method.NativeSize,
                    method.CompileCpuTimeMSec,
                    method.OptimizationTier.ToString()));
            }
        }

        return Summarize(records);
    }

    private static JitStatsResult Summarize(List<JitMethodRecord> records)
    {
        if (records.Count == 0)
        {
            return new JitStatsResult(0, 0.0, 0.0, 0.0, 0, 0, records);
        }

        double totalCompile = 0.0;
        double maxCompile = 0.0;
        long totalIL = 0;
        long totalNative = 0;

        foreach (JitMethodRecord method in records)
        {
            totalCompile += method.CompileMs;
            maxCompile = Math.Max(maxCompile, method.CompileMs);
            totalIL += method.ILSize;
            totalNative += method.NativeSize;
        }

        return new JitStatsResult(
            records.Count,
            totalCompile,
            maxCompile,
            totalCompile / records.Count,
            totalIL,
            totalNative,
            records);
    }
}
