// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public class StreamExtensionsTests
{
    [Fact]
    public void StreamExtensions_Read_Write_ArraySegment()
    {
        using MemoryStream memory = new();
        byte[] data = [1, 2, 3, 4, 5];

        // Write using ArraySegment
        memory.Write(new ArraySegment<byte>(data, 1, 3));
        memory.Position = 0;

        byte[] readBuffer = new byte[3];
        int read = memory.Read(new ArraySegment<byte>(readBuffer));
        read.Should().Be(3);
        readBuffer.Should().BeEquivalentTo([2, 3, 4]);
    }

    [Fact]
    public async Task StreamExtensions_ReadAsync_WriteAsync_ArraySegment()
    {
        using MemoryStream memory = new();
        byte[] data = [6, 7, 8, 9, 10];

        // Write asynchronously using ArraySegment
        await memory.WriteAsync(new ArraySegment<byte>(data, 2, 2));
        memory.Position = 0;

        byte[] readBuffer = new byte[2];
        int read = await memory.ReadAsync(new ArraySegment<byte>(readBuffer));
        read.Should().Be(2);
        readBuffer.Should().BeEquivalentTo([8, 9]);
    }

    [Fact]
    public void StreamExtensions_DefaultSegment_IsIgnored()
    {
        using MemoryStream memory = new();

        memory.Write(default);
        memory.Length.Should().Be(0);

        byte[] data = [1, 2, 3];
        memory.Write(data, 0, data.Length);
        memory.Position = 0;

        long initial = memory.Position;
        int read = memory.Read(new ArraySegment<byte>());
        read.Should().Be(0);
        memory.Position.Should().Be(initial);
    }

    [Fact]
    public async Task StreamExtensions_DefaultSegmentAsync_IsIgnored()
    {
        using MemoryStream memory = new();

        await memory.WriteAsync(new ArraySegment<byte>());
        memory.Length.Should().Be(0);

        byte[] data = [4, 5];
        await memory.WriteAsync(new ArraySegment<byte>(data));

        memory.Position = 0;

        long initial = memory.Position;
        int read = await memory.ReadAsync(new ArraySegment<byte>());
        read.Should().Be(0);
        memory.Position.Should().Be(initial);
    }
}
