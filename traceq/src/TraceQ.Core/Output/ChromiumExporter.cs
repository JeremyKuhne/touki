// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Text.Encodings.Web;
using System.Text.Json;

namespace TraceQ.Output;

/// <summary>
///  Exports a <see cref="Tracing.StackSampleSource"/> to the Chrome
///  <see href="https://docs.google.com/document/d/1CvAClvFfyA5R-PhYUmn5OOQtYMH4h6I0nSsKchNAySU">Trace
///  Event Format</see>, so any provider's stacks open as a flame graph in
///  <c>chrome://tracing</c> or the Perfetto UI with no PerfView dependency.
/// </summary>
/// <remarks>
///  <para>
///   The Chrome format is evented (begin <c>B</c> / end <c>E</c> pairs on a time
///   axis), so this reconstructs a synthetic timeline from the normalized
///   <see cref="Tracing.SampleStack"/> samples - the inverse of what the
///   speedscope reader does. Walking the samples in order, it advances a cursor
///   by each sample's weight and emits the begin / end events needed to morph the
///   previously open stack into the current one, so the rendered flame graph's
///   widths are proportional to weight.
///  </para>
///  <para>
///   The time axis carries the source metric's magnitude: microseconds for CPU
///   (a sample's millisecond weight scaled to the format's microsecond unit),
///   or the raw byte count for allocation - the viewer renders proportional
///   widths either way. A single aggregate process / thread is emitted, matching
///   the engine's cross-thread rankings.
///  </para>
/// </remarks>
internal static class ChromiumExporter
{
    // The format's timestamps are microseconds; scale a CPU millisecond weight up
    // so the rendered durations read naturally. Other metrics (bytes) are used as-is.
    private const double MillisecondsToMicroseconds = 1000.0;

    private static readonly JsonWriterOptions s_writerOptions = new()
    {
        Indented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    ///  Serializes <paramref name="source"/> to a Chrome Trace Event Format JSON string.
    /// </summary>
    /// <param name="source">The stack-sample source to export.</param>
    /// <param name="name">The thread name shown in the viewer.</param>
    /// <returns>The Chrome trace JSON.</returns>
    public static string Export(Tracing.StackSampleSource source, string name = "traceq")
    {
        ArgumentNullException.ThrowIfNull(source);

        bool isMilliseconds = source.Metric.Unit == "ms";
        double scale = isMilliseconds ? MillisecondsToMicroseconds : 1.0;

        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, s_writerOptions))
        {
            writer.WriteStartObject();

            // The format's time axis is milliseconds; only label it as such when the
            // metric really is time. For other metrics (allocation bytes) the ts field
            // carries the metric magnitude, so a "ms" label would mislabel the axis.
            if (isMilliseconds)
            {
                writer.WriteString("displayTimeUnit", "ms");
            }

            writer.WriteStartArray("traceEvents");

            // Name the single aggregate thread via a metadata event.
            WriteThreadName(writer, name);

            List<string> open = [];
            double cursor = 0.0;

            foreach (Tracing.SampleStack sample in source.Samples)
            {
                IReadOnlyList<string> frames = sample.Frames;

                int common = CommonPrefix(open, frames);

                // Close the previously open frames that are not shared with this
                // sample, deepest first.
                for (int i = open.Count - 1; i >= common; i--)
                {
                    WriteDuration(writer, "E", null, cursor * scale);
                }

                // Open this sample's frames below the shared prefix, shallowest first.
                for (int i = common; i < frames.Count; i++)
                {
                    WriteDuration(writer, "B", frames[i], cursor * scale);
                }

                open.Clear();
                open.AddRange(frames);
                cursor += sample.Weight;
            }

            // Close whatever remains open at the final cursor.
            for (int i = open.Count - 1; i >= 0; i--)
            {
                WriteDuration(writer, "E", null, cursor * scale);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.GetBuffer(), 0, checked((int)stream.Length));
    }

    private static int CommonPrefix(List<string> open, IReadOnlyList<string> frames)
    {
        int limit = Math.Min(open.Count, frames.Count);
        int i = 0;
        while (i < limit && string.Equals(open[i], frames[i], StringComparison.Ordinal))
        {
            i++;
        }

        return i;
    }

    private static void WriteDuration(Utf8JsonWriter writer, string phase, string? name, double microseconds)
    {
        writer.WriteStartObject();
        writer.WriteString("ph", phase);
        if (name is not null)
        {
            writer.WriteString("name", name);
        }

        writer.WriteString("cat", "traceq");
        writer.WriteNumber("ts", microseconds);
        writer.WriteNumber("pid", 1);
        writer.WriteNumber("tid", 1);
        writer.WriteEndObject();
    }

    private static void WriteThreadName(Utf8JsonWriter writer, string name)
    {
        writer.WriteStartObject();
        writer.WriteString("ph", "M");
        writer.WriteString("name", "thread_name");
        writer.WriteNumber("pid", 1);
        writer.WriteNumber("tid", 1);
        writer.WriteStartObject("args");
        writer.WriteString("name", name);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }
}
