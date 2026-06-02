// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Text;

namespace Touki;

public class StoringStringSegment
{
    public static IEnumerable<StringSegment> StringSegmentData()
    {
        yield return new StringSegment("Hello, World!");
        yield return new StringSegment("Hello, World!", 7, 5);
        yield return new StringSegment(string.Empty);
        yield return default;
    }

    [Test]
    [MethodDataSource(nameof(StringSegmentData))]
    public void StringSegmentImplicit(StringSegment segment)
    {
        Value value = segment;
        value.As<StringSegment>().Should().Be(segment);
        value.Type.Should().Be(typeof(StringSegment));
    }

    [Test]
    [MethodDataSource(nameof(StringSegmentData))]
    public void StringSegmentCreate(StringSegment segment)
    {
        Value value;
        using (MemoryWatch.Create)
        {
            value = Value.Create(segment);
        }

        value.As<StringSegment>().Should().Be(segment);
        value.Type.Should().Be(typeof(StringSegment));
    }

    [Test]
    [MethodDataSource(nameof(StringSegmentData))]
    public void StringSegmentInOut(StringSegment segment)
    {
        Value value = segment;
        bool success = value.TryGetValue(out StringSegment result);
        success.Should().BeTrue();
        result.Should().Be(segment);

        value.As<StringSegment>().Should().Be(segment);
        ((StringSegment)value).Should().Be(segment);
    }

    [Test]
    public void NestedSegments()
    {
        string text = "Hello, World! How are you?";
        StringSegment fullSegment = new(text);
        StringSegment worldSegment = new(text, 7, 5); // "World"
        StringSegment howSegment = new(text, 14, 3);  // "How"

        Value value1 = fullSegment;
        Value value2 = worldSegment;
        Value value3 = howSegment;

        value1.As<StringSegment>().Should().Be(fullSegment);
        value2.As<StringSegment>().Should().Be(worldSegment);
        value3.As<StringSegment>().Should().Be(howSegment);

        // Ensure the segments maintain their correct positions
        fullSegment.ToString().Should().Be("Hello, World! How are you?");
        worldSegment.ToString().Should().Be("World");
        howSegment.ToString().Should().Be("How");

        // Verify the retrieved segments have the same properties
        StringSegment retrieved2 = value2.As<StringSegment>();
        retrieved2._startIndex.Should().Be(7);
        retrieved2._length.Should().Be(5);
        retrieved2.Value.Should().Be(text);
    }

    [Test]
    public void DefaultStringSegment()
    {
        StringSegment defaultSegment = default;
        Value value = defaultSegment;

        value.Type.Should().Be(typeof(StringSegment));
        value.As<StringSegment>().Should().Be(defaultSegment);
        value.As<StringSegment>().Value.Should().Be(string.Empty);
        value.As<StringSegment>()._startIndex.Should().Be(0);
        value.As<StringSegment>()._length.Should().Be(0);
    }

    [Test]
    public void OutAsObject()
    {
        StringSegment segment = new("Test Segment", 0, 4); // "Test"
        Value value = segment;

        object o = value.As<object>();
        o.GetType().Should().Be(typeof(StringSegment));
        ((StringSegment)o).Should().Be(segment);
        ((StringSegment)o).ToString().Should().Be("Test");
    }

    [Test]
    public void EmptyStringSegment()
    {
        StringSegment emptySegment = new(string.Empty);
        Value value = emptySegment;

        value.Type.Should().Be(typeof(StringSegment));
        value.As<StringSegment>().Should().Be(emptySegment);
        value.As<StringSegment>().IsEmpty.Should().BeTrue();
    }

    [Test]
    public void StringSegmentRoundTrip()
    {
        string original = "This is a test of string segments";
        StringSegment[] segments =
        [
            new StringSegment(original),
            new StringSegment(original, 10, 4),  // "test"
            new StringSegment(original, 0, 4),   // "This"
            new StringSegment(original, 18, 8)   // "segments"
        ];

        foreach (StringSegment segment in segments)
        {
            Value value = segment;
            StringSegment roundTripped = value.As<StringSegment>();

            roundTripped.Value.Should().Be(segment.Value);
            roundTripped._startIndex.Should().Be(segment._startIndex);
            roundTripped._length.Should().Be(segment._length);
            roundTripped.ToString().Should().Be(segment.ToString());
        }
    }
}
