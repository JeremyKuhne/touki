// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Tracing.Readers;

/// <summary>
///  The normalized output of a trace reader: weighted samples plus the
///  format-specific quality signals the loader folds into a <see cref="TraceInfo"/>.
/// </summary>
/// <param name="Samples">The weighted samples, each ordered outermost-first.</param>
/// <param name="SymbolResolutionRate">Fraction in <c>[0, 1]</c> of frames that resolved to a method name.</param>
/// <param name="Warnings">Format-specific quality warnings.</param>
internal sealed record TraceReadResult(
    IReadOnlyList<SampleStack> Samples,
    double SymbolResolutionRate,
    IReadOnlyList<string> Warnings);

/// <summary>
///  Reads a trace file of a specific on-disk format into the normalized
///  weighted-sample model.
/// </summary>
internal interface ITraceReader
{
    /// <summary>
    ///  The format this reader handles.
    /// </summary>
    TraceFormat Format { get; }

    /// <summary>
    ///  Determines whether this reader can read the file at <paramref name="path"/>,
    ///  by file extension.
    /// </summary>
    /// <param name="path">The trace file path.</param>
    /// <returns><see langword="true"/> if the extension is recognized.</returns>
    bool CanRead(string path);

    /// <summary>
    ///  Reads the trace into the normalized weighted-sample model.
    /// </summary>
    /// <param name="path">The trace file path.</param>
    /// <param name="symbolsDirectory">
    ///  Optional build-output directory whose assemblies' embedded portable PDBs are
    ///  extracted to resolve managed frames to <c>file:line</c>. Ignored by formats
    ///  that carry no native frames (speedscope).
    /// </param>
    /// <param name="scope">
    ///  Optional process scope. A <see langword="null"/> request is the automatic
    ///  busiest-process default (the same as <see cref="ScopeRequest.Auto"/>): the read
    ///  is narrowed to an explicit name, the busiest process automatically, or every
    ///  process when opted out. Ignored by single-process formats (speedscope).
    /// </param>
    /// <returns>The normalized samples and quality signals.</returns>
    TraceReadResult Read(string path, string? symbolsDirectory = null, ScopeRequest? scope = null);
}
