// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Tracing;

/// <summary>
///  How a trace read should resolve <em>native</em> runtime symbols: the agent-facing
///  intent the reader turns into a symbol-server path and per-module lookups.
/// </summary>
/// <remarks>
///  <para>
///   Managed frames (including NGEN and ReadyToRun framework methods) resolve for
///   free from the CLR rundown baked into every trace, so the default - 
///   <see cref="None"/> - needs no symbol server and stays fully offline and
///   deterministic. The unmanaged runtime frames (the GC, the JIT, <c>memset</c> /
///   <c>memcpy</c>, write barriers) live in native modules - <c>coreclr</c>,
///   <c>clrjit</c>, <c>ntdll</c>, <c>ucrtbase</c> - whose PDBs are not in the trace;
///   resolving them requires fetching from the
///   <see href="https://msdl.microsoft.com/download/symbols">Microsoft public symbol
///   server</see>. That is opt-in (<see cref="WithCache"/>) because the first fetch
///   hits the network and writes a local cache, which a default-offline analysis and
///   CI must never do implicitly.
///  </para>
/// </remarks>
public sealed class SymbolOptions
{
    private SymbolOptions(bool resolveNativeRuntime, string? cacheDirectory)
    {
        ResolveNativeRuntime = resolveNativeRuntime;
        CacheDirectory = cacheDirectory;
    }

    /// <summary>
    ///  The default: resolve managed frames from the trace's rundown only, never
    ///  reaching a symbol server. Offline and deterministic.
    /// </summary>
    public static SymbolOptions None { get; } = new(resolveNativeRuntime: false, cacheDirectory: null);

    /// <summary>
    ///  Resolve native runtime frames from the Microsoft public symbol server, caching
    ///  downloaded PDBs under <paramref name="cacheDirectory"/>.
    /// </summary>
    /// <param name="cacheDirectory">
    ///  The local directory the symbol server caches downloaded PDBs in. When
    ///  <see langword="null"/> or empty the reader uses a default under the temp path
    ///  (<c>traceq-symbols</c>), so repeated reads reuse one cache.
    /// </param>
    /// <returns>The opt-in native-symbol options.</returns>
    public static SymbolOptions WithCache(string? cacheDirectory = null) =>
        new(resolveNativeRuntime: true, cacheDirectory: string.IsNullOrEmpty(cacheDirectory) ? null : cacheDirectory);

    /// <summary>
    ///  Whether to resolve native runtime frames from the public symbol server. When
    ///  <see langword="false"/> only the trace's own rundown is consulted (managed
    ///  frames), which is the offline default.
    /// </summary>
    public bool ResolveNativeRuntime { get; }

    /// <summary>
    ///  The local symbol-cache directory, or <see langword="null"/> to use the default
    ///  under the temp path. Only consulted when <see cref="ResolveNativeRuntime"/> is
    ///  <see langword="true"/>.
    /// </summary>
    public string? CacheDirectory { get; }

    /// <summary>
    ///  The default symbol-cache directory used when the caller does not supply one:
    ///  <c>traceq-symbols</c> under the system temp path.
    /// </summary>
    public static string DefaultCacheDirectory => Path.Combine(Path.GetTempPath(), "traceq-symbols");

    /// <summary>
    ///  A stable cache-key fragment for this options value, so a trace read with native
    ///  symbols is cached separately from one without.
    /// </summary>
    /// <returns>
    ///  <c>native:&lt;cache&gt;</c> when native resolution is on, otherwise <c>managed</c>.
    /// </returns>
    public string CacheKeyFragment() =>
        ResolveNativeRuntime ? $"native:{CacheDirectory ?? DefaultCacheDirectory}" : "managed";
}
