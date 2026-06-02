// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Text;

public class CharExtensionsTests
{
    [Test]
    [Arguments('a', true)]
    [Arguments('z', true)]
    [Arguments('A', true)]
    [Arguments('Z', true)]
    [Arguments('0', false)]
    [Arguments('9', false)]
    [Arguments(' ', false)]
    [Arguments('\t', false)]
    [Arguments('é', false)]
    public void IsAsciiLetter_Character_ReturnsExpectedResult(char c, bool expected)
    {
        char.IsAsciiLetter(c).Should().Be(expected);
    }

    [Test]
    [Arguments('a', true)]
    [Arguments('z', true)]
    [Arguments('A', false)]
    [Arguments('Z', false)]
    [Arguments('0', false)]
    [Arguments(' ', false)]
    public void IsAsciiLetterLower_Character_ReturnsExpectedResult(char c, bool expected)
    {
        char.IsAsciiLetterLower(c).Should().Be(expected);
    }

    [Test]
    [Arguments('A', true)]
    [Arguments('Z', true)]
    [Arguments('a', false)]
    [Arguments('z', false)]
    [Arguments('0', false)]
    [Arguments(' ', false)]
    public void IsAsciiLetterUpper_Character_ReturnsExpectedResult(char c, bool expected)
    {
        char.IsAsciiLetterUpper(c).Should().Be(expected);
    }

    [Test]
    [Arguments('0', true)]
    [Arguments('9', true)]
    [Arguments('a', false)]
    [Arguments('A', false)]
    [Arguments(' ', false)]
    public void IsAsciiDigit_Character_ReturnsExpectedResult(char c, bool expected)
    {
        char.IsAsciiDigit(c).Should().Be(expected);
    }

    [Test]
    [Arguments('a', true)]
    [Arguments('Z', true)]
    [Arguments('0', true)]
    [Arguments('9', true)]
    [Arguments(' ', false)]
    [Arguments('+', false)]
    public void IsAsciiLetterOrDigit_Character_ReturnsExpectedResult(char c, bool expected)
    {
        char.IsAsciiLetterOrDigit(c).Should().Be(expected);
    }

    [Test]
    [Arguments('0', true)]
    [Arguments('9', true)]
    [Arguments('a', true)]
    [Arguments('f', true)]
    [Arguments('A', true)]
    [Arguments('F', true)]
    [Arguments('g', false)]
    [Arguments('G', false)]
    [Arguments(' ', false)]
    public void IsAsciiHexDigit_Character_ReturnsExpectedResult(char c, bool expected)
    {
        char.IsAsciiHexDigit(c).Should().Be(expected);
    }

    [Test]
    [Arguments('0', true)]
    [Arguments('9', true)]
    [Arguments('A', true)]
    [Arguments('F', true)]
    [Arguments('a', false)]
    [Arguments('f', false)]
    [Arguments('G', false)]
    [Arguments(' ', false)]
    public void IsAsciiHexDigitUpper_Character_ReturnsExpectedResult(char c, bool expected)
    {
        char.IsAsciiHexDigitUpper(c).Should().Be(expected);
    }

    [Test]
    [Arguments('0', true)]
    [Arguments('9', true)]
    [Arguments('a', true)]
    [Arguments('f', true)]
    [Arguments('A', false)]
    [Arguments('F', false)]
    [Arguments('g', false)]
    [Arguments(' ', false)]
    public void IsAsciiHexDigitLower_Character_ReturnsExpectedResult(char c, bool expected)
    {
        char.IsAsciiHexDigitLower(c).Should().Be(expected);
    }

    [Test]
    [Arguments('b', 'a', 'c', true)]
    [Arguments('a', 'a', 'c', true)]
    [Arguments('c', 'a', 'c', true)]
    [Arguments('`', 'a', 'c', false)]
    [Arguments('d', 'a', 'c', false)]
    public void IsBetween_Character_ReturnsExpectedResult(char c, char min, char max, bool expected)
    {
        char.IsBetween(c, min, max).Should().Be(expected);
    }

    [Test]
    [Arguments('0', true, 0)]
    [Arguments('9', true, 9)]
    [Arguments('a', true, 10)]
    [Arguments('f', true, 15)]
    [Arguments('A', true, 10)]
    [Arguments('F', true, 15)]
    [Arguments('g', false, 0)]
    [Arguments('G', false, 0)]
    [Arguments(' ', false, 0)]
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
