// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace System;

public class NumberTests
{
    [Theory]
    [InlineData("0", -1, '\0')]
    public void Number_ParseFormatSpecifier(string format, int expectedDigits, char expectedFormatCharacter)
    {
        Number.ParseFormatSpecifier(format.AsSpan(), out int digits).Should().Be(expectedFormatCharacter);
        digits.Should().Be(expectedDigits);
    }

    [Theory]
    [InlineData(42, "0", "42")]
    [InlineData(42, "00", "42")]
    [InlineData(42, "000", "042")]
    [InlineData(-42, "000", "-042")]
    public void Number_FormatInt32(int value, string format, string expected)
    {
        Span<char> buffer = stackalloc char[32];
        Number.TryFormatInt32(value, ~0, format.AsSpan(), null, buffer, out int charsWritten).Should().BeTrue();
        charsWritten.Should().Be(expected.Length);
        buffer[..charsWritten].ToString().Should().Be(expected);
    }
}
