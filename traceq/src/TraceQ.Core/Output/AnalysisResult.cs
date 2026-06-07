// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Output;

/// <summary>
///  The stable envelope every analysis service returns its payload through: a
///  schema version, any warnings and steering hints, and the typed result.
/// </summary>
/// <remarks>
///  <para>
///   The envelope is the output contract's spine. It gives every verb - across
///   every provider family - one shape the CLI and MCP heads render uniformly:
///   a machine-readable <see cref="SchemaVersion"/> so a consumer can detect
///   format changes, a <see cref="Warnings"/> channel for quality signals (low
///   symbol resolution, truncated output), a <see cref="Hints"/> channel for
///   next-step nudges, and the typed <see cref="Result"/> payload.
///  </para>
///  <para>
///   This type only carries the channels. Populating <see cref="Warnings"/> from
///   the symbol gate and emitting <see cref="Hints"/> from each verb are later
///   increments; serializing the envelope deterministically is
///   <see cref="OutputJson"/>.
///  </para>
/// </remarks>
/// <typeparam name="T">The payload type the service produces.</typeparam>
public sealed class AnalysisResult<T>
{
    /// <summary>
    ///  The current output-contract schema version. Bumped when the serialized
    ///  shape changes in a way a consumer must notice.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Version 2 renamed the ranking weight fields from <c>*Milliseconds</c> to
    ///   the metric-neutral <c>*Weight</c> (so allocation rankings no longer report
    ///   bytes under a millisecond name).
    ///  </para>
    /// </remarks>
    public const int CurrentSchemaVersion = 2;

    /// <summary>
    ///  Initializes a new <see cref="AnalysisResult{T}"/>.
    /// </summary>
    /// <param name="result">The typed payload.</param>
    /// <param name="warnings">Quality-signal warnings, or <see langword="null"/> for none.</param>
    /// <param name="hints">Next-step steering hints, or <see langword="null"/> for none.</param>
    public AnalysisResult(T result, IReadOnlyList<string>? warnings = null, IReadOnlyList<string>? hints = null)
    {
        Result = result;
        Warnings = warnings ?? [];
        Hints = hints ?? [];
    }

    /// <summary>
    ///  The output-contract schema version this envelope was produced under.
    /// </summary>
    public int SchemaVersion => CurrentSchemaVersion;

    /// <summary>
    ///  Quality-signal warnings about the result (for example low symbol
    ///  resolution or truncated output). Empty when there are none.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>
    ///  Next-step steering hints for an agent consumer. Empty when there are none.
    /// </summary>
    public IReadOnlyList<string> Hints { get; }

    /// <summary>
    ///  The typed payload the service produced.
    /// </summary>
    public T Result { get; }
}
