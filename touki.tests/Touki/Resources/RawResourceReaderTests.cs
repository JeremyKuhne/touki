// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Buffers;
using System.Buffers.Binary;
using System.Collections;
using System.Resources;
using System.Resources.Extensions;
using System.Text;

namespace Touki.Resources;

[TestClass]
public class RawResourceReaderTests
{
    // Writes a default-format .resources into memory and returns the bytes.
    private static byte[] Write(Action<ResourceWriter> add)
    {
        MemoryStream stream = new();
        using (ResourceWriter writer = new(stream))
        {
            add(writer);
            writer.Generate();
        }

        return stream.ToArray();
    }

    // Bytes a BinaryWriter (the encoding ResourceWriter uses for primitive values) would produce.
    private static byte[] PrimitiveBytes(Action<System.IO.BinaryWriter> write)
    {
        using MemoryStream stream = new();
        using System.IO.BinaryWriter writer = new(stream);
        write(writer);
        writer.Flush();
        return stream.ToArray();
    }

    [TestMethod]
    public void Constructor_BadMagic_ThrowsArgumentException()
    {
        byte[] bytes = new byte[64];
        Action act = () => _ = new RawResourceReader(bytes);
        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void Constructor_TruncatedHeader_ThrowsBadImageFormatException()
    {
        byte[] valid = Write(w => w.AddResource("a", "b"));
        byte[] truncated = valid.AsSpan(0, 10).ToArray();
        Action act = () => _ = new RawResourceReader(truncated);
        act.Should().Throw<BadImageFormatException>();
    }

    [TestMethod]
    public void Constructor_Version1_ThrowsNotSupportedException()
    {
        byte[] bytes = Write(w => w.AddResource("a", "b"));

        // The format version int follows the ResourceManager header: magic(4) + rmHeaderVersion(4) +
        // numBytesToSkip(4) + numBytesToSkip bytes.
        int numBytesToSkip = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(8, 4));
        int versionOffset = 12 + numBytesToSkip;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(versionOffset, 4), 1);

