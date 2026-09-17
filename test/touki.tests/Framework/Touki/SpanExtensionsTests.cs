// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

[TestClass]
public class SpanExtensionsTests
{
    // We're replicating the functionality of the SpanExtensions.Replace .NET method, so testing against both.

    [TestMethod]
    public void Replace_BasicReplacement_ReplacesCharacter()
    {
        Span<char> span = "hello".ToCharArray();

        span.Replace('e', 'a');
        span.ToString().Should().Be("hallo");
    }

    [TestMethod]
    public void Replace_MultipleOccurrences_ReplacesAllCharacters()
    {
        Span<char> span = "mississippi".ToCharArray();

        span.Replace('i', 'x');
        span.ToString().Should().Be("mxssxssxppx");
    }

    [TestMethod]
    public void Replace_CharacterNotFound_MakesNoChanges()
    {
        Span<char> span = "hello".ToCharArray();

        span.Replace('z', 'a');
        span.ToString().Should().Be("hello");
    }

    [TestMethod]
    public void Replace_SameCharacters_ReturnsImmediately()
    {
        Span<char> span = "hello".ToCharArray();

        span.Replace('e', 'e');
        span.ToString().Should().Be("hello");
    }

    [TestMethod]
    public void Replace_EmptySpan_HandlesCorrectly()
    {
        Span<char> span = [];

        span.Replace('a', 'b');
        span.IsEmpty.Should().BeTrue();
    }

    [TestMethod]
    public void Replace_SingleCharacter_ReplacesIfMatches()
    {
        Span<char> span = ['a'];

        span.Replace('a', 'b');
        span.ToString().Should().Be("b");
    }

    [TestMethod]
    public void Replace_AllSameCharacters_ReplacesAll()
    {
        Span<char> span = "aaaaa".ToCharArray();

        span.Replace('a', 'b');
        span.ToString().Should().Be("bbbbb");
    }
}
