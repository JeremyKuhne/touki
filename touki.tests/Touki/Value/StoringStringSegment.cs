// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.ValueTests;

public class StoringStringSegment
{
    public static TheoryData<StringSegment> StringSegmentData => new()
    {
        { new StringSegment("Hello, World!") },
        { new StringSegment("Hello, World!", 7, 5) },
        { new StringSegment(string.Empty) },
        { default(StringSegment) }
    };

    [Theory]
    [MemberData(nameof(StringSegmentData))]
    public void StringSegmentImplicit(StringSegment segment)
    {
        Value value = segment;
        Assert.Equal(segment, value.As<StringSegment>());
        Assert.Equal(typeof(StringSegment), value.Type);
    }

    [Theory]
    [MemberData(nameof(StringSegmentData))]
    public void StringSegmentCreate(StringSegment segment)
    {
        Value value;
        using (MemoryWatch.Create)
        {
            value = Value.Create(segment);
        }

        Assert.Equal(segment, value.As<StringSegment>());
        Assert.Equal(typeof(StringSegment), value.Type);
    }

    [Theory]
    [MemberData(nameof(StringSegmentData))]
    public void StringSegmentInOut(StringSegment segment)
    {
        Value value = segment;
        bool success = value.TryGetValue(out StringSegment result);
        Assert.True(success);
        Assert.Equal(segment, result);

        Assert.Equal(segment, value.As<StringSegment>());
        Assert.Equal(segment, (StringSegment)value);
    }

    [Fact]
    public void NestedSegments()
    {
        string text = "Hello, World! How are you?";
        StringSegment fullSegment = new(text);
        StringSegment worldSegment = new(text, 7, 5); // "World"
        StringSegment howSegment = new(text, 14, 3);  // "How"

        Value value1 = fullSegment;
        Value value2 = worldSegment;
        Value value3 = howSegment;

        Assert.Equal(fullSegment, value1.As<StringSegment>());
        Assert.Equal(worldSegment, value2.As<StringSegment>());
        Assert.Equal(howSegment, value3.As<StringSegment>());

        // Ensure the segments maintain their correct positions
        Assert.Equal("Hello, World! How are you?", fullSegment.ToString());
        Assert.Equal("World", worldSegment.ToString());
        Assert.Equal("How", howSegment.ToString());

        // Verify the retrieved segments have the same properties
        StringSegment retrieved2 = value2.As<StringSegment>();
        Assert.Equal(7, retrieved2._startIndex);
        Assert.Equal(5, retrieved2._length);
        Assert.Equal(text, retrieved2.Value);
    }

    [Fact]
    public void DefaultStringSegment()
    {
        StringSegment defaultSegment = default;
        Value value = defaultSegment;

        Assert.Equal(typeof(StringSegment), value.Type);
        Assert.Equal(defaultSegment, value.As<StringSegment>());
        Assert.Equal(string.Empty, value.As<StringSegment>().Value);
        Assert.Equal(0, value.As<StringSegment>()._startIndex);
        Assert.Equal(0, value.As<StringSegment>()._length);
    }

    [Fact]
    public void OutAsObject()
    {
        StringSegment segment = new("Test Segment", 0, 4); // "Test"
        Value value = segment;

        object o = value.As<object>();
        Assert.Equal(typeof(StringSegment), o.GetType());
        Assert.Equal(segment, (StringSegment)o);
        Assert.Equal("Test", ((StringSegment)o).ToString());
    }

    [Fact]
    public void EmptyStringSegment()
    {
        StringSegment emptySegment = new(string.Empty);
        Value value = emptySegment;

        Assert.Equal(typeof(StringSegment), value.Type);
        Assert.Equal(emptySegment, value.As<StringSegment>());
        Assert.True(value.As<StringSegment>().IsEmpty);
    }

    [Fact]
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

            Assert.Equal(segment.Value, roundTripped.Value);
            Assert.Equal(segment._startIndex, roundTripped._startIndex);
            Assert.Equal(segment._length, roundTripped._length);
            Assert.Equal(segment.ToString(), roundTripped.ToString());
        }
    }
}
