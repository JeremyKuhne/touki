// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Text;

[TestClass]
public class CharExtensionsTests
{
    [TestMethod]
    [DataRow('a', true)]
    [DataRow('z', true)]
    [DataRow('A', true)]
    [DataRow('Z', true)]
    [DataRow('0', false)]
    [DataRow('9', false)]
    [DataRow(' ', false)]
    [DataRow('\t', false)]
    [DataRow('é', false)]
    public void IsAsciiLetter_Character_ReturnsExpectedResult(char c, bool expected)
    {
        char.IsAsciiLetter(c).Should().Be(expected);
    }

    [TestMethod]
    [DataRow('a', true)]
    [DataRow('z', true)]
    [DataRow('A', false)]
    [DataRow('Z', false)]
    [DataRow('0', false)]
    [DataRow(' ', false)]
    public void IsAsciiLetterLower_Character_ReturnsExpectedResult(char c, bool expected)
    {
        char.IsAsciiLetterLower(c).Should().Be(expected);
    }

    [TestMethod]
    [DataRow('A', true)]
    [DataRow('Z', true)]
    [DataRow('a', false)]
    [DataRow('z', false)]
    [DataRow('0', false)]
    [DataRow(' ', false)]
    public void IsAsciiLetterUpper_Character_ReturnsExpectedResult(char c, bool expected)
    {
        char.IsAsciiLetterUpper(c).Should().Be(expected);
    }

    [TestMethod]
    [DataRow('0', true)]
    [DataRow('9', true)]
    [DataRow('a', false)]
    [DataRow('A', false)]
    [DataRow(' ', false)]
    public void IsAsciiDigit_Character_ReturnsExpectedResult(char c, bool expected)
    {
        char.IsAsciiDigit(c).Should().Be(expected);
    }

    [TestMethod]
    [DataRow('a', true)]
    [DataRow('Z', true)]
    [DataRow('0', true)]
    [DataRow('9', true)]
    [DataRow(' ', false)]
    [DataRow('+', false)]
    public void IsAsciiLetterOrDigit_Character_ReturnsExpectedResult(char c, bool expected)
    {
        char.IsAsciiLetterOrDigit(c).Should().Be(expected);
    }

    [TestMethod]
    [DataRow('0', true)]
    [DataRow('9', true)]
    [DataRow('a', true)]
    [DataRow('f', true)]
    [DataRow('A', true)]
    [DataRow('F', true)]
    [DataRow('g', false)]
    [DataRow('G', false)]
    [DataRow(' ', false)]
    public void IsAsciiHexDigit_Character_ReturnsExpectedResult(char c, bool expected)
    {
        char.IsAsciiHexDigit(c).Should().Be(expected);
    }

    [TestMethod]
    [DataRow('0', true)]
    [DataRow('9', true)]
    [DataRow('A', true)]
    [DataRow('F', true)]
    [DataRow('a', false)]
    [DataRow('f', false)]
    [DataRow('G', false)]
    [DataRow(' ', false)]
    public void IsAsciiHexDigitUpper_Character_ReturnsExpectedResult(char c, bool expected)
    {
        char.IsAsciiHexDigitUpper(c).Should().Be(expected);
    }

    [TestMethod]
    [DataRow('0', true)]
    [DataRow('9', true)]
    [DataRow('a', true)]
    [DataRow('f', true)]
    [DataRow('A', false)]
    [DataRow('F', false)]
    [DataRow('g', false)]
    [DataRow(' ', false)]
    public void IsAsciiHexDigitLower_Character_ReturnsExpectedResult(char c, bool expected)
    {
        char.IsAsciiHexDigitLower(c).Should().Be(expected);
    }

    [TestMethod]
    [DataRow('b', 'a', 'c', true)]
    [DataRow('a', 'a', 'c', true)]
    [DataRow('c', 'a', 'c', true)]
    [DataRow('`', 'a', 'c', false)]
    [DataRow('d', 'a', 'c', false)]
    public void IsBetween_Character_ReturnsExpectedResult(char c, char min, char max, bool expected)
    {
        char.IsBetween(c, min, max).Should().Be(expected);
    }

    [TestMethod]
    [DataRow('0', true, 0)]
    [DataRow('9', true, 9)]
    [DataRow('a', true, 10)]
    [DataRow('f', true, 15)]
    [DataRow('A', true, 10)]
    [DataRow('F', true, 15)]
    [DataRow('g', false, 0)]
    [DataRow('G', false, 0)]
    [DataRow(' ', false, 0)]
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