        Action act = () => _ = new RawResourceReader(bytes);
        act.Should().Throw<NotSupportedException>();
    }

    [TestMethod]
    public void ResourceCount_EmptyFile_ReturnsZero()
    {
        RawResourceReader reader = new(Write(static _ => { }));
        reader.ResourceCount.Should().Be(0);
    }

    [TestMethod]
    public void ResourceCount_MatchesWrittenCount()
    {
        RawResourceReader reader = new(Write(static w =>
        {
            w.AddResource("one", "1");
            w.AddResource("two", "2");
            w.AddResource("three", "3");
        }));

        reader.ResourceCount.Should().Be(3);
    }

    [TestMethod]
    public void TryFindResource_ExistingName_ReturnsTrue()
    {
        RawResourceReader reader = new(Write(static w => w.AddResource("Greeting", "Hello")));

        reader.TryFindResource("Greeting", out ResourceLocation location).Should().BeTrue();
        location.TypeCode.Should().Be(ResourceTypeCode.String);
        location.IsUserType.Should().BeFalse();
    }

    [TestMethod]
    public void TryFindResource_MissingName_ReturnsFalse()
    {
        RawResourceReader reader = new(Write(static w => w.AddResource("Greeting", "Hello")));

        reader.TryFindResource("Absent", out ResourceLocation location).Should().BeFalse();
        location.Should().Be(default(ResourceLocation));
    }

    [TestMethod]
    public void TryFindResource_Span_ReturnsTrue()
    {
        RawResourceReader reader = new(Write(static w => w.AddResource("Greeting", "Hello")));

        ReadOnlySpan<char> name = "Greeting".AsSpan();
        reader.TryFindResource(name, out ResourceLocation location).Should().BeTrue();
        location.Index.Should().BeInRange(0, 0);
    }

    [TestMethod]
    public void TryFindResource_ManyResources_AllFoundWithCorrectContent()
    {
        const int count = 500;
        RawResourceReader reader = new(Write(w =>
        {
            for (int i = 0; i < count; i++)
            {
                w.AddResource($"key_{i}", $"value_{i}");
            }
        }));

        reader.ResourceCount.Should().Be(count);
        for (int i = 0; i < count; i++)
        {
            reader.TryFindResource($"key_{i}", out ResourceLocation location).Should().BeTrue();
            Span<byte> buffer = new byte[location.ByteLength];
            reader.TryGetResourceData(location.Index, buffer, out int written).Should().BeTrue();
            Encoding.UTF8.GetString(buffer[..written]).Should().Be($"value_{i}");
        }
    }

    [TestMethod]
    public void TryGetResourceData_String_ReturnsUtf8Content()
    {
        RawResourceReader reader = new(Write(static w => w.AddResource("s", "caf\u00e9 \ud83d\ude00")));

        reader.TryFindResource("s", out ResourceLocation location).Should().BeTrue();
        location.TypeCode.Should().Be(ResourceTypeCode.String);

        byte[] expected = Encoding.UTF8.GetBytes("caf\u00e9 \ud83d\ude00");
        location.ByteLength.Should().Be(expected.Length);

        Span<byte> buffer = new byte[location.ByteLength];
        reader.TryGetResourceData(location.Index, buffer, out int written).Should().BeTrue();
        buffer[..written].ToArray().Should().Equal(expected);
    }

    [TestMethod]
    public void TryGetResourceData_EmptyString_ReturnsZeroLength()
    {
        RawResourceReader reader = new(Write(static w => w.AddResource("s", "")));

        reader.TryFindResource("s", out ResourceLocation location).Should().BeTrue();
        location.TypeCode.Should().Be(ResourceTypeCode.String);
        location.ByteLength.Should().Be(0);
        reader.TryGetResourceData(location.Index, [], out int written).Should().BeTrue();
        written.Should().Be(0);
    }

    [TestMethod]
    public void TryGetResourceData_Primitives_MatchBinaryWriterEncoding()
    {
        (string Name, object Value, ResourceTypeCode Type, byte[] Expected)[] cases =
        [
            ("bool", true, ResourceTypeCode.Boolean, PrimitiveBytes(w => w.Write(true))),
            ("char", 'Z', ResourceTypeCode.Char, PrimitiveBytes(w => w.Write((ushort)'Z'))),
            ("byte", (byte)200, ResourceTypeCode.Byte, PrimitiveBytes(w => w.Write((byte)200))),
            ("sbyte", (sbyte)-5, ResourceTypeCode.SByte, PrimitiveBytes(w => w.Write((sbyte)-5))),
            ("short", (short)-1234, ResourceTypeCode.Int16, PrimitiveBytes(w => w.Write((short)-1234))),
            ("ushort", (ushort)54321, ResourceTypeCode.UInt16, PrimitiveBytes(w => w.Write((ushort)54321))),
            ("int", 1_000_000, ResourceTypeCode.Int32, PrimitiveBytes(w => w.Write(1_000_000))),
            ("uint", 4_000_000_000u, ResourceTypeCode.UInt32, PrimitiveBytes(w => w.Write(4_000_000_000u))),
            ("long", -9_000_000_000L, ResourceTypeCode.Int64, PrimitiveBytes(w => w.Write(-9_000_000_000L))),
            ("ulong", 18_000_000_000_000_000_000UL, ResourceTypeCode.UInt64, PrimitiveBytes(w => w.Write(18_000_000_000_000_000_000UL))),
            ("float", 3.14159f, ResourceTypeCode.Single, PrimitiveBytes(w => w.Write(3.14159f))),
            ("double", 2.718281828, ResourceTypeCode.Double, PrimitiveBytes(w => w.Write(2.718281828))),
            ("decimal", 12345.6789m, ResourceTypeCode.Decimal, PrimitiveBytes(w => w.Write(12345.6789m))),
            ("datetime", new DateTime(2026, 7, 7, 1, 2, 3, DateTimeKind.Utc), ResourceTypeCode.DateTime, PrimitiveBytes(w => w.Write(new DateTime(2026, 7, 7, 1, 2, 3, DateTimeKind.Utc).ToBinary()))),
            ("timespan", TimeSpan.FromMinutes(90), ResourceTypeCode.TimeSpan, PrimitiveBytes(w => w.Write(TimeSpan.FromMinutes(90).Ticks))),
        ];

        RawResourceReader reader = new(Write(w =>
        {
            foreach ((string name, object value, _, _) in cases)
            {
                w.AddResource(name, value);
            }
        }));

        foreach ((string name, _, ResourceTypeCode type, byte[] expected) in cases)
        {
            reader.TryFindResource(name, out ResourceLocation location).Should().BeTrue($"'{name}' should exist");
            location.TypeCode.Should().Be(type, $"'{name}' type");
            location.ByteLength.Should().Be(expected.Length, $"'{name}' length");

            Span<byte> buffer = new byte[location.ByteLength];
            reader.TryGetResourceData(location.Index, buffer, out int written).Should().BeTrue();
            buffer[..written].ToArray().Should().Equal(expected, $"'{name}' content");
        }
    }

    [TestMethod]
    public void TryGetResourceData_ByteArray_ReturnsRawBytes()
    {
        byte[] payload = [0, 1, 2, 250, 251, 252, 253, 254, 255];
        RawResourceReader reader = new(Write(w => w.AddResource("blob", payload)));

        reader.TryFindResource("blob", out ResourceLocation location).Should().BeTrue();
        location.TypeCode.Should().Be(ResourceTypeCode.ByteArray);
        location.ByteLength.Should().Be(payload.Length);

        Span<byte> buffer = new byte[location.ByteLength];
        reader.TryGetResourceData(location.Index, buffer, out int written).Should().BeTrue();
        buffer[..written].ToArray().Should().Equal(payload);
    }

    [TestMethod]
    public void TryGetResourceData_Stream_ReturnsRawBytes()
    {
        byte[] payload = [.. Enumerable.Range(0, 1000).Select(i => (byte)i)];
        RawResourceReader reader = new(Write(w => w.AddResource("stream", new MemoryStream(payload))));

        reader.TryFindResource("stream", out ResourceLocation location).Should().BeTrue();
        location.TypeCode.Should().Be(ResourceTypeCode.Stream);
        location.ByteLength.Should().Be(payload.Length);

        Span<byte> buffer = new byte[location.ByteLength];
        reader.TryGetResourceData(location.Index, buffer, out int written).Should().BeTrue();
        buffer[..written].ToArray().Should().Equal(payload);
    }

    [TestMethod]
    public void TryGetResourceData_Null_ReturnsZeroLength()
    {
        RawResourceReader reader = new(Write(static w => w.AddResource("nothing", (object?)null)));

        reader.TryFindResource("nothing", out ResourceLocation location).Should().BeTrue();
        location.TypeCode.Should().Be(ResourceTypeCode.Null);
        location.ByteLength.Should().Be(0);
    }

    [TestMethod]
    public void TryGetResourceData_UndersizedDestination_ReturnsFalse()
    {
        RawResourceReader reader = new(Write(static w => w.AddResource("s", "Hello")));

        reader.TryFindResource("s", out ResourceLocation location).Should().BeTrue();
        Span<byte> tooSmall = new byte[location.ByteLength - 1];
        reader.TryGetResourceData(location.Index, tooSmall, out int written).Should().BeFalse();
        written.Should().Be(0);
    }

    [TestMethod]
    public void TryCopyResourceData_String_WritesUtf8ToStream()
    {
        RawResourceReader reader = new(Write(static w => w.AddResource("s", "Hello, streams")));

        reader.TryFindResource("s", out ResourceLocation location).Should().BeTrue();
        using MemoryStream destination = new();
        reader.TryCopyResourceData(location.Index, destination).Should().BeTrue();
        destination.ToArray().Should().Equal(Encoding.UTF8.GetBytes("Hello, streams"));
    }

    [TestMethod]
    public void TryGetResourceName_ReturnsName()
    {
        RawResourceReader reader = new(Write(static w => w.AddResource("Greeting", "Hello")));

        reader.TryFindResource("Greeting", out ResourceLocation location).Should().BeTrue();
        Span<char> buffer = new char[64];
        reader.TryGetResourceName(location.Index, buffer, out int written).Should().BeTrue();
        buffer[..written].ToString().Should().Be("Greeting");
    }

    [TestMethod]
    public void TryGetResourceName_UndersizedDestination_ReturnsFalse()
    {
        RawResourceReader reader = new(Write(static w => w.AddResource("Greeting", "Hello")));

        reader.TryFindResource("Greeting", out ResourceLocation location).Should().BeTrue();
        Span<char> tooSmall = new char[3];
        reader.TryGetResourceName(location.Index, tooSmall, out int written).Should().BeFalse();
        written.Should().Be(0);
    }

    [TestMethod]
    public void GetLocation_InvalidIndex_ThrowsArgumentOutOfRange()
    {
        RawResourceReader reader = new(Write(static w => w.AddResource("a", "b")));

        Action low = () => reader.GetLocation(-1);
        Action high = () => reader.GetLocation(1);
        low.Should().Throw<ArgumentOutOfRangeException>();
        high.Should().Throw<ArgumentOutOfRangeException>();
    }

    [TestMethod]
    public void UserType_ReportsTypeNameAndExposesNoData()
    {
        // A TypeConverter-backed resource switches the file to the DeserializingResourceReader format
        // and stores a serialized user type. We should still read its type code and type name, and any
        // sibling string, while refusing to hand back the serialized value bytes.
        byte[] bytes = WritePreserialized(w =>
        {
            w.AddResource("plain", "readable");
            w.AddResource("point", "10,20", "System.Drawing.Point, System.Drawing.Primitives");
        });

        RawResourceReader reader = new(bytes);

        reader.TryFindResource("plain", out ResourceLocation plain).Should().BeTrue();
        plain.TypeCode.Should().Be(ResourceTypeCode.String);
        Span<byte> plainBuffer = new byte[plain.ByteLength];
        reader.TryGetResourceData(plain.Index, plainBuffer, out int plainWritten).Should().BeTrue();
        Encoding.UTF8.GetString(plainBuffer[..plainWritten]).Should().Be("readable");

        reader.TryFindResource("point", out ResourceLocation point).Should().BeTrue();
        point.IsUserType.Should().BeTrue();
        reader.TryGetResourceData(point.Index, new byte[256], out _).Should().BeFalse();

        Span<char> typeName = new char[256];
        reader.TryGetUserTypeName(point.Index, typeName, out int typeWritten).Should().BeTrue();
        typeName[..typeWritten].ToString().Should().Contain("System.Drawing.Point");
    }

    [TestMethod]
    public void TryGetUserTypeName_NonUserType_ReturnsFalse()
    {
        RawResourceReader reader = new(Write(static w => w.AddResource("s", "Hello")));

        reader.TryFindResource("s", out ResourceLocation location).Should().BeTrue();
        reader.TryGetUserTypeName(location.Index, new char[64], out int written).Should().BeFalse();
        written.Should().Be(0);
    }

    [TestMethod]
    public void Reader_MatchesResourceReaderOracle()
    {
        byte[] bytes = Write(static w =>
        {
            w.AddResource("greeting", "Hello");
            w.AddResource("count", 42);
            w.AddResource("pi", 3.14159);
            w.AddResource("flag", true);
            w.AddResource("empty", "");
            w.AddResource("unicode", "\u00e9\u00e8\u00ea");
        });

        // Oracle names via the runtime reader's enumerator keys (no value deserialization).
        List<string> oracleNames = [];
        using (ResourceReader oracle = new(new MemoryStream(bytes)))
        {
            IDictionaryEnumerator enumerator = oracle.GetEnumerator();
            while (enumerator.MoveNext())
            {
                oracleNames.Add((string)enumerator.Key);
            }
        }

        RawResourceReader reader = new(bytes);
        reader.ResourceCount.Should().Be(oracleNames.Count);

        // Every oracle name is found, and the raw content decodes back to the oracle's typed value.
        using ResourceReader values = new(new MemoryStream(bytes));
        IDictionaryEnumerator valuesEnumerator = values.GetEnumerator();
        while (valuesEnumerator.MoveNext())
        {
            string name = (string)valuesEnumerator.Key;
            object? value = valuesEnumerator.Value;

            reader.TryFindResource(name, out ResourceLocation location).Should().BeTrue($"'{name}' should exist");

            Span<byte> content = new byte[location.ByteLength];
            reader.TryGetResourceData(location.Index, content, out int written).Should().BeTrue();
            ReadOnlySpan<byte> data = content[..written];

            switch (value)
            {
                case string s:
                    location.TypeCode.Should().Be(ResourceTypeCode.String);
                    Encoding.UTF8.GetString(data).Should().Be(s);
                    break;
                case int i:
                    location.TypeCode.Should().Be(ResourceTypeCode.Int32);
                    BinaryPrimitives.ReadInt32LittleEndian(data).Should().Be(i);
                    break;
                case double d:
                    location.TypeCode.Should().Be(ResourceTypeCode.Double);
                    BitConverter.ToDouble(data.ToArray(), 0).Should().Be(d);
                    break;
                case bool b:
                    location.TypeCode.Should().Be(ResourceTypeCode.Boolean);
                    (data[0] != 0).Should().Be(b);
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected oracle value type for '{name}'.");
            }
        }

        // And the reader enumerates the same set of names.
        List<string> readerNames = [];
        for (int i = 0; i < reader.ResourceCount; i++)
        {
            Span<char> nameBuffer = new char[256];
            reader.TryGetResourceName(i, nameBuffer, out int nameWritten).Should().BeTrue();
            readerNames.Add(nameBuffer[..nameWritten].ToString());
        }

        readerNames.Should().BeEquivalentTo(oracleNames);
    }

    [TestMethod]
    public void Reader_OverNativeMemory_MatchesArrayBacking()
    {
        byte[] bytes = Write(static w =>
        {
            w.AddResource("greeting", "Hello");
            w.AddResource("number", 12345);
        });

        using NativeMemoryManager native = new(bytes);
        RawResourceReader arrayReader = new(bytes);
        RawResourceReader nativeReader = new(native.Memory);

        nativeReader.ResourceCount.Should().Be(arrayReader.ResourceCount);

        nativeReader.TryFindResource("greeting", out ResourceLocation location).Should().BeTrue();
        Span<byte> fromNative = new byte[location.ByteLength];
        nativeReader.TryGetResourceData(location.Index, fromNative, out int nativeWritten).Should().BeTrue();

        arrayReader.TryFindResource("greeting", out ResourceLocation arrayLocation).Should().BeTrue();
        Span<byte> fromArray = new byte[arrayLocation.ByteLength];
        arrayReader.TryGetResourceData(arrayLocation.Index, fromArray, out int arrayWritten).Should().BeTrue();

        fromNative[..nativeWritten].ToArray().Should().Equal(fromArray[..arrayWritten].ToArray());

        // TryCopyResourceData must also work when the memory is not array-backed.
        using MemoryStream copy = new();
        nativeReader.TryCopyResourceData(location.Index, copy).Should().BeTrue();
        copy.ToArray().Should().Equal(Encoding.UTF8.GetBytes("Hello"));
    }

    [TestMethod]
    public void CreateFromFile_MapsFileAndReadsWithoutCopying()
    {
        byte[] bytes = Write(static w =>
        {
            w.AddResource("greeting", "Hello");
            w.AddResource("number", 12345);
        });

        using TempFolder folder = new();
        string path = System.IO.Path.Combine(folder.TempPath, "test.resources");
        System.IO.File.WriteAllBytes(path, bytes);

        using RawResourceReader reader = RawResourceReader.CreateFromFile(path);

        reader.ResourceCount.Should().Be(2);
        reader.TryFindResource("greeting", out ResourceLocation location).Should().BeTrue();
        Span<byte> buffer = new byte[location.ByteLength];
        reader.TryGetResourceData(location.Index, buffer, out int written).Should().BeTrue();
        Encoding.UTF8.GetString(buffer[..written]).Should().Be("Hello");
    }

    [TestMethod]
    public void CreateFromFile_NullPath_ThrowsArgumentNullException()
    {
        Action act = () => _ = RawResourceReader.CreateFromFile(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void Dispose_MemoryBackedReader_IsIdempotentNoOp()
    {
        RawResourceReader reader = new(Write(static w => w.AddResource("s", "Hello")));
        reader.Dispose();
        reader.Dispose();

        // The caller still owns the memory, so the reader remains usable after a no-op dispose.
        reader.TryFindResource("s", out ResourceLocation location).Should().BeTrue();
        location.TypeCode.Should().Be(ResourceTypeCode.String);
    }

    private static byte[] WritePreserialized(Action<PreserializedResourceWriter> add)
    {
        MemoryStream stream = new();
        using (PreserializedResourceWriter writer = new(stream))
        {
            add(writer);
            writer.Generate();
        }

        return stream.ToArray();
    }

    private sealed unsafe class NativeMemoryManager : MemoryManager<byte>
    {
        private byte* _pointer;
        private readonly int _length;

        public NativeMemoryManager(ReadOnlySpan<byte> data)
        {
            _length = data.Length;
            _pointer = (byte*)Marshal.AllocHGlobal(_length);
            data.CopyTo(new Span<byte>(_pointer, _length));
        }

        public override Span<byte> GetSpan() => new(_pointer, _length);

        public override MemoryHandle Pin(int elementIndex = 0) => new(_pointer + elementIndex);

        public override void Unpin()
        {
        }

        protected override void Dispose(bool disposing)
        {
            if (_pointer is not null)
            {
                Marshal.FreeHGlobal((nint)_pointer);
                _pointer = null;
            }
        }
    }
}
