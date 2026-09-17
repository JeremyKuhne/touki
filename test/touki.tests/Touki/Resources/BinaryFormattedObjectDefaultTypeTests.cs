// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections;
using System.Formats.Nrbf;
using System.Globalization;
using System.Text;

namespace Touki.Resources;

[TestClass]
public class BinaryFormattedObjectDefaultTypeTests
{
    [TestMethod]
    public void Deserialize_FrameworkIntPtrPayload_ReturnsValue()
    {
        BinaryFormattedObject formatted = BinaryFormattedObjectFixtures.Parse(BinaryFormattedObjectFixtures.IntPtr);

        formatted.Deserialize().Should().Be((nint)42);
    }

    [TestMethod]
    public void Deserialize_FrameworkUIntPtrPayload_ReturnsValue()
    {
        BinaryFormattedObject formatted = BinaryFormattedObjectFixtures.Parse(BinaryFormattedObjectFixtures.UIntPtr);

        formatted.Deserialize().Should().Be((nuint)42);
    }

    [TestMethod]
    public void Deserialize_FrameworkNotSupportedExceptionPayload_ReturnsValue()
    {
        BinaryFormattedObject formatted = BinaryFormattedObjectFixtures.Parse(
            BinaryFormattedObjectFixtures.NotSupportedException);

        object result = formatted.Deserialize();

        result.Should().BeOfType<NotSupportedException>();
        ((NotSupportedException)result).Message.Should().Be("not supported");
    }

    [TestMethod]
    public void Deserialize_FrameworkDecimalPayload_ReturnsValue()
    {
        BinaryFormattedObject formatted = BinaryFormattedObjectFixtures.Parse(BinaryFormattedObjectFixtures.Decimal);

        formatted.Deserialize().Should().Be(12345.6789m);
    }

    [TestMethod]
    public void Deserialize_FrameworkTimeSpanPayload_ReturnsValue()
    {
        BinaryFormattedObject formatted = BinaryFormattedObjectFixtures.Parse(BinaryFormattedObjectFixtures.TimeSpan);

        formatted.Deserialize().Should().Be(TimeSpan.FromMinutes(90));
    }

    [TestMethod]
    public void Deserialize_FrameworkPrimitiveArrayPayload_ReturnsValues()
    {
        BinaryFormattedObject formatted = BinaryFormattedObjectFixtures.Parse(
            BinaryFormattedObjectFixtures.Int32Array);

        object result = formatted.Deserialize();

        result.Should().BeOfType<int[]>();
        ((int[])result).Should().Equal(2, 3, 5, 7);
    }

    [TestMethod]
    [DynamicData(nameof(PrimitiveArrayData))]
    public void Deserialize_FrameworkPrimitiveArrayPayload_ReturnsTypedValues(
        byte primitiveType,
        Array expected)
    {
        using MemoryStream stream = CreatePrimitiveArrayPayload(primitiveType, expected);
        BinaryFormattedObject formatted = new(stream);

        object result = formatted.Deserialize();

        result.Should().BeOfType(expected.GetType());
        ((IEnumerable)result).Cast<object>().Should().Equal(expected.Cast<object>());
    }

    [TestMethod]
    public void Deserialize_FrameworkStringArrayPayload_ReturnsValues()
    {
        BinaryFormattedObject formatted = BinaryFormattedObjectFixtures.Parse(
            BinaryFormattedObjectFixtures.StringArray);

        object result = formatted.Deserialize();

        result.Should().BeOfType<string[]>();
        ((string?[])result).Should().Equal("first", null, "third");
    }

    [TestMethod]
    public void Deserialize_FrameworkArrayListPayload_ReturnsValues()
    {
        BinaryFormattedObject formatted = BinaryFormattedObjectFixtures.Parse(
            BinaryFormattedObjectFixtures.ArrayList);

        object result = formatted.Deserialize();

        result.Should().BeOfType<ArrayList>();
        ((ArrayList)result).Cast<object?>().Should().Equal(1, "two", null);
    }

