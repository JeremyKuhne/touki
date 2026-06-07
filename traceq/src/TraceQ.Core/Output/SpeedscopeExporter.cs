// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Text.Encodings.Web;
using System.Text.Json;

namespace TraceQ.Output;

/// <summary>
///  Exports a <see cref="Tracing.StackSampleSource"/> to the
///  <see href="https://www.speedscope.app">speedscope</see> "sampled" file
///  format, so any provider's stacks - CPU, allocation, or a later family - can
///  be opened as an interactive flame graph with no PerfView dependency.
/// </summary>
/// <remarks>
///  <para>
///   The sampled format is the natural representation of the normalized
///   <see cref="Tracing.SampleStack"/> model: each sample is a root-to-leaf list
///   of frame indices into a shared frame table, paired with its weight. The
///   profile's <c>unit</c> reflects the source <see cref="Tracing.MetricInfo"/>
///   (milliseconds for CPU, bytes for allocation), so an allocation export is a
///   byte-weighted flame graph rather than a mislabeled time one.
///  </para>
///  <para>
///   A single profile is emitted for the whole source rather than one per thread:
///   the engine's rankings aggregate across threads, so the exported flame graph
///   matches what <c>rank</c> and <c>callers</c> report. Output is compact and
///   deterministic (a stable frame table ordered by first appearance), so the
///   same trace exports byte-identically.
///  </para>
/// </remarks>
internal static class SpeedscopeExporter
{
    private const string SchemaUrl = "https://www.speedscope.app/file-format-schema.json";

    private static readonly JsonWriterOptions s_writerOptions = new()
    {
        Indented = false,
        // Frame names carry '<', '>', '&' (for example "<root>" and generic
        // arguments); the relaxed encoder keeps them literal in this non-HTML
        // wire format rather than expanding them to "\u003C".
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    ///  Serializes <paramref name="source"/> to a speedscope sampled-profile JSON string.
    /// </summary>
    /// <param name="source">The stack-sample source to export.</param>
    /// <param name="name">The profile name shown in speedscope.</param>
    /// <returns>The speedscope JSON.</returns>
    public static string Export(Tracing.StackSampleSource source, string name = "traceq")
    {
        ArgumentNullException.ThrowIfNull(source);

        // Build a shared frame table indexed by first appearance, and the per-sample
        // index arrays plus weights.
        Dictionary<string, int> frameIndices = new(StringComparer.Ordinal);
        List<string> frameNames = [];
        List<int[]> sampleStacks = new(source.Samples.Count);
        List<double> weights = new(source.Samples.Count);
        double total = 0.0;

        foreach (Tracing.SampleStack sample in source.Samples)
        {
            IReadOnlyList<string> frames = sample.Frames;
            int[] indices = new int[frames.Count];
            for (int i = 0; i < frames.Count; i++)
            {
                string frame = frames[i];
                if (!frameIndices.TryGetValue(frame, out int index))
                {
                    index = frameNames.Count;
                    frameIndices[frame] = index;
                    frameNames.Add(frame);
                }

                indices[i] = index;
            }

            sampleStacks.Add(indices);
            weights.Add(sample.Weight);
            total += sample.Weight;
        }

        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, s_writerOptions))
        {
            writer.WriteStartObject();
            writer.WriteString("$schema", SchemaUrl);

            writer.WriteStartObject("shared");
            writer.WriteStartArray("frames");
            foreach (string frame in frameNames)
            {
                writer.WriteStartObject();
                writer.WriteString("name", frame);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();

            writer.WriteStartArray("profiles");
            writer.WriteStartObject();
            writer.WriteString("type", "sampled");
            writer.WriteString("name", name);
            writer.WriteString("unit", SpeedscopeUnit(source.Metric.Unit));
            writer.WriteNumber("startValue", 0);
            writer.WriteNumber("endValue", total);

            writer.WriteStartArray("samples");
            foreach (int[] indices in sampleStacks)
            {
                writer.WriteStartArray();
                foreach (int index in indices)
                {
                    writer.WriteNumberValue(index);
                }

                writer.WriteEndArray();
            }

            writer.WriteEndArray();

            writer.WriteStartArray("weights");
            foreach (double weight in weights)
            {
                writer.WriteNumberValue(weight);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        // Decode straight from the stream's backing buffer rather than ToArray(),
        // which would copy the whole payload first; the stream is ours, so the
        // buffer is always exposable.
        return stream.TryGetBuffer(out ArraySegment<byte> buffer)
            ? System.Text.Encoding.UTF8.GetString(buffer.Array!, buffer.Offset, buffer.Count)
            : System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    // Maps a MetricInfo unit onto a speedscope value-unit. Speedscope understands a
    // fixed set; anything else falls back to "none" (an unlabeled magnitude).
    private static string SpeedscopeUnit(string metricUnit) => metricUnit switch
    {
        "ms" => "milliseconds",
        "bytes" => "bytes",
        _ => "none"
    };
}
