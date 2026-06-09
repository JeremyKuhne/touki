// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Cli;

/// <summary>
///  The validated inputs to a diff run: the two traces to compare, how to scope
///  and fold each ranking, which measure to compare, and how to render it.
/// </summary>
/// <param name="BeforePath">The baseline trace file path.</param>
/// <param name="AfterPath">The current trace file path.</param>
/// <param name="Root">Substring scoping both rankings to a subtree, or empty for the whole trace.</param>
/// <param name="Top">Maximum number of changed rows to return.</param>
/// <param name="Fold">The leaf-frame fold patterns applied to both rankings.</param>
/// <param name="Measure">Which measure to compare.</param>
/// <param name="Format">The render format.</param>
/// <param name="Symbols">Optional build-output directory whose embedded PDBs resolve managed frames.</param>
/// <param name="Strict">Whether to trip the strict symbol-resolution exit gate.</param>
internal sealed record DiffRequest(
    string BeforePath,
    string AfterPath,
    string Root,
    int Top,
    IReadOnlyList<string> Fold,
    Measure Measure,
    OutputFormat Format,
    string? Symbols,
    bool Strict);
