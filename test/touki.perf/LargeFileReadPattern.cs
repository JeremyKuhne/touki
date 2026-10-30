// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Buffers.Binary;
using FileStream = System.IO.FileStream;
#if NETFRAMEWORK
#pragma warning disable IDE0005 // Also linked into touki.tests, which globally imports System.IO.
using System.IO;
#pragma warning restore IDE0005
#endif

namespace touki.perf;

internal static class LargeFileReadPattern
{
    internal const int BlockSize = 64 * 1024;
    internal const int WindowSize = 1024 * 1024;

    internal static void Fill(Span<byte> destination, long fileOffset, long length, bool blocks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fileOffset);
        ArgumentOutOfRangeException.ThrowIfLessThan(length, fileOffset);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((long)destination.Length, length - fileOffset);

        Span<byte> word = stackalloc byte[sizeof(long)];
        while (!destination.IsEmpty)
        {
            long wordOffset = fileOffset & ~7L;
            long value = blocks && wordOffset % BlockSize == sizeof(long)
                ? Math.Min(BlockSize, length - wordOffset + sizeof(long))
                : wordOffset;

            BinaryPrimitives.WriteInt64LittleEndian(word, value);
            int skip = (int)(fileOffset - wordOffset);
            int count = Math.Min(sizeof(long) - skip, destination.Length);
            word.Slice(skip, count).CopyTo(destination);
            destination = destination[count..];
            fileOffset += count;
        }
    }

    internal static void Create(string path, long length, bool blocks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        byte[] buffer = new byte[WindowSize];
        using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 1);
        for (long offset = 0; offset < length;)
        {
            int count = (int)Math.Min(buffer.Length, length - offset);
            Fill(buffer.AsSpan(0, count), offset, length, blocks);
            stream.Write(buffer, 0, count);
            offset += count;
        }

        stream.Flush(flushToDisk: true);
    }

    internal static void Verify(
        string path,
        bool blocks,
        LargeFileReadReference reference = LargeFileReadReference.TunedFileStream)
    {
        byte[] actual = new byte[WindowSize];
        byte[] expected = new byte[WindowSize];
        using LargeFileReadReferenceReader reader = new(path, reference, LargeFileReadScenario.Sequential);
        long length = reader.Length;
        for (long offset = 0; offset < length;)
        {
            int count = (int)Math.Min(actual.Length, length - offset);
            reader.ReadExactly(offset, actual, count);
            Fill(expected.AsSpan(0, count), offset, length, blocks);
            if (!actual.AsSpan(0, count).SequenceEqual(expected.AsSpan(0, count)))
            {
                throw new InvalidDataException($"Pattern mismatch in range beginning at {offset}.");
            }

            offset += count;
        }
    }
}
