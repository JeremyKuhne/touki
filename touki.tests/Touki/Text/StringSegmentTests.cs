// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Globalization;

namespace Touki;

public class StringSegmentTests
{
    [Fact]
    public void DefaultConstructor_CreatesEmptySegment()
    {
        StringSegment segment = new();
        segment.Length.Should().Be(0);
        segment.IsEmpty.Should().BeTrue();
        segment.ToString().Should().Be(string.Empty);
    }

    [Fact]
    public void Constructor_WithString_InitializesCorrectly()
    {
        string value = "Hello";
        StringSegment segment = new(value);
        segment.Length.Should().Be(value.Length);
        segment.IsEmpty.Should().BeFalse();
        segment.ToString().Should().Be(value);
    }

    [Fact]
    public void Constructor_WithNullString_ReturnsEmpty()
    {
        StringSegment segment = new(null!, 0, 0);
        segment.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithNegativeStart_ThrowsException()
    {
        Action action = () => new StringSegment("test", -1, 1);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithNegativeLength_ThrowsException()
    {
        Action action = () => new StringSegment("test", 0, -1);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithStartAndLengthExceedingBounds_ThrowsException()
    {
        Action action = () => new StringSegment("test", 3, 2);
        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Start and length exceed the bounds of the string*");
    }

    [Fact]
    public void Constructor_WithStartAndLength_InitializesCorrectly()
    {
        string value = "Hello World";
        StringSegment segment = new(value, 6, 5);
        segment.Length.Should().Be(5);
        segment.IsEmpty.Should().BeFalse();
        segment.ToString().Should().Be("World");
    }

    [Fact]
    public void Constructor_WithStart_InitializesCorrectly()
    {
        string value = "Hello World";
        StringSegment segment = new(value, 6);
        segment.Length.Should().Be(5);
        segment.IsEmpty.Should().BeFalse();
        segment.ToString().Should().Be("World");
    }

    [Fact]
    public void Indexer_ReturnsCorrectCharacter()
    {
        StringSegment segment = new("Hello", 1, 3);
        segment[0].Should().Be('e');
        segment[1].Should().Be('l');
        segment[2].Should().Be('l');
    }

    [Fact]
    public void Indexer_WithInvalidIndex_ThrowsException()
    {
        StringSegment segment = new("Hello");
        Action action = () => _ = segment[5];
        action.Should().Throw<IndexOutOfRangeException>();
    }

    [Fact]
    public void RangeIndexer_ReturnsCorrectSegment()
    {
        StringSegment segment = new("Hello World");
        StringSegment subSegment = segment[6..11];
        subSegment.ToString().Should().Be("World");
    }

    [Fact]
    public void Slice_WithStart_ReturnsCorrectSegment()
    {
        StringSegment segment = new("Hello World");
        StringSegment sliced = segment[6..];
        sliced.ToString().Should().Be("World");
    }

    [Fact]
    public void Slice_WithStartOutOfRange_ThrowsException()
    {
        StringSegment segment = new("Hello");
        Action action = () => _ = segment[6..];
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Slice_WithStartAndLength_ReturnsCorrectSegment()
    {
        StringSegment segment = new("Hello World");
        StringSegment sliced = segment[..5];
        sliced.ToString().Should().Be("Hello");
    }

    [Fact]
    public void Slice_WithStartAndLengthOutOfRange_ThrowsException()
    {
        StringSegment segment = new("Hello");
        Action action = () => segment.Slice(2, 4);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TrySplit_WithDelimiterPresent_SplitsCorrectly()
    {
        StringSegment segment = new("Hello,World");
        bool result = segment.TrySplit(',', out StringSegment left, out StringSegment right);

        result.Should().BeTrue();
        left.ToString().Should().Be("Hello");
        right.ToString().Should().Be("World");
    }

    [Fact]
    public void TrySplit_WithDelimiterNotPresent_ReturnsOriginalSegment()
    {
        StringSegment segment = new("HelloWorld");
        bool result = segment.TrySplit(',', out StringSegment left, out StringSegment right);

        result.Should().BeTrue();
        left.ToString().Should().Be("HelloWorld");
        right.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void TrySplit_WithEmptySegment_ReturnsFalse()
    {
        StringSegment segment = new();
        bool result = segment.TrySplit(',', out StringSegment left, out StringSegment right);

        result.Should().BeFalse();
        left.IsEmpty.Should().BeTrue();
        right.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void TrySplitAny_WithSameDelimiters_SplitsCorrectly()
    {
        StringSegment segment = new("Hello,World");
        bool result = segment.TrySplitAny(',', ',', out StringSegment left, out StringSegment right);

        result.Should().BeTrue();
        left.ToString().Should().Be("Hello");
        right.ToString().Should().Be("World");
    }

    [Fact]
    public void TrySplitAny_WithDifferentDelimiters_SplitsCorrectly()
    {
        StringSegment segment = new("Hello;World,Test");
        bool result = segment.TrySplitAny(',', ';', out StringSegment left, out StringSegment right);

        result.Should().BeTrue();
        left.ToString().Should().Be("Hello");
        right.ToString().Should().Be("World,Test");
    }

    [Fact]
    public void Contains_WithCharPresent_ReturnsTrue()
    {
        StringSegment segment = new("Hello");
        segment.Contains('e').Should().BeTrue();
    }

    [Fact]
    public void Contains_WithCharNotPresent_ReturnsFalse()
    {
        StringSegment segment = new("Hello");
        segment.Contains('z').Should().BeFalse();
    }

    [Fact]
    public void IndexOf_WithCharPresent_ReturnsCorrectIndex()
    {
        StringSegment segment = new("Hello", 1, 3);
        segment.IndexOf('l').Should().Be(1);
    }

    [Fact]
    public void IndexOf_WithCharNotPresent_ReturnsMinusOne()
    {
        StringSegment segment = new("Hello");
        segment.IndexOf('z').Should().Be(-1);
    }

    [Fact]
    public void IndexOfAny_WithCharsPresent_ReturnsCorrectIndex()
    {
        StringSegment segment = new("Hello");
        segment.IndexOfAny('e', 'o').Should().Be(1);
    }

    [Fact]
    public void Replace_ReplacesAllOccurrences()
    {
        StringSegment segment = new("Hello");
        StringSegment replaced = segment.Replace('l', 'x');
        replaced.ToString().Should().Be("Hexxo");
    }

    [Fact]
    public void Replace_WithNoMatches_ReturnsSameSegment()
    {
        StringSegment segment = new("Hello");
        StringSegment replaced = segment.Replace('z', 'x');
        replaced.Should().Be(segment);
    }

    [Fact]
    public void Replace_WithNoMatches_ReturnsSameInstance()
    {
        StringSegment segment = new("Hello");
        StringSegment replaced = segment.Replace('z', 'x');
        replaced.ToString().Should().BeSameAs((string)segment);
    }

    [Fact]
    public void AsSpan_ReturnsCorrectSpan()
    {
        StringSegment segment = new("Hello", 1, 3);
        ReadOnlySpan<char> span = segment.AsSpan();
        span.ToString().Should().Be("ell");
    }

    [Fact]
    public void AsSpan_WithStart_ReturnsCorrectSpan()
    {
        StringSegment segment = new("Hello");
        ReadOnlySpan<char> span = segment.AsSpan(1);
        span.ToString().Should().Be("ello");
    }

    [Fact]
    public void AsSpan_WithStartAndLength_ReturnsCorrectSpan()
    {
        StringSegment segment = new("Hello");
        ReadOnlySpan<char> span = segment.AsSpan(1, 3);
        span.ToString().Should().Be("ell");
    }

    [Fact]
    public void ImplicitConversion_ToReadOnlySpan_Succeeds()
    {
        StringSegment segment = new("Hello");
        ReadOnlySpan<char> span = segment;
        span.ToString().Should().Be("Hello");
    }

    [Fact]
    public void ExplicitConversion_ToString_Succeeds()
    {
        StringSegment segment = new("Hello");
        string value = (string)segment;
        value.Should().Be("Hello");
    }

    [Fact]
    public void ImplicitConversion_FromString_Succeeds()
    {
        string value = "Hello";
        StringSegment segment = value;
        segment.ToString().Should().Be("Hello");
    }

    [Fact]
    public void Equals_WithIdenticalSegments_ReturnsTrue()
    {
        StringSegment segment1 = new("Hello");
        StringSegment segment2 = new("Hello");
        segment1.Equals(segment2).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithDifferentSegments_ReturnsFalse()
    {
        StringSegment segment1 = new("Hello");
        StringSegment segment2 = new("World");
        segment1.Equals(segment2).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithIgnoreCase_ReturnsTrue()
    {
        StringSegment segment1 = new("Hello");
        StringSegment segment2 = new("hello");
        segment1.Equals(segment2, true).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithReadOnlySpan_ReturnsTrue()
    {
        StringSegment segment = new("Hello");
        ReadOnlySpan<char> span = "Hello".AsSpan();
        segment.Equals(span).Should().BeTrue();
    }

    [Fact]
    public void EqualsOperator_WithIdenticalSegments_ReturnsTrue()
    {
        StringSegment segment1 = new("Hello");
        StringSegment segment2 = new("Hello");
        (segment1 == segment2).Should().BeTrue();
    }

    [Fact]
    public void NotEqualsOperator_WithDifferentSegments_ReturnsTrue()
    {
        StringSegment segment1 = new("Hello");
        StringSegment segment2 = new("World");
        (segment1 != segment2).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_ReturnsSameValueForEqualSegments()
    {
        StringSegment segment1 = new("Hello");
        StringSegment segment2 = new("Hello");
        segment1.GetHashCode().Should().Be(segment2.GetHashCode());
    }

    [Fact]
    public void ToString_WithFullSegment_ReturnsOriginalString()
    {
        string original = "Hello";
        StringSegment segment = new(original);
        segment.ToString().Should().BeSameAs(original);
    }

    [Fact]
    public void ToString_WithPartialSegment_ReturnsSubstring()
    {
        StringSegment segment = new("Hello World", 6, 5);
        segment.ToString().Should().Be("World");
    }

    [Fact]
    public void IndexOfAny_WithReadOnlySpan_ReturnsCorrectIndex()
    {
        StringSegment segment = new("Hello World");

        // Empty span
        segment.IndexOfAny([]).Should().Be(-1);

        // Single char span
        segment.IndexOfAny("e".AsSpan()).Should().Be(1);

        // Two char span
        segment.IndexOfAny("lo".AsSpan()).Should().Be(2);

        // Multiple chars span
        segment.IndexOfAny("xyzW".AsSpan()).Should().Be(6);

        // No match
        segment.IndexOfAny("xyz".AsSpan()).Should().Be(-1);
    }

    [Fact]
    public void LastIndexOf_WithCharPresent_ReturnsCorrectIndex()
    {
        StringSegment segment = new("Hello World");
        segment.LastIndexOf('l').Should().Be(9);

        // Character at the beginning
        segment.LastIndexOf('H').Should().Be(0);

        // Character at the end
        segment.LastIndexOf('d').Should().Be(10);

        // Character not present
        segment.LastIndexOf('z').Should().Be(-1);

        // Segment with multiple occurrences
        StringSegment multipleOccurrences = new("Hello", 1, 3);  // "ell"
        multipleOccurrences.LastIndexOf('l').Should().Be(2);
    }

    [Fact]
    public void Equals_WithDifferentComparisonTypes_WorksCorrectly()
    {
        StringSegment lower = new("hello world");
        StringSegment upper = new("HELLO WORLD");
        StringSegment mixed = new("Hello World");

        // Ordinal comparison (case sensitive)
        lower.Equals(upper, StringComparison.Ordinal).Should().BeFalse();
        lower.Equals(lower, StringComparison.Ordinal).Should().BeTrue();

        // OrdinalIgnoreCase comparison
        lower.Equals(upper, StringComparison.OrdinalIgnoreCase).Should().BeTrue();

        // CurrentCulture comparison
        mixed.Equals(upper, StringComparison.CurrentCulture).Should().BeFalse();
        mixed.Equals(upper, StringComparison.CurrentCultureIgnoreCase).Should().BeTrue();

        // InvariantCulture comparison
        mixed.Equals(upper, StringComparison.InvariantCulture).Should().BeFalse();
        mixed.Equals(upper, StringComparison.InvariantCultureIgnoreCase).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithString_HandlesEdgeCases()
    {
        StringSegment empty = new();
        StringSegment segment = new("Hello World");
        StringSegment partial = new("Hello World", 6, 5);  // "World"

        // Null string
        empty.Equals((string?)null).Should().BeFalse();
        segment.Equals((string?)null).Should().BeFalse();

        // Empty string
        empty.Equals(string.Empty).Should().BeTrue();
        segment.Equals(string.Empty).Should().BeFalse();

        // Full match
        segment.Equals("Hello World").Should().BeTrue();

        // Partial match
        partial.Equals("World").Should().BeTrue();
        partial.Equals("Hello").Should().BeFalse();

        // Different length
        segment.Equals("Hello").Should().BeFalse();
    }

    [Fact]
    public void Equals_WithObject_HandlesVariousTypes()
    {
        StringSegment segment = new("Hello");

        // Same value as string
        object stringObj = "Hello";
        segment.Equals(stringObj).Should().BeTrue();

        // Same value as StringSegment
        object segmentObj = new StringSegment("Hello");
        segment.Equals(segmentObj).Should().BeTrue();

        // Different value as string
        object differentStringObj = "World";
        segment.Equals(differentStringObj).Should().BeFalse();

        // Different value as StringSegment
        object differentSegmentObj = new StringSegment("World");
        segment.Equals(differentSegmentObj).Should().BeFalse();

        // Null object
        segment.Equals((object?)null).Should().BeFalse();

        // Different type
        object intObj = 42;
        segment.Equals(intObj).Should().BeFalse();
    }

    [Fact]
    public void LastIndexOf_WithSegmentSlice_ReturnsCorrectIndex()
    {
        StringSegment segment = new("Hello World Hello");
        StringSegment sliced = segment[6..11];  // "World"

        sliced.LastIndexOf('o').Should().Be(1);
        sliced.LastIndexOf('l').Should().Be(3);
        sliced.LastIndexOf('W').Should().Be(0);
        sliced.LastIndexOf('H').Should().Be(-1);
    }

    [Fact]
    public void EmptySegment_MethodBehaviors()
    {
        StringSegment empty = new();

        // Index operations
        empty.IndexOf('a').Should().Be(-1);
        empty.IndexOfAny('a', 'b').Should().Be(-1);
        empty.IndexOfAny("abc".AsSpan()).Should().Be(-1);
        empty.LastIndexOf('a').Should().Be(-1);

        // AsSpan
        empty.AsSpan().Length.Should().Be(0);

        // Replace
        StringSegment replaced = empty.Replace('a', 'b');
        replaced.IsEmpty.Should().BeTrue();

        // Equals
        empty.Equals(string.Empty).Should().BeTrue();
        empty.Equals(new StringSegment()).Should().BeTrue();
        empty.Equals("a").Should().BeFalse();
    }

    [Fact]
    public void EmptySegment_Indexer_ThrowsException()
    {
        StringSegment empty = new();
        Action action = () => _ = empty[0];
        action.Should().Throw<IndexOutOfRangeException>();
    }

    [Fact]
    public void EmptySegment_RangeIndexer_ReturnsEmptySegment()
    {
        StringSegment empty = new();
        StringSegment result = empty[0..0];
        result.IsEmpty.Should().BeTrue();
        result.Length.Should().Be(0);
    }

    [Fact]
    public void EmptySegment_SliceWithStart_ReturnsEmptySegmentForZero()
    {
        StringSegment empty = new();
        StringSegment result = empty[..];
        result.IsEmpty.Should().BeTrue();
        result.Length.Should().Be(0);
    }

    [Fact]
    public void EmptySegment_SliceWithStart_ThrowsForNonZero()
    {
        StringSegment empty = new();
        Action action = () => _ = empty[1..];
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void EmptySegment_SliceWithStartAndLength_ReturnsEmptySegmentForZeros()
    {
        StringSegment empty = new();
        StringSegment result = empty[..0];
        result.IsEmpty.Should().BeTrue();
        result.Length.Should().Be(0);
    }

    [Fact]
    public void EmptySegment_SliceWithStartAndLength_ThrowsForNonZeroLength()
    {
        StringSegment empty = new();
        Action action = () => _ = empty[..1];
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void EmptySegment_TrySplitAny_ReturnsFalse()
    {
        StringSegment empty = new();
        bool result = empty.TrySplitAny(',', ';', out StringSegment left, out StringSegment right);
        result.Should().BeFalse();
        left.IsEmpty.Should().BeTrue();
        right.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void EmptySegment_Contains_ReturnsFalse()
    {
        StringSegment empty = new();
        empty.Contains('a').Should().BeFalse();
    }

    [Fact]
    public void EmptySegment_EqualsOperator_WorksCorrectly()
    {
        StringSegment empty1 = new();
        StringSegment empty2 = new();
        StringSegment nonEmpty = new("test");

        (empty1 == empty2).Should().BeTrue();
        (empty1 != nonEmpty).Should().BeTrue();
        (empty1 == string.Empty).Should().BeTrue();
    }

    [Fact]
    public void EmptySegment_Conversions_WorkCorrectly()
    {
        StringSegment empty = new();

        string str = (string)empty;
        str.Should().Be(string.Empty);

        ReadOnlySpan<char> span = empty;
        span.IsEmpty.Should().BeTrue();
        span.Length.Should().Be(0);
    }

    [Fact]
    public void StartsWith_WithString_ReturnsTrueForPrefix()
    {
        StringSegment segment = new("Hello World");
        segment.StartsWith("Hello").Should().BeTrue();
        segment.StartsWith("H").Should().BeTrue();
        segment.StartsWith("Hello World").Should().BeTrue();
    }

    [Fact]
    public void StartsWith_WithString_ReturnsFalseForNonPrefix()
    {
        StringSegment segment = new("Hello World");
        segment.StartsWith("World").Should().BeFalse();
        segment.StartsWith("hello").Should().BeFalse(); // Case sensitive by default
        segment.StartsWith("Hello World!").Should().BeFalse(); // Longer than segment
    }

    [Fact]
    public void StartsWith_WithString_HandlesCaseComparison()
    {
        StringSegment segment = new("Hello World");
        segment.StartsWith("hello", StringComparison.OrdinalIgnoreCase).Should().BeTrue();
        segment.StartsWith("hello", StringComparison.Ordinal).Should().BeFalse();
        segment.StartsWith("HELLO", StringComparison.InvariantCultureIgnoreCase).Should().BeTrue();
    }

    [Fact]
    public void StartsWith_WithStringSegment_ReturnsTrueForPrefix()
    {
        StringSegment segment = new("Hello World");
        StringSegment prefix1 = new("Hello");
        StringSegment prefix2 = new("Hello World", 0, 5); // "Hello"

        segment.StartsWith(prefix1).Should().BeTrue();
        segment.StartsWith(prefix2).Should().BeTrue();
        segment.StartsWith(segment).Should().BeTrue(); // Same segment
    }

    [Fact]
    public void StartsWith_WithStringSegment_ReturnsFalseForNonPrefix()
    {
        StringSegment segment = new("Hello World");
        StringSegment nonPrefix1 = new("World");
        StringSegment nonPrefix2 = new("Hello World", 6, 5); // "World"
        StringSegment nonPrefix3 = new("hello"); // Case sensitive by default

        segment.StartsWith(nonPrefix1).Should().BeFalse();
        segment.StartsWith(nonPrefix2).Should().BeFalse();
        segment.StartsWith(nonPrefix3).Should().BeFalse();
    }

    [Fact]
    public void StartsWith_WithStringSegment_HandlesCaseComparison()
    {
        StringSegment segment = new("Hello World");
        StringSegment lowerPrefix = new("hello");

        segment.StartsWith(lowerPrefix, StringComparison.OrdinalIgnoreCase).Should().BeTrue();
        segment.StartsWith(lowerPrefix, StringComparison.Ordinal).Should().BeFalse();
    }

    [Fact]
    public void StartsWith_WithReadOnlySpan_ReturnsTrueForPrefix()
    {
        StringSegment segment = new("Hello World");
        ReadOnlySpan<char> prefix1 = "Hello".AsSpan();
        ReadOnlySpan<char> prefix2 = "H".AsSpan();

        segment.StartsWith(prefix1).Should().BeTrue();
        segment.StartsWith(prefix2).Should().BeTrue();
        segment.StartsWith(segment.AsSpan()).Should().BeTrue(); // Full span
    }

    [Fact]
    public void StartsWith_WithReadOnlySpan_ReturnsFalseForNonPrefix()
    {
        StringSegment segment = new("Hello World");
        ReadOnlySpan<char> nonPrefix1 = "World".AsSpan();
        ReadOnlySpan<char> nonPrefix2 = "hello".AsSpan(); // Case sensitive by default

        segment.StartsWith(nonPrefix1).Should().BeFalse();
        segment.StartsWith(nonPrefix2).Should().BeFalse();
    }

    [Fact]
    public void StartsWith_WithReadOnlySpan_HandlesCaseComparison()
    {
        StringSegment segment = new("Hello World");
        ReadOnlySpan<char> lowerPrefix = "hello".AsSpan();

        segment.StartsWith(lowerPrefix, StringComparison.OrdinalIgnoreCase).Should().BeTrue();
        segment.StartsWith(lowerPrefix, StringComparison.Ordinal).Should().BeFalse();
    }

    [Fact]
    public void StartsWith_WithEmptyValues_ReturnsExpectedResults()
    {
        string hello = "Hello";
        hello.StartsWith("", StringComparison.Ordinal).Should().BeTrue();
        Action action = () => hello.StartsWith(null!, StringComparison.Ordinal);
        action.Should().Throw<ArgumentNullException>();

        "".StartsWith("", StringComparison.Ordinal).Should().BeTrue();

        StringSegment segment = new("Hello");
        StringSegment empty = new();

        // Empty values should never match as a prefix
        segment.StartsWith(string.Empty).Should().BeTrue();
        segment.StartsWith(empty).Should().BeTrue();
        segment.StartsWith("".AsSpan()).Should().BeTrue();

        // Empty segment behavior
        empty.StartsWith(string.Empty).Should().BeTrue();
        empty.StartsWith(empty).Should().BeTrue();
        empty.StartsWith("".AsSpan()).Should().BeTrue();
        empty.StartsWith("Hello").Should().BeFalse();
    }

    [Fact]
    public void StartsWith_WithPartialSegment_WorksCorrectly()
    {
        StringSegment segment = new("Hello World", 6, 5); // "World"

        segment.StartsWith("Wo").Should().BeTrue();
        segment.StartsWith("World").Should().BeTrue();
        segment.StartsWith("Hello").Should().BeFalse();
        segment.StartsWith("Worlds").Should().BeFalse();
    }

    [Fact]
    public void Trim_RemovesLeadingAndTrailingWhitespace()
    {
        StringSegment segment = new("  Hello World  ");
        StringSegment trimmed = segment.Trim();
        trimmed.ToString().Should().Be("Hello World");
    }

    [Fact]
    public void TrimStart_RemovesLeadingWhitespace()
    {
        StringSegment segment = new("  Hello World  ");
        StringSegment trimmed = segment.TrimStart();
        trimmed.ToString().Should().Be("Hello World  ");
    }

    [Fact]
    public void TrimEnd_RemovesTrailingWhitespace()
    {
        StringSegment segment = new("  Hello World  ");
        StringSegment trimmed = segment.TrimEnd();
        trimmed.ToString().Should().Be("  Hello World");
    }

    [Fact]
    public void Trim_WithNoWhitespace_ReturnsOriginalString()
    {
        string original = "HelloWorld";
        StringSegment segment = new(original);
        StringSegment trimmed = segment.Trim();
        trimmed.ToString().Should().BeSameAs(original);
    }

    [Fact]
    public void Trim_WithNoWhitespace_ReturnsSameInstance()
    {
        string original = "HelloWorld";
        StringSegment segment = new(original);
        StringSegment trimmed = segment.Trim();
        trimmed.ToString().Should().BeSameAs(original);
    }

    [Fact]
    public void TrimStart_WithNoLeadingWhitespace_ReturnsSameInstance()
    {
        StringSegment segment = new("HelloWorld");
        StringSegment trimmed = segment.TrimStart();
        trimmed.ToString().Should().BeSameAs((string)segment);
    }

    [Fact]
    public void TrimEnd_WithNoTrailingWhitespace_ReturnsSameInstance()
    {
        StringSegment segment = new("HelloWorld");
        StringSegment trimmed = segment.TrimEnd();
        trimmed.ToString().Should().BeSameAs((string)segment);
    }

    [Fact]
    public void Trim_WithEmptySegment_ReturnsEmptySegment()
    {
        StringSegment segment = new();
        StringSegment trimmed = segment.Trim();
        trimmed.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Trim_WithOnlyWhitespace_ReturnsEmptySegment()
    {
        StringSegment segment = new("   \t\n\r   ");
        StringSegment trimmed = segment.Trim();
        trimmed.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void TrimStart_WithOnlyWhitespace_ReturnsEmptySegment()
    {
        StringSegment segment = new("   \t\n\r   ");
        StringSegment trimmed = segment.TrimStart();
        trimmed.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void TrimEnd_WithOnlyWhitespace_ReturnsEmptySegment()
    {
        StringSegment segment = new("   \t\n\r   ");
        StringSegment trimmed = segment.TrimEnd();
        trimmed.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Trim_WithPartialSegment_TrimsCorrectly()
    {
        StringSegment segment = new("  Hello World  ", 2, 11);
        StringSegment trimmed = segment.Trim();
        trimmed.ToString().Should().Be("Hello World");
    }

    [Fact]
    public void TrimStart_WithPartialSegment_TrimsCorrectly()
    {
        StringSegment segment = new("  Hello World  ", 2, 11);
        StringSegment trimmed = segment.TrimStart();
        trimmed.ToString().Should().Be("Hello World");
    }

    [Fact]
    public void TrimEnd_WithPartialSegment_TrimsCorrectly()
    {
        StringSegment segment = new("  Hello World  ", 1, 11);
        StringSegment trimmed = segment.TrimEnd();
        trimmed.ToString().Should().Be(" Hello Worl");
    }

    [Fact]
    public void Trim_WithVariousWhitespaceCharacters_TrimsCorrectly()
    {
        StringSegment segment = new("\t \n\rHello World\r\n \t");
        StringSegment trimmed = segment.Trim();
        trimmed.ToString().Should().Be("Hello World");
    }

    [Fact]
    public void EndsWith_WithString_ReturnsTrueForSuffix()
    {
        StringSegment segment = new("Hello World");
        segment.EndsWith("World").Should().BeTrue();
        segment.EndsWith("d").Should().BeTrue();
        segment.EndsWith("Hello World").Should().BeTrue();
    }

    [Fact]
    public void EndsWith_WithString_ReturnsFalseForNonSuffix()
    {
        StringSegment segment = new("Hello World");
        segment.EndsWith("Hello").Should().BeFalse();
        segment.EndsWith("world").Should().BeFalse(); // Case sensitive by default
        segment.EndsWith("!Hello World").Should().BeFalse(); // Longer than segment
    }

    [Fact]
    public void EndsWith_WithString_HandlesCaseComparison()
    {
        StringSegment segment = new("Hello World");
        segment.EndsWith("world", StringComparison.OrdinalIgnoreCase).Should().BeTrue();
        segment.EndsWith("world", StringComparison.Ordinal).Should().BeFalse();
        segment.EndsWith("WORLD", StringComparison.InvariantCultureIgnoreCase).Should().BeTrue();
    }

    [Fact]
    public void EndsWith_WithStringSegment_ReturnsTrueForSuffix()
    {
        StringSegment segment = new("Hello World");
        StringSegment suffix1 = new("World");
        StringSegment suffix2 = new("Hello World", 6, 5); // "World"

        segment.EndsWith(suffix1).Should().BeTrue();
        segment.EndsWith(suffix2).Should().BeTrue();
        segment.EndsWith(segment).Should().BeTrue(); // Same segment
    }

    [Fact]
    public void EndsWith_WithStringSegment_ReturnsFalseForNonSuffix()
    {
        StringSegment segment = new("Hello World");
        StringSegment nonSuffix1 = new("Hello");
        StringSegment nonSuffix2 = new("Hello World", 0, 5); // "Hello"
        StringSegment nonSuffix3 = new("world"); // Case sensitive by default

        segment.EndsWith(nonSuffix1).Should().BeFalse();
        segment.EndsWith(nonSuffix2).Should().BeFalse();
        segment.EndsWith(nonSuffix3).Should().BeFalse();
    }

    [Fact]
    public void EndsWith_WithStringSegment_HandlesCaseComparison()
    {
        StringSegment segment = new("Hello World");
        StringSegment lowerSuffix = new("world");

        segment.EndsWith(lowerSuffix, StringComparison.OrdinalIgnoreCase).Should().BeTrue();
        segment.EndsWith(lowerSuffix, StringComparison.Ordinal).Should().BeFalse();
    }

    [Fact]
    public void EndsWith_WithReadOnlySpan_ReturnsTrueForSuffix()
    {
        StringSegment segment = new("Hello World");
        ReadOnlySpan<char> suffix1 = "World".AsSpan();
        ReadOnlySpan<char> suffix2 = "d".AsSpan();

        segment.EndsWith(suffix1).Should().BeTrue();
        segment.EndsWith(suffix2).Should().BeTrue();
        segment.EndsWith(segment.AsSpan()).Should().BeTrue(); // Full span
    }

    [Fact]
    public void EndsWith_WithReadOnlySpan_ReturnsFalseForNonSuffix()
    {
        StringSegment segment = new("Hello World");
        ReadOnlySpan<char> nonSuffix1 = "Hello".AsSpan();
        ReadOnlySpan<char> nonSuffix2 = "world".AsSpan(); // Case sensitive by default

        segment.EndsWith(nonSuffix1).Should().BeFalse();
        segment.EndsWith(nonSuffix2).Should().BeFalse();
    }

    [Fact]
    public void EndsWith_WithReadOnlySpan_HandlesCaseComparison()
    {
        StringSegment segment = new("Hello World");
        ReadOnlySpan<char> lowerSuffix = "world".AsSpan();

        segment.EndsWith(lowerSuffix, StringComparison.OrdinalIgnoreCase).Should().BeTrue();
        segment.EndsWith(lowerSuffix, StringComparison.Ordinal).Should().BeFalse();
    }

    [Fact]
    public void EndsWith_WithEmptyValues_ReturnsExpectedResults()
    {
        StringSegment segment = new("Hello");
        StringSegment empty = new();

        // Empty values should always match as a suffix
        segment.EndsWith(string.Empty).Should().BeTrue();
        segment.EndsWith(empty).Should().BeTrue();
        segment.EndsWith("".AsSpan()).Should().BeTrue();

        // Empty segment behavior
        empty.EndsWith(string.Empty).Should().BeTrue();
        empty.EndsWith(empty).Should().BeTrue();
        empty.EndsWith("".AsSpan()).Should().BeTrue();
        empty.EndsWith("Hello").Should().BeFalse();
    }

    [Fact]
    public void EndsWith_WithPartialSegment_WorksCorrectly()
    {
        StringSegment segment = new("Hello World", 0, 5); // "Hello"

        segment.EndsWith("lo").Should().BeTrue();
        segment.EndsWith("Hello").Should().BeTrue();
        segment.EndsWith("World").Should().BeFalse();
        segment.EndsWith("xHello").Should().BeFalse();
    }

    [Fact]
    public void LastIndexOfAny_WithCharsPresent_ReturnsCorrectIndex()
    {
        StringSegment segment = new("Hello World");
        segment.LastIndexOfAny('l', 'o').Should().Be(9); // 'l' at index 9 is the last occurrence

        // Character at the beginning
        segment.LastIndexOfAny('H', 'x').Should().Be(0);

        // Character at the end
        segment.LastIndexOfAny('x', 'd').Should().Be(10);

        // Multiple occurrences of both characters
        segment.LastIndexOfAny('l', 'o').Should().Be(9); // 'l' at index 9 comes after 'o' at index 7

        // Same character provided twice
        segment.LastIndexOfAny('l', 'l').Should().Be(9);
    }

    [Fact]
    public void LastIndexOfAny_WithCharsNotPresent_ReturnsMinusOne()
    {
        StringSegment segment = new("Hello World");
        segment.LastIndexOfAny('z', 'y').Should().Be(-1);
    }

    [Fact]
    public void LastIndexOfAny_WithEmptySegment_ReturnsMinusOne()
    {
        StringSegment empty = new();
        empty.LastIndexOfAny('a', 'b').Should().Be(-1);
    }

    [Fact]
    public void LastIndexOfAny_WithPartialSegment_ReturnsCorrectIndex()
    {
        StringSegment segment = new("Hello World", 3, 5); // "lo Wo"
        segment.LastIndexOfAny('l', 'o').Should().Be(4);  // 'o' at index 4 relative to the segment
        segment.LastIndexOfAny('H', 'e').Should().Be(-1); // Not in this segment
    }

    [Fact]
    public void LastIndexOfAny_WithReadOnlySpan_ReturnsCorrectIndex()
    {
        StringSegment segment = new("Hello World");

        // Empty span
        segment.LastIndexOfAny([]).Should().Be(-1);

        // Single char span
        segment.LastIndexOfAny("l".AsSpan()).Should().Be(9);

        // Two char span
        segment.LastIndexOfAny("lo".AsSpan()).Should().Be(9);

        // Multiple chars span
        segment.LastIndexOfAny("xyzWdol".AsSpan()).Should().Be(10); // 'd' at index 10

        // No match
        segment.LastIndexOfAny("xyz".AsSpan()).Should().Be(-1);
    }

    [Fact]
    public void LastIndexOfAny_WithReadOnlySpan_AndEmptySegment_ReturnsMinusOne()
    {
        StringSegment empty = new();
        empty.LastIndexOfAny("abc".AsSpan()).Should().Be(-1);
    }

    [Fact]
    public void LastIndexOfAny_WithReadOnlySpan_AndPartialSegment_ReturnsCorrectIndex()
    {
        StringSegment segment = new("Hello World Hello", 6, 5); // "World"

        segment.LastIndexOfAny("dlr".AsSpan()).Should().Be(4);  // 'd' at index 4 relative to the segment
        segment.LastIndexOfAny("o".AsSpan()).Should().Be(1);
        segment.LastIndexOfAny("W".AsSpan()).Should().Be(0);
        segment.LastIndexOfAny("HZ".AsSpan()).Should().Be(-1);
    }

    [Fact]
    public void Trim_WithSpecificChar_RemovesLeadingAndTrailingChar()
    {
        StringSegment segment = new("###Hello World###");
        StringSegment trimmed = segment.Trim('#');
        trimmed.ToString().Should().Be("Hello World");
    }

    [Fact]
    public void Trim_WithSpecificChar_WhenNotPresent_ReturnsOriginalSegment()
    {
        StringSegment segment = new("Hello World");
        StringSegment trimmed = segment.Trim('#');
        trimmed.Should().Be(segment);
    }

    [Fact]
    public void Trim_WithSpecificChar_WhenNotPresent_ReturnsSameInstance()
    {
        string original = "Hello World";
        StringSegment segment = new(original);
        StringSegment trimmed = segment.Trim('#');
        trimmed.ToString().Should().BeSameAs(original);
    }

    [Fact]
    public void Trim_WithSpecificChar_WhenOnlyTrimChar_ReturnsEmptySegment()
    {
        StringSegment segment = new("######");
        StringSegment trimmed = segment.Trim('#');
        trimmed.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Trim_WithTwoSpecificChars_RemovesLeadingAndTrailingChars()
    {
        StringSegment segment = new("###Hello*World***");
        StringSegment trimmed = segment.Trim('#', '*');
        trimmed.ToString().Should().Be("Hello*World");
    }

    [Fact]
    public void Trim_WithTwoSpecificChars_WhenOnlyTrimChars_ReturnsEmptySegment()
    {
        StringSegment segment = new("##**##**##");
        StringSegment trimmed = segment.Trim('#', '*');
        trimmed.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Trim_WithTwoSpecificChars_WhenNotPresent_ReturnsSameInstance()
    {
        StringSegment segment = new("Hello World");
        StringSegment trimmed = segment.Trim('#', '*');
        trimmed.ToString().Should().BeSameAs((string)segment);
    }

    [Fact]
    public void TrimStart_WithSpecificChar_RemovesLeadingChar()
    {
        StringSegment segment = new("###Hello World###");
        StringSegment trimmed = segment.TrimStart('#');
        trimmed.ToString().Should().Be("Hello World###");
    }

    [Fact]
    public void TrimStart_WithSpecificChar_WhenNotPresent_ReturnsOriginalSegment()
    {
        StringSegment segment = new("Hello World");
        StringSegment trimmed = segment.TrimStart('#');
        trimmed.Should().Be(segment);
    }

    [Fact]
    public void TrimStart_WithSpecificChar_WhenNotPresent_ReturnsSameInstance()
    {
        StringSegment segment = new("Hello World");
        StringSegment trimmed = segment.TrimStart('#');
        trimmed.ToString().Should().BeSameAs((string)segment);
    }

    [Fact]
    public void TrimStart_WithSpecificChar_WhenOnlyTrimChar_ReturnsEmptySegment()
    {
        StringSegment segment = new("######");
        StringSegment trimmed = segment.TrimStart('#');
        trimmed.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void TrimStart_WithTwoSpecificChars_RemovesLeadingChars()
    {
        StringSegment segment = new("##**Hello World");
        StringSegment trimmed = segment.TrimStart('#', '*');
        trimmed.ToString().Should().Be("Hello World");
    }

    [Fact]
    public void TrimStart_WithTwoSpecificChars_WhenNotPresent_ReturnsSameInstance()
    {
        StringSegment segment = new("Hello World");
        StringSegment trimmed = segment.TrimStart('#', '*');
        trimmed.ToString().Should().BeSameAs((string)segment);
    }

    [Fact]
    public void TrimEnd_WithSpecificChar_RemovesTrailingChar()
    {
        StringSegment segment = new("###Hello World###");
        StringSegment trimmed = segment.TrimEnd('#');
        trimmed.ToString().Should().Be("###Hello World");
    }

    [Fact]
    public void TrimEnd_WithSpecificChar_WhenNotPresent_ReturnsOriginalSegment()
    {
        StringSegment segment = new("Hello World");
        StringSegment trimmed = segment.TrimEnd('#');
        trimmed.Should().Be(segment);
    }

    [Fact]
    public void TrimEnd_WithSpecificChar_WhenNotPresent_ReturnsSameInstance()
    {
        StringSegment segment = new("Hello World");
        StringSegment trimmed = segment.TrimEnd('#');
        trimmed.ToString().Should().BeSameAs((string)segment);
    }

    [Fact]
    public void TrimEnd_WithSpecificChar_WhenOnlyTrimChar_ReturnsEmptySegment()
    {
        StringSegment segment = new("######");
        StringSegment trimmed = segment.TrimEnd('#');
        trimmed.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void TrimEnd_WithTwoSpecificChars_RemovesTrailingChars()
    {
        StringSegment segment = new("Hello World##**");
        StringSegment trimmed = segment.TrimEnd('#', '*');
        trimmed.ToString().Should().Be("Hello World");
    }

    [Fact]
    public void TrimEnd_WithTwoSpecificChars_WhenNotPresent_ReturnsSameInstance()
    {
        StringSegment segment = new("Hello World");
        StringSegment trimmed = segment.TrimEnd('#', '*');
        trimmed.ToString().Should().BeSameAs((string)segment);
    }

    [Fact]
    public void Trim_WithSpecificChar_OnPartialSegment_TrimsCorrectly()
    {
        StringSegment segment = new("###Hello World###", 3, 11); // "Hello World"
        StringSegment trimmed = segment.Trim('#');
        trimmed.ToString().Should().Be("Hello World");
    }

    [Fact]
    public void Trim_WithTwoSpecificChars_OnPartialSegment_TrimsCorrectly()
    {
        StringSegment segment = new("###Hello*World###", 3, 11); // "Hello*World"
        StringSegment trimmed = segment.Trim('*', '#');
        trimmed.ToString().Should().Be("Hello*World");
    }

    [Fact]
    public unsafe void Pinning_NonEmptySegment_ProvidesValidPointer()
    {
        string original = "Hello World";
        StringSegment segment = new(original);

        fixed (char* pSegment = segment)
        {
            // Pointer should not be null
            ((nint)pSegment).Should().NotBe(0);

            // Should be able to read characters through the pointer
            for (int i = 0; i < segment.Length; i++)
            {
                pSegment[i].Should().Be(original[i]);
            }
        }
    }

    [Fact]
    public unsafe void Pinning_PartialSegment_ProvidesValidPointerToSubstring()
    {
        string original = "Hello World";
        StringSegment segment = new(original, 6, 5); // "World"

        fixed (char* pSegment = segment)
        {
            // Pointer should not be null
            (pSegment is null).Should().BeFalse();

            // Should be able to read characters through the pointer
            for (int i = 0; i < segment.Length; i++)
            {
                pSegment[i].Should().Be(original[i + 6]);
            }

            // The pointer should point to the correct position in the original string
            fixed (char* pOriginal = original)
            {
                // The segment pointer should be offset from the original string
                nint offset = (nint)(pSegment - pOriginal);
                offset.Should().Be((nint)6);
            }
        }
    }

    [Fact]
    public unsafe void Pinning_EmptySegment_ReturnsNullPointer()
    {
        StringSegment empty = new();

        fixed (char* pEmpty = empty)
        {
            // Empty segment should return a null pointer
            (pEmpty is null).Should().BeTrue();
        }
    }

    [Fact]
    public unsafe void Pinning_SegmentWithEmptyString_ReturnsNonNullPointer()
    {
        // Empty string is different from a null string - it's a valid but zero-length buffer
        string emptyString = string.Empty;
        StringSegment segment = new(emptyString);

        fixed (char* pSegment = segment)
        {
            // Empty string segment should return a null pointer (to avoid empty string's buffer)
            (pSegment is null).Should().BeTrue();
        }
    }

    [Fact]
    public unsafe void Pinning_SegmentAfterSlicing_ProvidesCorrectPointer()
    {
        string original = "Hello World";
        StringSegment segment = new(original);
        StringSegment sliced = segment.Slice(6, 5); // "World"

        fixed (char* pSliced = sliced)
        {
            fixed (char* pOriginal = original)
            {
                // The sliced pointer should be offset from the original string
                nint offset = (nint)(pSliced - pOriginal);
                offset.Should().Be((nint)6);

                // Verify content
                for (int i = 0; i < sliced.Length; i++)
                {
                    pSliced[i].Should().Be(original[i + 6]);
                }
            }
        }
    }

    [Fact]
    public void GetHashCode_ForFullString_MatchesStringHashCode()
    {
        // Full string segment
        string original = "Hello World";
        StringSegment segment = new(original);

        // Should have same hash code as the original string
        segment.GetHashCode().Should().Be(original.GetHashCode());
    }

    [Fact]
    public void GetHashCode_ForPartialSegment_MatchesEquivalentStringHashCode()
    {
        // Partial segment
        string original = "Hello World";
        StringSegment segment = new(original, 6, 5); // "World"

        // Should have same hash code as the equivalent substring
        string equivalent = "World";
        segment.GetHashCode().Should().Be(equivalent.GetHashCode());
    }

    [Fact]
    public void GetHashCode_ForEmptySegment_MatchesEmptyStringHashCode()
    {
        // Empty segment
        StringSegment emptySegment = new();

        // Should have same hash code as empty string
        string emptyString = string.Empty;
        emptySegment.GetHashCode().Should().Be(emptyString.GetHashCode());
    }

    [Fact]
    public void GetHashCode_ForSubSegments_MatchesCorrespondingSubstrings()
    {
        string original = "This is a test string for hash code validation";

        // Test various segments
        TestSegmentHashCode(original, 0, 4);    // "This"
        TestSegmentHashCode(original, 5, 2);    // "is"
        TestSegmentHashCode(original, 10, 4);   // "test"
        TestSegmentHashCode(original, 22, 9);   // "hash code"

        // Local helper function
        static void TestSegmentHashCode(string source, int start, int length)
        {
            StringSegment segment = new(source, start, length);
            string substring = source.Substring(start, length);

            segment.GetHashCode().Should().Be(substring.GetHashCode(),
                $"Segment '{segment}' should have same hash code as substring '{substring}'");
        }
    }

    [Fact]
    public void GetHashCode_ForDifferentSegmentsWithSameContent_HaveSameHashCode()
    {
        // Different string sources with the same content in segments
        string source1 = "Hello World";
        string source2 = "TestHelloTest";

        StringSegment segment1 = new(source1, 0, 5); // "Hello"
        StringSegment segment2 = new(source2, 4, 5); // "Hello"

        // Should have the same hash code
        segment1.GetHashCode().Should().Be(segment2.GetHashCode());

        // Both should match the hash code of the equivalent string
        string equivalent = "Hello";
        segment1.GetHashCode().Should().Be(equivalent.GetHashCode());
        segment2.GetHashCode().Should().Be(equivalent.GetHashCode());
    }

    [Fact]
    public void GetHashCode_ForSegmentsAfterSlicing_MatchesCorrectSubstring()
    {
        string original = "Hello World";
        StringSegment segment = new(original);

        // Create segments through slicing
        StringSegment helloSegment = segment[..5];    // "Hello"
        StringSegment worldSegment = segment[6..];    // "World"

        // Compare with equivalent strings
        helloSegment.GetHashCode().Should().Be("Hello".GetHashCode());
        worldSegment.GetHashCode().Should().Be("World".GetHashCode());
    }

    [Fact]
    public void GetHashCode_IsConsistentForSameContent()
    {
        // Multiple calls should return the same hash code
        StringSegment segment = new("Test String");
        int hash1 = segment.GetHashCode();
        int hash2 = segment.GetHashCode();

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void GetHashCode_ForStringWithOddLength_MatchesStringHashCode()
    {
        // Test with odd-length string to validate hash algorithm handles it correctly
        string oddString = "Hello";  // 5 characters
        StringSegment segment = new(oddString);

        segment.GetHashCode().Should().Be(oddString.GetHashCode());
    }

    [Fact]
    public void GetHashCode_WithUnicodeCharacters_MatchesStringHashCode()
    {
        // Test with Unicode characters
        string unicodeString = "こんにちは世界";  // "Hello World" in Japanese
        StringSegment segment = new(unicodeString);

        segment.GetHashCode().Should().Be(unicodeString.GetHashCode());

        // Also test a segment
        StringSegment partialSegment = new(unicodeString, 0, 5); // "こんにちは"
        string partialString = unicodeString[..5];

        partialSegment.GetHashCode().Should().Be(partialString.GetHashCode());
    }

    [Fact]
    public void CompareTo_StringSegment_Ordinal_WorksCorrectly()
    {
        // Test segments with different positions
        StringSegment segment1 = new("Hello World", 0, 5);  // "Hello"
        StringSegment segment2 = new("Hello World", 6, 5);  // "World"
        StringSegment segment3 = new("Hello World", 0, 11); // "Hello World"
        StringSegment segment4 = new("Hello", 0, 5);        // "Hello"
        StringSegment segment5 = new("hello", 0, 5);        // "hello"
        StringSegment empty = new();

        // Same content from different sources
        segment1.CompareTo(segment4).Should().Be(
            string.Compare("Hello", "Hello", StringComparison.Ordinal));

        // Different content
        segment1.CompareTo(segment2).Should().Be(
            string.Compare("Hello", "World", StringComparison.Ordinal));
        segment2.CompareTo(segment1).Should().Be(
            string.Compare("World", "Hello", StringComparison.Ordinal));

        // Comparing with whole string
        segment1.CompareTo(segment3).Should().Be(
            string.Compare("Hello", "Hello World", StringComparison.Ordinal));
        segment3.CompareTo(segment1).Should().Be(
            string.Compare("Hello World", "Hello", StringComparison.Ordinal));

        // Case sensitivity (ordinal is case-sensitive)
        segment1.CompareTo(segment5).Should().Be(
            string.Compare("Hello", "hello", StringComparison.Ordinal));
        segment5.CompareTo(segment1).Should().Be(
            string.Compare("hello", "Hello", StringComparison.Ordinal));

        // Empty segment
        empty.CompareTo(segment1).Should().Be(
            string.Compare("", "Hello", StringComparison.Ordinal));
        segment1.CompareTo(empty).Should().Be(
            string.Compare("Hello", "", StringComparison.Ordinal));
        empty.CompareTo(empty).Should().Be(
            string.Compare("", "", StringComparison.Ordinal));
    }

    [Fact]
    public void CompareTo_StringSegment_WithComparison_WorksCorrectly()
    {
        StringSegment segment1 = new("Hello World", 0, 5);  // "Hello"
        StringSegment segment2 = new("hello world", 0, 5);  // "hello"

        // Ordinal (case-sensitive)
        segment1.CompareTo(segment2, StringComparison.Ordinal).Should().Be(
            string.Compare("Hello", "hello", StringComparison.Ordinal));

        // OrdinalIgnoreCase
        segment1.CompareTo(segment2, StringComparison.OrdinalIgnoreCase).Should().Be(
            string.Compare("Hello", "hello", StringComparison.OrdinalIgnoreCase));

        // CurrentCulture
        segment1.CompareTo(segment2, StringComparison.CurrentCulture).Should().Be(
            string.Compare("Hello", "hello", StringComparison.CurrentCulture));

        // CurrentCultureIgnoreCase
        segment1.CompareTo(segment2, StringComparison.CurrentCultureIgnoreCase).Should().Be(
            string.Compare("Hello", "hello", StringComparison.CurrentCultureIgnoreCase));

        // InvariantCulture
        segment1.CompareTo(segment2, StringComparison.InvariantCulture).Should().Be(
            string.Compare("Hello", "hello", StringComparison.InvariantCulture));

        // InvariantCultureIgnoreCase
        segment1.CompareTo(segment2, StringComparison.InvariantCultureIgnoreCase).Should().Be(
            string.Compare("Hello", "hello", StringComparison.InvariantCultureIgnoreCase));
    }

    [Fact]
    public void CompareTo_String_Ordinal_WorksCorrectly()
    {
        // Test segments with different positions
        StringSegment fullSegment = new("Hello World");
        StringSegment startSegment = new("Hello World", 0, 5);   // "Hello"
        StringSegment middleSegment = new("Hello World", 3, 5);  // "lo Wo"
        StringSegment endSegment = new("Hello World", 6, 5);     // "World"
        StringSegment empty = new();

        // Full segment comparison
        fullSegment.CompareTo("Hello World").Should().Be(
            string.Compare("Hello World", "Hello World", StringComparison.Ordinal));
        fullSegment.CompareTo("Hello").Should().Be(
            string.Compare("Hello World", "Hello", StringComparison.Ordinal));
        fullSegment.CompareTo("Zebra").Should().Be(
            string.Compare("Hello World", "Zebra", StringComparison.Ordinal));

        // Start segment comparison
        startSegment.CompareTo("Hello").Should().Be(
            string.Compare("Hello", "Hello", StringComparison.Ordinal));
        startSegment.CompareTo("Help").Should().Be(
            string.Compare("Hello", "Help", StringComparison.Ordinal));
        startSegment.CompareTo("Hel").Should().Be(
            string.Compare("Hello", "Hel", StringComparison.Ordinal));

        // Middle segment comparison
        middleSegment.CompareTo("lo Wo").Should().Be(
            string.Compare("lo Wo", "lo Wo", StringComparison.Ordinal));

        middleSegment.CompareTo("lo").Should().Be(
            string.Compare("lo Wo", "lo", StringComparison.Ordinal));
        middleSegment.CompareTo("lo Wp").Should().Be(
            string.Compare("lo Wo", "lo Wp", StringComparison.Ordinal));

        // End segment comparison
        endSegment.CompareTo("World").Should().Be(
            string.Compare("World", "World", StringComparison.Ordinal));
        endSegment.CompareTo("Worle").Should().Be(
            string.Compare("World", "Worle", StringComparison.Ordinal));
        endSegment.CompareTo("Worl").Should().Be(
            string.Compare("World", "Worl", StringComparison.Ordinal));

        // Empty segment
        empty.CompareTo("").Should().Be(
            string.Compare("", "", StringComparison.Ordinal));
        empty.CompareTo("Hello").Should().Be(
            string.Compare("", "Hello", StringComparison.Ordinal));
    }


    [Fact]
    public void CompareTo_String_WithComparison_WorksCorrectly()
    {
        StringSegment segment = new("Hello World", 0, 5);  // "Hello"

        // Ordinal
        segment.CompareTo("hello", StringComparison.Ordinal).Should().Be(
            string.Compare("Hello", "hello", StringComparison.Ordinal));
        segment.CompareTo("Hello", StringComparison.Ordinal).Should().Be(
            string.Compare("Hello", "Hello", StringComparison.Ordinal));

        // OrdinalIgnoreCase
        segment.CompareTo("hello", StringComparison.OrdinalIgnoreCase).Should().Be(
            string.Compare("Hello", "hello", StringComparison.OrdinalIgnoreCase));
        segment.CompareTo("help", StringComparison.OrdinalIgnoreCase).Should().Be(
            string.Compare("Hello", "help", StringComparison.OrdinalIgnoreCase));

        // CurrentCulture
        int expected = string.Compare("Hello", "hello", StringComparison.CurrentCulture);
        segment.CompareTo("hello", StringComparison.CurrentCulture).Should().Be(expected);
        segment.CompareTo("Hello", StringComparison.CurrentCulture).Should().Be(
            string.Compare("Hello", "Hello", StringComparison.CurrentCulture));

        // CurrentCultureIgnoreCase
        segment.CompareTo("hello", StringComparison.CurrentCultureIgnoreCase).Should().Be(
            string.Compare("Hello", "hello", StringComparison.CurrentCultureIgnoreCase));

        // InvariantCulture
        segment.CompareTo("hello", StringComparison.InvariantCulture).Should().Be(
            string.Compare("Hello", "hello", StringComparison.InvariantCulture));
        segment.CompareTo("Hello", StringComparison.InvariantCulture).Should().Be(
            string.Compare("Hello", "Hello", StringComparison.InvariantCulture));

        // InvariantCultureIgnoreCase
        segment.CompareTo("hello", StringComparison.InvariantCultureIgnoreCase).Should().Be(
            string.Compare("Hello", "hello", StringComparison.InvariantCultureIgnoreCase));

        // Different lengths
        segment.CompareTo("Hell", StringComparison.Ordinal).Should().Be(
            string.Compare("Hello", "Hell", StringComparison.Ordinal));
        segment.CompareTo("Helloz", StringComparison.Ordinal).Should().Be(
            string.Compare("Hello", "Helloz", StringComparison.Ordinal));
    }

    [Fact]
    public void ComparisonOperators_WithStringSegments_WorkCorrectly()
    {
        StringSegment segment1 = new("Hello");
        StringSegment segment2 = new("World");
        StringSegment segment3 = new("Hello");
        StringSegment empty = new();

        // Equality operators
        (segment1 == segment3).Should().BeTrue();
        (segment1 != segment2).Should().BeTrue();

        // Less than
        (segment1 < segment2).Should().BeTrue();
        (segment2 < segment1).Should().BeFalse();

        // Less than or equal
        (segment1 <= segment3).Should().BeTrue();
        (segment1 <= segment2).Should().BeTrue();
        (segment2 <= segment1).Should().BeFalse();

        // Greater than
        (segment2 > segment1).Should().BeTrue();
        (segment1 > segment2).Should().BeFalse();

        // Greater than or equal
        (segment1 >= segment3).Should().BeTrue();
        (segment2 >= segment1).Should().BeTrue();
        (segment1 >= segment2).Should().BeFalse();

        // Empty comparisons
        (empty < segment1).Should().BeTrue();
        (segment1 > empty).Should().BeTrue();
#pragma warning disable CS1718 // Comparison made to same variable
        (empty == empty).Should().BeTrue();
#pragma warning restore CS1718 // Comparison made to same variable

        // Verify against string comparisons
        (segment1 < segment2).Should().Be(string.Compare("Hello", "World", StringComparison.Ordinal) < 0);
        (segment2 > segment1).Should().Be(string.Compare("World", "Hello", StringComparison.Ordinal) > 0);
    }

    [Fact]
    public void ComparisonOperators_WithStrings_WorkCorrectly()
    {
        StringSegment segment = new("Hello");

        // Less than
        (segment < "World").Should().BeTrue();
        (segment < "Hello").Should().BeFalse();

        // Less than or equal
        (segment <= "Hello").Should().BeTrue();
        (segment <= "World").Should().BeTrue();
        (segment <= "Hel").Should().BeFalse();

        // Greater than
        (segment > "Hel").Should().BeTrue();
        (segment > "Hello").Should().BeFalse();

        // Greater than or equal
        (segment >= "Hello").Should().BeTrue();
        (segment >= "Hel").Should().BeTrue();
        (segment >= "World").Should().BeFalse();

        // Verify against string comparisons
        (segment < "World").Should().Be(string.Compare("Hello", "World", StringComparison.Ordinal) < 0);
        (segment > "Hel").Should().Be(string.Compare("Hello", "Hel", StringComparison.Ordinal) > 0);
    }

    [Fact]
    public void CompareTo_WithDifferentPositions_MatchesStringCompare()
    {
        string original = "This is a test string for comparison tests";

        // Test segments at different positions
        TestSegmentCompare(original, 0, 4);     // "This"
        TestSegmentCompare(original, 5, 2);     // "is"
        TestSegmentCompare(original, 10, 4);    // "test"
        TestSegmentCompare(original, 22, 9);    // "for compa"
        TestSegmentCompare(original, 32, 5);    // "rison"

        // Local helper function
        static void TestSegmentCompare(string source, int start, int length)
        {
            StringSegment segment = new(source, start, length);
            string substring = source.Substring(start, length);

            // Compare against multiple targets
            string[] targets =
            [
                "a",
                "z",
                substring,
                substring.ToLowerInvariant(),
                substring.ToUpperInvariant(),
                substring + "x"
            ];

            foreach (string target in targets)
            {
                // Test ordinal comparison
                segment.CompareTo(target).Should().Be(
                    string.Compare(substring, target, StringComparison.Ordinal),
                    $"Segment '{segment}' compared to '{target}' should match string comparison");

                // Test comparison with explicit comparison type
                segment.CompareTo(target, StringComparison.OrdinalIgnoreCase).Should().Be(
                    string.Compare(substring, target, StringComparison.OrdinalIgnoreCase),
                    $"Segment '{segment}' compared to '{target}' with OrdinalIgnoreCase should match string comparison");
            }
        }
    }

    [Fact]
    public void CompareTo_WithSpecialCases_WorksCorrectly()
    {
        // Unicode characters
        StringSegment unicodeSegment = new("こんにちは世界"); // "Hello World" in Japanese
        StringSegment partialUnicode = new("こんにちは世界", 0, 5); // "こんにちは" (Hello)

        unicodeSegment.CompareTo("こんにちは世界").Should().Be(
            string.Compare("こんにちは世界", "こんにちは世界", StringComparison.Ordinal),
            "Full Unicode segment comparison should match string.Compare");

        partialUnicode.CompareTo("こんにちは").Should().Be(
            string.Compare("こんにちは", "こんにちは", StringComparison.Ordinal),
            "Partial Unicode segment comparison should match string.Compare");

        unicodeSegment.CompareTo("こんにちは").Should().Be(
            string.Compare("こんにちは世界", "こんにちは", StringComparison.Ordinal),
            "Unicode segment compared to shorter string should match string.Compare");

        // Same prefix but different lengths
        StringSegment abcSegment = new("abcdef", 0, 3); // "abc"

        abcSegment.CompareTo("abc").Should().Be(
            string.Compare("abc", "abc", StringComparison.Ordinal),
            "Equal length comparison should match string.Compare");

        abcSegment.CompareTo("ab").Should().Be(
            string.Compare("abc", "ab", StringComparison.Ordinal),
            "Comparison with shorter string should match string.Compare");

        abcSegment.CompareTo("abcd").Should().Be(
            string.Compare("abc", "abcd", StringComparison.Ordinal),
            "Comparison with longer string should match string.Compare");

        // Empty segment
        StringSegment empty = new();

        empty.CompareTo("").Should().Be(
            string.Compare(string.Empty, "", StringComparison.Ordinal),
            "Empty segment compared to empty string should match string.Compare");

        empty.CompareTo("a").Should().Be(
            string.Compare(string.Empty, "a", StringComparison.Ordinal),
            "Empty segment compared to non-empty string should match string.Compare");

        empty.CompareTo(empty).Should().Be(
            string.Compare(string.Empty, string.Empty, StringComparison.Ordinal),
            "Empty segment compared to itself should match string.Compare");
    }

    [Fact]
    public void CompareTo_StringComparison_InvalidValue_ThrowsArgumentOutOfRangeException()
    {
        StringSegment segment = new("test");
        string other = "test";

        // Cast to an invalid StringComparison value
        StringComparison invalidComparison = (StringComparison)99;

        // Should throw for invalid comparison value
        Action action = () => segment.CompareTo(other, invalidComparison);
        action.Should().Throw<ArgumentOutOfRangeException>()
              .WithMessage("*Unsupported comparison type*");
    }

    [Fact]
    public void CompareTo_WithMixedAsciiAndNonAscii_WorksCorrectly()
    {
        // Strings with ASCII and non-ASCII
        StringSegment ascii = new("abcdef");
        StringSegment nonAscii = new("abcд");  // Cyrillic д (U+0434)
        StringSegment mixed = new("abc\u00E9f"); // é (U+00E9)
        StringSegment mixedSame = new("abc\u00E9f"); // same as above

        // Compare ASCII vs mixed
        ascii.CompareTo(mixed).Should().Be(
            string.Compare("abcdef", "abc\u00E9f", StringComparison.Ordinal),
            "ASCII and mixed comparison should match string.Compare");

        // Compare mixed vs same mixed
        mixed.CompareTo(mixedSame).Should().Be(0,
            "Identical mixed strings should compare as equal");

        // Compare mixed vs non-ASCII
        mixed.CompareTo(nonAscii).Should().Be(
            string.Compare("abc\u00E9f", "abcд", StringComparison.Ordinal),
            "Mixed and non-ASCII comparison should match string.Compare");

        // Comparison with strings differing in ASCII portion
        StringSegment mixedAsciiDiff = new("abd\u00E9f");
        mixed.CompareTo(mixedAsciiDiff).Should().Be(
            string.Compare("abc\u00E9f", "abd\u00E9f", StringComparison.Ordinal),
            "Strings differing in ASCII portion should compare correctly");

        // Comparison with strings differing in non-ASCII portion
        StringSegment mixedNonAsciiDiff = new("abc\u00EAf"); // ê (U+00EA) instead of é
        mixed.CompareTo(mixedNonAsciiDiff).Should().Be(
            string.Compare("abc\u00E9f", "abc\u00EAf", StringComparison.Ordinal),
            "Strings differing in non-ASCII portion should compare correctly");
    }

    [Fact]
    public void CompareTo_AsciiToNonAsciiTransition_WorksCorrectly()
    {
        // Test strings that differ at the transition from ASCII to non-ASCII
        StringSegment ascii1 = new("abcde");
        StringSegment ascii2 = new("abcdf");
        StringSegment transitionA = new("abcd\u00E9"); // é (U+00E9)
        StringSegment transitionB = new("abcd\u00EA"); // ê (U+00EA)

        // ASCII comparison before the transition point
        ascii1.CompareTo(ascii2).Should().Be(
            string.Compare("abcde", "abcdf", StringComparison.Ordinal),
            "ASCII strings should compare correctly");

        // Comparison at the transition point (ASCII to non-ASCII)
        ascii1.CompareTo(transitionA).Should().Be(
            string.Compare("abcde", "abcd\u00E9", StringComparison.Ordinal),
            "ASCII to non-ASCII transition should compare correctly");

        ascii2.CompareTo(transitionA).Should().Be(
            string.Compare("abcdf", "abcd\u00E9", StringComparison.Ordinal),
            "ASCII to non-ASCII transition should compare correctly");

        // Comparison between different non-ASCII transitions
        transitionA.CompareTo(transitionB).Should().Be(
            string.Compare("abcd\u00E9", "abcd\u00EA", StringComparison.Ordinal),
            "Different non-ASCII transitions should compare correctly");

        // Test with a partial segment
        StringSegment partialTransition = new("xxabcd\u00E9yy", 2, 5); // "abcd\u00E9"
        partialTransition.CompareTo(transitionA).Should().Be(0,
            "Partial segment with transition should compare correctly");
    }

    [Fact]
    public void CompareTo_OrdinalIgnoreCase_WithMixedCharacters_WorksCorrectly()
    {
        // Case differences with ASCII and non-ASCII characters
        StringSegment lower = new("abcdef");
        StringSegment upper = new("ABCDEF");
        StringSegment mixedCase = new("aBcDeF");

        StringSegment lowerWithNonAscii = new("abcdé"); // é (U+00E9)
        StringSegment upperWithNonAscii = new("ABCDé"); // é (U+00E9)
        StringSegment upperNonAsciiCase = new("abcdÉ"); // É (U+00C9)

        // ASCII case-insensitive comparison
        lower.CompareTo(upper, StringComparison.OrdinalIgnoreCase).Should().Be(0,
            "ASCII case-insensitive comparison should work correctly");

        lower.CompareTo(mixedCase, StringComparison.OrdinalIgnoreCase).Should().Be(0,
            "Mixed case ASCII should compare as equal with case-insensitive comparison");

        // Non-ASCII case-insensitive comparison
        lowerWithNonAscii.CompareTo(upperWithNonAscii, StringComparison.OrdinalIgnoreCase).Should().Be(0,
            "Case-insensitive comparison with non-ASCII should work correctly for ASCII part");

        lowerWithNonAscii.CompareTo(upperNonAsciiCase, StringComparison.OrdinalIgnoreCase).Should().Be(0,
            "Case-insensitive comparison with non-ASCII should work correctly for non-ASCII part");

        // Mixed ASCII/non-ASCII with case differences
        StringSegment complexLower = new("abc\u00E9\u00F1xyz"); // abcéñxyz
        StringSegment complexUpper = new("ABC\u00C9\u00D1XYZ"); // ABCÉÑxyz

        complexLower.CompareTo(complexUpper, StringComparison.OrdinalIgnoreCase).Should().Be(0,
            "Complex mixed case comparison should work correctly");

        // Different strings that should not be equal even with case-insensitive comparison
        StringSegment differAtAscii = new("abd\u00E9\u00F1xyz"); // abdéñxyz
        complexLower.CompareTo(differAtAscii, StringComparison.OrdinalIgnoreCase).Should().NotBe(0,
            "Different strings should not be equal with case-insensitive comparison");

        StringSegment differAtNonAscii = new("abc\u00EA\u00F1xyz"); // abcêñxyz
        complexLower.CompareTo(differAtNonAscii, StringComparison.OrdinalIgnoreCase).Should().NotBe(0,
            "Strings differing at non-ASCII should not be equal with case-insensitive comparison");
    }

    [Fact]
    public void CompareTo_HalfAsciiOptimization_WorksCorrectly()
    {
        // Test the half-ASCII optimization in OrdinalIgnoreCase comparison
        // The implementation first compares ASCII characters with a fast path,
        // then falls back to culture-aware comparison for the rest

        // Strings that are identical in their ASCII part but differ after
        StringSegment asciiSameNonAsciiDiff1 = new("abc\u00E9xyz");  // abcéxyz
        StringSegment asciiSameNonAsciiDiff2 = new("abc\u00EAxyz");  // abcêxyz

        // Should compare correctly with ordinal
        asciiSameNonAsciiDiff1.CompareTo(asciiSameNonAsciiDiff2, StringComparison.Ordinal)
            .Should().Be(
                string.Compare("abc\u00E9xyz", "abc\u00EAxyz", StringComparison.Ordinal),
                "Strings with same ASCII part should compare correctly with Ordinal");

        // Should compare correctly with ordinal ignore case
        asciiSameNonAsciiDiff1.CompareTo(asciiSameNonAsciiDiff2, StringComparison.OrdinalIgnoreCase)
            .Should().Be(
                string.Compare("abc\u00E9xyz", "abc\u00EAxyz", StringComparison.OrdinalIgnoreCase),
                "Strings with same ASCII part should compare correctly with OrdinalIgnoreCase");

        // Test with case differences in ASCII part and differences in non-ASCII part
        StringSegment mixedCase1 = new("aBc\u00E9xyz");  // aBcéxyz
        StringSegment mixedCase2 = new("AbC\u00EAxyz");  // AbCêxyz

        mixedCase1.CompareTo(mixedCase2, StringComparison.OrdinalIgnoreCase)
            .Should().Be(
                string.Compare("aBc\u00E9xyz", "AbC\u00EAxyz", StringComparison.OrdinalIgnoreCase),
                "Strings with case differences in ASCII and differences in non-ASCII should compare correctly");
    }

    [Fact]
    public void CompareTo_WithAsciiPrefixNonAsciiSuffix_OrdinalIgnoreCase()
    {
        // This tests the specific case where string comparison switches from the
        // ASCII-optimized path to the culture-aware path in OrdinalIgnoreCase

        // Create strings with identical ASCII prefix but different non-ASCII suffix
        StringSegment segment1 = new("hello\u00E9"); // helloé
        StringSegment segment2 = new("hello\u00EA"); // helloê
        StringSegment segment3 = new("HELLO\u00E9"); // HELLOé

        // Ordinal comparison should detect the difference
        segment1.CompareTo(segment2, StringComparison.Ordinal).Should().Be(
            string.Compare("hello\u00E9", "hello\u00EA", StringComparison.Ordinal),
            "Ordinal comparison should detect difference in non-ASCII suffix");

        // OrdinalIgnoreCase should ignore case in ASCII part but detect difference in non-ASCII
        segment1.CompareTo(segment3, StringComparison.OrdinalIgnoreCase).Should().Be(0,
            "OrdinalIgnoreCase should consider different case ASCII with same non-ASCII as equal");

        segment1.CompareTo(segment2, StringComparison.OrdinalIgnoreCase).Should().Be(
            string.Compare("hello\u00E9", "hello\u00EA", StringComparison.OrdinalIgnoreCase),
            "OrdinalIgnoreCase should detect difference in non-ASCII suffix");

        // Test with characters that need surrogate pairs (outside BMP)
        StringSegment surrogate1 = new("hello\U0001F600"); // hello😀 (GRINNING FACE)
        StringSegment surrogate2 = new("hello\U0001F601"); // hello😁 (GRINNING FACE WITH SMILING EYES)
        StringSegment surrogate3 = new("HELLO\U0001F600"); // HELLO😀

        surrogate1.CompareTo(surrogate2, StringComparison.Ordinal).Should().Be(
            string.Compare("hello\U0001F600", "hello\U0001F601", StringComparison.Ordinal),
            "Ordinal comparison should detect difference in surrogate pairs");

        surrogate1.CompareTo(surrogate3, StringComparison.OrdinalIgnoreCase).Should().Be(0,
            "OrdinalIgnoreCase should consider different case ASCII with same surrogate pairs as equal");
    }

    [Fact]
    public void ImplicitConversion_ToReadOnlyMemory_WorksCorrectly()
    {
        // Full segment
        string original = "Hello World";
        StringSegment segment = new(original);
        ReadOnlyMemory<char> memory = segment;

        memory.Span.ToString().Should().Be(original);
        memory.Length.Should().Be(original.Length);

        // Partial segment
        StringSegment partialSegment = new(original, 6, 5); // "World"
        ReadOnlyMemory<char> partialMemory = partialSegment;

        partialMemory.Span.ToString().Should().Be("World");
        partialMemory.Length.Should().Be(5);

        // Empty segment
        StringSegment emptySegment = new();
        ReadOnlyMemory<char> emptyMemory = emptySegment;

        emptyMemory.Length.Should().Be(0);
        emptyMemory.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void TryFormat_WithSufficientBuffer_ReturnsTrue()
    {
        StringSegment segment = new("Hello World");
        Span<char> destination = new char[segment.Length];

        bool result = ((ISpanFormattable)segment).TryFormat(
            destination,
            out int charsWritten,
            [],
            null);

        result.Should().BeTrue();
        charsWritten.Should().Be(segment.Length);
        destination.ToString().Should().Be("Hello World");
    }

    [Fact]
    public void TryFormat_WithInsufficientBuffer_ReturnsFalse()
    {
        StringSegment segment = new("Hello World");
        Span<char> destination = new char[segment.Length - 1]; // One character too small

        bool result = ((ISpanFormattable)segment).TryFormat(
            destination,
            out int charsWritten,
            [],
            null);

        result.Should().BeFalse();
        charsWritten.Should().Be(0);
    }

    [Fact]
    public void TryFormat_WithFormat_IgnoresFormat()
    {
        StringSegment segment = new("123");
        Span<char> destination = new char[segment.Length];

        // Format should be ignored, as specified in the implementation
        bool result = ((ISpanFormattable)segment).TryFormat(
            destination,
            out int charsWritten,
            "N2".AsSpan(), // Number format that would add commas if used
            CultureInfo.InvariantCulture);

        charsWritten.Should().Be(segment.Length);
        result.Should().BeTrue();
        destination.ToString().Should().Be("123"); // Not "123.00"
    }

    [Fact]
    public void GetHashCode_WithNull_ReturnsEmptyStringHashCode()
    {
        StringSegment segment = new(null!);
        segment.GetHashCode().Should().Be(string.Empty.GetHashCode());
    }

    [Fact]
    public void GetHashCode_PreservesHash_ForPartialSegments()
    {
        string str = "Hello World Test";

        // Test that the hash is the same as the corresponding substring
        for (int start = 0; start < str.Length; start++)
        {
            for (int length = 1; length <= str.Length - start; length++)
            {
                StringSegment segment = new(str, start, length);
                string substring = str.Substring(start, length);

                segment.GetHashCode().Should().Be(substring.GetHashCode(),
                    $"Hash code for segment '{segment}' should match substring '{substring}'");
            }
        }
    }

    [Fact]
    public void Equals_WithReadOnlySpan_IgnoresCase()
    {
        StringSegment segment = new("Hello");
        ReadOnlySpan<char> lower = "hello".AsSpan();

        // StringSegment.Equals(ReadOnlySpan<char>) doesn't have a case-insensitive option
        // This test demonstrates that it's case-sensitive
        segment.Equals(lower).Should().BeFalse();

        // For comparison, string.Equals() with OrdinalIgnoreCase would return true
        string.Equals(segment.ToString(), lower.ToString(), StringComparison.OrdinalIgnoreCase).Should().BeTrue();
    }

    [Fact]
    public unsafe void Pinning_WithDifferentSegmentCreationPaths_WorksConsistently()
    {
        // Test pinning with segments created via different paths
        string original = "Test String";

        // Direct constructor
        StringSegment segment1 = new(original);

        // Slice
        StringSegment segment2 = segment1[..original.Length];

        // Range indexer
        StringSegment segment3 = segment1[0..original.Length];

        // Implicit conversion
        StringSegment segment4 = original;

        fixed (char* p1 = segment1)
        fixed (char* p2 = segment2)
        fixed (char* p3 = segment3)
        fixed (char* p4 = segment4)
        fixed (char* po = original)
        {
            // All should point to the same memory location
            ((nint)p1).Should().Be((nint)po);
            ((nint)p2).Should().Be((nint)po);
            ((nint)p3).Should().Be((nint)po);
            ((nint)p4).Should().Be((nint)po);
        }
    }

    [Fact]
    public void IFormattable_ToString_ReturnsSameAsToString()
    {
        StringSegment segment = new("Hello World");
        IFormattable formattable = segment;

        // Should ignore format string and provider
        string result = formattable.ToString("N2", CultureInfo.InvariantCulture);

        result.Should().Be(segment.ToString());
        result.Should().Be("Hello World");
    }

    [Fact]
    public void CompareTo_WithNullString_ReturnsPositiveValue()
    {
        string hello = "Hello";
#pragma warning disable CA1310 // Specify StringComparison for correctness
        hello.CompareTo(null).Should().Be(1);
#pragma warning restore CA1310

        // StringSegment with content should return positive value when compared to null
        StringSegment segment = new("Hello");
        segment.CompareTo(null).Should().Be(1);

#pragma warning disable CA1310 // Specify StringComparison for correctness
        "".CompareTo(null).Should().Be(1);
#pragma warning restore CA1310

        // Empty StringSegment should return 1 when compared to null (special case)
        StringSegment empty = new();
        empty.CompareTo(null).Should().Be(1);
    }

    [Fact]
    public void ImplicitConversion_ToReadOnlyMemory_WithNullValue_ReturnsEmptyMemory()
    {
        StringSegment segment = new(null!);
        ReadOnlyMemory<char> memory = segment;

        memory.IsEmpty.Should().BeTrue();
        memory.Length.Should().Be(0);
    }
}
