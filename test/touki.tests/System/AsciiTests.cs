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
[TestClass]
public class AsciiTests
{
    [TestMethod]
    public void EqualsIgnoreCase_BothEmpty_ReturnsTrue()
    {
        Ascii.EqualsIgnoreCase(ReadOnlySpan<char>.Empty, ReadOnlySpan<char>.Empty).Should().BeTrue();
    }

    [TestMethod]
    public void EqualsIgnoreCase_LengthMismatch_ReturnsFalse()
    {
        Ascii.EqualsIgnoreCase("abc".AsSpan(), "ab".AsSpan()).Should().BeFalse();
        Ascii.EqualsIgnoreCase("ab".AsSpan(), "abc".AsSpan()).Should().BeFalse();
    }

    [TestMethod]
    [DataRow("abc", "abc")]
    [DataRow("ABC", "ABC")]
    [DataRow("Hello, World!", "Hello, World!")]
    [DataRow("0123456789", "0123456789")]
    public void EqualsIgnoreCase_EqualAscii_ReturnsTrue(string left, string right)
    {
        Ascii.EqualsIgnoreCase(left.AsSpan(), right.AsSpan()).Should().BeTrue();
    }

    [TestMethod]
    [DataRow("abc", "ABC")]
    [DataRow("ABC", "abc")]
    [DataRow("Hello, World!", "HELLO, WORLD!")]
    [DataRow("MixedCase", "mIXEDcASE")]
    [DataRow("a", "A")]
    [DataRow("z", "Z")]
    public void EqualsIgnoreCase_CaseFoldedAscii_ReturnsTrue(string left, string right)
    {
        Ascii.EqualsIgnoreCase(left.AsSpan(), right.AsSpan()).Should().BeTrue();
    }

    [TestMethod]
    [DataRow("abc", "abd")]
    [DataRow("abc", "xyz")]
    [DataRow("0", "1")]
    [DataRow("hello!", "hello?")]
    public void EqualsIgnoreCase_DifferentAscii_ReturnsFalse(string left, string right)
    {
        Ascii.EqualsIgnoreCase(left.AsSpan(), right.AsSpan()).Should().BeFalse();
    }

    [TestMethod]
    // ASCII letter pairs differ by bit 5 BUT do not fold to a letter.
    // '[' (0x5B) | 0x20 == '{' (0x7B); '@' (0x40) | 0x20 == '`' (0x60).
    [DataRow("[", "{")]
    [DataRow("@", "`")]
    public void EqualsIgnoreCase_NonLetterAsciiOnly_Bit5DifferenceDoesNotMatch(string left, string right)
    {
        // Only ASCII letters fold; bit-5 differences elsewhere must not compare equal.
        Ascii.EqualsIgnoreCase(left.AsSpan(), right.AsSpan()).Should().BeFalse();
    }

    [TestMethod]
    // Both sides identical Unicode -> BCL Ascii returns false because they are non-ASCII.
    [DataRow("café", "café")]
    [DataRow("\u00e9", "\u00e9")]              // é vs é
    [DataRow("\u4e2d\u6587", "\u4e2d\u6587")]  // CJK vs CJK
    public void EqualsIgnoreCase_BothNonAscii_ReturnsFalse(string left, string right)
    {
        // BCL contract: "If both buffers contain equal, but non-ASCII characters,
        // the method returns false."
        Ascii.EqualsIgnoreCase(left.AsSpan(), right.AsSpan()).Should().BeFalse();
    }

    [TestMethod]
    [DataRow("\u00e9", "\u00c9")]              // é vs É (Latin-1 lowercase/uppercase)
    [DataRow("caf\u00e9", "CAF\u00c9")]        // café vs CAFÉ
    public void EqualsIgnoreCase_NonAsciiCaseFold_ReturnsFalse(string left, string right)
    {
        // Unicode case-folding is NOT performed; non-ASCII characters force a false return.
        Ascii.EqualsIgnoreCase(left.AsSpan(), right.AsSpan()).Should().BeFalse();
    }

    [TestMethod]
    [DataRow("a", "\u00e9")]   // ASCII vs Latin-1
    [DataRow("\u00e9", "a")]
    [DataRow("hello\u00e9", "helloa")]
    public void EqualsIgnoreCase_OneSideNonAscii_ReturnsFalse(string left, string right)
    {
        Ascii.EqualsIgnoreCase(left.AsSpan(), right.AsSpan()).Should().BeFalse();
    }

    [TestMethod]
    // Inputs straddling the BCL vectorization threshold (16 chars). Cover both sides
    // and both the equal and case-folded cases so any specialized small-string or
    // vector path is exercised on net10.
    [DataRow(1)]
    [DataRow(8)]
    [DataRow(15)]
    [DataRow(16)]
    [DataRow(17)]
    [DataRow(32)]
    [DataRow(63)]
    [DataRow(64)]
    [DataRow(255)]
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
