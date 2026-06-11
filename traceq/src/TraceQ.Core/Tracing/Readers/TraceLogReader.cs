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
    public TraceReadResult Read(
        string path,
        string? symbolsDirectory = null,
        ScopeRequest? scope = null,
        SymbolOptions? symbolOptions = null)
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

            // Opt-in native runtime symbols: point the reader at the Microsoft public
            // symbol server (a local cache fronting it) and resolve the unmanaged
            // runtime modules - the GC, the JIT, memset/memcpy, write barriers - whose
            // PDBs are not in the trace. Off by default so the common path stays offline
            // and deterministic; managed frames resolve from the rundown regardless.
            if (symbolOptions is { ResolveNativeRuntime: true })
            {
                ResolveNativeRuntimeSymbols(traceLog, symbolReader, symbolOptions);
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
            // An empty result has two distinct causes now that scoping can drop every
            // sample: a scope that matched no process (the trace is fine), or a trace
            // that carried no CPU samples at all. Name the right one so the message does
            // not blame the capture when the scope is at fault.
            warnings.Add(appliedScopeName is not null
                ? $"No samples remained after scoping to the '{appliedScopeName}' process tree; "
                    + "the scope may match no process with samples - pass --all-processes to read every process."
                : "No sampled-profile (CPU) events were found. Was the trace captured with a CPU sampler?");
        }
        else if (appliedScopeName is not null)
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
    ///  Points <paramref name="symbolReader"/> at the Microsoft public symbol server
    ///  (fronted by a local cache) and resolves the names of the unmanaged runtime
    ///  modules, so native runtime frames (the GC, the JIT, <c>memset</c> /
    ///  <c>memcpy</c>, write barriers) carry method names instead of a bare module
    ///  address.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Only the runtime/OS modules are looked up - <c>coreclr</c> / <c>clr</c>,
    ///   <c>clrjit</c>, <c>ntdll</c>, <c>kernelbase</c>, <c>kernel32</c>,
    ///   <c>ucrtbase</c>, <c>msvcrt</c> - rather than every loaded module, so the
    ///   download is bounded to the frames a runtime profile actually needs. Each
    ///   lookup is best-effort: a module whose PDB cannot be fetched (offline, or no
    ///   published symbols) simply keeps its unresolved frames rather than failing the
    ///   whole read.
    ///  </para>
    /// </remarks>
    private static void ResolveNativeRuntimeSymbols(
        TraceLog traceLog,
        SymbolReader symbolReader,
        SymbolOptions options)
    {
        string cacheDirectory = options.CacheDirectory ?? SymbolOptions.DefaultCacheDirectory;
        Directory.CreateDirectory(cacheDirectory);

        // The standard symbol-path form: a local downstream cache backed by the public
        // server, so the first read downloads and later reads hit the cache. Preserve
        // any path already set (the local build-output PDBs) by appending the server.
        string serverPath = $"srv*{cacheDirectory}*https://msdl.microsoft.com/download/symbols";
        symbolReader.SymbolPath = string.IsNullOrEmpty(symbolReader.SymbolPath)
            ? serverPath
            : $"{symbolReader.SymbolPath}{Path.PathSeparator}{serverPath}";

        foreach (TraceModuleFile moduleFile in traceLog.ModuleFiles)
        {
            if (!IsRuntimeModule(moduleFile.Name))
            {
                continue;
            }

            try
            {
                traceLog.CodeAddresses.LookupSymbolsForModule(symbolReader, moduleFile);
            }
            catch (Exception ex) when (ex is not (OutOfMemoryException or AccessViolationException))
            {
                // Best-effort: an unfetchable module keeps its unresolved frames rather
                // than failing the read. Offline use and modules with no published PDB
                // both land here. Fatal process-corruption conditions (out of memory,
                // access violation) are allowed to surface rather than being masked as
                // a merely-missing symbol.
            }
        }
    }

    /// <summary>
    ///  Whether <paramref name="moduleName"/> is one of the unmanaged .NET runtime or
    ///  OS modules whose symbols answer "where did the native time go" (GC, JIT,
    ///  memory operations), so native resolution can be bounded to them.
    /// </summary>
    private static bool IsRuntimeModule(string? moduleName)
    {
        if (string.IsNullOrEmpty(moduleName))
        {
            return false;
        }

        // Match the runtime/OS modules by name substring (case-insensitive); the
        // module name carries no extension here. `clr` is matched exactly because it is
        // a short token that would otherwise match unrelated names.
        foreach (string token in s_runtimeModuleTokens)
        {
            if (moduleName.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return moduleName.Equals("clr", StringComparison.OrdinalIgnoreCase);
    }

    private static readonly string[] s_runtimeModuleTokens =
    [
        "coreclr",
        "clrjit",
        "ntdll",
        "kernelbase",
        "kernel32",
        "ucrtbase",
        "msvcrt"
    ];

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
