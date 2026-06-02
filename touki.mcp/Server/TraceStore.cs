// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Concurrent;
using Touki.Mcp.Tracing;

namespace Touki.Mcp.Server;

/// <summary>
///  Loads traces on demand and caches the parsed model per absolute path, so
///  repeated queries against the same trace avoid re-parsing.
/// </summary>
public sealed class TraceStore
{
    private readonly TraceLoader _loader = new();

    // Match the cache's path comparison to the host file system: Windows and macOS
    // are case-insensitive, Linux is case-sensitive, so distinct-by-case paths must
    // not be conflated there.
    private readonly ConcurrentDictionary<string, LoadedTrace> _cache = new(
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
    internal LoadedTrace Get(string path, string? symbolsDirectory = null)
    {
        string fullPath = Path.GetFullPath(path);
        string symbolsKey = string.IsNullOrEmpty(symbolsDirectory)
            ? ""
            : Path.GetFullPath(symbolsDirectory);
        string key = $"{fullPath}|{symbolsKey}";
        return _cache.GetOrAdd(key, _ => _loader.Load(fullPath, symbolsDirectory));
    }
}
