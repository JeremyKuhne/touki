// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public class StringsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Aar")]
    [InlineData("The quick brown fox jumps over the lazy dog.")]
    public void GetHashCode_EqualsString(string? value)
    {
        ReadOnlySpan<char> span = value.AsSpan();
        value ??= "";

        int hash1 = Strings.GetHashCode(span);
        int hash2 = value.GetHashCode();

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void GetHashCode_EqualsString_Sliced()
    {
        string test = "The quick brown fox jumps over the lazy dog.";
        ReadOnlySpan<char> span = test.AsSpan()[11..];

        int hash1 = Strings.GetHashCode(span);
        int hash2 = span.ToString().GetHashCode();

        hash1.Should().Be(hash2);
    }
}
