// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Buffers.Binary;
using System.Resources;

namespace Touki.Resources;

[TestClass]
public class StreamStringResourceReaderTests
{
    private const int MagicNumber = unchecked((int)0xBEEFCACE);

    private static byte[] WriteResources()
    {
        using MemoryStream stream = new();
        using ResourceWriter writer = new(stream);
        writer.AddResource("Greeting", "Hello");
        writer.Generate();
        return stream.ToArray();
    }

    private static byte[] WriteHeader(int resourceCount)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        writer.Write(MagicNumber);
        writer.Write(1);
        writer.Write(0);
        writer.Write(2);
        writer.Write(resourceCount);
        writer.Write(0);
        return stream.ToArray();
    }

    [TestMethod]
    public void Constructor_IndexDoesNotFitStream_ThrowsBadImageFormatExceptionWithoutAllocatingIndex()
    {
        using StringResourceManagerTestStream stream = new(
            WriteHeader(resourceCount: int.MaxValue),
            maximumReadSize: 3,
            canSeek: true);

        Action action = () => _ = new StreamStringResourceReader(stream);

        action.Should().Throw<BadImageFormatException>();
        stream.IsDisposed.Should().BeTrue();
    }

    [TestMethod]
    public void Constructor_NamePositionOutsideNameSection_ThrowsBadImageFormatException()
    {
        byte[] resources = WriteResources();
        (int namePositionOffset, int nameSectionOffset, int dataSectionOffset, _) = GetOffsets(resources);
        BinaryPrimitives.WriteInt32LittleEndian(
            resources.AsSpan(namePositionOffset),
            dataSectionOffset - nameSectionOffset);
        using StringResourceManagerTestStream stream = new(
            resources,
            maximumReadSize: 3,
            canSeek: true);

        Action action = () => _ = new StreamStringResourceReader(stream);

        action.Should().Throw<BadImageFormatException>();
        stream.IsDisposed.Should().BeTrue();
    }

    [TestMethod]
    public void GetResourceName_DeclaredLengthExceedsRemainingBytes_ThrowsBadImageFormatException()
    {
        byte[] resources = WriteResources();
        (_, int nameSectionOffset, _, int namePosition) = GetOffsets(resources);
        resources[nameSectionOffset + namePosition] = 0xFE;
        resources[nameSectionOffset + namePosition + 1] = 0x7F;
        using StringResourceManagerTestStream stream = new(
            resources,
            maximumReadSize: 3,
            canSeek: true);
        using StreamStringResourceReader reader = new(stream);

        Action action = () => reader.GetResourceName(0);

        action.Should().Throw<BadImageFormatException>();
    }

    [TestMethod]
    public void ReadMembers_ValidStringResource_ReturnExpectedValues()
    {
        byte[] resources = WriteResources();
        using StringResourceManagerTestStream stream = new(
            resources,
            maximumReadSize: 3,
            canSeek: true);
        using StreamStringResourceReader reader = new(stream);

        reader.ResourceCount.Should().Be(1);
        reader.GetResourceName(0).Should().Be("Greeting");
        reader.GetResourceTypeCode(0).Should().Be(ResourceTypeCode.String);
        reader.GetString(0).Should().Be("Hello");
        reader.Lookup("Missing", out string? value).Should().Be(StringResourceLookupKind.Missing);
        value.Should().BeNull();
    }

    private static (int NamePositionOffset, int NameSectionOffset, int DataSectionOffset, int NamePosition)
        GetOffsets(byte[] resources)
    {
        using MemoryStream stream = new(resources, writable: false);
        using BinaryReader reader = new(stream);
        reader.ReadInt32().Should().Be(MagicNumber);
        _ = reader.ReadInt32();
        int bytesToSkip = reader.ReadInt32();
        stream.Position += bytesToSkip;
        reader.ReadInt32().Should().Be(2);
        int resourceCount = reader.ReadInt32();
        resourceCount.Should().Be(1);
        int typeCount = reader.ReadInt32();
        for (int i = 0; i < typeCount; i++)
        {
            _ = reader.ReadString();
        }

        stream.Position = (stream.Position + 7) & ~7;
        stream.Position += resourceCount * sizeof(int);
        int namePositionOffset = (int)stream.Position;
        int namePosition = reader.ReadInt32();
        int dataSectionOffset = reader.ReadInt32();
        int nameSectionOffset = (int)stream.Position;
        return (namePositionOffset, nameSectionOffset, dataSectionOffset, namePosition);
    }
}
