// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Mcp;

/// <summary>
///  The MCP server's <c>instructions</c> text: the workflow summary a client shows
///  the model so it picks the right tool order without trial and error.
/// </summary>
/// <remarks>
///  <para>
///   The text is carried on the server's <c>instructions</c> field at initialize
///   time. It is kept short on purpose - it shares the model's context budget with
///   the per-tool descriptions - and names the tools in the order an investigation
///   naturally runs them.
///  </para>
/// </remarks>
public static class TraceServerInstructions
{
    /// <summary>
    ///  The server instructions text.
    /// </summary>
    public const string Text =
        "traceq analyzes .NET CPU, allocation, exception, and thread-time traces (.nettrace, .etl, and "
        + "speedscope) from the command line - no GUI. Workflow: call trace_info first to see the format, "
        + "sample count, and symbol-resolution rate; a rate below 0.8 means symbols are missing and the "
        + "rankings should not be trusted. Use trace_rank (metric: cpu, threadtime, alloc, or exceptions) "
        + "to find the hottest frames by self or inclusive time, then trace_callers to see what drives a "
        + "frame. trace_lines and trace_heatmap attribute time to a source file:line and need a .nettrace "
        + "or .etl trace read with portable PDBs - pass the build-output directory as 'symbols'. Every "
        + "result shares one envelope: a schemaVersion, a warnings list, next-step hints, and the typed result.";
}
