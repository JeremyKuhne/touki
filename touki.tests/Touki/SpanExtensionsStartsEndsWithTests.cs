// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

[TestClass]
public class SpanExtensionsStartsEndsWithTests
{
    [TestMethod]
    public void StartsWith_Char_Match_ReturnsTrue()
    {
        ReadOnlySpan<char> span = "hello".AsSpan();
        span.StartsWith('h').Should().BeTrue();
    }

    [TestMethod]
    public void StartsWith_Char_NoMatch_ReturnsFalse()
    {
        ReadOnlySpan<char> span = "hello".AsSpan();
        span.StartsWith('e').Should().BeFalse();
    }

    [TestMethod]
    public void StartsWith_EmptySpan_ReturnsFalse()
    {
        ReadOnlySpan<char> span = default;
        span.StartsWith('h').Should().BeFalse();
    }

    [TestMethod]
    public void StartsWith_SingleElement_Match_ReturnsTrue()
    {
        ReadOnlySpan<int> span = [42];
        span.StartsWith(42).Should().BeTrue();
    }

    [TestMethod]
    public void StartsWith_Byte_Match_ReturnsTrue()
    {
        ReadOnlySpan<byte> span = [1, 2, 3];
        span.StartsWith((byte)1).Should().BeTrue();
        span.StartsWith((byte)2).Should().BeFalse();
    }

    [TestMethod]
    public void StartsWith_String_ReferenceType_ReturnsCorrect()
    {
        ReadOnlySpan<string> span = ["a", "b", "c"];
        span.StartsWith("a").Should().BeTrue();
        span.StartsWith("b").Should().BeFalse();
    }

    [TestMethod]
    public void StartsWith_NullReference_Match_ReturnsTrue()
    {
        ReadOnlySpan<string?> span = [null, "b"];
        span.StartsWith((string?)null).Should().BeTrue();
    }

    [TestMethod]
    public void StartsWith_OnSpan_Char_ReturnsCorrect()
    {
        Span<char> span = "hello".ToCharArray();
        span.StartsWith('h').Should().BeTrue();
        span.StartsWith('e').Should().BeFalse();
    }

    [TestMethod]
    public void EndsWith_Char_Match_ReturnsTrue()
    {
        ReadOnlySpan<char> span = "hello".AsSpan();
        span.EndsWith('o').Should().BeTrue();
    }

    [TestMethod]
    public void EndsWith_Char_NoMatch_ReturnsFalse()
    {
        ReadOnlySpan<char> span = "hello".AsSpan();
        span.EndsWith('l').Should().BeFalse();
    }

    [TestMethod]
    public void EndsWith_EmptySpan_ReturnsFalse()
    {
        ReadOnlySpan<char> span = default;
        span.EndsWith('o').Should().BeFalse();
    }

    [TestMethod]
    public void EndsWith_SingleElement_Match_ReturnsTrue()
    {
        ReadOnlySpan<int> span = [42];
        span.EndsWith(42).Should().BeTrue();
    }

    [TestMethod]
    public void EndsWith_Byte_Match_ReturnsTrue()
    {
        ReadOnlySpan<byte> span = [1, 2, 3];
        span.EndsWith((byte)3).Should().BeTrue();
        span.EndsWith((byte)2).Should().BeFalse();
    }

    [TestMethod]
    public void EndsWith_NullReference_Match_ReturnsTrue()
    {
        ReadOnlySpan<string?> span = ["a", null];
        span.EndsWith((string?)null).Should().BeTrue();
    }

    [TestMethod]
    public void EndsWith_OnSpan_Char_ReturnsCorrect()
    {
        Span<char> span = "hello".ToCharArray();
        span.EndsWith('o').Should().BeTrue();
        span.EndsWith('l').Should().BeFalse();
    }
}
