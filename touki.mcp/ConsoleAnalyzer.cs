// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Mcp.Tracing;

namespace Touki.Mcp;

/// <summary>
///  A minimal command-line front end mirroring <c>tools/Get-TraceHotspots.ps1</c>,
///  for smoke-testing the readers and aggregator without an MCP client.
/// </summary>
/// <remarks>
///  <para>
///   Usage: <c>touki.mcp analyze &lt;trace&gt; [--root &lt;substr&gt;] [--callers &lt;substr&gt;] [--lines [&lt;methodSubstr&gt;]] [--heatmap &lt;sourceFile&gt;] [--symbols &lt;dir&gt;] [--top N]</c>.
///  </para>
/// </remarks>
internal static class ConsoleAnalyzer
{
    /// <summary>
    ///  Runs the console analyzer.
    /// </summary>
    /// <param name="args">Arguments after the <c>analyze</c> verb.</param>
    /// <returns>A process exit code.</returns>
    public static int Run(ReadOnlySpan<string> args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: touki.mcp analyze <trace> [--root <substr>] [--callers <substr>] [--lines [<methodSubstr>]] [--heatmap <sourceFile>] [--symbols <dir>] [--top N]");
            return 1;
        }

        string path = args[0];
        string root = "";
        string callers = "";
        string lines = "";
        string heatmap = "";
        string symbols = "";
        bool linesRequested = false;
        int top = 25;

        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--root":
                    if (i + 1 < args.Length)
                    {
                        root = args[++i];
                    }

                    break;
                case "--callers":
                    if (i + 1 < args.Length)
                    {
                        callers = args[++i];
                    }

                    break;
                case "--lines":
                    linesRequested = true;
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                    {
                        lines = args[++i];
                    }

                    break;
                case "--heatmap":
                    if (i + 1 < args.Length)
                    {
                        heatmap = args[++i];
                    }

                    break;
                case "--symbols":
                    if (i + 1 < args.Length)
                    {
                        symbols = args[++i];
                    }

                    break;
                case "--top":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out int parsedTop))
                    {
                        top = parsedTop;
                    }

                    break;
            }
        }

        LoadedTrace trace;
        try
        {
            trace = new TraceLoader().Load(path, symbols.Length > 0 ? symbols : null);
        }
        catch (Exception ex) when (ex is FileNotFoundException or NotSupportedException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        TraceInfo info = trace.Info;
        Console.WriteLine($"Format: {info.Format}  Samples: {info.SampleCount}  Duration: {info.DurationMs:N1} ms  Symbols: {info.SymbolResolutionRate:P0}");
        foreach (string warning in info.Warnings)
        {
            Console.WriteLine($"  ! {warning}");
        }

        if (callers.Length > 0)
        {
            CallersResult result = trace.Aggregator.CallersOf(callers, root, top);
            Console.WriteLine($"\nCALLERS OF '{callers}' ({result.TargetMilliseconds:N1} ms, {result.PercentOfScope:N1}% of scope)");
            foreach (CallerRow row in result.Callers)
            {
                Console.WriteLine($"  {row.Milliseconds,9:N1} ms  {row.PercentOfTarget,5:N1}%  {row.Caller}");
            }

            return 0;
        }

        if (linesRequested)
        {
            LineRankingResult result = trace.Aggregator.HotLines(lines, FrameNames.DefaultFoldPatterns, top);
            string scope = result.MethodFilter.Length > 0 ? $"method '{result.MethodFilter}'" : "all methods";
            Console.WriteLine($"\nHOT LINES ({scope}, scope {result.ScopeMilliseconds:N1} ms)");
            foreach (LineRow row in result.Rows)
            {
                Console.WriteLine($"  {row.Milliseconds,9:N1} ms  {row.PercentOfScope,5:N1}%  {row.Location}  {row.Method}");
            }

            return 0;
        }

        if (heatmap.Length > 0)
        {
            string fileName = Path.GetFileName(heatmap);
            SourceHeatmapResult result = trace.Aggregator.SourceHeatmap(fileName, FrameNames.DefaultFoldPatterns);
            Console.WriteLine(
                $"\nSOURCE HEATMAP '{result.File}' ({result.FileMilliseconds:N1} ms, "
                + $"{(result.ScopeMilliseconds > 0 ? 100.0 * result.FileMilliseconds / result.ScopeMilliseconds : 0.0):N1}% of trace)");

            if (result.Lines.Count == 0)
            {
                Console.WriteLine($"  No samples attributed to '{fileName}'. Check the trace has PDBs (--symbols) and the file name.");
                return 0;
            }

            if (SourceAnnotator.TryReadSourceLines(heatmap, out string[] sourceLines))
            {
                Console.WriteLine();
                Console.Write(SourceAnnotator.Render(sourceLines, result.Lines, result.FileMilliseconds));
            }
            else
            {
                Console.WriteLine("  (pass the full on-disk path to render annotated source; showing line data only)");
                foreach (HeatLine row in result.Lines)
                {
                    Console.WriteLine($"  {row.Milliseconds,9:N1} ms  {row.PercentOfScope,5:N1}%  line {row.Line,5}  {row.Method}");
                }
            }

            return 0;
        }

        PrintRanking("TOP SELF-TIME (helpers folded into caller)", trace.Aggregator.SelfTime(root, FrameNames.DefaultFoldPatterns, top));
        PrintRanking("TOP INCLUSIVE-TIME", trace.Aggregator.InclusiveTime(root, FrameNames.DefaultFoldPatterns, top));
        return 0;
    }

    private static void PrintRanking(string title, RankingResult result)
    {
        Console.WriteLine($"\n===== {title} (scope {result.ScopeMilliseconds:N1} ms) =====");
        foreach (RankRow row in result.Rows)
        {
            Console.WriteLine($"  {row.Milliseconds,9:N1} ms  {row.PercentOfScope,5:N1}%  {row.Frame}");
        }
    }
}
