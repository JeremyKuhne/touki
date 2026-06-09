// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Output;
using TraceQ.Tracing;

namespace TraceQ.Cli;

/// <summary>
///  Renders a call tree as an indented text view a human reads at the terminal: a
///  one-line trace banner, then each node on its own line with its weight, share of
///  scope, and an indent reflecting its depth, deepest paths following their parent.
/// </summary>
internal static class TreeTextRenderer
{
    private const int WeightColumnWidth = 16;
    private const int PercentColumnWidth = 6;

    /// <summary>
    ///  Renders the call-tree envelope to <paramref name="output"/>.
    /// </summary>
    /// <param name="envelope">The call-tree result, with its warnings.</param>
    /// <param name="info">The loaded trace's metadata, for the banner line.</param>
    /// <param name="metric">The metric the weights are measured in.</param>
    /// <param name="output">The writer the text is rendered to.</param>
    public static void Render(
        AnalysisResult<CallTreeResult> envelope,
        TraceInfo info,
        MetricInfo metric,
        TextWriter output)
    {
        CallTreeResult tree = envelope.Result;
        string unit = metric.Unit;
        string scope = tree.RootFrame.Length > 0 ? $"scoped to '{tree.RootFrame}'" : "whole trace";

        output.WriteLine(
            $"{info.Format}  {info.SampleCount} samples  {info.TotalWeight:N1} {unit}  symbols {info.SymbolResolutionRate:P0}");
        output.WriteLine();
        output.WriteLine($"{metric.Name} call tree  -  scope {tree.ScopeWeight:N2} {unit}  ({scope})");
        output.WriteLine($"  {"weight",WeightColumnWidth}  {"%",PercentColumnWidth}  frame");

        RenderNode(tree.Root, unit, depth: 0, output);

        if (tree.Root.Children.Count == 0)
        {
            output.WriteLine("  (no frames in scope)");
        }

        foreach (string warning in envelope.Warnings)
        {
            output.WriteLine($"! {warning}");
        }
    }

    private static void RenderNode(TreeNode node, string unit, int depth, TextWriter output)
    {
        // Two spaces of indent per depth level; the synthetic root sits at depth 0.
        string indent = new(' ', depth * 2);
        output.WriteLine(
            $"  {$"{node.Weight:N2} {unit}",WeightColumnWidth}  {node.PercentOfScope,PercentColumnWidth:N2}  {indent}{node.Frame}");

        foreach (TreeNode child in node.Children)
        {
            RenderNode(child, unit, depth + 1, output);
        }
    }
}
