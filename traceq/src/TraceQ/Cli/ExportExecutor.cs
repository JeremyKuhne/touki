// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Output;
using TraceQ.Tracing;

namespace TraceQ.Cli;

/// <summary>
///  Runs an export request against the analysis core: load the trace and serialize
///  its sample source to a speedscope or Chrome-trace flame-graph file, written to
///  a file or standard output.
/// </summary>
/// <remarks>
///  <para>
///   Export is a raw conversion of the whole sample source, so it takes no folding,
///   scoping, or ranking options. Any symbol-resolution warning is written to the
///   error writer rather than mixed into the flame-graph output, keeping the written
///   JSON clean for the viewer.
///  </para>
/// </remarks>
internal static class ExportExecutor
{
    /// <summary>
    ///  Executes the export request.
    /// </summary>
    /// <param name="request">The validated export inputs.</param>
    /// <param name="output">The writer the flame graph or a confirmation is written to.</param>
    /// <param name="error">The writer load errors and quality warnings are reported to.</param>
    /// <returns>A process exit code (see <see cref="ExitCodes"/>).</returns>
    public static int Run(ExportRequest request, TextWriter output, TextWriter error)
    {
        if (!TraceExecution.TryLoad(request.Path, request.Symbols, error, out LoadedTrace? trace))
        {
            return ExitCodes.InputError;
        }

        // Symbol warnings go to the error writer so the flame-graph JSON on the output
        // writer stays clean for redirection into a file the viewer reads.
        foreach (string warning in TraceExecution.SymbolWarnings(trace.Info))
        {
            error.WriteLine($"! {warning}");
        }

        string exported = request.Format == ExportFormat.Chromium
            ? ChromiumExporter.Export(trace.Source, request.Name)
            : SpeedscopeExporter.Export(trace.Source, request.Name);

        if (request.Output is null)
        {
            // Write without a trailing newline so a stdout redirect produces a file
            // byte-for-byte identical to the --output path below.
            output.Write(exported);
            return ExitCodes.Success;
        }

        try
        {
            File.WriteAllText(request.Output, exported);
        }
        catch (Exception ex) when (
            ex is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or System.Security.SecurityException
            or ArgumentException)
        {
            // A bad or unwritable output path fails with a defined exit code rather
            // than crashing the process.
            error.WriteLine($"Could not write '{request.Output}': {ex.Message}");
            return ExitCodes.InputError;
        }

        output.WriteLine($"Wrote {request.Format.ToString().ToLowerInvariant()} export to {request.Output}");
        return ExitCodes.Success;
    }
}
