// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public unsafe class DefaultInterpolatedStringHandlerTests
{
    // On .NET Framework this is our implementation. On .NET we're getting built-in.
    // Testing both so we can validate behavior and expected allocations.

    [Theory]
    [MemberData(nameof(IntegerData))]
    public void InterpolatedStrings_Integer(int value)
    {
        string interpolatedResult = $"Value is {value}.";
        string formatResult = string.Format("Value is {0}.", value);
        interpolatedResult.Should().Be(formatResult);
    }

    [Theory]
    [MemberData(nameof(IntegerData))]
    public void InterpolatedStrings_Integer_WithHexFormat(int value)
    {
        string interpolatedResult = $"Value is {value:X8}.";
        string formatResult = string.Format("Value is {0:X8}.", value);
        interpolatedResult.Should().Be(formatResult);

        interpolatedResult = $"Value is {value:x8}.";
        formatResult = string.Format("Value is {0:x8}.", value);
        interpolatedResult.Should().Be(formatResult);

        interpolatedResult = $"Value is {value:X1}.";
        formatResult = string.Format("Value is {0:X1}.", value);
        interpolatedResult.Should().Be(formatResult);

        interpolatedResult = $"Value is {value:x0}.";
        formatResult = string.Format("Value is {0:x0}.", value);
        interpolatedResult.Should().Be(formatResult);

        interpolatedResult = $"Value is {value:x50}.";
        formatResult = string.Format("Value is {0:x50}.", value);
        interpolatedResult.Should().Be(formatResult);
    }

    [Theory]
    [MemberData(nameof(ByteData))]
    public void InterpolatedStrings_Byte(byte value)
    {
        string interpolatedResult = $"Value is {value}.";
        string formatResult = string.Format("Value is {0}.", value);
        interpolatedResult.Should().Be(formatResult);
    }

    [Theory]
    [MemberData(nameof(ByteData))]
    public void InterpolatedStrings_Byte_WithHexFormat(byte value)
    {
        string interpolatedResult = $"Value is {value:X2}.";
        string formatResult = string.Format("Value is {0:X2}.", value);
        interpolatedResult.Should().Be(formatResult);

        interpolatedResult = $"Value is {value:x2}.";
        formatResult = string.Format("Value is {0:x2}.", value);
        interpolatedResult.Should().Be(formatResult);
    }

    [Theory]
    [MemberData(nameof(SByteData))]
    public void InterpolatedStrings_SByte(sbyte value)
    {
        string interpolatedResult = $"Value is {value}.";
        string formatResult = string.Format("Value is {0}.", value);
        interpolatedResult.Should().Be(formatResult);
    }

    [Theory]
    [MemberData(nameof(ShortData))]
    public void InterpolatedStrings_Short(short value)
    {
        string interpolatedResult = $"Value is {value}.";
        string formatResult = string.Format("Value is {0}.", value);
        interpolatedResult.Should().Be(formatResult);
    }

    [Theory]
    [MemberData(nameof(ShortData))]
    public void InterpolatedStrings_Short_WithFormat(short value)
    {
        string interpolatedResult = $"Value is {value:D5}.";
        string formatResult = string.Format("Value is {0:D5}.", value);
        interpolatedResult.Should().Be(formatResult);

        interpolatedResult = $"Value is {value:N0}.";
        formatResult = string.Format("Value is {0:N0}.", value);
        interpolatedResult.Should().Be(formatResult);
    }

    [Theory]
    [MemberData(nameof(UShortData))]
    public void InterpolatedStrings_UShort(ushort value)
    {
        string interpolatedResult = $"Value is {value}.";
        string formatResult = string.Format("Value is {0}.", value);
        interpolatedResult.Should().Be(formatResult);
    }

    [Theory]
    [MemberData(nameof(LongData))]
    public void InterpolatedStrings_Long(long value)
    {
        string interpolatedResult = $"Value is {value}.";
        string formatResult = string.Format("Value is {0}.", value);
        interpolatedResult.Should().Be(formatResult);
    }

    [Theory]
    [MemberData(nameof(LongData))]
    public void InterpolatedStrings_Long_WithFormat(long value)
    {
        string interpolatedResult = $"Value is {value:X16}.";
        string formatResult = string.Format("Value is {0:X16}.", value);
        interpolatedResult.Should().Be(formatResult);

        interpolatedResult = $"Value is {value:C}.";
        formatResult = string.Format("Value is {0:C}.", value);
        interpolatedResult.Should().Be(formatResult);
    }

    [Theory]
    [MemberData(nameof(ULongData))]
    public void InterpolatedStrings_ULong(ulong value)
    {
        string interpolatedResult = $"Value is {value}.";
        string formatResult = string.Format("Value is {0}.", value);
        interpolatedResult.Should().Be(formatResult);
    }

    [Theory]
    [MemberData(nameof(UIntData))]
    public void InterpolatedStrings_UInt(uint value)
    {
        string interpolatedResult = $"Value is {value}.";
        string formatResult = string.Format("Value is {0}.", value);
        interpolatedResult.Should().Be(formatResult);
    }

    [Theory]
    [MemberData(nameof(FloatData))]
    public void InterpolatedStrings_Float(float value)
    {
        string interpolatedResult = $"Value is {value}.";
        string formatResult = string.Format("Value is {0}.", value);
        interpolatedResult.Should().Be(formatResult);
    }

    [Theory]
    [MemberData(nameof(FloatData))]
    public void InterpolatedStrings_Float_WithFormat(float value)
    {
#if NETFRAMEWORK
        if (value is float.MaxValue or float.MinValue)
        {
            // These hit a bug in .NET Framework 4.8 where the format string is not applied correctly.
            // https://devblogs.microsoft.com/dotnet/floating-point-parsing-and-formatting-improvements-in-net-core-3-0/
            return;
        }
#endif

        string interpolatedResult = $"Value is {value:F2}.";
        string formatResult = string.Format("Value is {0:F2}.", value);
        interpolatedResult.Should().Be(formatResult);

        interpolatedResult = $"Value is {value:E3}.";
        formatResult = string.Format("Value is {0:E3}.", value);
        interpolatedResult.Should().Be(formatResult);

        interpolatedResult = $"Value is {value:G}.";
        formatResult = string.Format("Value is {0:G}.", value);
        interpolatedResult.Should().Be(formatResult);
    }

    [Theory]
    [MemberData(nameof(DoubleData))]
    public void InterpolatedStrings_Double(double value)
    {
        string interpolatedResult = $"Value is {value}.";
        string formatResult = string.Format("Value is {0}.", value);
        interpolatedResult.Should().Be(formatResult);
    }

    [Theory]
    [MemberData(nameof(DoubleData))]
    public void InterpolatedStrings_Double_WithFormat(double value)
    {
#if NETFRAMEWORK
        if (value is double.MaxValue or double.MinValue)
        {
            // These hit a bug in .NET Framework 4.8 where the format string is not applied correctly.
            // https://devblogs.microsoft.com/dotnet/floating-point-parsing-and-formatting-improvements-in-net-core-3-0/
            return;
        }
#endif

        string interpolatedResult = $"Value is {value:F4}.";
        string formatResult = string.Format("Value is {0:F4}.", value);
        interpolatedResult.Should().Be(formatResult);

        interpolatedResult = $"Value is {value:E}.";
        formatResult = string.Format("Value is {0:E}.", value);
        interpolatedResult.Should().Be(formatResult);

        interpolatedResult = $"Value is {value:P2}.";
        formatResult = string.Format("Value is {0:P2}.", value);
        interpolatedResult.Should().Be(formatResult);
    }

    [Theory]
    [MemberData(nameof(DecimalData))]
    public void InterpolatedStrings_Decimal(decimal value)
    {
        string interpolatedResult = $"Value is {value}.";
        string formatResult = string.Format("Value is {0}.", value);
        interpolatedResult.Should().Be(formatResult);
    }

    [Theory]
    [MemberData(nameof(DecimalData))]
    public void InterpolatedStrings_Decimal_WithFormat(decimal value)
    {
        string interpolatedResult = $"Value is {value:C2}.";
        string formatResult = string.Format("Value is {0:C2}.", value);
        interpolatedResult.Should().Be(formatResult);

        interpolatedResult = $"Value is {value:F3}.";
        formatResult = string.Format("Value is {0:F3}.", value);
        interpolatedResult.Should().Be(formatResult);

        interpolatedResult = $"Value is {value:N4}.";
        formatResult = string.Format("Value is {0:N4}.", value);
        interpolatedResult.Should().Be(formatResult);
    }

    [Theory]
    [MemberData(nameof(DateTimeData))]
    public void InterpolatedStrings_DateTime(DateTime value)
    {
        string interpolatedResult = $"Value is {value}.";
        string formatResult = string.Format("Value is {0}.", value);
        interpolatedResult.Should().Be(formatResult);
    }

    [Theory]
    [MemberData(nameof(DateTimeData))]
    public void InterpolatedStrings_DateTime_WithFormat(DateTime value)
    {
        string interpolatedResult = $"Value is {value:yyyy-MM-dd}.";
        string formatResult = string.Format("Value is {0:yyyy-MM-dd}.", value);
        interpolatedResult.Should().Be(formatResult);

        interpolatedResult = $"Value is {value:HH:mm:ss}.";
        formatResult = string.Format("Value is {0:HH:mm:ss}.", value);
        interpolatedResult.Should().Be(formatResult);

        interpolatedResult = $"Value is {value:F}.";
        formatResult = string.Format("Value is {0:F}.", value);
        interpolatedResult.Should().Be(formatResult);

        interpolatedResult = $"Value is {value:d}.";
        formatResult = string.Format("Value is {0:d}.", value);
        interpolatedResult.Should().Be(formatResult);

        interpolatedResult = $"Value is {value:T}.";
        formatResult = string.Format("Value is {0:T}.", value);
        interpolatedResult.Should().Be(formatResult);
    }

    [Theory]
    [MemberData(nameof(DateTimeOffsetData))]
    public void InterpolatedStrings_DateTimeOffset(DateTimeOffset value)
    {
        string interpolatedResult = $"Value is {value}.";
        string formatResult = string.Format("Value is {0}.", value);
        interpolatedResult.Should().Be(formatResult);
    }

    [Theory]
    [MemberData(nameof(DateTimeOffsetData))]
    public void InterpolatedStrings_DateTimeOffset_WithFormat(DateTimeOffset value)
    {
        string interpolatedResult = $"Value is {value:yyyy-MM-dd HH:mm:ss zzz}.";
        string formatResult = string.Format("Value is {0:yyyy-MM-dd HH:mm:ss zzz}.", value);
        interpolatedResult.Should().Be(formatResult);

        interpolatedResult = $"Value is {value:O}.";
        formatResult = string.Format("Value is {0:O}.", value);
        interpolatedResult.Should().Be(formatResult);

        interpolatedResult = $"Value is {value:R}.";
        formatResult = string.Format("Value is {0:R}.", value);
        interpolatedResult.Should().Be(formatResult);

        interpolatedResult = $"Value is {value:s}.";
        formatResult = string.Format("Value is {0:s}.", value);
        interpolatedResult.Should().Be(formatResult);
    }

    [Theory]
    [MemberData(nameof(IntegerData))]
    public void InterpolatedStrings_Integer_WithVariousFormats(int value)
    {
        string interpolatedResult = $"Value is {value:C}.";
        string formatResult = string.Format("Value is {0:C}.", value);
        interpolatedResult.Should().Be(formatResult);

        interpolatedResult = $"Value is {value:D8}.";
        formatResult = string.Format("Value is {0:D8}.", value);
        interpolatedResult.Should().Be(formatResult);

        interpolatedResult = $"Value is {value:E2}.";
        formatResult = string.Format("Value is {0:E2}.", value);
        interpolatedResult.Should().Be(formatResult);

        interpolatedResult = $"Value is {value:F4}.";
        formatResult = string.Format("Value is {0:F4}.", value);
        interpolatedResult.Should().Be(formatResult);

        interpolatedResult = $"Value is {value:G}.";
        formatResult = string.Format("Value is {0:G}.", value);
        interpolatedResult.Should().Be(formatResult);

        interpolatedResult = $"Value is {value:N}.";
        formatResult = string.Format("Value is {0:N}.", value);
        interpolatedResult.Should().Be(formatResult);

        interpolatedResult = $"Value is {value:P}.";
        formatResult = string.Format("Value is {0:P}.", value);
        interpolatedResult.Should().Be(formatResult);
    }

    [Fact]
    public void InterpolatedStrings_MultipleValues()
    {
        int intValue = 42;
        double doubleValue = 3.14159;
        DateTime dateValue = new(2025, 6, 17, 14, 30, 0);

        string interpolatedResult = $"Int: {intValue}, Double: {doubleValue:F2}, Date: {dateValue:yyyy-MM-dd}";
        string formatResult = string.Format("Int: {0}, Double: {1:F2}, Date: {2:yyyy-MM-dd}", intValue, doubleValue, dateValue);
        interpolatedResult.Should().Be(formatResult);
    }

    [Fact]
    public void InterpolatedStrings_WithAlignment()
    {
        int value = 42;

        string interpolatedResult = $"Value: {value,10}";
        string formatResult = string.Format("Value: {0,10}", value);
        interpolatedResult.Should().Be(formatResult);

        interpolatedResult = $"Value: {value,-10}";
        formatResult = string.Format("Value: {0,-10}", value);
        interpolatedResult.Should().Be(formatResult);

        interpolatedResult = $"Value: {value,10:X8}";
        formatResult = string.Format("Value: {0,10:X8}", value);
        interpolatedResult.Should().Be(formatResult);
    }

    [Fact]
    public void InterpolatedStrings_SpecialFloatingPointValues()
    {
        double positiveInfinity = double.PositiveInfinity;
        double negativeInfinity = double.NegativeInfinity;
        double nan = double.NaN;

        string interpolatedResult = $"Value: {positiveInfinity}";
        string formatResult = string.Format("Value: {0}", positiveInfinity);
        interpolatedResult.Should().Be(formatResult);

        interpolatedResult = $"Value: {negativeInfinity}";
        formatResult = string.Format("Value: {0}", negativeInfinity);
        interpolatedResult.Should().Be(formatResult);

        interpolatedResult = $"Value: {nan}";
        formatResult = string.Format("Value: {0}", nan);
        interpolatedResult.Should().Be(formatResult);
    }

    public static TheoryData<int> IntegerData { get; } =
    [
        42,
        -42,
        0,
        int.MaxValue,
        int.MinValue
    ];

    public static TheoryData<byte> ByteData { get; } =
    [
        1,
        42,
        127,
        byte.MaxValue,
        byte.MinValue
    ];

    public static TheoryData<sbyte> SByteData { get; } =
    [
        0,
        1,
        -1,
        42,
        -42,
        sbyte.MaxValue,
        sbyte.MinValue
    ];

    public static TheoryData<short> ShortData { get; } =
    [
        0,
        1,
        -1,
        42,
        -42,
        1000,
        -1000,
        short.MaxValue,
        short.MinValue
    ];

    public static TheoryData<ushort> UShortData { get; } =
    [
        1,
        42,
        1000,
        ushort.MaxValue,
        ushort.MinValue
    ];

    public static TheoryData<long> LongData { get; } =
    [
        0L,
        1L,
        -1L,
        42L,
        -42L,
        1000000000000L,
        -1000000000000L,
        long.MaxValue,
        long.MinValue
    ];

    public static TheoryData<ulong> ULongData { get; } =
    [
        1UL,
        42UL,
        1000000000000UL,
        ulong.MaxValue,
        ulong.MinValue
    ];

    public static TheoryData<uint> UIntData { get; } =
    [
        1U,
        42U,
        1000000U,
        uint.MaxValue,
        uint.MinValue
    ];

    public static TheoryData<float> FloatData { get; } =
    [
        0.0f,
        1.0f,
        -1.0f,
        3.14159f,
        -3.14159f,
        42.5f,
        -42.5f,
        0.000123f,
        123000000.0f,
        float.MaxValue,
        float.MinValue,
        float.Epsilon
    ];

    public static TheoryData<double> DoubleData { get; } =
    [
        0.0,
        1.0,
        -1.0,
        3.141592653589793,
        -3.141592653589793,
        42.123456789,
        -42.123456789,
        0.000000000123,
        123000000000000.0,
        double.MaxValue,
        double.MinValue,
        double.Epsilon
    ];

    public static TheoryData<decimal> DecimalData { get; } =
    [
        0.0m,
        1.0m,
        -1.0m,
        3.141592653589793m,
        -3.141592653589793m,
        42.123456789123456789m,
        -42.123456789123456789m,
        0.000000000000000001m,
        123456789012345678.90m,
        decimal.MaxValue,
        decimal.MinValue
    ];

    public static TheoryData<DateTime> DateTimeData { get; } =
    [
        new DateTime(2025, 1, 1),
        new DateTime(2025, 6, 17, 14, 30, 45),
        new DateTime(2025, 12, 31, 23, 59, 59),
        new DateTime(1900, 1, 1),
        new DateTime(2100, 12, 31),
        DateTime.MinValue,
        DateTime.MaxValue,
        new DateTime(2025, 6, 17, 0, 0, 0, DateTimeKind.Utc),
        new DateTime(2025, 6, 17, 12, 0, 0, DateTimeKind.Local)
    ];

    public static TheoryData<DateTimeOffset> DateTimeOffsetData { get; } =
    [
        new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2025, 6, 17, 14, 30, 45, TimeSpan.FromHours(-5)),
        new DateTimeOffset(2025, 12, 31, 23, 59, 59, TimeSpan.FromHours(8)),
        new DateTimeOffset(2025, 6, 17, 12, 0, 0, TimeSpan.FromMinutes(330)),
        DateTimeOffset.MinValue,
        DateTimeOffset.MaxValue,
        new DateTimeOffset(DateTimeOffset.UtcNow.Date),
        new DateTimeOffset(2025, 6, 17, 0, 0, 0, 123, TimeSpan.Zero)
    ];
}
