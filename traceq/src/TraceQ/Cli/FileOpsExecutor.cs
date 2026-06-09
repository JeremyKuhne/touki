// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Tracing;

namespace TraceQ.Cli;

/// <summary>
///  Runs the trace file-op verbs (<c>convert</c>, <c>clean</c>) against the analysis
///  core's <see cref="TraceConverter"/>, mapping its failure modes to a defined exit
///  code rather than an unhandled exception.
/// </summary>
/// <remarks>
///  <para>
///   These verbs manage the ETLX conversion cache TraceEvent keeps beside a
///   <c>.nettrace</c> or <c>.etl</c>: <c>convert</c> builds it up front so the first
///   real query is fast, <c>clean</c> removes it to force a rebuild. They are file
///   operations, not analysis, so they bypass the ranking pipeline entirely.
///  </para>
///  <para>
///   The execution is independent of the command-line parser; it takes its inputs
///   directly and writes to the supplied writers, so it can be driven in tests as
///   well as from the verb handlers in <see cref="TraceCommands"/>.
///  </para>
/// </remarks>
internal static class FileOpsExecutor
{
    /// <summary>
    ///  Builds the ETLX cache for the trace at <paramref name="path"/>.
    /// </summary>
    /// <param name="path">The trace file path.</param>
    /// <param name="output">The writer the result is reported to.</param>
    /// <param name="error">The writer a failure message is reported to.</param>
    /// <returns>A process exit code (see <see cref="ExitCodes"/>).</returns>
    public static int Convert(string path, TextWriter output, TextWriter error)
    {
        try
        {
            string etlxPath = TraceConverter.Convert(path);
            long bytes = new FileInfo(etlxPath).Length;
            output.WriteLine($"Converted to {etlxPath} ({bytes:N0} bytes).");
            return ExitCodes.Success;
        }
        catch (Exception ex) when (
            ex is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException)
        {
            error.WriteLine(ex.Message);
            return ExitCodes.InputError;
        }
    }

    /// <summary>
    ///  Removes the ETLX cache for the trace at <paramref name="path"/>.
    /// </summary>
    /// <param name="path">The trace file path.</param>
    /// <param name="output">The writer the result is reported to.</param>
    /// <param name="error">The writer a failure message is reported to.</param>
    /// <returns>A process exit code (see <see cref="ExitCodes"/>).</returns>
    public static int Clean(string path, TextWriter output, TextWriter error)
    {
        try
        {
            string? deleted = TraceConverter.Clean(path);
            output.WriteLine(deleted is null
                ? "No ETLX cache to remove."
                : $"Removed {deleted}.");
            return ExitCodes.Success;
        }
        catch (Exception ex) when (
            ex is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException)
        {
            error.WriteLine(ex.Message);
            return ExitCodes.InputError;
        }
    }
}
