// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

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
///  <para>
///   Serialization goes through the <see cref="TraceQJsonContext"/> source-generated
///   metadata rather than reflection, so the published heads stay Native-AOT- and
///   trim-safe. The shared options seed from that context (inheriting the camel-case
///   naming and the type-info resolver) and layer on the relaxed encoder and the
///   double-rounding converter that the wire format requires.
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
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">
    ///  <typeparamref name="T"/> has no entry in <see cref="TraceQJsonContext"/>; add a
    ///  <c>[JsonSerializable(typeof(AnalysisResult&lt;T&gt;))]</c> declaration there.
    /// </exception>
    public static string Serialize<T>(AnalysisResult<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        // Resolve the source-generated metadata for this closed generic and serialize
        // through the JsonTypeInfo overload - the reflection-based Serialize overload is
        // neither AOT- nor trim-safe.
        JsonTypeInfo<AnalysisResult<T>> typeInfo =
            (JsonTypeInfo<AnalysisResult<T>>)s_options.GetTypeInfo(typeof(AnalysisResult<T>));
        return JsonSerializer.Serialize(result, typeInfo);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        // Seed from the source-gen context so the camel-case naming policy and the
        // type-info resolver carry over, then layer on the write-time concerns the
        // context attribute cannot express: the relaxed encoder and the rounding
        // converter.
        JsonSerializerOptions options = new(TraceQJsonContext.Default.Options)
        {
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
