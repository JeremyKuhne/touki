// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using TraceQ.Server;
using TraceQ.Tracing;

namespace TraceQ.Cli;

/// <summary>
///  The shared steps every engine-verb executor performs: validate the
///  user-supplied fold patterns (for the verbs that accept them), load the trace
///  while mapping its failure modes to defined exit codes, collect the
///  symbol-resolution warning, and decide the <c>--strict</c> exit.
/// </summary>
/// <remarks>
///  <para>
///   Centralizing these keeps one copy of the security-relevant input handling -
///   for the verbs that take a fold list a malformed fold regex is a usage error,
///   and for every verb missing, unreadable, or malformed trace input terminates
///   with a defined exit code rather than crashing - rather than duplicating the
///   handling per executor.
///  </para>
/// </remarks>
internal static class TraceExecution
{
    /// <summary>
    ///  Validates that every user-supplied fold pattern compiles.
    /// </summary>
    /// <param name="fold">The fold patterns.</param>
    /// <param name="error">The writer a malformed-pattern message is reported to.</param>
    /// <returns>
    ///  <see langword="true"/> when all patterns compile; otherwise <see langword="false"/>,
    ///  and the caller should return <see cref="ExitCodes.UsageError"/>.
    /// </returns>
    public static bool TryValidateFold(IReadOnlyList<string> fold, TextWriter error)
    {
        try
        {
            _ = FrameNames.CompileFoldPatterns(fold);
            return true;
        }
        catch (ArgumentException ex)
        {
            error.WriteLine(ex.Message);
            return false;
        }
    }

    /// <summary>
    ///  Loads the CPU view of the trace at <paramref name="path"/>, mapping the
    ///  loader's failure modes to a clean error rather than an unhandled exception.
    /// </summary>
    /// <param name="path">The trace file path.</param>
    /// <param name="symbols">Optional build-output directory for symbol resolution.</param>
    /// <param name="error">The writer a load-failure message is reported to.</param>
    /// <param name="trace">The loaded trace on success.</param>
    /// <returns>
    ///  <see langword="true"/> on success; otherwise <see langword="false"/>, and the
    ///  caller should return <see cref="ExitCodes.InputError"/>.
    /// </returns>
    public static bool TryLoad(
        string path,
        string? symbols,
        TextWriter error,
        [NotNullWhen(true)] out LoadedTrace? trace) =>
        TryLoad(path, TraceMetric.Cpu, symbols, error, out trace);

    /// <summary>
    ///  Loads the <paramref name="metric"/> view of the trace at <paramref name="path"/>,
    ///  mapping the loader's failure modes to a clean error rather than an unhandled
    ///  exception.
    /// </summary>
    /// <param name="path">The trace file path.</param>
    /// <param name="metric">Which provider view to load (CPU, allocations, ...).</param>
    /// <param name="symbols">Optional build-output directory for symbol resolution.</param>
    /// <param name="error">The writer a load-failure message is reported to.</param>
    /// <param name="trace">The loaded trace on success.</param>
    /// <param name="scope">
    ///  Optional process scope (an explicit name, the automatic busiest-process
    ///  default when <see langword="null"/>, or every process). Consumed only by the
    ///  CPU and thread-time metrics.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> on success; otherwise <see langword="false"/>, and the
    ///  caller should return <see cref="ExitCodes.InputError"/>.
    /// </returns>
    public static bool TryLoad(
        string path,
        TraceMetric metric,
        string? symbols,
        TextWriter error,
        [NotNullWhen(true)] out LoadedTrace? trace,
        ScopeRequest? scope = null,
        SymbolOptions? symbolOptions = null)
    {
        try
        {
            trace = new TraceStore().Get(path, symbols, metric, scope, symbolOptions);
            return true;
        }
        catch (Exception ex) when (
            ex is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or JsonException
            or KeyNotFoundException
            or InvalidOperationException
            or FormatException
            or ArgumentException)
        {
            // Missing, unreadable, or malformed trace input - including a format that
            // does not carry the selected metric's data (NotSupportedException) -
            // terminates with a defined exit code rather than crashing the process.
            // The KeyNotFoundException, InvalidOperationException, and FormatException
            // arms cover well-formed JSON whose shape is wrong: a missing or
            // wrong-typed field surfaces from the readers' JsonElement access
            // (GetProperty / GetDouble / GetInt32).
            error.WriteLine(ex.Message);
            trace = null;
            return false;
        }
    }

