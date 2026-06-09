// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Cli;

/// <summary>
///  The validated inputs to a source heat-map run: which trace to load, which
///  source file to map, how to fold, and how to render it.
/// </summary>
/// <param name="Path">The trace file path.</param>
/// <param name="File">
///  The source file to build the heat map for. Its file name is matched against the
///  trace's recorded locations; when it is a real path on disk the text renderer
///  overlays the heat onto the source.
/// </param>
/// <param name="Fold">The leaf-frame fold patterns.</param>
/// <param name="Symbols">Optional build-output directory whose embedded PDBs resolve managed frames.</param>
/// <param name="Format">The render format.</param>
/// <param name="Strict">Whether to trip the strict symbol-resolution exit gate.</param>
internal sealed record HeatmapRequest(
    string Path,
    string File,
    IReadOnlyList<string> Fold,
    string? Symbols,
    OutputFormat Format,
    bool Strict);
