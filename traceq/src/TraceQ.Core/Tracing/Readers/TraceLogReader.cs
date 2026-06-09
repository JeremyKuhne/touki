// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Globalization;
using Microsoft.Diagnostics.Symbols;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.EventPipe;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;

namespace TraceQ.Tracing.Readers;

/// <summary>
///  Shared core for the TraceEvent-backed readers. Converts an ETW (<c>.etl</c>)
///  or EventPipe (<c>.nettrace</c>) trace into the normalized weighted-sample
///  model by walking the call stack of every sampled-profile event.
/// </summary>
/// <remarks>
///  <para>
///   Both formats normalize, through <see cref="TraceLog"/>, onto the same
///   <see cref="SampledProfileTraceData"/> CPU-sample event with a resolvable
///   <see cref="TraceCallStack"/>. Managed method names resolve from the CLR
///   rundown embedded in the trace, so no external symbol server is needed for
///   managed frames; native frames may remain unresolved.
///  </para>
///  <para>
///   Each sample is weighted as one millisecond. Sampled-profile CPU profiling
///   runs at roughly a 1 kHz cadence on both runtimes, so scoped durations are
///   accurate to within the sampling interval and the relative percentages -
///   what the rankings actually report - are unaffected.
///  </para>
/// </remarks>
internal abstract class TraceLogReader : ITraceReader
{
    /// <inheritdoc/>
    public abstract TraceFormat Format { get; }

    /// <inheritdoc/>
    public abstract bool CanRead(string path);

    /// <summary>
    ///  Converts the trace at <paramref name="path"/> to an ETLX
    ///  <see cref="TraceLog"/> the caller then reads.
    /// </summary>
    /// <param name="path">The trace file path.</param>
    /// <returns>The opened trace log.</returns>
    protected abstract TraceLog OpenTraceLog(string path);

    /// <inheritdoc/>
    public TraceReadResult Read(string path, string? symbolsDirectory = null, ScopeRequest? scope = null)
    {
        using TraceLog traceLog = OpenTraceLog(path);

        // Local-only symbol reader: an empty symbol path never reaches a symbol
        // server, but portable PDBs sitting next to a traced module still
        // resolve, which is all the managed touki frames need for line-level
        // attribution. Frames without a local PDB (BCL, OS) simply carry no line.
        using SymbolReader symbolReader = new(TextWriter.Null, "", null);

        // touki and its sibling assemblies ship embedded portable PDBs, which
        // TraceEvent's SymbolReader cannot read, and BenchmarkDotNet's ephemeral
        // run directory is gone by analysis time. When the caller points us at a
        // build-output directory we re-materialize those embedded PDBs as
        // standalone files in a temp directory and add it to the symbol path so
        // TraceEvent can match a module by its PDB GUID and resolve source lines.
        string? extractedPdbDirectory = symbolsDirectory is null
            ? null
            : EmbeddedPdbExtractor.Extract(symbolsDirectory);

        // The name the scope resolved to (set by ResolveScope below), surfaced as a
        // warning so the caller knows a machine-wide capture was narrowed automatically.
        string? appliedScopeName = null;

        try
        {
            if (extractedPdbDirectory is not null)
            {
                symbolReader.SymbolPath = $"{symbolsDirectory}{Path.PathSeparator}{extractedPdbDirectory}";
            }
            else if (!string.IsNullOrEmpty(symbolsDirectory))
            {
                symbolReader.SymbolPath = symbolsDirectory;
            }

            // Resolve the scope intent (an explicit name, the busiest process under the
            // automatic default, or every process when opted out) to the set of process
            // IDs to keep. A null request means "unspecified", which is the automatic
            // default - the same as ScopeRequest.Auto - so a caller that passes nothing
            // still gets scenario scope. A null pid set means no scoping (every process,
            // the all-processes opt-out). This is lossless: the trace is fully
            // symbol-resolved by TraceLog before any sample is dropped.
            HashSet<int>? scopePids = ProcessTree.ResolveScope(
                traceLog, scope ?? ScopeRequest.Auto, out appliedScopeName);

            return ReadCore(traceLog, symbolReader, scopePids, appliedScopeName);
        }
        finally
        {
            if (extractedPdbDirectory is not null)
            {
                try
                {
                    Directory.Delete(extractedPdbDirectory, recursive: true);
                }
                catch (Exception)
                {
                    // Best-effort cleanup of a temp directory; a leftover under
                    // %TEMP% is harmless if the delete races a still-open handle.
                }
            }
        }
    }

