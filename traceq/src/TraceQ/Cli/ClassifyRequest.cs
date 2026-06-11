// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Tracing;

namespace TraceQ.Cli;

/// <summary>
///  The validated inputs to a classify run: which trace to load, how to scope it, how
///  to resolve symbols, and how to render it.
/// </summary>
/// <remarks>
///  <para>
///   Classification buckets CPU self-time by runtime work category, so it reads the
///   CPU view and - like <c>cpu</c> - takes a process scope and native-symbol options
///   (the categories only distinguish the native runtime work once its symbols
///   resolve). Folding is fixed to marker-only inside the aggregator, so there is no
///   fold option here.
///  </para>
/// </remarks>
/// <param name="Path">The trace file path.</param>
/// <param name="Root">Substring scoping the classification to a subtree, or empty for the whole trace.</param>
/// <param name="Symbols">Optional build-output directory whose embedded PDBs resolve managed frames.</param>
/// <param name="Format">The render format.</param>
/// <param name="Strict">Whether to trip the strict symbol-resolution exit gate.</param>
/// <param name="Scope">The process scope, or <see langword="null"/> for the automatic default.</param>
/// <param name="SymbolOptions">Native-symbol resolution, or <see langword="null"/> for managed-only (the offline default).</param>
internal sealed record ClassifyRequest(
    string Path,
    string Root,
    string? Symbols,
    OutputFormat Format,
    bool Strict,
    ScopeRequest? Scope = null,
    SymbolOptions? SymbolOptions = null);
