// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

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
    public void ImplicitConversion_ToString_Succeeds()
    {
        StringSegment segment = new("Hello");
        string value = segment;
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
    public void EmptySegment_ImplicitConversions_WorkCorrectly()
    {
        StringSegment empty = new();

        string str = empty;
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
        StringSegment segment = new("Hello");
        StringSegment empty = new();

        // Empty values should always match as a prefix
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
}
