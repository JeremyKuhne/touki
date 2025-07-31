// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Text;

/// <summary>
/// Tests for ValueStringBuilder enum formatting with all integer backing types.
/// </summary>
public unsafe class ValueStringBuilderEnumTests
{
    // Enum definitions for all integer backing types
    private enum ByteBackedEnum : byte
    {
        Zero = 0,
        Max = byte.MaxValue,
        Value42 = 42
    }

    private enum SByteBackedEnum : sbyte
    {
        Min = sbyte.MinValue,
        Negative = -1,
        Zero = 0,
        Positive = 1,
        Max = sbyte.MaxValue
    }

    private enum Int16BackedEnum : short
    {
        Min = short.MinValue,
        Negative = -1,
        Zero = 0,
        Positive = 1,
        Max = short.MaxValue
    }

    private enum UInt16BackedEnum : ushort
    {
        Zero = 0,
        Value = 1,
        Max = ushort.MaxValue
    }

    private enum Int32BackedEnum : int
    {
        Min = int.MinValue,
        Negative = -1,
        Zero = 0,
        Positive = 1,
        Max = int.MaxValue
    }

    private enum UInt32BackedEnum : uint
    {
        Zero = 0,
        Value = 1,
        Max = uint.MaxValue
    }

    private enum Int64BackedEnum : long
    {
        Min = long.MinValue,
        Negative = -1,
        Zero = 0,
        Positive = 1,
        Max = long.MaxValue
    }

    private enum UInt64BackedEnum : ulong
    {
        Zero = 0,
        Value = 1,
        Max = ulong.MaxValue
    }

    // Flags enums for all integer backing types
    [Flags]
    private enum ByteFlagsEnum : byte
    {
        None = 0,
        Flag1 = 1,
        Flag2 = 2,
        Flag4 = 4,
        All = Flag1 | Flag2 | Flag4
    }

    [Flags]
    private enum SByteFlagsEnum : sbyte
    {
        None = 0,
        Flag1 = 1,
        Flag2 = 2,
        Flag4 = 4,
        All = Flag1 | Flag2 | Flag4
    }

    [Flags]
    private enum Int16FlagsEnum : short
    {
        None = 0,
        Flag1 = 1,
        Flag2 = 2,
        Flag4 = 4,
        All = Flag1 | Flag2 | Flag4
    }

    [Flags]
    private enum UInt16FlagsEnum : ushort
    {
        None = 0,
        Flag1 = 1,
        Flag2 = 2,
        Flag4 = 4,
        All = Flag1 | Flag2 | Flag4
    }

    [Flags]
    private enum Int32FlagsEnum : int
    {
        None = 0,
        Flag1 = 1,
        Flag2 = 2,
        Flag4 = 4,
        All = Flag1 | Flag2 | Flag4
    }

    [Flags]
    private enum UInt32FlagsEnum : uint
    {
        None = 0,
        Flag1 = 1,
        Flag2 = 2,
        Flag4 = 4,
        All = Flag1 | Flag2 | Flag4
    }

    [Flags]
    private enum Int64FlagsEnum : long
    {
        None = 0,
        Flag1 = 1,
        Flag2 = 2,
        Flag4 = 4,
        All = Flag1 | Flag2 | Flag4
    }

    [Flags]
    private enum UInt64FlagsEnum : ulong
    {
        None = 0,
        Flag1 = 1,
        Flag2 = 2,
        Flag4 = 4,
        All = Flag1 | Flag2 | Flag4
    }

