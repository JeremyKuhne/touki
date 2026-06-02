// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.CodeDom.Compiler;
using System.Globalization;
using System.Text;
using Touki.Text;

namespace Touki;

public class ValueStringBuilderCopyToTests
{
    // ---------- CopyTo(Stream) ----------

    [Test]
    public void CopyTo_Stream_PopulatedBuilder_WithStackBuffer_WritesBytes()
    {
        using MemoryStream stream = new();
        using ValueStringBuilder builder = new(stackalloc char[16]);
        builder.Append("hi!");
        builder.CopyTo(stream);

        // The bytes are the UTF-16 representation of the characters.
        byte[] expected = MemoryMarshal.AsBytes("hi!".AsSpan()).ToArray();
        stream.ToArray().Should().Equal(expected);
    }

    [Test]
    public void CopyTo_Stream_PopulatedBuilder_WithRentedBuffer_WritesBytes()
    {
        using MemoryStream stream = new();
        // Start with an initial capacity that forces a rented array up front so
        // the net472 `_arrayToReturnToPool is not null` path is exercised.
        using ValueStringBuilder builder = new(initialCapacity: 32);
        builder.Append("hello world");
        builder.CopyTo(stream);

        byte[] expected = MemoryMarshal.AsBytes("hello world".AsSpan()).ToArray();
        stream.ToArray().Should().Equal(expected);
    }

    // ---------- CopyTo(TextWriter) ----------

    [Test]
    public void CopyTo_TextWriter_StringWriter_AppendsContent()
    {
        System.IO.StringWriter writer = new();
        using ValueStringBuilder builder = new(stackalloc char[16]);
        builder.Append("first-second");
        builder.CopyTo(writer);

        writer.ToString().Should().Be("first-second");
    }

    [Test]
    public void CopyTo_TextWriter_StringWriterWithExistingContent_Appends()
    {
        System.IO.StringWriter writer = new();
        writer.Write("prefix-");
        using ValueStringBuilder builder = new(stackalloc char[16]);
        builder.Append("body");
        builder.CopyTo(writer);

        writer.ToString().Should().Be("prefix-body");
    }

    [Test]
    public void CopyTo_TextWriter_IndentedTextWriter_ForwardsContent()
    {
        System.IO.StringWriter inner = new();
        IndentedTextWriter indented = new(inner, "  ");
        using ValueStringBuilder builder = new(stackalloc char[16]);
        builder.Append("indented");
        builder.CopyTo(indented);
        indented.Flush();

        // We don't care about the exact indent prefix (it differs between
        // TFMs); we just want to confirm the content reached the inner writer.
        inner.ToString().Should().EndWith("indented");
    }

