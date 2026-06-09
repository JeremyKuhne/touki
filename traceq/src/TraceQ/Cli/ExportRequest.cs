// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Cli;

/// <summary>
///  The validated inputs to an export run: which trace to load, the flame-graph
///  format to emit, where to write it, and the profile name shown in the viewer.
/// </summary>
/// <param name="Path">The trace file path.</param>
/// <param name="Format">The flame-graph format to write.</param>
/// <param name="Output">The output file path, or <see langword="null"/> to write to standard output.</param>
/// <param name="Symbols">Optional build-output directory whose embedded PDBs resolve managed frames.</param>
/// <param name="Name">The profile name shown in the viewer.</param>
internal sealed record ExportRequest(
    string Path,
    ExportFormat Format,
    string? Output,
    string? Symbols,
    string Name);
