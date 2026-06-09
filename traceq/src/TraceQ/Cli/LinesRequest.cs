// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Cli;

/// <summary>
///  The validated inputs to a hot-lines run: which trace to load, which methods to
///  scope to, how to fold, and how to render it.
/// </summary>
/// <param name="Path">The trace file path.</param>
/// <param name="Method">Substring scoping the ranking to matching methods, or empty for every method.</param>
/// <param name="Fold">The leaf-frame fold patterns.</param>
/// <param name="Top">Maximum number of ranked lines to return.</param>
/// <param name="Symbols">Optional build-output directory whose embedded PDBs resolve managed frames.</param>
/// <param name="Format">The render format.</param>
/// <param name="Strict">Whether to trip the strict symbol-resolution exit gate.</param>
internal sealed record LinesRequest(
    string Path,
    string Method,
    IReadOnlyList<string> Fold,
    int Top,
    string? Symbols,
    OutputFormat Format,
    bool Strict);