    [TestMethod]
    public void Deserialize_FrameworkHashtablePayload_ReturnsValues()
    {
        BinaryFormattedObject formatted = BinaryFormattedObjectFixtures.Parse(
            BinaryFormattedObjectFixtures.Hashtable);

        object result = formatted.Deserialize();

        result.Should().BeOfType<Hashtable>();
        Hashtable hashtable = (Hashtable)result;
        hashtable["one"].Should().Be(1);
        hashtable["two"].Should().Be(2);
    }

    public static IEnumerable<object[]> PrimitiveArrayData()
    {
        yield return [(byte)1, new bool[] { true, false, true }];
        yield return [(byte)2, new byte[] { 0, 42, byte.MaxValue }];
        yield return [(byte)3, new char[] { 'A', '\0', '\u03a9' }];
        yield return [(byte)5, new decimal[] { -1.5m, 0m, decimal.MaxValue }];
        yield return [(byte)6, new double[] { -1.5, 0, double.PositiveInfinity }];
        yield return [(byte)7, new short[] { short.MinValue, 0, short.MaxValue }];
        yield return [(byte)8, new int[] { int.MinValue, 0, int.MaxValue }];
        yield return [(byte)9, new long[] { long.MinValue, 0, long.MaxValue }];
        yield return [(byte)10, new sbyte[] { sbyte.MinValue, 0, sbyte.MaxValue }];
        yield return [(byte)11, new float[] { -1.5f, 0, float.PositiveInfinity }];
        yield return [(byte)12, new TimeSpan[] { TimeSpan.FromTicks(-1), TimeSpan.FromDays(2) }];
        yield return [(byte)13, new DateTime[] { new(2000, 1, 2, 3, 4, 5, DateTimeKind.Utc), DateTime.MinValue }];
        yield return [(byte)14, new ushort[] { 0, 42, ushort.MaxValue }];
        yield return [(byte)15, new uint[] { 0, 42, uint.MaxValue }];
        yield return [(byte)16, new ulong[] { 0, 42, ulong.MaxValue }];
    }

    private static MemoryStream CreatePrimitiveArrayPayload(byte primitiveType, Array values)
    {
        MemoryStream stream = new();
        using (System.IO.BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((byte)SerializationRecordType.SerializedStreamHeader);
            writer.Write(1);
            writer.Write(-1);
            writer.Write(1);
            writer.Write(0);

            writer.Write((byte)SerializationRecordType.ArraySinglePrimitive);
            writer.Write(1);
            writer.Write(values.Length);
            writer.Write(primitiveType);

            foreach (object value in values)
            {
                WritePrimitiveValue(writer, primitiveType, value);
            }

            writer.Write((byte)SerializationRecordType.MessageEnd);
        }

        stream.Position = 0;
        return stream;
    }

    private static void WritePrimitiveValue(System.IO.BinaryWriter writer, byte primitiveType, object value)
    {
        switch (primitiveType)
        {
            case 1:
                writer.Write((bool)value);
                break;
            case 2:
                writer.Write((byte)value);
                break;
            case 3:
                writer.Write((char)value);
                break;
            case 5:
                writer.Write(((decimal)value).ToString(CultureInfo.InvariantCulture));
                break;
            case 6:
                writer.Write((double)value);
                break;
            case 7:
                writer.Write((short)value);
                break;
            case 8:
                writer.Write((int)value);
                break;
            case 9:
                writer.Write((long)value);
                break;
            case 10:
                writer.Write((sbyte)value);
                break;
            case 11:
                writer.Write((float)value);
                break;
            case 12:
                writer.Write(((TimeSpan)value).Ticks);
                break;
            case 13:
                writer.Write(((DateTime)value).ToBinary());
                break;
            case 14:
                writer.Write((ushort)value);
                break;
            case 15:
                writer.Write((uint)value);
                break;
            case 16:
                writer.Write((ulong)value);
                break;
            default:
                throw new InvalidOperationException($"Unknown primitive type '{primitiveType}'.");
        }
    }
}