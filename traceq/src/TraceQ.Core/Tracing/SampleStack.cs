// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Tracing;

/// <summary>
///  A single weighted sample: the full call stack captured at a point in time,
///  ordered outermost-first (<c>Frames[0]</c> is the process/thread root,
///  <c>Frames[^1]</c> is the leaf), together with the weight attributed to it in
///  the source metric's unit (milliseconds for CPU time, bytes for allocations).
/// </summary>
/// <remarks>
///  <para>
///   Frame strings are stored in their full, unshortened form (typically
///   <c>module!Namespace.Type.Method(args)</c>). The aggregator shortens them
///   on demand so root-frame scoping can still match against the full text.
///  </para>
/// </remarks>
public sealed class SampleStack
{
    /// <summary>
    ///  Initializes a new <see cref="SampleStack"/>.
    /// </summary>
    /// <param name="frames">Frames ordered outermost-first.</param>
    /// <param name="weight">Weight attributed to the sample, in the source metric's unit (milliseconds for CPU, bytes for allocations).</param>
    /// <param name="thread">A label identifying the thread the sample came from.</param>
    /// <param name="frameLocations">
    ///  Optional per-frame source locations (<c>file:line</c>), parallel to
    ///  <paramref name="frames"/> and ordered the same way. An entry is empty
    ///  when the frame did not resolve to a source line. Pass
    ///  <see langword="null"/> when the source format carries no line data.
    /// </param>
    /// <param name="process">
    ///  A label identifying the process the sample came from, for traces that span
    ///  more than one. Empty for single-process trace formats.
    /// </param>
    public SampleStack(
        IReadOnlyList<string> frames,
        double weight,
        string thread = "",
        IReadOnlyList<string>? frameLocations = null,
        string process = "")
    {
        Frames = frames;
        Weight = weight;
        Thread = thread;
        FrameLocations = frameLocations;
        Process = process;
    }

    /// <summary>
    ///  The call stack, ordered outermost-first.
    /// </summary>
    public IReadOnlyList<string> Frames { get; }

    /// <summary>
    ///  Per-frame source locations (<c>file:line</c>), parallel to <see cref="Frames"/>
    ///  and ordered the same way, or <see langword="null"/> when the source format
    ///  carries no line data. An individual entry is empty when that frame did not
    ///  resolve to a source line.
    /// </summary>
    public IReadOnlyList<string>? FrameLocations { get; }

    /// <summary>
    ///  Weight attributed to this sample, in the source metric's unit
    ///  (milliseconds for CPU time, bytes for allocations).
    /// </summary>
    public double Weight { get; }

    /// <summary>
    ///  A label identifying the thread the sample came from.
    /// </summary>
    public string Thread { get; }

    /// <summary>
    ///  A label identifying the process the sample came from, for traces that span
    ///  more than one. Empty for single-process trace formats.
    /// </summary>
    public string Process { get; }
}
