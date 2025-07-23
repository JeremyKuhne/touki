// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.IO;

namespace Touki.Docs;

public class SampleTests
{
    [Fact]
    public void StringSegment_Indexing()
    {
        string csv = "apple,banana,cherry";
        StringSegment full = new(csv);
        int comma = full.IndexOf(',');
        StringSegment first = full[..comma];

        first.Should().Be("apple");

        List<string> segments = [];

        StringSegment right = full;
        while (right.TrySplit(',', out StringSegment left, out right))
        {
            // left will be "apple", "banana", "cherry" in each iteration
            segments.Add(left.ToString());
        }

        segments.Should().BeEquivalentTo(["apple", "banana", "cherry"]);
    }

    [Fact]
    public void Value_Demo()
    {
        Value[] args = [1, 2.5, "three"];
        string fmt = "{0} - {1} - {2}";
        string result = Strings.Format(fmt, args);
        result.Should().Be("1 - 2.5 - three");
    }

    [Fact]
    public void StreamExtension()
    {
        string name = "Name";
        int version = 12;

        using MemoryStream stream = new();
        stream.WriteFormatted($"Library: {name}, Version: {version}");
    }
}