    [Test]
    public void CopyTo_TextWriter_StreamWriter_WritesUtf8()
    {
        using MemoryStream stream = new();
        using System.IO.StreamWriter writer = new(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        using (ValueStringBuilder builder = new(initialCapacity: 16))
        {
            builder.Append("stream!");
            builder.CopyTo(writer);
        }

        writer.Flush();
        byte[] bytes = stream.ToArray();
        Encoding.UTF8.GetString(bytes).Should().Be("stream!");
    }

    [Test]
    public void CopyTo_TextWriter_DerivedStreamWriter_UsesGenericFallback()
    {
        // A subclass of StreamWriter does NOT match `writer.GetType() == typeof(StreamWriter)`,
        // so on net472 the builder takes the generic `writer.Write(AsSpan())` path.
        using MemoryStream stream = new();
        using DerivedStreamWriter writer = new(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        using (ValueStringBuilder builder = new(stackalloc char[16]))
        {
            builder.Append("derived");
            builder.CopyTo(writer);
        }

        writer.Flush();
        byte[] bytes = stream.ToArray();
        Encoding.UTF8.GetString(bytes).Should().Be("derived");
    }

    [Test]
    public void CopyTo_TextWriter_CustomTextWriter_WritesContent()
    {
        RecordingTextWriter writer = new();
        using (ValueStringBuilder builder = new(stackalloc char[16]))
        {
            builder.Append("custom!");
            builder.CopyTo(writer);
        }

        writer.Captured.Should().Be("custom!");
    }

    // ---------- Constructor (literalLength, formattedCount, provider) ----------

    [Test]
    public void Constructor_LiteralLengthFormattedCount_Provider_UsesProvider()
    {
        // Use a culture whose number format differs from invariant so we can
        // confirm the provider was actually wired up.
        CultureInfo provider = CultureInfo.GetCultureInfo("de-DE");
        using ValueStringBuilder builder = new(literalLength: 0, formattedCount: 1, provider: provider);
        builder.AppendFormatted(1234.5, "N1");

        builder.ToString().Should().Be("1.234,5");
    }

    [Test]
    public void Constructor_LiteralLengthFormattedCount_NullProvider_UsesCurrent()
    {
        using ValueStringBuilder builder = new(literalLength: 0, formattedCount: 1, provider: null);
        builder.AppendFormatted(1234.5, "N1");

        builder.ToString().Should().Be(1234.5.ToString("N1", CultureInfo.CurrentCulture));
    }

    // ---------- Test helpers ----------

#if NETFRAMEWORK
    // ---------- FormatterHelper<T> (net472 only) ----------
    //
    // On modern .NET, AppendFormattedSlow uses a constrained call against
    // ISpanFormattable directly. On net472 it routes value-type
    // ISpanFormattable implementations through FormatterHelper<T> to avoid
    // boxing. These tests exercise that path and the DoubleRemaining grow
    // loop it sits inside.

    [Test]
    public void AppendFormatted_CustomSpanFormattableStruct_AppendsFormattedValue()
    {
        using ValueStringBuilder builder = new(stackalloc char[32]);
        builder.AppendFormatted(new FixedSpanFormattable("hello"), default(StringSpan));

        builder.ToString().Should().Be("hello");
    }

#if !DEBUG
    // Allocation assertion only runs in Release. Debug-mode net481 introduces
    // JIT-level allocations (no inlining, no enregistration) that aren't
    // representative of production behavior and that the test cannot
    // control. MemoryWatch is only meaningful when the JIT optimizations
    // it's measuring against are actually in effect.
    [Test]
    public void AppendFormatted_CustomSpanFormattableStruct_DoesNotAllocate()
    {
        // Warm up the FormatterHelper<T> static (delegate creation
        // allocates the first time the generic is touched).
        using (ValueStringBuilder warmup = new(stackalloc char[32]))
        {
            warmup.AppendFormatted(new FixedSpanFormattable("warm"), default(StringSpan));
        }

        ValueStringBuilder builder = new(stackalloc char[32]);
        try
        {
            using (MemoryWatch.Create)
            {
                builder.AppendFormatted(new FixedSpanFormattable("zero-alloc"), default(StringSpan));
            }
        }
        finally
        {
            builder.Dispose();
        }
    }
#endif

    [Test]
    public void AppendFormatted_CustomSpanFormattableStruct_GrowsToFit()
    {
        // Start with a tiny buffer so the formatter's first TryFormat call
        // returns false and DoubleRemaining is invoked at least once.
        using ValueStringBuilder builder = new(stackalloc char[4]);
        builder.AppendFormatted(new FixedSpanFormattable("0123456789ABCDEF"), default(StringSpan));

        builder.ToString().Should().Be("0123456789ABCDEF");
    }

    private readonly struct FixedSpanFormattable : ISpanFormattable
    {
        private readonly string _value;

        public FixedSpanFormattable(string value) => _value = value;

        public string ToString(string? format, IFormatProvider? formatProvider) => _value;

        public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        {
            if (destination.Length < _value.Length)
            {
                charsWritten = 0;
                return false;
            }

            _value.AsSpan().CopyTo(destination);
            charsWritten = _value.Length;
            return true;
        }
    }
#endif

    private sealed class DerivedStreamWriter : System.IO.StreamWriter
    {
        public DerivedStreamWriter(System.IO.Stream stream, Encoding encoding) : base(stream, encoding) { }
    }

    private sealed class RecordingTextWriter : System.IO.TextWriter
    {
        private readonly StringBuilder _captured = new();

        public string Captured => _captured.ToString();

        public override Encoding Encoding => Encoding.Unicode;

        public override void Write(char value) => _captured.Append(value);

        public override void Write(char[] buffer, int index, int count) =>
            _captured.Append(buffer, index, count);

        public override void Write(string? value)
        {
            if (value is not null)
            {
                _captured.Append(value);
            }
        }
    }
}
