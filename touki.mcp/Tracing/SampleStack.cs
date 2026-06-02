// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Mcp.Tracing;

/// <summary>
///  A single weighted CPU sample: the full call stack captured at a point in
///  time, ordered outermost-first (<c>Frames[0]</c> is the process/thread root,
///  <c>Frames[^1]</c> is the leaf), together with the wall-clock weight in
///  milliseconds attributed to it.
/// </summary>
/// <remarks>
///  <para>
///   Frame strings are stored in their full, unshortened form (typically
///   <c>module!Namespace.Type.Method(args)</c>). The aggregator shortens them
///   on demand so root-frame scoping can still match against the full text.
///  </para>
/// </remarks>
internal sealed class SampleStack
{
    /// <summary>
    ///  Initializes a new <see cref="SampleStack"/>.
    /// </summary>
    /// <param name="frames">Frames ordered outermost-first.</param>
    /// <param name="weightMs">Wall-clock weight attributed to the sample, in milliseconds.</param>
    /// <param name="thread">A label identifying the thread the sample came from.</param>
    /// <param name="frameLocations">
    ///  Optional per-frame source locations (<c>file:line</c>), parallel to
    ///  <paramref name="frames"/> and ordered the same way. An entry is empty
    ///  when the frame did not resolve to a source line. Pass
    ///  <see langword="null"/> when the source format carries no line data.
    /// </param>
    public SampleStack(
        IReadOnlyList<string> frames,
        double weightMs,
        string thread = "",
        IReadOnlyList<string>? frameLocations = null)
    {
        Frames = frames;
        WeightMs = weightMs;
        Thread = thread;
        FrameLocations = frameLocations;
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
    ///  Wall-clock weight attributed to this sample, in milliseconds.
    /// </summary>
    public double WeightMs { get; }

    /// <summary>
    ///  A label identifying the thread the sample came from.
    /// </summary>
    public string Thread { get; }
}
