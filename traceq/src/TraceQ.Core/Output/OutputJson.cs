// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TraceQ.Output;

/// <summary>
///  Serializes an <see cref="AnalysisResult{T}"/> to the contract's compact,
///  deterministic JSON: no indentation, camel-cased names, and doubles rounded
///  to a fixed precision so the same trace produces byte-identical output on
///  every machine.
/// </summary>
/// <remarks>
///  <para>
///   Compact (single-line) output is the agent-facing wire format - it spends no
///   tokens on whitespace and, having no line breaks, compares cleanly in
///   cross-platform golden-file tests. Rounding the doubles keeps the output
///   stable and free of floating-point noise.
///  </para>
/// </remarks>
public static class OutputJson
{
    /// <summary>
    ///  The number of decimal places doubles are rounded to in the serialized output.
    /// </summary>
    public const int DoublePrecision = 2;

    private static readonly JsonSerializerOptions s_options = CreateOptions();

    /// <summary>
    ///  Serializes an analysis result to compact, deterministic JSON.
    /// </summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="result">The envelope to serialize.</param>
    /// <returns>The compact JSON representation.</returns>
    public static string Serialize<T>(AnalysisResult<T> result) =>
        JsonSerializer.Serialize(result, s_options);

    private static JsonSerializerOptions CreateOptions()
    {
        JsonSerializerOptions options = new()
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,

            // Frame names carry '<', '>', '&' (for example "<root>" and generic
            // arguments). This output is an agent / CLI wire format, never embedded
            // in HTML, so the relaxed encoder leaves those characters literal rather
            // than bloating them into "\u003C" escapes - more readable and fewer tokens.
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        options.Converters.Add(new RoundingDoubleConverter(DoublePrecision));
        return options;
    }

    /// <summary>
    ///  Writes doubles rounded to a fixed number of decimal places so serialized
    ///  rankings are deterministic and free of floating-point noise.
    /// </summary>
    private sealed class RoundingDoubleConverter : JsonConverter<double>
    {
        private readonly int _digits;

        public RoundingDoubleConverter(int digits) => _digits = digits;

        public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.GetDouble();

        public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options) =>
            writer.WriteNumberValue(Math.Round(value, _digits, MidpointRounding.AwayFromZero));
    }
}
