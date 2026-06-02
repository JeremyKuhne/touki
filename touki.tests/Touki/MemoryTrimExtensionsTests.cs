// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public class MemoryTrimExtensionsTests
{
    // -----------------------------------------------------------------------
    //  Memory<T> single-element trim
    // -----------------------------------------------------------------------

    [Test]
    public void Trim_Element_Memory_RemovesBothEnds()
    {
        Memory<int> memory = new int[] { 0, 1, 2, 3, 0, 0 };
        Memory<int> trimmed = memory.Trim(0);
        trimmed.ToArray().Should().Equal(1, 2, 3);
    }

    [Test]
    public void TrimStart_Element_Memory_RemovesLeading()
    {
        Memory<int> memory = new int[] { 0, 0, 1, 2, 0 };
        memory.TrimStart(0).ToArray().Should().Equal(1, 2, 0);
    }

    [Test]
    public void TrimEnd_Element_Memory_RemovesTrailing()
    {
        Memory<int> memory = new int[] { 0, 1, 2, 0, 0 };
        memory.TrimEnd(0).ToArray().Should().Equal(0, 1, 2);
    }

    [Test]
    public void Trim_Element_Memory_AllMatch_ReturnsEmpty()
    {
        Memory<int> memory = new int[] { 0, 0, 0 };
        memory.Trim(0).Length.Should().Be(0);
    }

    [Test]
    public void Trim_Element_Memory_NoMatch_ReturnsAsIs()
    {
        Memory<int> memory = new int[] { 1, 2, 3 };
        memory.Trim(0).ToArray().Should().Equal(1, 2, 3);
    }

    [Test]
    public void Trim_Element_Memory_Empty_ReturnsEmpty()
    {
        Memory<int>.Empty.Trim(0).Length.Should().Be(0);
    }

    [Test]
    public void Trim_Element_Memory_NullableReference()
    {
        Memory<string?> memory = new string?[] { null, "a", "b", null };
        memory.Trim((string?)null).ToArray().Should().Equal("a", "b");
    }

    // -----------------------------------------------------------------------
    //  Memory<T> set trim
    // -----------------------------------------------------------------------

    [Test]
    public void Trim_Set_Memory_RemovesAnyOfSet()
    {
        Memory<int> memory = new int[] { 0, 1, 5, 2, 1, 0 };
        Memory<int> trimmed = memory.Trim([0, 1]);
        trimmed.ToArray().Should().Equal(5, 2);
    }

    [Test]
    public void TrimStart_Set_Memory_EmptySet_ReturnsAsIs()
    {
        Memory<int> memory = new int[] { 1, 2, 3 };
        memory.TrimStart([]).ToArray().Should().Equal(1, 2, 3);
    }

    [Test]
    public void TrimEnd_Set_Memory_RemovesTrailing()
    {
        Memory<int> memory = new int[] { 5, 1, 2, 0, 1 };
        memory.TrimEnd([0, 1]).ToArray().Should().Equal(5, 1, 2);
    }

    // -----------------------------------------------------------------------
    //  ReadOnlyMemory<T>
    // -----------------------------------------------------------------------

    [Test]
    public void Trim_Element_ReadOnlyMemory_RemovesBothEnds()
    {
        ReadOnlyMemory<int> memory = new int[] { 0, 1, 2, 0 };
        memory.Trim(0).ToArray().Should().Equal(1, 2);
    }

    [Test]
    public void TrimStart_Element_ReadOnlyMemory_RemovesLeading()
    {
        ReadOnlyMemory<int> memory = new int[] { 0, 0, 1, 2 };
        memory.TrimStart(0).ToArray().Should().Equal(1, 2);
    }

    [Test]
    public void TrimEnd_Set_ReadOnlyMemory_RemovesTrailing()
    {
        ReadOnlyMemory<int> memory = new int[] { 5, 1, 2, 0, 1 };
        memory.TrimEnd([0, 1]).ToArray().Should().Equal(5, 1, 2);
    }

    // -----------------------------------------------------------------------
    //  Memory<char> / ROM<char> whitespace trim
    // -----------------------------------------------------------------------

    [Test]
    public void Trim_Memory_Char_RemovesWhitespace()
    {
        Memory<char> memory = "  hello  ".ToCharArray();
        memory.Trim().ToArray().Should().Equal('h', 'e', 'l', 'l', 'o');
    }

    [Test]
    public void TrimStart_Memory_Char_RemovesLeading()
    {
        Memory<char> memory = "  hi".ToCharArray();
        memory.TrimStart().ToArray().Should().Equal('h', 'i');
    }

    [Test]
    public void TrimEnd_Memory_Char_RemovesTrailing()
    {
        Memory<char> memory = "hi  ".ToCharArray();
        memory.TrimEnd().ToArray().Should().Equal('h', 'i');
    }

    [Test]
    public void Trim_ReadOnlyMemory_Char_RemovesWhitespace()
    {
        ReadOnlyMemory<char> memory = "  world  ".AsMemory();
        memory.Trim().ToString().Should().Be("world");
    }

    [Test]
    public void Trim_ReadOnlyMemory_Char_AllWhitespace_ReturnsEmpty()
    {
        ReadOnlyMemory<char> memory = "   ".AsMemory();
        memory.Trim().Length.Should().Be(0);
    }

    [Test]
    public void Trim_ReadOnlyMemory_Char_UnicodeWhitespace()
    {
        ReadOnlyMemory<char> memory = "\u00A0hi\u3000".AsMemory();
        memory.Trim().ToString().Should().Be("hi");
    }
}
