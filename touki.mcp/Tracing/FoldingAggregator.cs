// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Text.RegularExpressions;

namespace Touki.Mcp.Tracing;

/// <summary>
///  Aggregates weighted CPU samples into self-time, inclusive-time and
///  caller rankings, folding JIT-helper sampling artifacts and the synthetic
///  BenchmarkDotNet markers back into the real methods that incurred them.
/// </summary>
/// <remarks>
///  <para>
///   This is a direct port of the aggregation in
///   <c>tools/Get-TraceHotspots.ps1</c>. Two facts make a naive "top self-time"
///   reading wrong, and this type corrects both:
///  </para>
///  <para>
///   The leaf self-time of every sample collapses into a synthetic
///   <c>CPU_TIME</c> / <c>UNMANAGED_CODE_TIME</c> marker, so an unfolded
///   self-time aggregation reports almost all time against that marker. And
///   when a sample's instruction pointer lands inside a JIT helper (a write
///   barrier, a memmove, the GC-poll thunk at a loop back-edge), the
///   managed-only walker resolves the leaf to the helper thunk instead of the
///   method whose hot loop is actually running.
///  </para>
///  <para>
///   Self-time walks up past folded frames to credit the nearest real leaf;
///   inclusive-time simply skips folded frames. The result matches what PerfView
///   produces with <c>/FoldPats</c>.
///  </para>
/// </remarks>
internal sealed class FoldingAggregator
{
    private readonly IReadOnlyList<SampleStack> _samples;
    private readonly Dictionary<string, string> _shortCache = new(StringComparer.Ordinal);

    /// <summary>
    ///  Initializes a new <see cref="FoldingAggregator"/> over the given samples.
    /// </summary>
    /// <param name="samples">The normalized weighted samples.</param>
    public FoldingAggregator(IReadOnlyList<SampleStack> samples)
    {
        _samples = samples;
    }

    private string ShortOf(string name)
    {
        if (!_shortCache.TryGetValue(name, out string? value))
        {
            value = FrameNames.Short(name);
            _shortCache[name] = value;
        }

        return value;
    }

    /// <summary>
    ///  Finds the index of the first frame (outermost-first) containing
    ///  <paramref name="rootFrame"/>, or <c>0</c> when no root scoping is
    ///  requested. Returns <see langword="false"/> in <paramref name="include"/>
    ///  when a root frame was requested but not present on the stack.
    /// </summary>
    private static int ResolveStart(IReadOnlyList<string> frames, string rootFrame, out bool include)
    {
        if (string.IsNullOrEmpty(rootFrame))
        {
            include = true;
            return 0;
        }

        for (int i = 0; i < frames.Count; i++)
        {
            if (frames[i].Contains(rootFrame, StringComparison.Ordinal))
            {
                include = true;
                return i;
            }
        }

        include = false;
        return 0;
    }

    /// <summary>
    ///  Computes the folded self-time ranking.
    /// </summary>
    /// <param name="rootFrame">Substring scoping the ranking to a subtree, or empty for the whole trace.</param>
    /// <param name="foldPatterns">Leaf-frame fold patterns.</param>
    /// <param name="top">Maximum number of rows to return.</param>
    /// <returns>The self-time ranking.</returns>
    public RankingResult SelfTime(string rootFrame, IReadOnlyList<string> foldPatterns, int top)
    {
        Regex[] fold = FrameNames.CompileFoldPatterns(foldPatterns);
        Dictionary<string, double> selfTime = new(StringComparer.Ordinal);
        double total = 0.0;

        foreach (SampleStack sample in _samples)
        {
            IReadOnlyList<string> frames = sample.Frames;
            int startIdx = ResolveStart(frames, rootFrame, out bool include);
            if (!include || frames.Count == 0)
            {
                continue;
            }

            total += sample.WeightMs;

            int leafIdx = frames.Count - 1;
            while (leafIdx > startIdx && FrameNames.IsFolded(ShortOf(frames[leafIdx]), fold))
            {
                leafIdx--;
            }

            string leaf = ShortOf(frames[leafIdx]);
            selfTime.TryGetValue(leaf, out double current);
            selfTime[leaf] = current + sample.WeightMs;
        }

        return new RankingResult(total, rootFrame, RankRows(selfTime, total, top));
    }

    /// <summary>
    ///  Computes the inclusive-time ranking, crediting each distinct non-folded
    ///  frame on a stack once per sample.
    /// </summary>
    /// <param name="rootFrame">Substring scoping the ranking to a subtree, or empty for the whole trace.</param>
    /// <param name="foldPatterns">Frame fold patterns.</param>
    /// <param name="top">Maximum number of rows to return.</param>
    /// <returns>The inclusive-time ranking.</returns>
    public RankingResult InclusiveTime(string rootFrame, IReadOnlyList<string> foldPatterns, int top)
    {
        Regex[] fold = FrameNames.CompileFoldPatterns(foldPatterns);
        Dictionary<string, double> inclTime = new(StringComparer.Ordinal);
        HashSet<string> seen = new(StringComparer.Ordinal);
        double total = 0.0;

        foreach (SampleStack sample in _samples)
        {
            IReadOnlyList<string> frames = sample.Frames;
            int startIdx = ResolveStart(frames, rootFrame, out bool include);
            if (!include || frames.Count == 0)
            {
                continue;
            }

            total += sample.WeightMs;
            seen.Clear();

            for (int fi = startIdx; fi < frames.Count; fi++)
            {
                string name = ShortOf(frames[fi]);
                if (FrameNames.IsFolded(name, fold))
                {
                    continue;
                }

                if (seen.Add(name))
                {
                    inclTime.TryGetValue(name, out double current);
                    inclTime[name] = current + sample.WeightMs;
                }
            }
        }

        return new RankingResult(total, rootFrame, RankRows(inclTime, total, top));
    }

