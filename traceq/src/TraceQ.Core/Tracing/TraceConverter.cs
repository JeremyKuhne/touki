// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.Diagnostics.Tracing.Etlx;

namespace TraceQ.Tracing;

/// <summary>
///  Builds and removes the <c>.etlx</c> conversion cache TraceEvent writes beside a
///  <c>.nettrace</c> or <c>.etl</c> trace, backing the <c>convert</c> and
///  <c>clean</c> file-op verbs.
/// </summary>
/// <remarks>
///  <para>
///   Every analysis of a <c>.nettrace</c> or <c>.etl</c> first converts it to an
///   ETLX file (the indexed form TraceEvent reads). TraceEvent caches that ETLX
///   beside the source and reuses it on the next read, so converting up front makes
///   the first real query fast, and cleaning it forces a rebuild when a stale cache
///   is suspected. A speedscope export carries no ETLX (it is parsed as JSON), so
///   neither operation applies to it.
///  </para>
/// </remarks>
public static class TraceConverter
{
    /// <summary>
    ///  Converts the <c>.nettrace</c> or <c>.etl</c> trace at <paramref name="path"/>
    ///  to its ETLX cache, returning the ETLX file path.
    /// </summary>
    /// <param name="path">The trace file path.</param>
    /// <returns>The path of the ETLX file written beside the trace.</returns>
    /// <exception cref="ArgumentException"><paramref name="path"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="NotSupportedException">The file is not a convertible trace format.</exception>
    public static string Convert(string path)
    {
        string fullPath = ValidateConvertible(path);

        if (fullPath.EndsWith(".nettrace", StringComparison.OrdinalIgnoreCase))
        {
            // CreateFromEventPipeDataFile writes the ETLX beside the source and returns
            // its path - the authoritative location, captured rather than recomputed.
            return TraceLog.CreateFromEventPipeDataFile(
                fullPath,
                null,
                new TraceLogOptions { ContinueOnError = true });
        }

        // OpenOrConvert writes "<trace>.etlx" beside the source as a side effect; open
        // it (disposing immediately) to force the write, then return that path.
        using (TraceLog.OpenOrConvert(fullPath, new TraceLogOptions { ContinueOnError = true }))
        {
        }

        return EtlxPathFor(fullPath);
    }

    /// <summary>
    ///  Removes the ETLX cache beside the trace at <paramref name="path"/>, if present.
    /// </summary>
    /// <param name="path">The trace file path.</param>
    /// <returns>
    ///  The ETLX path that was deleted, or <see langword="null"/> when no cache existed.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="path"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="NotSupportedException">The file is not a convertible trace format.</exception>
    public static string? Clean(string path)
    {
        string fullPath = ValidateConvertible(path);
        string etlxPath = EtlxPathFor(fullPath);

        if (!File.Exists(etlxPath))
        {
            return null;
        }

        File.Delete(etlxPath);
        return etlxPath;
    }

    /// <summary>
    ///  The ETLX cache path TraceEvent uses for the trace at <paramref name="path"/>:
    ///  the trace path with <c>.etlx</c> appended.
    /// </summary>
    /// <param name="path">The trace file path.</param>
    /// <returns>The ETLX file path.</returns>
    public static string EtlxPathFor(string path) => path + ".etlx";

    private static string ValidateConvertible(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Trace file not found: {fullPath}", fullPath);
        }

        if (!fullPath.EndsWith(".nettrace", StringComparison.OrdinalIgnoreCase)
            && !fullPath.EndsWith(".etl", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                $"Only .nettrace and .etl traces have an ETLX cache; '{fullPath}' does not.");
        }

        return fullPath;
    }
}
