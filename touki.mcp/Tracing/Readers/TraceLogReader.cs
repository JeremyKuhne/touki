// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.Diagnostics.Symbols;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.EventPipe;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;

namespace Touki.Mcp.Tracing.Readers;

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
    public TraceReadResult Read(string path, string? symbolsDirectory = null)
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

        try
        {
            if (extractedPdbDirectory is not null)
            {
                symbolReader.SymbolPath = $"{symbolsDirectory};{extractedPdbDirectory}";
            }
            else if (!string.IsNullOrEmpty(symbolsDirectory))
            {
                symbolReader.SymbolPath = symbolsDirectory;
            }

            return ReadCore(traceLog, symbolReader);
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

    private static TraceReadResult ReadCore(TraceLog traceLog, SymbolReader symbolReader)
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

            samples.Add(new SampleStack(frames, 1.0, data.ThreadID.ToString(), locations));
        }

        double resolutionRate = totalFrames > 0 ? (double)resolvedFrames / totalFrames : 0.0;

        List<string> warnings = [];
        if (samples.Count == 0)
        {
            warnings.Add("No sampled-profile (CPU) events were found. Was the trace captured with a CPU sampler?");
        }

        if (totalFrames > 0 && resolutionRate < 0.8)
        {
            warnings.Add(
                $"Only {resolutionRate:P0} of frames resolved to a method name; native frames may be unresolved. "
                + "Managed touki frames resolve from CLR rundown, so self/inclusive rankings of managed methods are still usable.");
        }

        warnings.Add("Sample weight is approximate (1 ms per sample); relative percentages are exact.");

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
