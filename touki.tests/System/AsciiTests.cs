// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Text;

namespace Touki;

/// <summary>
///  Tests for <c>System.Text.Ascii.EqualsIgnoreCase(ReadOnlySpan&lt;char&gt;, ReadOnlySpan&lt;char&gt;)</c>.
///  On net10 this exercises the BCL; on net472/net481 it exercises the touki polyfill.
///  The contract is identical and these tests pin it down for both targets.
/// </summary>
public class AsciiTests
{
    [Fact]
    public void EqualsIgnoreCase_BothEmpty_ReturnsTrue()
    {
        Ascii.EqualsIgnoreCase(ReadOnlySpan<char>.Empty, ReadOnlySpan<char>.Empty).Should().BeTrue();
    }

    [Fact]
    public void EqualsIgnoreCase_LengthMismatch_ReturnsFalse()
    {
        Ascii.EqualsIgnoreCase("abc".AsSpan(), "ab".AsSpan()).Should().BeFalse();
        Ascii.EqualsIgnoreCase("ab".AsSpan(), "abc".AsSpan()).Should().BeFalse();
    }

    [Theory]
    [InlineData("abc", "abc")]
    [InlineData("ABC", "ABC")]
    [InlineData("Hello, World!", "Hello, World!")]
    [InlineData("0123456789", "0123456789")]
    public void EqualsIgnoreCase_EqualAscii_ReturnsTrue(string left, string right)
    {
        Ascii.EqualsIgnoreCase(left.AsSpan(), right.AsSpan()).Should().BeTrue();
    }

    [Theory]
    [InlineData("abc", "ABC")]
    [InlineData("ABC", "abc")]
    [InlineData("Hello, World!", "HELLO, WORLD!")]
    [InlineData("MixedCase", "mIXEDcASE")]
    [InlineData("a", "A")]
    [InlineData("z", "Z")]
    public void EqualsIgnoreCase_CaseFoldedAscii_ReturnsTrue(string left, string right)
    {
        Ascii.EqualsIgnoreCase(left.AsSpan(), right.AsSpan()).Should().BeTrue();
    }

    [Theory]
    [InlineData("abc", "abd")]
    [InlineData("abc", "xyz")]
    [InlineData("0", "1")]
    [InlineData("hello!", "hello?")]
    public void EqualsIgnoreCase_DifferentAscii_ReturnsFalse(string left, string right)
    {
        Ascii.EqualsIgnoreCase(left.AsSpan(), right.AsSpan()).Should().BeFalse();
    }

    [Theory]
    // ASCII letter pairs differ by bit 5 BUT do not fold to a letter.
    // '[' (0x5B) | 0x20 == '{' (0x7B); '@' (0x40) | 0x20 == '`' (0x60).
    [InlineData("[", "{")]
    [InlineData("@", "`")]
    public void EqualsIgnoreCase_NonLetterAsciiOnly_Bit5DifferenceDoesNotMatch(string left, string right)
    {
        // Only ASCII letters fold; bit-5 differences elsewhere must not compare equal.
        Ascii.EqualsIgnoreCase(left.AsSpan(), right.AsSpan()).Should().BeFalse();
    }

    [Theory]
    // Both sides identical Unicode -> BCL Ascii returns false because they are non-ASCII.
    [InlineData("café", "café")]
    [InlineData("\u00e9", "\u00e9")]              // é vs é
    [InlineData("\u4e2d\u6587", "\u4e2d\u6587")]  // CJK vs CJK
    public void EqualsIgnoreCase_BothNonAscii_ReturnsFalse(string left, string right)
    {
        // BCL contract: "If both buffers contain equal, but non-ASCII characters,
        // the method returns false."
        Ascii.EqualsIgnoreCase(left.AsSpan(), right.AsSpan()).Should().BeFalse();
    }

    [Theory]
    [InlineData("\u00e9", "\u00c9")]              // é vs É (Latin-1 lowercase/uppercase)
    [InlineData("caf\u00e9", "CAF\u00c9")]        // café vs CAFÉ
    public void EqualsIgnoreCase_NonAsciiCaseFold_ReturnsFalse(string left, string right)
    {
        // Unicode case-folding is NOT performed; non-ASCII characters force a false return.
        Ascii.EqualsIgnoreCase(left.AsSpan(), right.AsSpan()).Should().BeFalse();
    }

    [Theory]
    [InlineData("a", "\u00e9")]   // ASCII vs Latin-1
    [InlineData("\u00e9", "a")]
    [InlineData("hello\u00e9", "helloa")]
    public void EqualsIgnoreCase_OneSideNonAscii_ReturnsFalse(string left, string right)
    {
        Ascii.EqualsIgnoreCase(left.AsSpan(), right.AsSpan()).Should().BeFalse();
    }

    [Theory]
    // Inputs straddling the BCL vectorization threshold (16 chars). Cover both sides
    // and both the equal and case-folded cases so any specialized small-string or
    // vector path is exercised on net10.
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(32)]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(255)]
    public void EqualsIgnoreCase_VariousLengths_ConsistentResults(int length)
    {
        // Build a same-case-and-different-case pair of ASCII letters of the given length.
        char[] lowerChars = new char[length];
        char[] upperChars = new char[length];
        for (int i = 0; i < length; i++)
        {
            lowerChars[i] = (char)('a' + (i % 26));
            upperChars[i] = (char)('A' + (i % 26));
        }
        string lower = new(lowerChars);
        string upper = new(upperChars);

        Ascii.EqualsIgnoreCase(lower.AsSpan(), lower.AsSpan()).Should().BeTrue();
        Ascii.EqualsIgnoreCase(lower.AsSpan(), upper.AsSpan()).Should().BeTrue();
        Ascii.EqualsIgnoreCase(upper.AsSpan(), lower.AsSpan()).Should().BeTrue();

        if (length > 0)
        {
            // Flip the last character to something that can't fold to a match.
            char[] differing = (char[])lowerChars.Clone();
            differing[^1] = '!';
            string differingString = new(differing);
            Ascii.EqualsIgnoreCase(lower.AsSpan(), differingString.AsSpan()).Should().BeFalse();
        }
    }
}
