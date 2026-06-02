// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace System;

public class ConvertBase64PolyfillTests
{
    private static readonly byte[] s_sample = [0xFE, 0xED, 0xFA, 0xCE];
    private const string SampleBase64 = "/u36zg==";

    // ---- ToBase64String(ReadOnlySpan<byte>) ----

    [Test]
    public void ToBase64String_Empty_ReturnsEmpty()
    {
        Convert.ToBase64String([]).Should().BeEmpty();
    }

    [Test]
    public void ToBase64String_Sample_ReturnsExpected()
    {
        Convert.ToBase64String((ReadOnlySpan<byte>)s_sample).Should().Be(SampleBase64);
    }

    [Test]
    public void ToBase64String_LargeBuffer_RoundTrips()
    {
        byte[] buffer = new byte[1024];
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = (byte)(i & 0xFF);
        }

        string encoded = Convert.ToBase64String((ReadOnlySpan<byte>)buffer);
        byte[] decoded = Convert.FromBase64String(encoded);
        decoded.Should().Equal(buffer);
    }

    // ---- TryToBase64Chars ----

    [Test]
    public void TryToBase64Chars_DestinationLargeEnough_WritesAndReturnsTrue()
    {
        Span<char> dest = new char[16];
        bool ok = Convert.TryToBase64Chars(s_sample, dest, out int written);
        ok.Should().BeTrue();
        written.Should().Be(SampleBase64.Length);
        dest[..written].ToString().Should().Be(SampleBase64);
    }

    [Test]
    public void TryToBase64Chars_DestinationTooSmall_ReturnsFalse()
    {
        Span<char> dest = new char[4];
        bool ok = Convert.TryToBase64Chars(s_sample, dest, out int written);
        ok.Should().BeFalse();
        written.Should().Be(0);
    }

    [Test]
    public void TryToBase64Chars_Empty_ReturnsTrueWithZeroWritten()
    {
        Span<char> dest = [];
        Convert.TryToBase64Chars([], dest, out int written).Should().BeTrue();
        written.Should().Be(0);
    }

    // ---- TryFromBase64Chars / TryFromBase64String ----

    [Test]
    public void TryFromBase64Chars_ValidInput_DecodesCorrectly()
    {
        Span<byte> dest = new byte[8];
        bool ok = Convert.TryFromBase64Chars(SampleBase64.AsSpan(), dest, out int written);
        ok.Should().BeTrue();
        written.Should().Be(s_sample.Length);
        dest[..written].ToArray().Should().Equal(s_sample);
    }

    [Test]
    public void TryFromBase64Chars_DestinationTooSmall_ReturnsFalse()
    {
        Span<byte> dest = new byte[2];
        bool ok = Convert.TryFromBase64Chars(SampleBase64.AsSpan(), dest, out int written);
        ok.Should().BeFalse();
        written.Should().Be(0);
    }

    [Test]
    public void TryFromBase64Chars_InvalidInput_ReturnsFalse()
    {
        Span<byte> dest = new byte[8];
        bool ok = Convert.TryFromBase64Chars("not valid!".AsSpan(), dest, out int written);
        ok.Should().BeFalse();
        written.Should().Be(0);
    }

    [Test]
    public void TryFromBase64Chars_EmptyInput_ReturnsTrue()
    {
        Span<byte> dest = [];
        Convert.TryFromBase64Chars([], dest, out int written).Should().BeTrue();
        written.Should().Be(0);
    }

    [Test]
    public void TryFromBase64String_ValidInput_DecodesCorrectly()
    {
        Span<byte> dest = new byte[8];
        Convert.TryFromBase64String(SampleBase64, dest, out int written).Should().BeTrue();
        written.Should().Be(s_sample.Length);
    }

    [Test]
    public void TryFromBase64String_NullString_Throws()
    {
        byte[] destArray = new byte[8];
        Action action = () => Convert.TryFromBase64String(null!, destArray, out _);
        action.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void ToBase64String_InsertLineBreaks_AddsLineBreaksEvery76Chars()
    {
        // 60 bytes encodes to 80 base64 chars; with line breaks the BCL inserts CRLF after 76 chars.
        byte[] data = new byte[60];
        string encoded = Convert.ToBase64String((ReadOnlySpan<byte>)data, Base64FormattingOptions.InsertLineBreaks);
        encoded.IndexOf("\r\n", StringComparison.Ordinal).Should().BeGreaterThan(0);
    }

    [Test]
    public void TryToBase64Chars_ExactFitDestination_Succeeds()
    {
        // s_sample is 4 bytes -> 8 base64 chars.
        Span<char> dest = new char[8];
        Convert.TryToBase64Chars(s_sample, dest, out int written).Should().BeTrue();
        written.Should().Be(8);
        dest.ToString().Should().Be(SampleBase64);
    }

    [Test]
    public void TryFromBase64Chars_NonBase64Char_ReturnsFalse()
    {
        Span<byte> dest = new byte[8];
        // '!' is not in the base64 alphabet.
        Convert.TryFromBase64Chars("ABC!".AsSpan(), dest, out int written).Should().BeFalse();
        written.Should().Be(0);
    }

    [Test]
    public void TryFromBase64Chars_BadPadding_ReturnsFalse()
    {
        Span<byte> dest = new byte[8];
        // 5 chars cannot be a valid base64 sequence (must be multiple of 4 once whitespace stripped).
        Convert.TryFromBase64Chars("ABCDE".AsSpan(), dest, out int written).Should().BeFalse();
        written.Should().Be(0);
    }

    [Test]
    public void RoundTrip_SpanEncodeMatchesBclByteArrayEncode()
    {
        // The span polyfill must produce byte-identical output to the BCL byte[] overload.
        byte[] data = new byte[100];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i * 7);
        }

        string viaSpan = Convert.ToBase64String((ReadOnlySpan<byte>)data);
        string viaArray = Convert.ToBase64String(data);
        viaSpan.Should().Be(viaArray);
    }

    [Test]
    public void ToBase64String_OneByte_TwoPaddingChars()
    {
        byte[] one = [0xFF];
        Convert.ToBase64String((ReadOnlySpan<byte>)one).Should().Be("/w==");
    }

    [Test]
    public void ToBase64String_TwoBytes_OnePaddingChar()
    {
        byte[] two = [0xFF, 0xEE];
        Convert.ToBase64String((ReadOnlySpan<byte>)two).Should().Be("/+4=");
    }

    [Test]
    public void ToBase64String_ThreeBytes_NoPaddingChars()
    {
        byte[] three = [0xFF, 0xEE, 0xDD];
        Convert.ToBase64String((ReadOnlySpan<byte>)three).Should().Be("/+7d");
    }
}
