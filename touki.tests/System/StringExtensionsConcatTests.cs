// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public class StringExtensionsConcatTests
{
    [Test]
    public void Concat_TwoSpans_Concatenates()
    {
        string.Concat("hello".AsSpan(), " world".AsSpan()).Should().Be("hello world");
    }

    [Test]
    public void Concat_TwoSpans_BothEmpty_ReturnsEmpty()
    {
        string.Concat(default, default(ReadOnlySpan<char>)).Should().BeEmpty();
    }

    [Test]
    public void Concat_TwoSpans_FirstEmpty_ReturnsSecond()
    {
        string.Concat(default, "abc".AsSpan()).Should().Be("abc");
    }

    [Test]
    public void Concat_TwoSpans_SecondEmpty_ReturnsFirst()
    {
        string.Concat("abc".AsSpan(), default).Should().Be("abc");
    }

    [Test]
    public void Concat_ThreeSpans_Concatenates()
    {
        string.Concat("a".AsSpan(), "b".AsSpan(), "c".AsSpan()).Should().Be("abc");
    }

    [Test]
    public void Concat_FourSpans_Concatenates()
    {
        string.Concat("a".AsSpan(), "b".AsSpan(), "c".AsSpan(), "d".AsSpan()).Should().Be("abcd");
    }

    [Test]
    public unsafe void Concat_TwoSpans_OverflowingLengths_Throws()
    {
        // The checked length sum must reject this before allocating. We construct
        // spans with bogus lengths over a single pinned char; the test never reads
        // the spans, it only checks that the length addition itself overflows.
        char dummy;
        ReadOnlySpan<char> a = new(&dummy, int.MaxValue);
        ReadOnlySpan<char> b = new(&dummy, 2);

        bool threw = false;
        try
        {
            _ = string.Concat(a, b);
        }
        catch (OverflowException)
        {
            threw = true;
        }

        threw.Should().BeTrue();
    }

    [Test]
    public unsafe void Concat_ThreeSpans_OverflowingLengths_Throws()
    {
        char dummy;
        ReadOnlySpan<char> a = new(&dummy, int.MaxValue);
        ReadOnlySpan<char> b = new(&dummy, 1);
        ReadOnlySpan<char> c = new(&dummy, 1);

        bool threw = false;
        try
        {
            _ = string.Concat(a, b, c);
        }
        catch (OverflowException)
        {
            threw = true;
        }

        threw.Should().BeTrue();
    }

    [Test]
    public unsafe void Concat_FourSpans_OverflowingLengths_Throws()
    {
        char dummy;
        ReadOnlySpan<char> a = new(&dummy, int.MaxValue);
        ReadOnlySpan<char> b = new(&dummy, 1);
        ReadOnlySpan<char> c = new(&dummy, 1);
        ReadOnlySpan<char> d = new(&dummy, 1);

        bool threw = false;
        try
        {
            _ = string.Concat(a, b, c, d);
        }
        catch (OverflowException)
        {
            threw = true;
        }

        threw.Should().BeTrue();
    }
}
