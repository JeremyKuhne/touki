// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace System.Tests;

public class EnumExtensionsTests
{
    public enum Color
    {
        Red = 1,
        Green = 2,
        Blue = 4,
    }

    [Flags]
    public enum FileAccess
    {
        None = 0,
        Read = 1,
        Write = 2,
        ReadWrite = Read | Write,
    }

    public enum SByteEnum : sbyte
    {
        Min = sbyte.MinValue,
        One = 1,
        Two = 2,
        Max = sbyte.MaxValue,
    }

    public enum ByteEnum : byte
    {
        Min = byte.MinValue,
        One = 1,
        Two = 2,
        Max = byte.MaxValue,
    }

    public enum Int16Enum : short
    {
        Min = short.MinValue,
        One = 1,
        Max = short.MaxValue,
    }

    public enum UInt16Enum : ushort
    {
        Min = ushort.MinValue,
        One = 1,
        Max = ushort.MaxValue,
    }

    public enum UInt32Enum : uint
    {
        Min = uint.MinValue,
        One = 1u,
        Max = uint.MaxValue,
    }

    public enum Int64Enum : long
    {
        Min = long.MinValue,
        One = 1L,
        Max = long.MaxValue,
    }

    public enum UInt64Enum : ulong
    {
        Min = ulong.MinValue,
        One = 1UL,
        Max = ulong.MaxValue,
    }

    // ---- GetValues<TEnum> ----

    [Fact]
    public void GetValues_ReturnsAllValuesInDeclarationOrder()
    {
        Color[] values = Enum.GetValues<Color>();
        values.Should().Equal(Color.Red, Color.Green, Color.Blue);
    }

    [Fact]
    public void GetValues_FlagsEnum_IncludesCombinedValues()
    {
        FileAccess[] values = Enum.GetValues<FileAccess>();
        values.Should().Contain(FileAccess.None).And.Contain(FileAccess.ReadWrite);
    }

    // ---- GetNames<TEnum> ----

    [Fact]
    public void GetNames_ReturnsNamesInValueOrder()
    {
        string[] names = Enum.GetNames<Color>();
        names.Should().Equal("Red", "Green", "Blue");
    }

    // ---- GetName<TEnum> ----

    [Fact]
    public void GetName_DefinedValue_ReturnsName()
    {
        Enum.GetName(Color.Green).Should().Be("Green");
    }

    [Fact]
    public void GetName_UndefinedValue_ReturnsNull()
    {
        Enum.GetName((Color)99).Should().BeNull();
    }

    // ---- IsDefined<TEnum> ----

    [Theory]
    [InlineData(Color.Red, true)]
    [InlineData(Color.Green, true)]
    [InlineData(Color.Blue, true)]
    [InlineData((Color)99, false)]
    [InlineData((Color)0, false)]
    public void IsDefined_ReturnsExpected(Color value, bool expected)
    {
        Enum.IsDefined(value).Should().Be(expected);
    }

    [Fact]
    public void IsDefined_FlagsEnumCombined_ReturnsTrueForExplicitlyDefined()
    {
        // ReadWrite is explicitly defined as Read | Write.
        Enum.IsDefined(FileAccess.ReadWrite).Should().BeTrue();
        // An undefined combination is not.
        Enum.IsDefined((FileAccess)99).Should().BeFalse();
    }

    // ---- Parse<TEnum>(ROS<char>) ----

    [Fact]
    public void Parse_Generic_Name_ReturnsValue()
    {
        Enum.Parse<Color>("Red".AsSpan()).Should().Be(Color.Red);
    }

    [Fact]
    public void Parse_Generic_NumericString_ReturnsValue()
    {
        Enum.Parse<Color>("2".AsSpan()).Should().Be(Color.Green);
    }

    [Fact]
    public void Parse_Generic_NumericStringNotADefinedValue_StillReturnsCastValue()
    {
        // BCL behavior: numeric forms always parse, even if the value isn't a defined member.
        Enum.Parse<Color>("99".AsSpan()).Should().Be((Color)99);
    }

    [Fact]
    public void Parse_Generic_FlagsCommaSeparated_ReturnsCombined()
    {
        Enum.Parse<FileAccess>("Read, Write".AsSpan()).Should().Be(FileAccess.ReadWrite);
    }