    /// <summary>
    ///  Runs a structured-report provider (GC, JIT, events) against a
    ///  <c>.nettrace</c> EventPipe trace, applying the format guardrail and mapping
    ///  the provider's failure modes to a clean error rather than an unhandled
    ///  exception.
    /// </summary>
    /// <typeparam name="T">The report result type.</typeparam>
    /// <param name="path">The trace file path.</param>
    /// <param name="reportName">
    ///  The report's name, used in the wrong-format message (for example <c>GC</c>).
    /// </param>
    /// <param name="read">The provider call producing the report.</param>
    /// <param name="error">The writer a guardrail or failure message is reported to.</param>
    /// <param name="result">The report on success.</param>
    /// <returns>
    ///  <see langword="true"/> on success; otherwise <see langword="false"/>, and the
    ///  caller should return <see cref="ExitCodes.InputError"/>.
    /// </returns>
    /// <remarks>
    ///  <para>
    ///   The report providers read EventPipe traces only (they assemble structured
    ///   records from a <c>.nettrace</c>), so a non-<c>.nettrace</c> input is rejected
    ///   up front by extension rather than failing deep inside TraceEvent's EventPipe
    ///   parser with an opaque message.
    ///  </para>
    /// </remarks>
    public static bool TryReadNetTraceReport<T>(
        string path,
        string reportName,
        Func<T> read,
        TextWriter error,
        [NotNullWhen(true)] out T? result) where T : class
    {
        // Format guardrail (an extension test, no I/O): the report providers parse the
        // EventPipe format, so reject an .etl or speedscope export cleanly here.
        if (!path.EndsWith(".nettrace", StringComparison.OrdinalIgnoreCase))
        {
            error.WriteLine(
                $"The {reportName} report requires a .nettrace EventPipe trace; '{path}' is not a .nettrace file.");
            result = null;
            return false;
        }

        try
        {
            result = read();
            return true;
        }
        catch (Exception ex) when (
            ex is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or InvalidOperationException
            or FormatException
            or ArgumentException)
        {
            // A missing, unreadable, or malformed .nettrace terminates with a defined
            // exit code rather than crashing the process; a corrupt EventPipe stream
            // surfaces from TraceEvent as one of these.
            error.WriteLine(ex.Message);
            result = null;
            return false;
        }
    }

    /// <summary>
    ///  The quality warnings to attach to a result envelope: the full list the reader
    ///  and loader recorded on <see cref="TraceInfo.Warnings"/>.
    /// </summary>
    /// <param name="info">The loaded trace's metadata.</param>
    /// <returns>The warnings to attach to the result envelope.</returns>
    /// <remarks>
    ///  <para>
    ///   <see cref="TraceInfo.Warnings"/> is the authoritative list - the reader builds
    ///   it with the symbol-resolution warning, the no-samples notice, and the
    ///   applied-scope notice when a machine-wide capture was narrowed. Forwarding it
    ///   whole keeps every quality signal visible rather than recomputing or
    ///   cherry-picking a subset, which silently dropped the others.
    ///  </para>
    /// </remarks>
    public static IReadOnlyList<string> ResultWarnings(TraceInfo info) => info.Warnings;

    /// <summary>
    ///  Decides the exit code for an otherwise successful run, applying the
    ///  <c>--strict</c> symbol-resolution gate.
    /// </summary>
    /// <param name="info">The loaded trace's metadata.</param>
    /// <param name="strict">Whether the strict gate is enabled.</param>
    /// <returns>
    ///  <see cref="ExitCodes.StrictGate"/> when strict and resolution is below the
    ///  threshold; otherwise <see cref="ExitCodes.Success"/>.
    /// </returns>
    public static int StrictExit(TraceInfo info, bool strict) =>
        strict && SymbolGate.IsBelowThreshold(info.SymbolResolutionRate, info.SampleCount)
            ? ExitCodes.StrictGate
            : ExitCodes.Success;
}
