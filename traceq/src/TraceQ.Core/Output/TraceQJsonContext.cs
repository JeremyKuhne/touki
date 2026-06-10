// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Text.Json.Serialization;
using TraceQ.Tracing;
using TraceQ.Tracing.Providers;

namespace TraceQ.Output;

/// <summary>
///  The System.Text.Json source-generation context for every analysis payload the
///  CLI and MCP heads serialize through <see cref="OutputJson"/>.
/// </summary>
/// <remarks>
///  <para>
///   Native AOT is a goal for the published heads, and the reflection-based
///   <c>JsonSerializer.Serialize</c> overload is not AOT- or trim-safe. Declaring
///   each closed <see cref="AnalysisResult{T}"/> here makes the metadata available
///   ahead of time so <see cref="OutputJson"/> can serialize through a
///   <c>JsonTypeInfo</c> rather than reflecting at run time. The generator walks
///   each declared type transitively, so the nested payload records (ranking rows,
///   call-tree nodes, and so on) do not need their own entries.
///  </para>
///  <para>
///   <see cref="JsonSourceGenerationMode.Metadata"/> is used rather than the
///   serialization fast path because the output contract relies on a custom
///   double-rounding converter and a relaxed encoder configured on the runtime
///   options (see <see cref="OutputJson"/>); metadata mode resolves writing through
///   that converter chain, while the fast path would bypass it.
///  </para>
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Metadata,
    WriteIndented = false)]
[JsonSerializable(typeof(AnalysisResult<TraceInfoView>))]
[JsonSerializable(typeof(AnalysisResult<RankingResult>))]
[JsonSerializable(typeof(AnalysisResult<CallersResult>))]
[JsonSerializable(typeof(AnalysisResult<LineRankingResult>))]
[JsonSerializable(typeof(AnalysisResult<SourceHeatmapResult>))]
[JsonSerializable(typeof(AnalysisResult<CallTreeResult>))]
[JsonSerializable(typeof(AnalysisResult<RankingDiffResult>))]
[JsonSerializable(typeof(AnalysisResult<JitStatsResult>))]
[JsonSerializable(typeof(AnalysisResult<GcStatsResult>))]
[JsonSerializable(typeof(AnalysisResult<EventQueryResult>))]
[JsonSerializable(typeof(AnalysisResult<ExportResult>))]
internal sealed partial class TraceQJsonContext : JsonSerializerContext
{
}
