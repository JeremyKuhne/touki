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
    ///  Loads the trace at <paramref name="path"/>, mapping the loader's failure
    ///  modes to a clean error rather than an unhandled exception.
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
        [NotNullWhen(true)] out LoadedTrace? trace)
    {
        try
        {
            trace = new TraceStore().Get(path, symbols);
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
            // Missing, unreadable, or malformed trace input terminates with a defined
            // exit code rather than crashing the process. The KeyNotFoundException,
            // InvalidOperationException, and FormatException arms cover well-formed JSON
            // whose shape is wrong: a missing or wrong-typed field surfaces from the
            // readers' JsonElement access (GetProperty / GetDouble / GetInt32).
            error.WriteLine(ex.Message);
            trace = null;
            return false;
        }
    }

    /// <summary>
    ///  Produces the symbol-resolution warning for a loaded trace, or an empty list
    ///  when resolution is above the trusted threshold.
    /// </summary>
    /// <param name="info">The loaded trace's metadata.</param>
    /// <returns>The warnings to attach to the result envelope.</returns>
    public static IReadOnlyList<string> SymbolWarnings(TraceInfo info) =>
        SymbolGate.TryGetWarning(info.SymbolResolutionRate, info.SampleCount, out string? warning)
            ? [warning]
            : [];

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
