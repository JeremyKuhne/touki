// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using global::Touki.Text;
using TStringExtensions = global::Touki.Text.StringExtensions;

namespace Framework.Touki.Text;

[TestClass]
public class StringExtensionsTests
{
    [TestMethod]
    [DataRow("")]
    [DataRow("a")]
    [DataRow("ab")]
    [DataRow("abc")]
    [DataRow("abcd")]
    [DataRow("abcde")]
    [DataRow("abcdef")]
    [DataRow("abcdefg")]
    [DataRow("hello world")]
    [DataRow("0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF")]
    public void GetHashCode_Span_MatchesString(string value)
    {
        int spanHash = string.GetHashCode(value.AsSpan());
        int stringHash = value.GetHashCode();
        spanHash.Should().Be(stringHash);
    }

    [TestMethod]
    public void GetHashCode_Span_EmptyMatchesEmptyString()
    {
        string.GetHashCode(ReadOnlySpan<char>.Empty).Should().Be(string.Empty.GetHashCode());
    }

    [TestMethod]
    public void Create_WithState_PopulatesString()
    {
        string created = string.Create(5, 'X', static (span, state) =>
        {
            for (int i = 0; i < span.Length; i++)
            {
                span[i] = state;
            }
        });

        created.Should().Be("XXXXX");
    }

    [TestMethod]
    public void Create_WithState_ZeroLength_ReturnsEmpty()
    {
        string created = string.Create(0, 0, static (_, _) => { });
        created.Should().BeSameAs(string.Empty);
    }

    [TestMethod]
    public void Create_NullAction_Throws()
    {
        Action action = () => string.Create<int>(5, 0, null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void Create_NegativeLength_Throws()
    {
        Action action = () => string.Create(-1, 0, static (_, _) => { });
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [TestMethod]
    public void Create_InterpolatedHandler_FormatsString()
    {
        int value = 42;
        string created = string.Create(global::System.Globalization.CultureInfo.InvariantCulture, $"value={value}");
        created.Should().Be("value=42");
    }

    [TestMethod]
    public void Create_InterpolatedHandlerWithBuffer_FormatsString()
    {
        char[] buffer = new char[64];
        int value = 7;
        string created = string.Create(global::System.Globalization.CultureInfo.InvariantCulture, buffer, $"v={value}");
        created.Should().Be("v=7");
    }

    [TestMethod]
    public void Concat_TwoSpans_Concatenates()
    {
        TStringExtensions.Concat("foo".AsSpan(), "bar".AsSpan()).Should().Be("foobar");
    }

    [TestMethod]
    public void Concat_TwoSpans_BothEmpty_ReturnsEmpty()
    {
        TStringExtensions.Concat(ReadOnlySpan<char>.Empty, ReadOnlySpan<char>.Empty).Should().BeSameAs(string.Empty);
    }

    [TestMethod]
    public void Concat_ThreeSpans_Concatenates()
    {
        TStringExtensions.Concat("a".AsSpan(), "b".AsSpan(), "c".AsSpan()).Should().Be("abc");
    }

    [TestMethod]
    public void Concat_ThreeSpans_AllEmpty_ReturnsEmpty()
    {
        TStringExtensions.Concat(ReadOnlySpan<char>.Empty, ReadOnlySpan<char>.Empty, ReadOnlySpan<char>.Empty)
            .Should().BeSameAs(string.Empty);
    }

    [TestMethod]
    public void Concat_FourSpans_Concatenates()
    {
        TStringExtensions.Concat("a".AsSpan(), "b".AsSpan(), "c".AsSpan(), "d".AsSpan()).Should().Be("abcd");
    }

    [TestMethod]
    public void Concat_FourSpans_AllEmpty_ReturnsEmpty()
    {
        TStringExtensions.Concat(
            ReadOnlySpan<char>.Empty,
            ReadOnlySpan<char>.Empty,
            ReadOnlySpan<char>.Empty,
            ReadOnlySpan<char>.Empty).Should().BeSameAs(string.Empty);
    }
}