    [Fact]
    public void AppendFormat_EnumBackedByAllIntegerTypes()
    {
        using ValueStringBuilder builder = new(stackalloc char[200]);

        // Test byte-backed enum
        builder.AppendFormat("Byte: {0}", ByteBackedEnum.Max);
        builder.ToString().Should().Be("Byte: Max");
        builder.Clear();

        // Test sbyte-backed enum with negative value
        builder.AppendFormat("SByte: {0}", SByteBackedEnum.Negative);
        builder.ToString().Should().Be("SByte: Negative");
        builder.Clear();

        // Test short-backed enum
        builder.AppendFormat("Short: {0}", Int16BackedEnum.Min);
        builder.ToString().Should().Be("Short: Min");
        builder.Clear();

        // Test ushort-backed enum via Value wrapper
        Value ushortValue = Value.Create(UInt16BackedEnum.Max);
        builder.AppendFormat("UShort: {0}", ushortValue);
        builder.ToString().Should().Be("UShort: Max");
        builder.Clear();

        // Test int-backed enum
        builder.AppendFormat("Int: {0}", Int32BackedEnum.Max);
        builder.ToString().Should().Be("Int: Max");
        builder.Clear();

        // Test uint-backed enum via Value wrapper
        Value uintValue = Value.Create(UInt32BackedEnum.Max);
        builder.AppendFormat("UInt: {0}", uintValue);
        builder.ToString().Should().Be("UInt: Max");
        builder.Clear();

        // Test long-backed enum
        builder.AppendFormat("Long: {0}", Int64BackedEnum.Min);
        builder.ToString().Should().Be("Long: Min");
        builder.Clear();

        // Test ulong-backed enum via Value wrapper
        Value ulongValue = Value.Create(UInt64BackedEnum.Max);
        builder.AppendFormat("ULong: {0}", ulongValue);
        builder.ToString().Should().Be("ULong: Max");
    }

    [Fact]
    public void AppendFormat_UndefinedEnumValues()
    {
        using ValueStringBuilder builder = new(stackalloc char[200]);

        // Test undefined positive values
        ByteBackedEnum undefinedByte = (ByteBackedEnum)100;
        builder.AppendFormat("UndefinedByte: {0}", undefinedByte);
        builder.ToString().Should().Be("UndefinedByte: 100");
        string expected = string.Format("UndefinedByte: {0}", undefinedByte);
        builder.ToString().Should().Be(expected);
        builder.Clear();

        // Test undefined negative values for signed types
        SByteBackedEnum undefinedSByte = (SByteBackedEnum)(-50);
        builder.AppendFormat("UndefinedSByte: {0}", undefinedSByte);
        builder.ToString().Should().Be("UndefinedSByte: -50");
        expected = string.Format("UndefinedSByte: {0}", undefinedSByte);
        builder.ToString().Should().Be(expected);
        builder.Clear();

        // Test undefined values with Value wrapper
        Int64BackedEnum undefinedLong = (Int64BackedEnum)(-999999999999L);
        Value longValue = Value.Create(undefinedLong);
        builder.AppendFormat("UndefinedLong: {0}", longValue);
        builder.ToString().Should().Be("UndefinedLong: -999999999999");
        expected = string.Format("UndefinedLong: {0}", undefinedLong);
        builder.ToString().Should().Be(expected);
    }

    [Fact]
    public void AppendFormat_FlagsEnumAllBackingTypes()
    {
        using ValueStringBuilder builder = new(stackalloc char[300]);

        // Test flags combinations for different backing types
        builder.AppendFormat("ByteFlags: {0}", ByteFlagsEnum.Flag1 | ByteFlagsEnum.Flag2);
        builder.ToString().Should().Be("ByteFlags: Flag1, Flag2");
        builder.Clear();

        // Test flags with Value wrapper
        Value int16FlagsValue = Value.Create(Int16FlagsEnum.All);
        builder.AppendFormat("Int16Flags: {0}", int16FlagsValue);
        builder.ToString().Should().Be("Int16Flags: All");
        builder.Clear();

        // Test undefined flags combinations
        UInt64FlagsEnum undefinedFlags = UInt64FlagsEnum.Flag1 | (UInt64FlagsEnum)128;
        builder.AppendFormat("UndefinedFlags: {0}", undefinedFlags);
        builder.ToString().Should().Be("UndefinedFlags: 129");
        string expected = string.Format("UndefinedFlags: {0}", undefinedFlags);
        builder.ToString().Should().Be(expected);
        builder.Clear();

        // Test zero flags values
        builder.AppendFormat("ZeroFlags: {0}, {1}, {2}",
            Value.Create(SByteFlagsEnum.None),
            Value.Create(UInt32FlagsEnum.None),
            Value.Create(Int64FlagsEnum.None));
        builder.ToString().Should().Be("ZeroFlags: None, None, None");
    }