    [Fact]
    public void Parse_Generic_CaseSensitiveByDefault_ThrowsForLowercase()
    {
        Action action = () => Enum.Parse<Color>("red".AsSpan());
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_Generic_IgnoreCaseTrue_AcceptsLowercase()
    {
        Enum.Parse<Color>("red".AsSpan(), ignoreCase: true).Should().Be(Color.Red);
    }

    [Fact]
    public void Parse_Generic_IgnoreCaseFalse_RejectsLowercase()
    {
        Action action = () => Enum.Parse<Color>("red".AsSpan(), ignoreCase: false);
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_Generic_UnknownName_Throws()
    {
        Action action = () => Enum.Parse<Color>("Magenta".AsSpan());
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_Generic_EmptySpan_Throws()
    {
        Action action = () => Enum.Parse<Color>([]);
        action.Should().Throw<ArgumentException>();
    }

    // ---- TryParse<TEnum>(ROS<char>) ----

    [Fact]
    public void TryParse_Generic_ValidName_ReturnsTrue()
    {
        Enum.TryParse("Blue".AsSpan(), out Color value).Should().BeTrue();
        value.Should().Be(Color.Blue);
    }

    [Fact]
    public void TryParse_Generic_Numeric_ReturnsTrue()
    {
        Enum.TryParse("4".AsSpan(), out Color value).Should().BeTrue();
        value.Should().Be(Color.Blue);
    }

    [Fact]
    public void TryParse_Generic_Invalid_ReturnsFalse()
    {
        Enum.TryParse("Magenta".AsSpan(), out Color value).Should().BeFalse();
        value.Should().Be(default);
    }

    [Fact]
    public void TryParse_Generic_CaseSensitive_RejectsLowercase()
    {
        Enum.TryParse("red".AsSpan(), out Color _).Should().BeFalse();
    }

    [Fact]
    public void TryParse_Generic_IgnoreCase_AcceptsLowercase()
    {
        Enum.TryParse("red".AsSpan(), ignoreCase: true, out Color value).Should().BeTrue();
        value.Should().Be(Color.Red);
    }

    [Fact]
    public void TryParse_Generic_EmptySpan_ReturnsFalse()
    {
        Enum.TryParse([], out Color value).Should().BeFalse();
        value.Should().Be(default);
    }

    [Fact]
    public void TryParse_Generic_FlagsCommaSeparated_ReturnsCombined()
    {
        Enum.TryParse("Read, Write".AsSpan(), out FileAccess value).Should().BeTrue();
        value.Should().Be(FileAccess.ReadWrite);
    }

    // ---- Parse(Type, ROS<char>) ----

    [Fact]
    public void Parse_NonGeneric_Name_ReturnsBoxedValue()
    {
        Enum.Parse(typeof(Color), "Green".AsSpan()).Should().Be(Color.Green);
    }

    [Fact]
    public void Parse_NonGeneric_IgnoreCase_AcceptsLowercase()
    {
        Enum.Parse(typeof(Color), "blue".AsSpan(), ignoreCase: true).Should().Be(Color.Blue);
    }

    [Fact]
    public void Parse_NonGeneric_Unknown_Throws()
    {
        Action action = () => Enum.Parse(typeof(Color), "Magenta".AsSpan());
        action.Should().Throw<ArgumentException>();
    }

    // ---- TryParse(Type, ROS<char>, out object?) ----

    [Fact]
    public void TryParse_NonGeneric_Valid_ReturnsTrue()
    {
        Enum.TryParse(typeof(Color), "Red".AsSpan(), out object? value).Should().BeTrue();
        value.Should().Be(Color.Red);
    }

    [Fact]
    public void TryParse_NonGeneric_Invalid_ReturnsFalseAndNull()
    {
        Enum.TryParse(typeof(Color), "Magenta".AsSpan(), out object? value).Should().BeFalse();
        value.Should().BeNull();
    }

    [Fact]
    public void TryParse_NonGeneric_IgnoreCase_AcceptsLowercase()
    {
        Enum.TryParse(typeof(Color), "green".AsSpan(), ignoreCase: true, out object? value)
            .Should().BeTrue();
        value.Should().Be(Color.Green);
    }

    [Fact]
    public void TryParse_NonGeneric_NullEnumType_Throws()
    {
        // BCL contract: invalid enumType throws even from TryParse.
        Action action = () => Enum.TryParse(null!, "Red".AsSpan(), out object? _);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TryParse_NonGeneric_NonEnumType_Throws()
    {
        Action action = () => Enum.TryParse(typeof(int), "1".AsSpan(), out object? _);
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TryParse_NonGeneric_IgnoreCase_NullEnumType_Throws()
    {
        Action action = () => Enum.TryParse(null!, "Red".AsSpan(), ignoreCase: true, out object? _);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TryParse_NonGeneric_IgnoreCase_NonEnumType_Throws()
    {
        Action action = () => Enum.TryParse(typeof(int), "1".AsSpan(), ignoreCase: true, out object? _);
        action.Should().Throw<ArgumentException>();
    }

    // ---- Whitespace handling (matches BCL: leading/trailing whitespace trimmed) ----

    [Theory]
    [InlineData(" Red", Color.Red)]
    [InlineData("Red ", Color.Red)]
    [InlineData(" Red ", Color.Red)]
    [InlineData(" 1 ", Color.Red)]
    public void Parse_Generic_LeadingTrailingWhitespace_Trimmed(string input, Color expected)
    {
        Enum.Parse<Color>(input.AsSpan()).Should().Be(expected);
    }

    [Fact]
    public void Parse_Generic_FlagsWithExtraInteriorSpaces_Parses()
    {
        Enum.Parse<FileAccess>(" Read , Write ".AsSpan()).Should().Be(FileAccess.ReadWrite);
    }

    [Fact]
    public void Parse_Generic_FlagsWithDuplicateNames_CollapsesToOneFlag()
    {
        // BCL behavior: duplicates collapse via OR.
        Enum.Parse<FileAccess>("Read, Read, Write".AsSpan()).Should().Be(FileAccess.ReadWrite);
    }

    // ---- Negative numeric strings for signed enums ----

    [Fact]
    public void Parse_Generic_NegativeNumeric_Signed_ReturnsCastValue()
    {
        Enum.Parse<Int64Enum>("-42".AsSpan()).Should().Be((Int64Enum)(-42));
    }

    [Fact]
    public void Parse_Generic_NegativeNumeric_Unsigned_Throws()
    {
        // Unsigned enum can't take a negative literal. The exact exception type depends on TFM:
        // older runtimes throw ArgumentException via the parser; modern runtimes throw OverflowException.
        Action action = () => Enum.Parse<UInt32Enum>("-1".AsSpan());
        Exception? exception = Record.Exception(action);
        exception.Should().NotBeNull();
        (exception is OverflowException or ArgumentException).Should().BeTrue();
    }

    // ---- Multiple underlying integral types ----

    [Theory]
    [InlineData("Min", true)]
    [InlineData("One", true)]
    [InlineData("Max", true)]
    [InlineData("Bogus", false)]
    public void TryParse_Generic_SByteEnum(string input, bool expectedSuccess)
    {
        Enum.TryParse(input.AsSpan(), out SByteEnum _).Should().Be(expectedSuccess);
    }

    [Fact]
    public void Parse_Generic_ByteEnum_NumericMaxValue()
    {
        Enum.Parse<ByteEnum>(byte.MaxValue.ToString().AsSpan()).Should().Be(ByteEnum.Max);
    }

    [Fact]
    public void Parse_Generic_Int16Enum_NumericMinValue()
    {
        Enum.Parse<Int16Enum>(short.MinValue.ToString().AsSpan()).Should().Be(Int16Enum.Min);
    }

    [Fact]
    public void Parse_Generic_UInt16Enum_NumericMaxValue()
    {
        Enum.Parse<UInt16Enum>(ushort.MaxValue.ToString().AsSpan()).Should().Be(UInt16Enum.Max);
    }

    [Fact]
    public void Parse_Generic_UInt32Enum_NumericMaxValue()
    {
        Enum.Parse<UInt32Enum>(uint.MaxValue.ToString().AsSpan()).Should().Be(UInt32Enum.Max);
    }

    [Fact]
    public void Parse_Generic_Int64Enum_NumericMinValue()
    {
        Enum.Parse<Int64Enum>(long.MinValue.ToString().AsSpan()).Should().Be(Int64Enum.Min);
    }

    [Fact]
    public void Parse_Generic_UInt64Enum_NumericMaxValue()
    {
        Enum.Parse<UInt64Enum>(ulong.MaxValue.ToString().AsSpan()).Should().Be(UInt64Enum.Max);
    }

    // ---- IgnoreCase variations ----

    [Fact]
    public void Parse_Generic_IgnoreCase_MixedCase_Accepted()
    {
        Enum.Parse<Int16Enum>("mAx".AsSpan(), ignoreCase: true).Should().Be(Int16Enum.Max);
    }

    [Fact]
    public void Parse_Generic_IgnoreCase_AllUppercase_Accepted()
    {
        Enum.Parse<Color>("BLUE".AsSpan(), ignoreCase: true).Should().Be(Color.Blue);
    }

    // ---- IsDefined across underlying types ----

    [Theory]
    [InlineData(SByteEnum.One, true)]
    [InlineData((SByteEnum)99, false)]
    public void IsDefined_Generic_SByteEnum(SByteEnum value, bool expected)
    {
        Enum.IsDefined(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(UInt64Enum.One, true)]
    [InlineData((UInt64Enum)99UL, false)]
    public void IsDefined_Generic_UInt64Enum(UInt64Enum value, bool expected)
    {
        Enum.IsDefined(value).Should().Be(expected);
    }

    // ---- Error-message details (BCL behavior: unknown name appears in message) ----

    [Theory]
    [InlineData("Yellow")]
    [InlineData("Yellow,Orange")]
    public void Parse_Generic_NonExistentValue_NameIncludedInErrorMessage(string value)
    {
        Action action = () => Enum.Parse<FileAccess>(value.AsSpan());
        // Match BCL: the message contains the bad name token (first token for comma-separated).
        action.Should().Throw<ArgumentException>()
            .Which.Message.Should().Contain("Yellow");
    }
}
