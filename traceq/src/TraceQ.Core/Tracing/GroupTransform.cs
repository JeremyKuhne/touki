// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Text.RegularExpressions;

namespace TraceQ.Tracing;

/// <summary>
///  The frame-grouping transform: rewrites every frame belonging to a matched
///  module into a single labelled box (<c>module!</c>), so an agent can treat
///  unrelated libraries as opaque and focus the ranking on its own code.
/// </summary>
/// <remarks>
///  <para>
///   This is the grouping altitude of the implementation plan's filter grammar
///   (PerfView's <c>[group module entries]</c> preset). The common, well-defined
///   case is module grouping: a frame named <c>module!Namespace.Type.Method(args)</c>
///   whose module matches a pattern collapses to <c>module!</c>, so all of that
///   module's frames become one node in the ranking and call tree.
///  </para>
///  <para>
///   Like <see cref="ScopeFilter"/>, this is a transform from one
///   <see cref="StackSampleSource"/> to another of the same metric, so every
///   engine verb composes with it unchanged. Collapsing adjacent frames of the
///   same module would leave consecutive duplicates on a stack, which would make
///   an inclusive-time ranking count the group more than once per sample, so the
///   transform also removes consecutive duplicate frames.
///  </para>
/// </remarks>
public sealed class GroupTransform
{
    /// <summary>
    ///  The identity transform: leaves every frame unchanged.
    /// </summary>
    public static GroupTransform None { get; } = new([]);

    /// <summary>
    ///  Initializes a new <see cref="GroupTransform"/>.
    /// </summary>
    /// <param name="modulePatterns">
    ///  Patterns matched against each frame's module name; a frame whose module
    ///  matches any pattern collapses to <c>module!</c>. Empty leaves every frame
    ///  unchanged.
    /// </param>
    public GroupTransform(IReadOnlyList<string> modulePatterns)
    {
        ModulePatterns = modulePatterns;
    }

    /// <summary>
    ///  Module-name patterns whose frames collapse to a single <c>module!</c> box.
    /// </summary>
    public IReadOnlyList<string> ModulePatterns { get; }

    /// <summary>
    ///  Whether the transform has no patterns and so leaves every frame unchanged.
    /// </summary>
    public bool IsEmpty => ModulePatterns.Count == 0;

    /// <summary>
    ///  Applies the transform to <paramref name="source"/>, returning a source of
    ///  the same metric with matched modules' frames collapsed. The same instance
    ///  is returned when the transform is empty.
    /// </summary>
    /// <param name="source">The source to transform.</param>
    /// <returns>The grouped source.</returns>
    /// <exception cref="ArgumentException">A module pattern is not a valid regular expression.</exception>
    public StackSampleSource Apply(StackSampleSource source)
    {
        if (IsEmpty)
        {
            return source;
        }

        Regex[] patterns = Compile(ModulePatterns);

        List<SampleStack> grouped = new(source.Samples.Count);
        List<string> rewritten = [];
        foreach (SampleStack sample in source.Samples)
        {
            IReadOnlyList<string> frames = sample.Frames;
            rewritten.Clear();

            string? previous = null;
            for (int i = 0; i < frames.Count; i++)
            {
                string frame = GroupFrame(frames[i], patterns);

                // Collapsing adjacent same-module frames produces consecutive
                // duplicates; keep only the first so inclusive ranking counts the
                // group once per sample.
                if (frame == previous)
                {
                    continue;
                }

                rewritten.Add(frame);
                previous = frame;
            }

            // Grouping rewrites frame names, so any per-frame source locations no
            // longer line up with the collapsed stack; drop them.
            grouped.Add(new SampleStack([.. rewritten], sample.Weight, sample.Thread));
        }

        return new StackSampleSource(source.Metric, grouped);
    }

    // Collapses a frame to "module!" when its module matches any pattern, otherwise
    // returns it unchanged. A frame with no "module!" prefix is never grouped.
    private static string GroupFrame(string frame, Regex[] patterns)
    {
        int bang = frame.IndexOf('!');
        if (bang <= 0)
        {
            return frame;
        }

        string module = frame[..bang];
        foreach (Regex pattern in patterns)
        {
            try
            {
                if (pattern.IsMatch(module))
                {
                    return $"{module}!";
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // A pathological pattern/input pair that hits the match timeout is
                // treated as a non-match: grouping must never fail a ranking query.
            }
        }

        return frame;
    }

    private static Regex[] Compile(IReadOnlyList<string> patterns)
    {
        try
        {
            return FrameNames.CompileFoldPatterns(patterns);
        }
        catch (ArgumentException ex)
        {
            throw new ArgumentException($"Invalid group module pattern: {ex.Message}", nameof(ModulePatterns), ex);
        }
    }
}