    private static TraceReadResult ReadCore(
        TraceLog traceLog,
        SymbolReader symbolReader,
        HashSet<int>? scopePids,
        string? appliedScopeName)
    {
        Dictionary<int, string> locationCache = [];

        List<SampleStack> samples = [];
        long totalFrames = 0;
        long resolvedFrames = 0;
        List<string> leafToRoot = [];
        List<string> leafToRootLocations = [];

        foreach (TraceEvent data in traceLog.Events)
        {
            // ETW (.etl) surfaces CPU samples as SampledProfileTraceData; EventPipe
            // (.nettrace) surfaces them as the SampleProfiler's ClrThreadSampleTraceData.
            if (data is ClrThreadSampleTraceData clrSample)
            {
                if (clrSample.Type == ClrThreadSampleType.Error)
                {
                    continue;
                }
            }
            else if (data is not SampledProfileTraceData)
            {
                continue;
            }

            // When scoped to a process tree, drop samples from any process outside it.
            // The trace is already fully resolved, so this is a lossless narrowing.
            if (scopePids is not null && !scopePids.Contains(data.ProcessID))
            {
                continue;
            }

            TraceCallStack? callStack = data.CallStack();
            if (callStack is null)
            {
                continue;
            }

            leafToRoot.Clear();
            leafToRootLocations.Clear();
            for (TraceCallStack? frame = callStack; frame is not null; frame = frame.Caller)
            {
                TraceCodeAddress address = frame.CodeAddress;
                string method = address.FullMethodName;
                string module = address.ModuleName;

                totalFrames++;
                string name;
                if (string.IsNullOrEmpty(method))
                {
                    name = $"{(string.IsNullOrEmpty(module) ? "?" : module)}!?";
                }
                else
                {
                    resolvedFrames++;
                    name = string.IsNullOrEmpty(module) ? method : $"{module}!{method}";
                }

                leafToRoot.Add(name);
                leafToRootLocations.Add(ResolveLocation(symbolReader, address, locationCache));
            }

            if (leafToRoot.Count == 0)
            {
                continue;
            }

            int count = leafToRoot.Count;
            string[] frames = new string[count];
            string[] locations = new string[count];
            for (int i = 0; i < count; i++)
            {
                frames[i] = leafToRoot[count - 1 - i];
                locations[i] = leafToRootLocations[count - 1 - i];
            }

            // Tag the sample with its owning process so a multi-process trace can be
            // reasoned about per process; empty resolves to just the numeric id. IDs are
            // formatted invariantly so the labels stay ASCII-stable across locales.
            string processName = data.ProcessName;
            string pid = data.ProcessID.ToString(CultureInfo.InvariantCulture);
            string process = string.IsNullOrEmpty(processName) ? pid : $"{processName}({pid})";

            samples.Add(new SampleStack(
                frames,
                1.0,
                data.ThreadID.ToString(CultureInfo.InvariantCulture),
                locations,
                process));
        }

        double resolutionRate = totalFrames > 0 ? (double)resolvedFrames / totalFrames : 0.0;

        List<string> warnings = [];
        if (samples.Count == 0)
        {
            warnings.Add("No sampled-profile (CPU) events were found. Was the trace captured with a CPU sampler?");
        }

        if (appliedScopeName is not null)
        {
            warnings.Add(
                $"Scoped to the '{appliedScopeName}' process tree; pass --all-processes to read every process.");
        }

        if (SymbolGate.TryGetWarning(resolutionRate, samples.Count, out string? symbolWarning))
        {
            warnings.Add(symbolWarning);
        }

        return new TraceReadResult(samples, resolutionRate, warnings);
    }

    /// <summary>
    ///  Resolves the source location (<c>file:line</c>) for a code address,
    ///  caching the result by code-address index so repeated frames cost one
    ///  symbol lookup. Returns an empty string when no local PDB maps the
    ///  address to a source line.
    /// </summary>
    private static string ResolveLocation(SymbolReader reader, TraceCodeAddress address, Dictionary<int, string> cache)
    {
        int key = (int)address.CodeAddressIndex;
        if (cache.TryGetValue(key, out string? cached))
        {
            return cached;
        }

        string location = "";
        try
        {
            SourceLocation? source = address.GetSourceLine(reader);
            if (source?.SourceFile is { } file)
            {
                location = $"{Path.GetFileName(file.BuildTimeFilePath)}:{source.LineNumber}";
            }
        }
        catch (Exception)
        {
            // Symbol resolution is best-effort; an unresolved frame carries no line.
        }

        cache[key] = location;
        return location;
    }
}
