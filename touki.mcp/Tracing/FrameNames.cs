// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Text.RegularExpressions;

namespace Touki.Mcp.Tracing;

/// <summary>
///  Frame-name shortening and fold matching shared by every reader and the
///  aggregator. Ports the <c>Short</c> and <c>IsFolded</c> helpers from
///  <c>tools/Get-TraceHotspots.ps1</c> so the folding semantics stay identical
///  across input formats.
/// </summary>
internal static partial class FrameNames
{
    /// <summary>
    ///  The default set of leaf-frame fold patterns. A leaf frame whose shortened
    ///  name matches any of these is folded into its caller. Covers the synthetic
    ///  BenchmarkDotNet sample markers and the common JIT-helper thunks the
    ///  managed-only stack walker mis-attributes time to.
    /// </summary>
    public static IReadOnlyList<string> DefaultFoldPatterns { get; } =
    [
        "CPU_TIME",
        "UNMANAGED_CODE_TIME",
        "BulkMoveWithWriteBarrier",
        "PollGC",
        "Memmove",
        "WriteBarrier",
        "JIT_"
    ];

    [GeneratedRegex(@"!([^(]+)")]
    private static partial Regex AfterModuleRegex();

    /// <summary>
    ///  Trims a verbose CLR frame signature to a method identifier for ranking
    ///  and display: keeps the text after the <c>module!</c> prefix and before
    ///  the argument list, and strips <c>value class</c> / <c>class</c> noise.
    /// </summary>
    /// <param name="name">The full frame name.</param>
    /// <returns>The shortened identifier.</returns>
    public static string Short(string name)
    {
        Match match = AfterModuleRegex().Match(name);
        string result = match.Success ? match.Groups[1].Value : name;
        result = result.Replace("value class ", "").Replace("class ", "");
        return result;
    }

    /// <summary>
    ///  Compiles a list of fold patterns into regular expressions once so the
    ///  per-sample hot loop avoids repeated pattern parsing.
    /// </summary>
    /// <param name="patterns">The fold patterns.</param>
    /// <returns>The compiled matchers.</returns>
    public static Regex[] CompileFoldPatterns(IReadOnlyList<string> patterns)
    {
        Regex[] compiled = new Regex[patterns.Count];
        for (int i = 0; i < patterns.Count; i++)
        {
            compiled[i] = new Regex(patterns[i], RegexOptions.CultureInvariant);
        }

        return compiled;
    }

    /// <summary>
    ///  Determines whether a shortened frame name matches any compiled fold pattern.
    /// </summary>
    /// <param name="shortName">The shortened frame name.</param>
    /// <param name="foldPatterns">The compiled fold patterns.</param>
    /// <returns><see langword="true"/> if the frame should be folded into its caller.</returns>
    public static bool IsFolded(string shortName, Regex[] foldPatterns)
    {
        foreach (Regex pattern in foldPatterns)
        {
            if (pattern.IsMatch(shortName))
            {
                return true;
            }
        }

        return false;
    }
}
