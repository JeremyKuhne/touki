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
    ///  extracted to resolve managed frames to <c>file:line</c>. The cache keys on it,
    ///  so the same trace loaded with and without symbols is cached separately.
    /// </param>
    /// <returns>The cached loaded trace.</returns>
    public LoadedTrace Get(string path, string? symbolsDirectory = null)
    {
        string fullPath = Path.GetFullPath(path);
        string? fullSymbols = string.IsNullOrEmpty(symbolsDirectory)
            ? null
            : Path.GetFullPath(symbolsDirectory);

        // Length-prefix the first path so the two components cannot be confused for a
        // different pair: '|' - like every other ASCII separator - is a legal POSIX
        // file-name character, so a plain "a|b" delimiter could collide ("a|b" + "c"
        // versus "a" + "b|c"). Loading uses the normalized symbols path so a relative
        // symbolsDirectory resolves exactly the way it was keyed.
        string key = $"{fullPath.Length}|{fullPath}{fullSymbols}";
        return _cache.GetOrAdd(key, _ => _loader.Load(fullPath, fullSymbols));
    }
}