    /// <summary>
    ///  Reports the immediate callers of the topmost frame matching
    ///  <paramref name="focus"/>, with the time each caller contributes.
    /// </summary>
    /// <param name="focus">Substring identifying the focus frame.</param>
    /// <param name="rootFrame">Substring scoping the analysis to a subtree, or empty for the whole trace.</param>
    /// <param name="top">Maximum number of caller rows to return.</param>
    /// <returns>The caller breakdown.</returns>
    public CallersResult CallersOf(string focus, string rootFrame, int top)
    {
        Dictionary<string, double> callerTime = new(StringComparer.Ordinal);
        double targetTotal = 0.0;
        double total = 0.0;

        foreach (SampleStack sample in _samples)
        {
            IReadOnlyList<string> frames = sample.Frames;
            int startIdx = ResolveStart(frames, rootFrame, out bool include);
            if (!include || frames.Count == 0)
            {
                continue;
            }

            total += sample.WeightMs;

            for (int si = frames.Count - 1; si >= startIdx; si--)
            {
                string name = ShortOf(frames[si]);
                if (!name.Contains(focus, StringComparison.Ordinal))
                {
                    continue;
                }

                targetTotal += sample.WeightMs;
                string caller = si > startIdx ? ShortOf(frames[si - 1]) : "<root>";
                callerTime.TryGetValue(caller, out double current);
                callerTime[caller] = current + sample.WeightMs;
                break;
            }
        }

        List<CallerRow> rows = [];
        foreach (KeyValuePair<string, double> pair in callerTime)
        {
            double pct = targetTotal > 0 ? 100.0 * pair.Value / targetTotal : 0.0;
            rows.Add(new CallerRow(pair.Key, pair.Value, pct));
        }

        rows.Sort(static (a, b) => b.Milliseconds.CompareTo(a.Milliseconds));
        if (rows.Count > top)
        {
            rows.RemoveRange(top, rows.Count - top);
        }

        double pctOfScope = total > 0 ? 100.0 * targetTotal / total : 0.0;
        return new CallersResult(focus, targetTotal, pctOfScope, total, rows);
    }

    /// <summary>
    ///  Computes the line-level self-time ranking: each leaf sample (after
    ///  folding JIT-helper leaves into their caller) is attributed to the source
    ///  line that was executing, scoped to the methods whose shortened name
    ///  contains <paramref name="methodFilter"/>.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Only samples carrying per-frame source locations contribute; speedscope
    ///   inputs have none, so this ranking is meaningful only for
    ///   <c>.nettrace</c> and <c>.etl</c> traces read with local PDBs present.
    ///  </para>
    /// </remarks>
    /// <param name="methodFilter">Substring scoping to matching methods, or empty for every method.</param>
    /// <param name="foldPatterns">Leaf-frame fold patterns.</param>
    /// <param name="top">Maximum number of rows to return.</param>
    /// <returns>The line-level self-time ranking.</returns>
    public LineRankingResult HotLines(string methodFilter, IReadOnlyList<string> foldPatterns, int top)
    {
        Regex[] fold = FrameNames.CompileFoldPatterns(foldPatterns);
        Dictionary<string, (double Ms, string Method, string Location)> lineTime = new(StringComparer.Ordinal);
        double total = 0.0;

        foreach (SampleStack sample in _samples)
        {
            IReadOnlyList<string>? locations = sample.FrameLocations;
            IReadOnlyList<string> frames = sample.Frames;
            if (locations is null || frames.Count == 0)
            {
                continue;
            }

            int leafIdx = frames.Count - 1;
            while (leafIdx > 0 && FrameNames.IsFolded(ShortOf(frames[leafIdx]), fold))
            {
                leafIdx--;
            }

            string method = ShortOf(frames[leafIdx]);
            if (methodFilter.Length > 0 && !method.Contains(methodFilter, StringComparison.Ordinal))
            {
                continue;
            }

            total += sample.WeightMs;

            string location = leafIdx < locations.Count && locations[leafIdx].Length > 0
                ? locations[leafIdx]
                : "<no source>";

            string key = $"{method}\u0000{location}";
            lineTime.TryGetValue(key, out (double Ms, string Method, string Location) current);
            lineTime[key] = (current.Ms + sample.WeightMs, method, location);
        }

        List<LineRow> rows = [];
        foreach (KeyValuePair<string, (double Ms, string Method, string Location)> pair in lineTime)
        {
            double pct = total > 0 ? 100.0 * pair.Value.Ms / total : 0.0;
            rows.Add(new LineRow(pair.Value.Method, pair.Value.Location, pair.Value.Ms, pct));
        }

        rows.Sort(static (a, b) => b.Milliseconds.CompareTo(a.Milliseconds));
        if (rows.Count > top)
        {
            rows.RemoveRange(top, rows.Count - top);
        }

        return new LineRankingResult(total, methodFilter, rows);
    }

    private static List<RankRow> RankRows(Dictionary<string, double> times, double total, int top)
    {
        List<RankRow> rows = [];
        foreach (KeyValuePair<string, double> pair in times)
        {
            double pct = total > 0 ? 100.0 * pair.Value / total : 0.0;
            rows.Add(new RankRow(pair.Key, pair.Value, pct));
        }

        rows.Sort(static (a, b) => b.Milliseconds.CompareTo(a.Milliseconds));
        if (rows.Count > top)
        {
            rows.RemoveRange(top, rows.Count - top);
        }

        return rows;
    }
}
