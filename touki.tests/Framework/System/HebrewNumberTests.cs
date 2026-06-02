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

    [Test]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    [Arguments(4)]
    [Arguments(5)]
    [Arguments(6)]
    [Arguments(7)]
    [Arguments(8)]
    [Arguments(9)]
    public void Append_SingleDigit_AppendsLetterAndApostrophe(int number)
    {
        // Single-character output gets a trailing apostrophe.
        Format(number).Should().Be($"{Unit(number)}'");
    }

    [Test]
    [Arguments(10)]
    [Arguments(20)]
    [Arguments(30)]
    [Arguments(40)]
    [Arguments(50)]
    [Arguments(60)]
    [Arguments(70)]
    [Arguments(80)]
    [Arguments(90)]
    public void Append_RoundTens_AppendsTensLetterAndApostrophe(int number)
    {
        // 10, 20, ..., 90 are single-letter outputs.
        Format(number).Should().Be($"{Tens(number)}'");
    }

    [Test]
    [Arguments(11, '\x05d9', '\x05d0')] // 11 = 10 + 1, yod + alef
    [Arguments(12, '\x05d9', '\x05d1')] // 10 + 2
    [Arguments(13, '\x05d9', '\x05d2')] // 10 + 3
    [Arguments(14, '\x05d9', '\x05d3')] // 10 + 4
    [Arguments(17, '\x05d9', '\x05d6')] // 10 + 7
    [Arguments(18, '\x05d9', '\x05d7')] // 10 + 8
    [Arguments(19, '\x05d9', '\x05d8')] // 10 + 9
    public void Append_TwoCharNumber_InsertsGershayimBeforeLastChar(int number, char tens, char units)
    {
        // Multi-character output gets a gershayim (") inserted before the last character.
        Format(number).Should().Be($"{tens}\"{units}");
    }

    [Test]
    public void Append_Fifteen_UsesNineSixSpelling()
    {
        // The number 15 is traditionally written as 9+6 (טו) rather than 10+5 (יה)
        // to avoid spelling part of the Tetragrammaton.
        Format(15).Should().Be("\x05d8\"\x05d5"); // tet + gershayim + vav
    }

    [Test]
    public void Append_Sixteen_UsesNineSevenSpelling()
    {
        // 16 is similarly written as 9+7 (טז) rather than 10+6 (יו).
        Format(16).Should().Be("\x05d8\"\x05d6"); // tet + gershayim + zayin
    }

    [Test]
    [Arguments(100, '\x05e7')] // qof
    [Arguments(200, '\x05e8')] // resh
    [Arguments(300, '\x05e9')] // shin
    [Arguments(400, '\x05ea')] // tav
    public void Append_RoundHundreds_UpToFourHundred(int number, char hundreds)
    {
        // Hundreds 100-400 use single letters qof/resh/shin/tav and end with apostrophe.
        Format(number).Should().Be($"{hundreds}'");
    }

    [Test]
    [Arguments(500, "\x05ea\"\x05e7")] // 400 + 100
    [Arguments(600, "\x05ea\"\x05e8")] // 400 + 200
    [Arguments(700, "\x05ea\"\x05e9")] // 400 + 300
    [Arguments(800, "\x05ea\"\x05ea")] // 400 + 400, with gershayim before the last tav
    [Arguments(900, "\x05ea\x05ea\"\x05e7")] // 400 + 400 + 100
    public void Append_HundredsAboveFourHundred_UsesTavMultiples(int number, string expected)
    {
        Format(number).Should().Be(expected);
    }

    [Test]
    [Arguments(101, "\x05e7\"\x05d0")] // 100 + 1
    [Arguments(115, "\x05e7\x05d8\"\x05d5")] // 100 + 15 (qof + tet + gershayim + vav)
    [Arguments(116, "\x05e7\x05d8\"\x05d6")] // 100 + 16
    [Arguments(248, "\x05e8\x05de\"\x05d7")] // 200 + 40 + 8 - "Ramach", classical mitzvot count
    [Arguments(613, "\x05ea\x05e8\x05d9\"\x05d2")] // 400 + 200 + 10 + 3 - taryag
    [Arguments(999, "\x05ea\x05ea\x05e7\x05e6\"\x05d8")] // 400 + 400 + 100 + 90 + 9
    public void Append_CompositeNumbers_FormatCorrectly(int number, string expected)
    {
        Format(number).Should().Be(expected);
    }

    [Test]
    [Arguments(5001, "\x05d0'")] // 5000 + 1 -> 'alef + apostrophe (single-letter form)
    [Arguments(5781, "\x05ea\x05e9\x05e4\"\x05d0")] // 5781 -> 781 = 400 + 300 + 80 + 1
    public void Append_NumberAbove5000_SubtractsFiveThousand(int input, string expected)
    {
        // Numbers above 5000 are reduced by 5000 before formatting (per the source comments).
        // Asserting against explicit expected strings ensures the reduction itself is tested,
        // not just consistency with another invocation of the helper.
        Format(input).Should().Be(expected);
    }

    [Test]
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

    [Test]
    public void DateTimeFormat_WithHebrewCalendar_EmitsGematria()
    {
        // Regression test for the by-ref forwarding in DateTimeFormat.HebrewFormatDigits.
        // Before the fix, this path produced output with the gematria characters silently
        // truncated because HebrewNumber.Append took its builder by value.
        CultureInfo culture = new("he-IL");
        culture.DateTimeFormat.Calendar = new HebrewCalendar();

        DateTime date = new(2026, 5, 1);
        ValueStringBuilder builder = new(stackalloc char[64]);
        try
        {
            DateTimeFormat.Format(date, "dd MM yyyy", culture.DateTimeFormat, ref builder);
            string formatted = builder.ToString();
            formatted.Should().NotBeEmpty();
            bool hasHebrewLetter = formatted.Any(c => c is >= '\x05d0' and <= '\x05ea');
            hasHebrewLetter.Should().BeTrue("Hebrew calendar formatting must emit gematria characters");
        }
        finally
        {
            builder.Dispose();
        }
    }
}
