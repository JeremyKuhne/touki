// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace TraceQ.Tracing;

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
    private readonly StackSampleSource _source;
    private readonly IReadOnlyList<SampleStack> _samples;

    // A single FoldingAggregator is cached on LoadedTrace and can be queried
    // concurrently through the singleton TraceStore, so the short-name cache must
    // be safe for parallel readers and writers.
    private readonly ConcurrentDictionary<string, string> _shortCache = new(StringComparer.Ordinal);

    /// <summary>
    ///  Initializes a new <see cref="FoldingAggregator"/> over the given source.
    /// </summary>
    /// <param name="source">The stack-sample source to rank.</param>
    public FoldingAggregator(StackSampleSource source)
    {
        _source = source;
        _samples = source.Samples;
    }

    /// <summary>
    ///  The metric the ranked sample weights are measured in.
    /// </summary>
    public MetricInfo Metric => _source.Metric;

    private string ShortOf(string name) => _shortCache.GetOrAdd(name, static n => FrameNames.Short(n));

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

            total += sample.Weight;

            int leafIdx = frames.Count - 1;
            while (leafIdx > startIdx && FrameNames.IsFolded(ShortOf(frames[leafIdx]), fold))
            {
                leafIdx--;
            }

            string leaf = ShortOf(frames[leafIdx]);
            selfTime.TryGetValue(leaf, out double current);
            selfTime[leaf] = current + sample.Weight;
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

            total += sample.Weight;
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
                    inclTime[name] = current + sample.Weight;
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

            total += sample.Weight;

            for (int si = frames.Count - 1; si >= startIdx; si--)
            {
                string name = ShortOf(frames[si]);
                if (!name.Contains(focus, StringComparison.Ordinal))
                {
                    continue;
                }

                targetTotal += sample.Weight;
                string caller = si > startIdx ? ShortOf(frames[si - 1]) : "<root>";
                callerTime.TryGetValue(caller, out double current);
                callerTime[caller] = current + sample.Weight;
                break;
            }
        }

        List<CallerRow> rows = [];
        foreach (KeyValuePair<string, double> pair in callerTime)
        {
            double pct = targetTotal > 0 ? 100.0 * pair.Value / targetTotal : 0.0;
            rows.Add(new CallerRow(pair.Key, pair.Value, pct));
        }

        // Break ties by caller name so the ordering is deterministic across runs and machines.
        rows.Sort(static (a, b) =>
        {
            int byWeight = b.Weight.CompareTo(a.Weight);
            return byWeight != 0 ? byWeight : string.CompareOrdinal(a.Caller, b.Caller);
        });
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

            total += sample.Weight;

            string location = leafIdx < locations.Count && locations[leafIdx].Length > 0
                ? locations[leafIdx]
                : "<no source>";

            string key = $"{method}\u0000{location}";
            lineTime.TryGetValue(key, out (double Ms, string Method, string Location) current);
            lineTime[key] = (current.Ms + sample.Weight, method, location);
        }

        List<LineRow> rows = [];
        foreach (KeyValuePair<string, (double Ms, string Method, string Location)> pair in lineTime)
        {
            double pct = total > 0 ? 100.0 * pair.Value.Ms / total : 0.0;
            rows.Add(new LineRow(pair.Value.Method, pair.Value.Location, pair.Value.Ms, pct));
        }

        // Break ties by source location, then method, so the ordering is fully
        // deterministic even when two methods map to the same file:line with equal time.
        rows.Sort(static (a, b) =>
        {
            int byWeight = b.Weight.CompareTo(a.Weight);
            if (byWeight != 0)
            {
                return byWeight;
            }

            int byLocation = string.CompareOrdinal(a.Location, b.Location);
            return byLocation != 0 ? byLocation : string.CompareOrdinal(a.Method, b.Method);
        });
        if (rows.Count > top)
        {
            rows.RemoveRange(top, rows.Count - top);
        }

        return new LineRankingResult(total, methodFilter, rows);
    }

    /// <summary>
    ///  Computes a per-line self-time heat map for a single source file: each leaf
    ///  sample (after folding JIT-helper leaves into their caller) whose executing
    ///  source line belongs to <paramref name="fileName"/> is bucketed by line
    ///  number, ordered by line for overlaying onto the source.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Matching is by file name only (the trace records the build-time file name,
    ///   not its full path), so two source files that share a name are merged. The
    ///   percent on each line is the share of whole-trace time, making absolute
    ///   hotness comparable across files.
    ///  </para>
    /// </remarks>
    /// <param name="fileName">The source file name to build the heat map for (no directory).</param>
    /// <param name="foldPatterns">Leaf-frame fold patterns.</param>
    /// <returns>The per-line heat map, ordered by line number.</returns>
    public SourceHeatmapResult SourceHeatmap(string fileName, IReadOnlyList<string> foldPatterns)
    {
        Regex[] fold = FrameNames.CompileFoldPatterns(foldPatterns);
        Dictionary<int, LineAccumulator> lines = new();
        double traceTotal = 0.0;
        double fileTotal = 0.0;
        string matchedFile = fileName;

        foreach (SampleStack sample in _samples)
        {
            IReadOnlyList<string> frames = sample.Frames;
            if (frames.Count == 0)
            {
                continue;
            }

            traceTotal += sample.Weight;

            IReadOnlyList<string>? locations = sample.FrameLocations;
            if (locations is null)
            {
                continue;
            }

            int leafIdx = frames.Count - 1;
            while (leafIdx > 0 && FrameNames.IsFolded(ShortOf(frames[leafIdx]), fold))
            {
                leafIdx--;
            }

            if (leafIdx >= locations.Count
                || !TrySplitLocation(locations[leafIdx], out string leafFile, out int line)
                || !string.Equals(leafFile, fileName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Preserve the file name's casing as recorded in the trace.
            matchedFile = leafFile;
            fileTotal += sample.Weight;

            if (!lines.TryGetValue(line, out LineAccumulator? accumulator))
            {
                accumulator = new LineAccumulator();
                lines[line] = accumulator;
            }

            accumulator.Add(sample.Weight, ShortOf(frames[leafIdx]));
        }

        List<HeatLine> rows = new(lines.Count);
        foreach (KeyValuePair<int, LineAccumulator> pair in lines)
        {
            LineAccumulator accumulator = pair.Value;
            double pct = traceTotal > 0 ? 100.0 * accumulator.Weight / traceTotal : 0.0;
            rows.Add(new HeatLine(pair.Key, accumulator.Weight, pct, accumulator.SampleCount, accumulator.DominantMethod));
        }

        rows.Sort(static (a, b) => a.Line.CompareTo(b.Line));
        return new SourceHeatmapResult(traceTotal, matchedFile, fileTotal, rows);
    }

    /// <summary>
    ///  Splits a <c>file:line</c> location into its file name and line number.
    ///  Returns <see langword="false"/> for empty, unresolved (<c>&lt;no source&gt;</c>)
    ///  or otherwise malformed locations.
    /// </summary>
    private static bool TrySplitLocation(string location, out string file, out int line)
    {
        file = "";
        line = 0;
        if (location.Length == 0)
        {
            return false;
        }

        int colon = location.LastIndexOf(':');
        if (colon <= 0 || colon == location.Length - 1)
        {
            return false;
        }

        if (!int.TryParse(location.AsSpan(colon + 1), out line))
        {
            return false;
        }

        // Line numbers are 1-based; reject zero and negative values that cannot map onto source.
        if (line < 1)
        {
            line = 0;
            return false;
        }

        file = location[..colon];
        return true;
    }

    private static List<RankRow> RankRows(Dictionary<string, double> times, double total, int top)
    {
        List<RankRow> rows = [];
        foreach (KeyValuePair<string, double> pair in times)
        {
            double pct = total > 0 ? 100.0 * pair.Value / total : 0.0;
            rows.Add(new RankRow(pair.Key, pair.Value, pct));
        }

        // Break ties by frame name so the ordering is deterministic across runs and machines.
        rows.Sort(static (a, b) =>
        {
            int byWeight = b.Weight.CompareTo(a.Weight);
            return byWeight != 0 ? byWeight : string.CompareOrdinal(a.Frame, b.Frame);
        });
        if (rows.Count > top)
        {
            rows.RemoveRange(top, rows.Count - top);
        }

        return rows;
    }

    /// <summary>
    ///  Accumulates the self-weight and sample count attributed to one source line,
    ///  tracking which method dominates the line's weight.
    /// </summary>
    private sealed class LineAccumulator
    {
        private readonly Dictionary<string, double> _methods = new(StringComparer.Ordinal);

        public double Weight { get; private set; }

        public int SampleCount { get; private set; }

        public void Add(double weight, string method)
        {
            Weight += weight;
            SampleCount++;
            _methods.TryGetValue(method, out double current);
            _methods[method] = current + weight;
        }

        public string DominantMethod
        {
            get
            {
                string dominant = "";
                double dominantMs = -1.0;
                foreach (KeyValuePair<string, double> pair in _methods)
                {
                    if (pair.Value > dominantMs)
                    {
                        dominantMs = pair.Value;
                        dominant = pair.Key;
                    }
                }

                return dominant;
            }
        }
    }
}
