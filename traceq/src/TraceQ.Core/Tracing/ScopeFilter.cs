// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Text.RegularExpressions;

namespace TraceQ.Tracing;

/// <summary>
///  The virtual scope applied to a stack-sample source before ranking: keep only
///  the samples whose call stack matches the include patterns, and drop those
///  matching the exclude patterns, so an agent can narrow a ranking to the
///  relevant code without re-capturing the trace.
/// </summary>
/// <remarks>
///  <para>
///   This is the applicable subset of the implementation plan's filter grammar.
///   The two altitudes that need data the normalized <see cref="SampleStack"/>
///   model does not carry yet - time-windowing (no per-sample timestamp) and
///   process scoping (no per-sample process; the EventPipe captures are
///   single-process regardless) - are deferred, as is the frame-grouping
///   transform. Include / exclude on frame names, together with the aggregator's
///   existing root-frame scope and fold patterns, are what the model supports
///   today.
///  </para>
///  <para>
///   The filter is a transform from one <see cref="StackSampleSource"/> to a
///   narrower one of the same metric, so every engine verb composes with it
///   without change: rank a filtered source, diff two filtered sources, export a
///   filtered source.
///  </para>
/// </remarks>
internal sealed class ScopeFilter
{
    /// <summary>
    ///  The identity filter: keeps every sample.
    /// </summary>
    public static ScopeFilter None { get; } = new([], []);

    /// <summary>
    ///  Initializes a new <see cref="ScopeFilter"/>.
    /// </summary>
    /// <param name="include">
    ///  Frame-name patterns; a sample is kept only if some frame matches at least
    ///  one. Empty keeps every sample (subject to <paramref name="exclude"/>).
    /// </param>
    /// <param name="exclude">
    ///  Frame-name patterns; a sample is dropped if some frame matches any. Exclude
    ///  takes precedence over include.
    /// </param>
    public ScopeFilter(IReadOnlyList<string> include, IReadOnlyList<string> exclude)
    {
        Include = include;
        Exclude = exclude;
    }

    /// <summary>
    ///  Patterns a kept sample must match on at least one frame.
    /// </summary>
    public IReadOnlyList<string> Include { get; }

    /// <summary>
    ///  Patterns that drop a sample when any frame matches.
    /// </summary>
    public IReadOnlyList<string> Exclude { get; }

    /// <summary>
    ///  Whether the filter has no patterns and so keeps every sample.
    /// </summary>
    public bool IsEmpty => Include.Count == 0 && Exclude.Count == 0;

    /// <summary>
    ///  Applies the filter to <paramref name="source"/>, returning a narrower
    ///  source of the same metric. The same instance is returned when the filter
    ///  is empty.
    /// </summary>
    /// <param name="source">The source to scope.</param>
    /// <returns>The filtered source.</returns>
    /// <exception cref="ArgumentException">An include or exclude pattern is not a valid regular expression.</exception>
    public StackSampleSource Apply(StackSampleSource source)
    {
        if (IsEmpty)
        {
            return source;
        }

        Regex[] include = Compile(Include, nameof(Include));
        Regex[] exclude = Compile(Exclude, nameof(Exclude));

        List<SampleStack> kept = [];
        foreach (SampleStack sample in source.Samples)
        {
            // Exclude wins: a sample with any excluded frame is dropped regardless
            // of include matches.
            if (exclude.Length > 0 && AnyFrameMatches(sample, exclude))
            {
                continue;
            }

            if (include.Length > 0 && !AnyFrameMatches(sample, include))
            {
                continue;
            }

            kept.Add(sample);
        }

        return new StackSampleSource(source.Metric, kept);
    }

    // Compiles a set of filter patterns, recasting the shared helper's "fold
    // pattern" wording into the include / exclude context the caller passed so a
    // bad regex reports against the right parameter.
    private static Regex[] Compile(IReadOnlyList<string> patterns, string which)
    {
        try
        {
            return FrameNames.CompileFoldPatterns(patterns);
        }
        catch (ArgumentException ex)
        {
            throw new ArgumentException($"Invalid {which} filter pattern: {ex.Message}", which.ToLowerInvariant(), ex);
        }
    }

    private static bool AnyFrameMatches(SampleStack sample, Regex[] patterns)
    {
        IReadOnlyList<string> frames = sample.Frames;
        for (int i = 0; i < frames.Count; i++)
        {
            string frame = frames[i];
            foreach (Regex pattern in patterns)
            {
                try
                {
                    if (pattern.IsMatch(frame))
                    {
                        return true;
                    }
                }
                catch (RegexMatchTimeoutException)
                {
                    // A pathological pattern/input pair that hits the match timeout is
                    // treated as a non-match: scoping must never fail a ranking query.
                }
            }
        }

        return false;
    }
}
