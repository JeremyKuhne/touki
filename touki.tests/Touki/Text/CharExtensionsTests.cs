// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Text;

public class CharExtensionsTests
{
    [Theory]
    [InlineData('a', true)]
    [InlineData('z', true)]
    [InlineData('A', true)]
    [InlineData('Z', true)]
    [InlineData('0', false)]
    [InlineData('9', false)]
    [InlineData(' ', false)]
    [InlineData('\t', false)]
    [InlineData('é', false)]
    public void IsAsciiLetter_Character_ReturnsExpectedResult(char c, bool expected)
    {
        char.IsAsciiLetter(c).Should().Be(expected);
    }

    [Theory]
    [InlineData('a', true)]
    [InlineData('z', true)]
    [InlineData('A', false)]
    [InlineData('Z', false)]
    [InlineData('0', false)]
    [InlineData(' ', false)]
    public void IsAsciiLetterLower_Character_ReturnsExpectedResult(char c, bool expected)
    {
        char.IsAsciiLetterLower(c).Should().Be(expected);
    }

    [Theory]
    [InlineData('A', true)]
    [InlineData('Z', true)]
    [InlineData('a', false)]
    [InlineData('z', false)]
    [InlineData('0', false)]
    [InlineData(' ', false)]
    public void IsAsciiLetterUpper_Character_ReturnsExpectedResult(char c, bool expected)
    {
        char.IsAsciiLetterUpper(c).Should().Be(expected);
    }

    [Theory]
    [InlineData('0', true)]
    [InlineData('9', true)]
    [InlineData('a', false)]
    [InlineData('A', false)]
    [InlineData(' ', false)]
    public void IsAsciiDigit_Character_ReturnsExpectedResult(char c, bool expected)
    {
        char.IsAsciiDigit(c).Should().Be(expected);
    }

    [Theory]
    [InlineData('a', true)]
    [InlineData('Z', true)]
    [InlineData('0', true)]
    [InlineData('9', true)]
    [InlineData(' ', false)]
    [InlineData('+', false)]
    public void IsAsciiLetterOrDigit_Character_ReturnsExpectedResult(char c, bool expected)
    {
        char.IsAsciiLetterOrDigit(c).Should().Be(expected);
    }

    [Theory]
    [InlineData('0', true)]
    [InlineData('9', true)]
    [InlineData('a', true)]
    [InlineData('f', true)]
    [InlineData('A', true)]
    [InlineData('F', true)]
    [InlineData('g', false)]
    [InlineData('G', false)]
    [InlineData(' ', false)]
    public void IsAsciiHexDigit_Character_ReturnsExpectedResult(char c, bool expected)
    {
        char.IsAsciiHexDigit(c).Should().Be(expected);
    }

    [Theory]
    [InlineData('0', true)]
    [InlineData('9', true)]
    [InlineData('A', true)]
    [InlineData('F', true)]
    [InlineData('a', false)]
    [InlineData('f', false)]
    [InlineData('G', false)]
    [InlineData(' ', false)]
    public void IsAsciiHexDigitUpper_Character_ReturnsExpectedResult(char c, bool expected)
    {
        char.IsAsciiHexDigitUpper(c).Should().Be(expected);
    }

    [Theory]
    [InlineData('0', true)]
    [InlineData('9', true)]
    [InlineData('a', true)]
    [InlineData('f', true)]
    [InlineData('A', false)]
    [InlineData('F', false)]
    [InlineData('g', false)]
    [InlineData(' ', false)]
    public void IsAsciiHexDigitLower_Character_ReturnsExpectedResult(char c, bool expected)
    {
        char.IsAsciiHexDigitLower(c).Should().Be(expected);
    }

    [Theory]
    [InlineData('b', 'a', 'c', true)]
    [InlineData('a', 'a', 'c', true)]
    [InlineData('c', 'a', 'c', true)]
    [InlineData('`', 'a', 'c', false)]
    [InlineData('d', 'a', 'c', false)]
    public void IsBetween_Character_ReturnsExpectedResult(char c, char min, char max, bool expected)
    {
        char.IsBetween(c, min, max).Should().Be(expected);
    }

    [Theory]
    [InlineData('0', true, 0)]
    [InlineData('9', true, 9)]
    [InlineData('a', true, 10)]
    [InlineData('f', true, 15)]
    [InlineData('A', true, 10)]
    [InlineData('F', true, 15)]
    [InlineData('g', false, 0)]
    [InlineData('G', false, 0)]
    [InlineData(' ', false, 0)]
    public void TryDecodeHexDigit_Character_ReturnsExpectedResult(char c, bool expectedSuccess, int expectedValue)
    {
        bool success = char.TryDecodeHexDigit(c, out int value);
        success.Should().Be(expectedSuccess);
        if (expectedSuccess)
        {
            value.Should().Be(expectedValue);
        }
    }
}
