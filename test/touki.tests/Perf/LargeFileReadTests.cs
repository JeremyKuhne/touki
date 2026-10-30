// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Buffers.Binary;
using FileStream = System.IO.FileStream;

namespace touki.perf;

[TestClass]
public partial class LargeFileReadTests
{
    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(65535)]
    [DataRow(65536)]
    [DataRow(65537)]
    [DataRow(1048575)]
    [DataRow(1048576)]
    [DataRow(1048577)]
    public void Create_BoundaryLengths_AllReferencesReturnExactPattern(int length)
    {
        using TempFolder folder = new();
        string path = Path.Join(folder.TempPath, "pattern.bin");
        LargeFileReadPattern.Create(path, length, blocks: false);
        foreach (LargeFileReadReference reference in AvailableReferences())
        {
            foreach (LargeFileReadScenario scenario in Enum.GetValues(typeof(LargeFileReadScenario)))
            {
                using LargeFileReadReferenceReader reader = new(path, reference, scenario);
                byte[] actual = new byte[7777];
                byte[] expected = new byte[actual.Length];
                for (long offset = 0; offset < length;)
                {
                    int count = (int)Math.Min(actual.Length, length - offset);
                    reader.ReadExactly(offset, actual, count);
                    LargeFileReadPattern.Fill(expected.AsSpan(0, count), offset, length, blocks: false);
                    actual.AsSpan(0, count).SequenceEqual(expected.AsSpan(0, count)).Should().BeTrue();
                    offset += count;
                }
            }
        }
    }

    [TestMethod]
    public void Fill_OffsetAboveFourGiB_EncodesFullOffset()
    {
        byte[] buffer = new byte[16];
        long offset = (1L << 32) + 32;
        LargeFileReadPattern.Fill(buffer, offset, offset + buffer.Length, blocks: false);
        BinaryPrimitives.ReadInt64LittleEndian(buffer).Should().Be(offset);
        BinaryPrimitives.ReadInt64LittleEndian(buffer.AsSpan(8)).Should().Be(offset + 8);
    }

    [TestMethod]
    public void Fill_BlockHeaderAboveFourGiB_EncodesFinalPartialBlock()
    {
        byte[] buffer = new byte[16];
        long offset = (1L << 32) + LargeFileReadPattern.BlockSize;
        LargeFileReadPattern.Fill(buffer, offset, offset + 19, blocks: true);
        BinaryPrimitives.ReadInt64LittleEndian(buffer).Should().Be(offset);
        BinaryPrimitives.ReadInt64LittleEndian(buffer.AsSpan(8)).Should().Be(19);
    }

    [TestMethod]
    public void Fill_PartialWords_MatchesIndependentByteOracle()
    {
        byte[] buffer = new byte[31];
        long offset = (1L << 32) + 3;
        LargeFileReadPattern.Fill(buffer, offset, offset + buffer.Length, blocks: false);
        for (int i = 0; i < buffer.Length; i++)
        {
            long position = offset + i;
            byte expected = (byte)((position & ~7L) >> ((int)(position & 7) * 8));
            buffer[i].Should().Be(expected);
        }
    }

    [TestMethod]
    public void Create_BlockPattern_StoresOffsetAndActualBlockLength()
    {
        using TempFolder folder = new();
        string path = Path.Join(folder.TempPath, "blocks.bin");
        int length = LargeFileReadPattern.BlockSize + 19;
        LargeFileReadPattern.Create(path, length, blocks: true);
        LargeFileReadPattern.Verify(path, blocks: true);
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read);
        byte[] header = new byte[16];
        stream.Read(header, 0, header.Length).Should().Be(header.Length);
        BinaryPrimitives.ReadInt64LittleEndian(header).Should().Be(0);
        BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(8)).Should().Be(LargeFileReadPattern.BlockSize);
        stream.Position = LargeFileReadPattern.BlockSize;
        stream.Read(header, 0, header.Length).Should().Be(header.Length);
        BinaryPrimitives.ReadInt64LittleEndian(header).Should().Be(LargeFileReadPattern.BlockSize);
        BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(8)).Should().Be(19);
    }

    [TestMethod]
    public void Create_ExistingPath_DoesNotOverwrite()
    {
        using TempFolder folder = new();
        string path = Path.Join(folder.TempPath, "existing.bin");
        File.WriteAllBytes(path, [42]);
        Action action = () => LargeFileReadPattern.Create(path, 100, blocks: false);
        action.Should().Throw<IOException>();
        File.ReadAllBytes(path).Should().Equal(42);
    }

    [TestMethod]
    public void Verify_CorruptedByte_RejectsFile()
    {
        using TempFolder folder = new();
        string path = Path.Join(folder.TempPath, "pattern.bin");
        LargeFileReadPattern.Create(path, 100, blocks: false);
        using (FileStream stream = new(path, FileMode.Open, FileAccess.Write))
        {
            stream.Position = 17;
            stream.WriteByte(255);
        }

        Action action = () => LargeFileReadPattern.Verify(path, blocks: false);
        action.Should().Throw<InvalidDataException>();
    }

    [TestMethod]
    public void ReadExactly_BackwardAndCrossWindowReads_ReturnsExactBytes()
    {
        using TempFolder folder = new();
        string path = Path.Join(folder.TempPath, "pattern.bin");
        int length = 3 * LargeFileReadPattern.WindowSize + 1;
        LargeFileReadPattern.Create(path, length, blocks: false);
        long[] offsets = [1048503, 0, 2097151, 1048576, length - 151];
        foreach (LargeFileReadReference reference in AvailableReferences())
        {
            using LargeFileReadReferenceReader reader = new(path, reference, LargeFileReadScenario.ScriptedSequential);
            byte[] actual = new byte[151];
            byte[] expected = new byte[actual.Length];
            foreach (long offset in offsets)
            {
                reader.ReadExactly(offset, actual, actual.Length);
                LargeFileReadPattern.Fill(expected, offset, length, blocks: false);
                actual.Should().Equal(expected);
            }
        }
    }

    [TestMethod]
    public void ReadExactly_OutOfRange_ThrowsWithoutIssuingRead()
    {
        using TempFolder folder = new();
        string path = Path.Join(folder.TempPath, "pattern.bin");
        LargeFileReadPattern.Create(path, 8, blocks: false);
        using LargeFileReadReferenceReader reader = new(
            path,
            LargeFileReadReference.TunedFileStream,
            LargeFileReadScenario.Sequential);

        Action action = () => reader.ReadExactly(long.MaxValue, new byte[1], 1);
        action.Should().Throw<EndOfStreamException>();
        reader.RequestsIssued.Should().Be(0);
    }

    [TestMethod]
    public void Run_AllScenarios_ReferencesAgree()
    {
        using TempFolder folder = new();
        string path = Path.Join(folder.TempPath, "pattern.bin");
        LargeFileReadPattern.Create(path, 2 * LargeFileReadPattern.WindowSize + 133, blocks: true);
        foreach (LargeFileReadScenario scenario in Enum.GetValues(typeof(LargeFileReadScenario)))
        {
            using LargeFileReadReferenceReader tuned = new(path, LargeFileReadReference.TunedFileStream, scenario);
            long expected = LargeFileReadReplay.Run(tuned, scenario);
            foreach (LargeFileReadReference reference in AvailableReferences())
            {
                using LargeFileReadReferenceReader reader = new(path, reference, scenario);
                LargeFileReadReplay.Run(reader, scenario).Should().Be(expected);
            }
        }
    }

    private static IEnumerable<LargeFileReadReference> AvailableReferences()
    {
        foreach (LargeFileReadReference reference in Enum.GetValues(typeof(LargeFileReadReference)))
        {
#if NET
            bool needsReopen = reference is LargeFileReadReference.LargeFileStreamDualTransport
                or LargeFileReadReference.LargeFileStreamDualTimedTransport;

            if (needsReopen && !OperatingSystem.IsWindowsVersionAtLeast(10))
            {
                continue;
            }
#endif

            yield return reference;
        }
    }
}
