// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Text;

public class CharsTests
{
    [Fact]
    public void GetRandomSimpleChar_WithNullRandom_ReturnsAllowedBmpChar()
    {
        int samples = 200;

        for (int i = 0; i < samples; i++)
        {
            char c = char.GetRandomSimpleChar(null);
            int code = c;

            bool inAsciiPrintable = code is >= 0x0020 and <= 0x007E;
            bool inGapA2 = code is >= 0x00A0 and <= 0xD7FF;
            bool inGapC = code is >= 0xE000 and <= 0xFFFD;

            bool inNonCharacters = code is >= 0xFDD0 and <= 0xFDEF;

            bool allowed = (inAsciiPrintable || inGapA2 || inGapC) && !inNonCharacters;

            allowed.Should().BeTrue($"U+{code:X4} should be an allowed BMP non-control, non-noncharacter");
        }
    }

    [Fact]
    public void GetRandomSimpleChar_WithDeterministicRandom_ReturnsOnlyAllowedRanges()
    {
        int samples = 10000;
        Random random = new(12345);

        for (int i = 0; i < samples; i++)
        {
            char c = char.GetRandomSimpleChar(random);
            int code = c;

            bool inAsciiPrintable = code is >= 0x0020 and <= 0x007E;
            bool inGapA2 = code is >= 0x00A0 and <= 0xD7FF;
            bool inGapC = code is >= 0xE000 and <= 0xFFFD;

            bool inNonCharacters = code is >= 0xFDD0 and <= 0xFDEF;

            // Explicitly exclude disallowed ranges for clarity.
            bool isControlOrDel = code is < 0x0020 or 0x007F;
            bool isC1Control = code is >= 0x0080 and <= 0x009F;
            bool isSurrogate = code is >= 0xD800 and <= 0xDFFF;
            bool isBeyondBmpAllowedMax = code > 0xFFFD;

            bool allowed = (inAsciiPrintable || inGapA2 || inGapC) && !inNonCharacters;

            allowed.Should().BeTrue($"U+{code:X4} expected in allowed ranges");
            isControlOrDel.Should().BeFalse($"U+{code:X4} must not be ASCII control or DEL");
            isC1Control.Should().BeFalse($"U+{code:X4} must not be C1 control");
            isSurrogate.Should().BeFalse($"U+{code:X4} must not be a surrogate");
            isBeyondBmpAllowedMax.Should().BeFalse($"U+{code:X4} must not exceed U+FFFD");
        }
    }

    [Fact]
    public void GetRandomSimpleChar_WithDeterministicRandom_NeverReturnsFdd0ToFdef()
    {
        int samples = 10000;
        Random random = new(987654321);

        for (int i = 0; i < samples; i++)
        {
            char c = char.GetRandomSimpleChar(random);
            int code = c;

            bool inForbiddenNonCharacters = code is >= 0xFDD0 and <= 0xFDEF;

            inForbiddenNonCharacters.Should().BeFalse($"U+{code:X4} must not be one of the 32 BMP noncharacters");
        }
    }
}
