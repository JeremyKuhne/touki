// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Caching;
using TraceQ.Tracing;

namespace TraceQ.Server;

/// <summary>
///  Loads traces on demand and caches the parsed model per absolute path, so
///  repeated queries against the same trace avoid re-parsing.
/// </summary>
/// <remarks>
///  <para>
///   The cache is a bounded least-recently-used cache: a long agent session can
///   touch many traces, and each parsed model is potentially large, so the store
///   retains only the most recently used traces rather than growing without limit.
///  </para>
/// </remarks>
public sealed class TraceStore
{
    /// <summary>
    ///  The maximum number of parsed traces retained before the least-recently-used
    ///  one is evicted.
    /// </summary>
    public const int DefaultCapacity = 16;

    private readonly TraceLoader _loader = new();

    // Match the cache's path comparison to the host file system: Windows and macOS
    // are case-insensitive, Linux is case-sensitive, so distinct-by-case paths must
    // not be conflated there.
    private readonly LruCache<string, LoadedTrace> _cache;

    /// <summary>
    ///  Initializes a new <see cref="TraceStore"/> retaining at most
    ///  <see cref="DefaultCapacity"/> traces.
    /// </summary>
    public TraceStore()
        : this(DefaultCapacity)
    {
    }

    /// <summary>
    ///  Initializes a new <see cref="TraceStore"/> retaining at most
    ///  <paramref name="capacity"/> traces.
    /// </summary>
    /// <param name="capacity">The maximum number of parsed traces to retain. Must be positive.</param>
    internal TraceStore(int capacity) =>
        _cache = new LruCache<string, LoadedTrace>(
            capacity,
            OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///  Returns the loaded trace for <paramref name="path"/>, loading and caching
    ///  it on first use.
    /// </summary>
    /// <param name="path">The trace file path.</param>
    /// <param name="symbolsDirectory">
    ///  Optional build-output directory whose assemblies' embedded portable PDBs are
    ///  extracted to resolve managed frames to <c>file:line</c>. Consumed only by the
    ///  CPU metric; the other providers resolve frames from the trace's own rundown
    ///  and ignore it. The cache keys on it for the CPU metric, so the same trace
    ///  loaded with and without symbols is cached separately.
    /// </param>
    /// <param name="metric">
    ///  Which provider's view to load: the CPU sampler's stacks (the default), the
    ///  allocation sites, and so on. The cache keys on it, so the same trace's CPU
    ///  and allocation views are cached separately.
    /// </param>
    /// <param name="scope">
    ///  Optional process scope (an explicit name, the automatic busiest-process
    ///  default, or every process). Consumed only by the CPU and thread-time metrics;
    ///  the other providers read a single-process EventPipe trace and ignore it. The
    ///  cache keys on it for those metrics, so the same trace scoped two ways is cached
    ///  separately.
    /// </param>
    /// <returns>The cached loaded trace.</returns>
    public LoadedTrace Get(
        string path,
        string? symbolsDirectory = null,
        TraceMetric metric = TraceMetric.Cpu,
        ScopeRequest? scope = null)
    {
        string fullPath = Path.GetFullPath(path);

        // Only the CPU loader consumes symbolsDirectory; the other providers resolve
        // frames from the trace's own rundown and ignore it. Drop it for those metrics
        // so two calls that differ only in an ignored symbols directory dedupe to one
        // cache entry instead of forcing a redundant provider read - and, for thread
        // time, a redundant ETLX conversion.
        string? fullSymbols = metric == TraceMetric.Cpu && !string.IsNullOrEmpty(symbolsDirectory)
            ? Path.GetFullPath(symbolsDirectory)
            : null;

        // Only the CPU and thread-time metrics read a multi-process capture, so scope
        // only distinguishes those; drop it for the single-process EventPipe metrics so
        // their cache entries are not split by an ignored scope.
        string scopeKey = metric is TraceMetric.Cpu or TraceMetric.ThreadTime
            ? ScopeKey(scope)
            : "-";

        // Length-prefix the first path so the two components cannot be confused for a
        // different pair: '|' - like every other ASCII separator - is a legal POSIX
        // file-name character, so a plain "a|b" delimiter could collide ("a|b" + "c"
        // versus "a" + "b|c"). The metric and scope prefixes keep a trace's distinct
        // provider views and scopes from sharing one cache entry. Loading uses the
        // normalized symbols path so a relative symbolsDirectory resolves exactly the
        // way it was keyed.
        string key = $"{(int)metric}:{scopeKey}:{fullPath.Length}|{fullPath}{fullSymbols}";
        return _cache.GetOrAdd(key, _ => _loader.Load(fullPath, metric, fullSymbols, scope));
    }

    // A stable cache-key fragment for a scope request: 'all' for all-processes, 'auto'
    // for the automatic busiest-process default (a null request is unspecified, which is
    // the same default), or the explicit process name. Because the load path treats a
    // null request as the automatic default, null and ScopeRequest.Auto resolve to the
    // same trace and so share the 'auto' fragment by design. The name is length-prefixed
    // so it cannot be confused with the sentinels or run into the following key segment.
    private static string ScopeKey(ScopeRequest? scope)
    {
        if (scope is null || (scope.ProcessName is null && !scope.IncludeAll))
        {
            return "auto";
        }

        if (scope.IncludeAll)
        {
            return "all";
        }

        string name = scope.ProcessName!;
        return $"p{(scope.IncludeChildren ? "+" : "-")}{name.Length}:{name}";
    }
}
