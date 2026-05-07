// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Text;

namespace System.Globalization;

public class HebrewNumberTests
{
    private static string Format(int number)
    {
        ValueStringBuilder builder = new(stackalloc char[16]);
        try
        {
            HebrewNumber.Append(ref builder, number);
            return builder.ToString();
        }
        finally
        {
            builder.Dispose();
        }
    }

    // Unit characters for digits 1..9 (\x05d0..\x05d8).
    private static char Unit(int n) => (char)('\x05d0' + n - 1);

    // Tens character for 10/20/.../90.
    private static char Tens(int n) => n switch
    {
        10 => '\x05d9',
        20 => '\x05db',
        30 => '\x05dc',
        40 => '\x05de',
        50 => '\x05e0',
        60 => '\x05e1',
        70 => '\x05e2',
        80 => '\x05e4',
        90 => '\x05e6',
        _ => throw new ArgumentOutOfRangeException(nameof(n)),
    };

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    public void Append_SingleDigit_AppendsLetterAndApostrophe(int number)
    {
        // Single-character output gets a trailing apostrophe.
        Format(number).Should().Be($"{Unit(number)}'");
    }

    [Theory]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(30)]
    [InlineData(40)]
    [InlineData(50)]
    [InlineData(60)]
    [InlineData(70)]
    [InlineData(80)]
    [InlineData(90)]
    public void Append_RoundTens_AppendsTensLetterAndApostrophe(int number)
    {
        // 10, 20, ..., 90 are single-letter outputs.
        Format(number).Should().Be($"{Tens(number)}'");
    }

    [Theory]
    [InlineData(11, '\x05d9', '\x05d0')] // 11 = 10 + 1, yod + alef
    [InlineData(12, '\x05d9', '\x05d1')] // 10 + 2
    [InlineData(13, '\x05d9', '\x05d2')] // 10 + 3
    [InlineData(14, '\x05d9', '\x05d3')] // 10 + 4
    [InlineData(17, '\x05d9', '\x05d6')] // 10 + 7
    [InlineData(18, '\x05d9', '\x05d7')] // 10 + 8
    [InlineData(19, '\x05d9', '\x05d8')] // 10 + 9
    public void Append_TwoCharNumber_InsertsGershayimBeforeLastChar(int number, char tens, char units)
    {
        // Multi-character output gets a gershayim (") inserted before the last character.
        Format(number).Should().Be($"{tens}\"{units}");
    }

    [Fact]
    public void Append_Fifteen_UsesNineSixSpelling()
    {
        // The number 15 is traditionally written as 9+6 (טו) rather than 10+5 (יה)
        // to avoid spelling part of the Tetragrammaton.
        Format(15).Should().Be("\x05d8\"\x05d5"); // tet + gershayim + vav
    }

    [Fact]
    public void Append_Sixteen_UsesNineSevenSpelling()
    {
        // 16 is similarly written as 9+7 (טז) rather than 10+6 (יו).
        Format(16).Should().Be("\x05d8\"\x05d6"); // tet + gershayim + zayin
    }

    [Theory]
    [InlineData(100, '\x05e7')] // qof
    [InlineData(200, '\x05e8')] // resh
    [InlineData(300, '\x05e9')] // shin
    [InlineData(400, '\x05ea')] // tav
    public void Append_RoundHundreds_UpToFourHundred(int number, char hundreds)
    {
        // Hundreds 100-400 use single letters qof/resh/shin/tav and end with apostrophe.
        Format(number).Should().Be($"{hundreds}'");
    }

    [Theory]
    [InlineData(500, "\x05ea\"\x05e7")] // 400 + 100
    [InlineData(600, "\x05ea\"\x05e8")] // 400 + 200
    [InlineData(700, "\x05ea\"\x05e9")] // 400 + 300
    [InlineData(800, "\x05ea\"\x05ea")] // 400 + 400, with gershayim before the last tav
    [InlineData(900, "\x05ea\x05ea\"\x05e7")] // 400 + 400 + 100
    public void Append_HundredsAboveFourHundred_UsesTavMultiples(int number, string expected)
    {
        Format(number).Should().Be(expected);
    }

    [Theory]
    [InlineData(101, "\x05e7\"\x05d0")] // 100 + 1
    [InlineData(115, "\x05e7\x05d8\"\x05d5")] // 100 + 15 (qof + tet + gershayim + vav)
    [InlineData(116, "\x05e7\x05d8\"\x05d6")] // 100 + 16
    [InlineData(248, "\x05e8\x05de\"\x05d7")] // 200 + 40 + 8 - "Ramach", classical mitzvot count
    [InlineData(613, "\x05ea\x05e8\x05d9\"\x05d2")] // 400 + 200 + 10 + 3 - taryag
    [InlineData(999, "\x05ea\x05ea\x05e7\x05e6\"\x05d8")] // 400 + 400 + 100 + 90 + 9
    public void Append_CompositeNumbers_FormatCorrectly(int number, string expected)
    {
        Format(number).Should().Be(expected);
    }

    [Theory]
    [InlineData(5001, 1)]
    [InlineData(5781, 781)] // recent civil-calendar Hebrew year
    public void Append_NumberAbove5000_SubtractsFiveThousand(int input, int reduced)
    {
        // Numbers above 5000 are reduced by 5000 before formatting (per the source comments).
        Format(input).Should().Be(Format(reduced));
    }

    [Fact]
    public void Append_AppendsToExistingBuilderContents()
    {
        ValueStringBuilder builder = new(stackalloc char[16]);
        try
        {
            builder.Append("Year ");
            HebrewNumber.Append(ref builder, 1);
            builder.ToString().Should().Be("Year \x05d0'");
        }
        finally
        {
            builder.Dispose();
        }
    }
}
