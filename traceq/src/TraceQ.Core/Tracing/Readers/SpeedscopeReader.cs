// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Text.Json;

namespace TraceQ.Tracing.Readers;

/// <summary>
///  Reads a BenchmarkDotNet EventPipe speedscope export
///  (<c>.speedscope.json</c>) into weighted samples.
/// </summary>
/// <remarks>
///  <para>
///   The evented speedscope format records frame open (<c>O</c>) and close
///   (<c>C</c>) events with timestamps. The wall-clock delta between consecutive
///   events is attributed to whatever frames are open at that moment - exactly
///   the model the aggregator consumes. Each profile maps to one thread, and
///   frame names are already symbol-resolved.
///  </para>
/// </remarks>
internal sealed class SpeedscopeReader : ITraceReader
{
    /// <inheritdoc/>
    public TraceFormat Format => TraceFormat.Speedscope;

    /// <inheritdoc/>
    public bool CanRead(string path) =>
        path.EndsWith(".speedscope.json", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public TraceReadResult Read(string path, string? symbolsDirectory = null)
    {
        using FileStream stream = File.OpenRead(path);
        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement root = document.RootElement;

        string[] frameNames = ReadFrameNames(root);
        List<SampleStack> samples = [];

        if (root.TryGetProperty("profiles", out JsonElement profiles)
            && profiles.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement profile in profiles.EnumerateArray())
            {
                ReadProfile(profile, frameNames, samples);
            }
        }

        List<string> warnings = [];
        if (samples.Count == 0)
        {
            warnings.Add("No timed samples were found in the speedscope file.");
        }

        return new TraceReadResult(samples, 1.0, warnings);
    }

    private static string[] ReadFrameNames(JsonElement root)
    {
        if (!root.TryGetProperty("shared", out JsonElement shared)
            || !shared.TryGetProperty("frames", out JsonElement frames)
            || frames.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        string[] names = new string[frames.GetArrayLength()];
        int i = 0;
        foreach (JsonElement frame in frames.EnumerateArray())
        {
            names[i++] = frame.TryGetProperty("name", out JsonElement name)
                ? name.GetString() ?? ""
                : "";
        }

        return names;
    }

    private static void ReadProfile(JsonElement profile, string[] frameNames, List<SampleStack> samples)
    {
        if (!profile.TryGetProperty("events", out JsonElement events)
            || events.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        string thread = profile.TryGetProperty("name", out JsonElement profileName)
            ? profileName.GetString() ?? ""
            : "";

        List<int> stack = [];
        double? lastAt = null;

        foreach (JsonElement e in events.EnumerateArray())
        {
            double at = e.GetProperty("at").GetDouble();

            if (lastAt is double previous && stack.Count > 0)
            {
                double delta = at - previous;
                if (delta > 0)
                {
                    string[] frames = new string[stack.Count];
                    for (int i = 0; i < stack.Count; i++)
                    {
                        int index = stack[i];
                        frames[i] = (uint)index < (uint)frameNames.Length ? frameNames[index] : "?";
                    }

                    samples.Add(new SampleStack(frames, delta, thread));
                }
            }

            string type = e.GetProperty("type").GetString() ?? "";
            int frameIndex = e.GetProperty("frame").GetInt32();

            if (type == "O")
            {
                stack.Add(frameIndex);
            }
            else if (type == "C")
            {
                for (int k = stack.Count - 1; k >= 0; k--)
                {
                    if (stack[k] == frameIndex)
                    {
                        stack.RemoveAt(k);
                        break;
                    }
                }
            }

            lastAt = at;
        }
    }
}