    [Fact]
    public void AppendFormat_EnumFormattingConsistency()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);

        // Test various enum types for consistency
        TestEnumConsistency(builder, ByteBackedEnum.Value42);
        TestEnumConsistency(builder, SByteBackedEnum.Negative);
        TestEnumConsistency(builder, Int16BackedEnum.Positive);
        TestEnumConsistency(builder, UInt16BackedEnum.Value);
        TestEnumConsistency(builder, Int32BackedEnum.Zero);
        TestEnumConsistency(builder, UInt32BackedEnum.Value);
        TestEnumConsistency(builder, Int64BackedEnum.Positive);
        TestEnumConsistency(builder, UInt64BackedEnum.Value);
    }

    private static void TestEnumConsistency<TEnum>(ValueStringBuilder builder, TEnum enumValue)
        where TEnum : unmanaged, Enum
    {
        // Test direct enum formatting
        builder.AppendFormat("Direct: {0}", enumValue);
        string directResult = builder.ToString();

        builder.Clear();

        // Test Value-wrapped enum formatting
        Value wrappedValue = Value.Create(enumValue);
        builder.AppendFormat("Wrapped: {0}", wrappedValue);
        string wrappedResult = builder.ToString();

        // Both should produce the same enum name
        string expectedDirect = $"Direct: {enumValue}";
        string expectedWrapped = $"Wrapped: {enumValue}";

        directResult.Should().Be(expectedDirect);
        wrappedResult.Should().Be(expectedWrapped);

        // Should match string.Format behavior
        string stringFormatResult = string.Format("Direct: {0}", enumValue);
        directResult.Should().Be(stringFormatResult);

        builder.Clear();
    }

    [Fact]
    public void AppendFormat_EnumFormatsAllBackingTypes()
    {
        using ValueStringBuilder builder = new(stackalloc char[200]);

        // Test decimal format on different backing types
        builder.AppendFormat("Formats: {0:D}, {1:D}, {2:D}",
            Value.Create(ByteBackedEnum.Max),
            Value.Create(Int16BackedEnum.Max),
            Value.Create(Int32BackedEnum.Max));
        builder.ToString().Should().Be("Formats: 255, 32767, 2147483647");
        builder.Clear();

        // Test hex format
        builder.AppendFormat("Hex: {0:X}, {1:X}",
            Value.Create(ByteBackedEnum.Max),
            Value.Create(UInt32BackedEnum.Max));
        builder.ToString().Should().Be("Hex: FF, FFFFFFFF");
        builder.Clear();

        // Test general format (default)
        builder.AppendFormat("General: {0:G}, {1:G}",
            Value.Create(Int64BackedEnum.Positive),
            Value.Create(UInt64BackedEnum.Value));
        builder.ToString().Should().Be("General: Positive, Value");
    }

    [Fact]
    public void AppendFormat_EnumExtremeValues()
    {
        using ValueStringBuilder builder = new(stackalloc char[300]);

        // Test extreme values for signed types
        builder.AppendFormat("Extremes: {0}, {1}, {2}, {3}",
            Value.Create(SByteBackedEnum.Min),
            Value.Create(Int16BackedEnum.Min),
            Value.Create(Int32BackedEnum.Min),
            Value.Create(Int64BackedEnum.Min));

        string expected = string.Format(
            "Extremes: {0}, {1}, {2}, {3}",
            SByteBackedEnum.Min,
            Int16BackedEnum.Min,
            Int32BackedEnum.Min,
            Int64BackedEnum.Min);

        builder.ToString().Should().Be(expected);
        builder.Clear();

        // Test extreme values for unsigned types
        builder.AppendFormat("MaxValues: {0}, {1}, {2}, {3}",
            Value.Create(ByteBackedEnum.Max),
            Value.Create(UInt16BackedEnum.Max),
            Value.Create(UInt32BackedEnum.Max),
            Value.Create(UInt64BackedEnum.Max));

        expected = string.Format("MaxValues: {0}, {1}, {2}, {3}",
            ByteBackedEnum.Max, UInt16BackedEnum.Max, UInt32BackedEnum.Max, UInt64BackedEnum.Max);
        builder.ToString().Should().Be(expected);
    }
}
