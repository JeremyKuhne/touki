// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Cli;

/// <summary>
///  The validated inputs to a callers run: which trace to load, the focus frame
///  whose immediate callers are reported, how to scope it, and how to render it.
/// </summary>
/// <param name="Path">The trace file path.</param>
/// <param name="Frame">Substring identifying the focus frame whose callers are reported.</param>
/// <param name="Root">Substring scoping the analysis to a subtree, or empty for the whole trace.</param>
/// <param name="Top">Maximum number of caller rows to return.</param>
/// <param name="Symbols">Optional build-output directory whose embedded PDBs resolve managed frames.</param>
/// <param name="Format">The render format.</param>
/// <param name="Strict">Whether to trip the strict symbol-resolution exit gate.</param>
internal sealed record CallersRequest(
    string Path,
    string Frame,
    string Root,
    int Top,
    string? Symbols,
    OutputFormat Format,
    bool Strict);
