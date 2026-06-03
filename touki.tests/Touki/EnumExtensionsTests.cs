// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

[TestClass]
public class EnumExtensionsTests
{
    #region AreFlagsSet Tests

    [TestMethod]
    [DataRow(ByteFlags.One, ByteFlags.One, true)]
    [DataRow(ByteFlags.One | ByteFlags.Two, ByteFlags.One, true)]
    [DataRow(ByteFlags.One | ByteFlags.Two, ByteFlags.One | ByteFlags.Two, true)]
    [DataRow(ByteFlags.One, ByteFlags.Two, false)]
    [DataRow(default(ByteFlags), ByteFlags.One, false)]
    [DataRow(ByteFlags.One, default(ByteFlags), true)] // HasFlag behavior: any value has flag 0
    public void EnumExtensions_AreFlagsSet_ByteFlags(ByteFlags value, ByteFlags flags, bool expected)
    {
        value.AreFlagsSet(flags).Should().Be(expected);
    }

    [TestMethod]
    [DataRow(ShortFlags.One, ShortFlags.One, true)]
    [DataRow(ShortFlags.One | ShortFlags.Two, ShortFlags.One, true)]
    [DataRow(ShortFlags.One | ShortFlags.Two, ShortFlags.One | ShortFlags.Two, true)]
    [DataRow(ShortFlags.One, ShortFlags.Two, false)]
    [DataRow(default(ShortFlags), ShortFlags.One, false)]
    public void EnumExtensions_AreFlagsSet_ShortFlags(ShortFlags value, ShortFlags flags, bool expected)
    {
        value.AreFlagsSet(flags).Should().Be(expected);
    }

    [TestMethod]
    [DataRow(IntFlags.One, IntFlags.One, true)]
    [DataRow(IntFlags.One | IntFlags.Two, IntFlags.One, true)]
    [DataRow(IntFlags.One | IntFlags.Two, IntFlags.One | IntFlags.Two, true)]
    [DataRow(IntFlags.One, IntFlags.Two, false)]
    [DataRow(default(IntFlags), IntFlags.One, false)]
    public void EnumExtensions_AreFlagsSet_IntFlags(IntFlags value, IntFlags flags, bool expected)
    {
        value.AreFlagsSet(flags).Should().Be(expected);
    }

    [TestMethod]
    [DataRow(LongFlags.One, LongFlags.One, true)]
    [DataRow(LongFlags.One | LongFlags.Two, LongFlags.One, true)]
    [DataRow(LongFlags.One | LongFlags.Two, LongFlags.One | LongFlags.Two, true)]
    [DataRow(LongFlags.One, LongFlags.Two, false)]
    [DataRow(default(LongFlags), LongFlags.One, false)]
    public void EnumExtensions_AreFlagsSet_LongFlags(LongFlags value, LongFlags flags, bool expected)
    {
        value.AreFlagsSet(flags).Should().Be(expected);
    }

    #endregion

    #region IsOnlyOneFlagSet Tests

    [TestMethod]
    [DataRow(ByteFlags.One, ByteFlags.One, true)]
    [DataRow(default(ByteFlags), ByteFlags.One, false)]
    [DataRow(default(ByteFlags), default(ByteFlags), false)]
    [DataRow(ByteFlags.One, default(ByteFlags), false)]
    [DataRow(ByteFlags.One, (ByteFlags)0xFF, true)]
    [DataRow((ByteFlags)0xFF, (ByteFlags)0xFF, false)]
    [DataRow((ByteFlags)0xFF, (ByteFlags)0x00, false)]
    [DataRow((ByteFlags)0x00, (ByteFlags)0xFF, false)]
    [DataRow((ByteFlags)0xFF, (ByteFlags)0b1000_0000, true)]
    [DataRow((ByteFlags)0xFF, (ByteFlags)0b0000_0001, true)]
    [DataRow((ByteFlags)0xFF, (ByteFlags)0b1000_0001, false)]
    public void EnumExtensions_IsOnlyOneFlagSet_ByteFlags(ByteFlags value, ByteFlags flag, bool expected)
    {
        value.IsOnlyOneFlagSet(flag).Should().Be(expected);
    }

    [TestMethod]
    [DataRow(ShortFlags.One, ShortFlags.One, true)]
    [DataRow(default(ShortFlags), ShortFlags.One, false)]
    [DataRow(default(ShortFlags), default(ShortFlags), false)]
    [DataRow(ShortFlags.One, default(ShortFlags), false)]
    [DataRow(ShortFlags.One | ShortFlags.Two, ShortFlags.One | ShortFlags.Two, false)]
    [DataRow(ShortFlags.One | ShortFlags.Two, ShortFlags.One, true)]
    [DataRow(ShortFlags.One | ShortFlags.Two, ShortFlags.Two, true)]
    public void EnumExtensions_IsOnlyOneFlagSet_ShortFlags(ShortFlags value, ShortFlags flag, bool expected)
    {
        value.IsOnlyOneFlagSet(flag).Should().Be(expected);
    }

    [TestMethod]
    [DataRow(IntFlags.One, IntFlags.One, true)]
    [DataRow(default(IntFlags), IntFlags.One, false)]
    [DataRow(default(IntFlags), default(IntFlags), false)]
    [DataRow(IntFlags.One, default(IntFlags), false)]
    [DataRow(IntFlags.One | IntFlags.Two, IntFlags.One | IntFlags.Two, false)]
    [DataRow(IntFlags.One | IntFlags.Two, IntFlags.One, true)]
    [DataRow(IntFlags.One | IntFlags.Two, IntFlags.Two, true)]
    public void EnumExtensions_IsOnlyOneFlagSet_IntFlags(IntFlags value, IntFlags flag, bool expected)
    {
        value.IsOnlyOneFlagSet(flag).Should().Be(expected);
    }

    [TestMethod]
    [DataRow(LongFlags.One, LongFlags.One, true)]
    [DataRow(default(LongFlags), LongFlags.One, false)]
    [DataRow(default(LongFlags), default(LongFlags), false)]
    [DataRow(LongFlags.One, default(LongFlags), false)]
    [DataRow(LongFlags.One | LongFlags.Two, LongFlags.One | LongFlags.Two, false)]
    [DataRow(LongFlags.One | LongFlags.Two, LongFlags.One, true)]
    [DataRow(LongFlags.One | LongFlags.Two, LongFlags.Two, true)]
    public void EnumExtensions_IsOnlyOneFlagSet_LongFlags(LongFlags value, LongFlags flag, bool expected)
    {
        value.IsOnlyOneFlagSet(flag).Should().Be(expected);
    }

    #endregion

    #region AreAnyFlagsSet Tests

    [TestMethod]
    [DataRow(ByteFlags.One, ByteFlags.One, true)]
    [DataRow(ByteFlags.One | ByteFlags.Two, ByteFlags.One, true)]
    [DataRow(ByteFlags.One | ByteFlags.Two, ByteFlags.Two, true)]
    [DataRow(ByteFlags.One | ByteFlags.Two, ByteFlags.Four, false)]
    [DataRow(ByteFlags.One, ByteFlags.Two, false)]
    [DataRow(default(ByteFlags), ByteFlags.One, false)]
    [DataRow(ByteFlags.One, default(ByteFlags), false)]
    public void EnumExtensions_AreAnyFlagsSet_ByteFlags(ByteFlags value, ByteFlags flags, bool expected)
    {
        value.AreAnyFlagsSet(flags).Should().Be(expected);
    }

    [TestMethod]
    [DataRow(ShortFlags.One, ShortFlags.One, true)]
    [DataRow(ShortFlags.One | ShortFlags.Two, ShortFlags.One, true)]
    [DataRow(ShortFlags.One | ShortFlags.Two, ShortFlags.Two, true)]
    [DataRow(ShortFlags.One | ShortFlags.Two, ShortFlags.Four, false)]
    [DataRow(ShortFlags.One, ShortFlags.Two, false)]
    [DataRow(default(ShortFlags), ShortFlags.One, false)]
    [DataRow(ShortFlags.One, default(ShortFlags), false)]
    public void EnumExtensions_AreAnyFlagsSet_ShortFlags(ShortFlags value, ShortFlags flags, bool expected)
    {
        value.AreAnyFlagsSet(flags).Should().Be(expected);
    }

    [TestMethod]
    [DataRow(IntFlags.One, IntFlags.One, true)]
    [DataRow(IntFlags.One | IntFlags.Two, IntFlags.One, true)]
    [DataRow(IntFlags.One | IntFlags.Two, IntFlags.Two, true)]
    [DataRow(IntFlags.One | IntFlags.Two, IntFlags.Four, false)]
    [DataRow(IntFlags.One, IntFlags.Two, false)]
    [DataRow(default(IntFlags), IntFlags.One, false)]
    [DataRow(IntFlags.One, default(IntFlags), false)]
    public void EnumExtensions_AreAnyFlagsSet_IntFlags(IntFlags value, IntFlags flags, bool expected)
    {
        value.AreAnyFlagsSet(flags).Should().Be(expected);
    }

    [TestMethod]
    [DataRow(LongFlags.One, LongFlags.One, true)]
    [DataRow(LongFlags.One | LongFlags.Two, LongFlags.One, true)]
    [DataRow(LongFlags.One | LongFlags.Two, LongFlags.Two, true)]
    [DataRow(LongFlags.One | LongFlags.Two, LongFlags.Four, false)]
    [DataRow(LongFlags.One, LongFlags.Two, false)]
    [DataRow(default(LongFlags), LongFlags.One, false)]
    [DataRow(LongFlags.One, default(LongFlags), false)]
    public void EnumExtensions_AreAnyFlagsSet_LongFlags(LongFlags value, LongFlags flags, bool expected)
    {
        value.AreAnyFlagsSet(flags).Should().Be(expected);
    }

    #endregion

    #region SetFlags Tests

    [TestMethod]
    public void EnumExtensions_SetFlags_ByteFlags()
    {
        ByteFlags value = ByteFlags.One;
        value.SetFlags(ByteFlags.Two);
        value.Should().Be(ByteFlags.One | ByteFlags.Two);

        value = default;
        value.SetFlags(ByteFlags.Four);
        value.Should().Be(ByteFlags.Four);

        value = ByteFlags.One | ByteFlags.Two;
        value.SetFlags(ByteFlags.One); // Setting already set flag
        value.Should().Be(ByteFlags.One | ByteFlags.Two);

        value = ByteFlags.One;
        value.SetFlags(ByteFlags.Two | ByteFlags.Four); // Setting multiple flags
        value.Should().Be(ByteFlags.One | ByteFlags.Two | ByteFlags.Four);
    }

    [TestMethod]
    public void EnumExtensions_SetFlags_ShortFlags()
    {
        ShortFlags value = ShortFlags.One;
        value.SetFlags(ShortFlags.Two);
        value.Should().Be(ShortFlags.One | ShortFlags.Two);

        value = default;
        value.SetFlags(ShortFlags.Four);
        value.Should().Be(ShortFlags.Four);

        value = ShortFlags.One | ShortFlags.Two;
        value.SetFlags(ShortFlags.One); // Setting already set flag
        value.Should().Be(ShortFlags.One | ShortFlags.Two);

        value = ShortFlags.One;
        value.SetFlags(ShortFlags.Two | ShortFlags.Four); // Setting multiple flags
        value.Should().Be(ShortFlags.One | ShortFlags.Two | ShortFlags.Four);
    }

    [TestMethod]
    public void EnumExtensions_SetFlags_IntFlags()
    {
        IntFlags value = IntFlags.One;
        value.SetFlags(IntFlags.Two);
        value.Should().Be(IntFlags.One | IntFlags.Two);

        value = default;
        value.SetFlags(IntFlags.Four);
        value.Should().Be(IntFlags.Four);

        value = IntFlags.One | IntFlags.Two;
        value.SetFlags(IntFlags.One); // Setting already set flag
        value.Should().Be(IntFlags.One | IntFlags.Two);

        value = IntFlags.One;
        value.SetFlags(IntFlags.Two | IntFlags.Four); // Setting multiple flags
        value.Should().Be(IntFlags.One | IntFlags.Two | IntFlags.Four);
    }

    [TestMethod]
    public void EnumExtensions_SetFlags_LongFlags()
    {
        LongFlags value = LongFlags.One;
        value.SetFlags(LongFlags.Two);
        value.Should().Be(LongFlags.One | LongFlags.Two);

        value = default;
        value.SetFlags(LongFlags.Four);
        value.Should().Be(LongFlags.Four);

        value = LongFlags.One | LongFlags.Two;
        value.SetFlags(LongFlags.One); // Setting already set flag
        value.Should().Be(LongFlags.One | LongFlags.Two);

        value = LongFlags.One;
        value.SetFlags(LongFlags.Two | LongFlags.Four); // Setting multiple flags
        value.Should().Be(LongFlags.One | LongFlags.Two | LongFlags.Four);
    }

    #endregion

    #region ClearFlags Tests

    [TestMethod]
    public void EnumExtensions_ClearFlags_ByteFlags()
    {
        ByteFlags value = ByteFlags.One | ByteFlags.Two;
        value.ClearFlags(ByteFlags.One);
        value.Should().Be(ByteFlags.Two);

        value = ByteFlags.One | ByteFlags.Two | ByteFlags.Four;
        value.ClearFlags(ByteFlags.Two | ByteFlags.Four); // Clearing multiple flags
        value.Should().Be(ByteFlags.One);

        value = ByteFlags.One;
        value.ClearFlags(ByteFlags.Two); // Clearing unset flag
        value.Should().Be(ByteFlags.One);

        value = ByteFlags.One | ByteFlags.Two;
        value.ClearFlags(ByteFlags.One | ByteFlags.Two); // Clearing all flags
        value.Should().Be(default);
    }

    [TestMethod]
    public void EnumExtensions_ClearFlags_ShortFlags()
    {
        ShortFlags value = ShortFlags.One | ShortFlags.Two;
        value.ClearFlags(ShortFlags.One);
        value.Should().Be(ShortFlags.Two);

        value = ShortFlags.One | ShortFlags.Two | ShortFlags.Four;
        value.ClearFlags(ShortFlags.Two | ShortFlags.Four); // Clearing multiple flags
        value.Should().Be(ShortFlags.One);

        value = ShortFlags.One;
        value.ClearFlags(ShortFlags.Two); // Clearing unset flag
        value.Should().Be(ShortFlags.One);

        value = ShortFlags.One | ShortFlags.Two;
        value.ClearFlags(ShortFlags.One | ShortFlags.Two); // Clearing all flags
        value.Should().Be(default);
    }

    [TestMethod]
    public void EnumExtensions_ClearFlags_IntFlags()
    {
        IntFlags value = IntFlags.One | IntFlags.Two;
        value.ClearFlags(IntFlags.One);
        value.Should().Be(IntFlags.Two);

        value = IntFlags.One | IntFlags.Two | IntFlags.Four;
        value.ClearFlags(IntFlags.Two | IntFlags.Four); // Clearing multiple flags
        value.Should().Be(IntFlags.One);

        value = IntFlags.One;
        value.ClearFlags(IntFlags.Two); // Clearing unset flag
        value.Should().Be(IntFlags.One);

        value = IntFlags.One | IntFlags.Two;
        value.ClearFlags(IntFlags.One | IntFlags.Two); // Clearing all flags
        value.Should().Be(default);
    }

    [TestMethod]
    public void EnumExtensions_ClearFlags_LongFlags()
    {
        LongFlags value = LongFlags.One | LongFlags.Two;
        value.ClearFlags(LongFlags.One);
        value.Should().Be(LongFlags.Two);

        value = LongFlags.One | LongFlags.Two | LongFlags.Four;
        value.ClearFlags(LongFlags.Two | LongFlags.Four); // Clearing multiple flags
        value.Should().Be(LongFlags.One);

        value = LongFlags.One;
        value.ClearFlags(LongFlags.Two); // Clearing unset flag
        value.Should().Be(LongFlags.One);

        value = LongFlags.One | LongFlags.Two;
        value.ClearFlags(LongFlags.One | LongFlags.Two); // Clearing all flags
        value.Should().Be(default);
    }

    #endregion

    #region Signed / negative-value sign-extension regression tests

    // These tests exercise enums with signed underlying types (sbyte and
    // short) using values whose bit patterns have the sign bit set. On
    // .NET Framework RyuJIT there is a documented foot-gun where reading
    // a sub-int value (byte/sbyte/short/ushort) and feeding it through an
    // arithmetic expression can sign-extend incorrectly when the JIT
    // inlines aggressively. The methods on EnumExtensions go through
    // memory (`*(byte*)&value` etc.) rather than `Unsafe.As`, which is the
    // safe pattern, but we want a regression net in case the
    // implementation is ever refactored to use a faster pattern that
    // would trip the bug.
    //
    // Every method is tested with:
    //   - an "all bits set" value (sbyte=-1 / short=-1)
    //   - a "high bit only" value (sbyte=-128 / short.MinValue)
    //   - a mixed positive/negative value

    [Flags]
    private enum SByteFlags : sbyte
    {
        None = 0,
        Low = 0x01,
        High = unchecked((sbyte)0x80), // -128: sign bit only
        AllBits = unchecked((sbyte)0xFF), // -1: every bit set
    }

    [Flags]
    private enum SShortFlags : short
    {
        None = 0,
        Low = 0x0001,
        High = unchecked((short)0x8000), // short.MinValue: sign bit only
        AllBits = unchecked((short)0xFFFF), // -1: every bit set
    }

    [TestMethod]
    public void EnumExtensions_AreFlagsSet_NegativeValues_AreCorrect()
    {
        // sbyte "all bits set" contains every flag
        SByteFlags.AllBits.AreFlagsSet(SByteFlags.Low).Should().BeTrue();
        SByteFlags.AllBits.AreFlagsSet(SByteFlags.High).Should().BeTrue();
        SByteFlags.AllBits.AreFlagsSet(SByteFlags.AllBits).Should().BeTrue();

        // sbyte "high bit only" only contains High
        SByteFlags.High.AreFlagsSet(SByteFlags.High).Should().BeTrue();
        SByteFlags.High.AreFlagsSet(SByteFlags.Low).Should().BeFalse();
        SByteFlags.High.AreFlagsSet(SByteFlags.AllBits).Should().BeFalse();

        // short with high bit set
        SShortFlags.AllBits.AreFlagsSet(SShortFlags.High).Should().BeTrue();
        SShortFlags.High.AreFlagsSet(SShortFlags.High).Should().BeTrue();
        SShortFlags.High.AreFlagsSet(SShortFlags.Low).Should().BeFalse();
        SShortFlags.High.AreFlagsSet(SShortFlags.AllBits).Should().BeFalse();
    }

    [TestMethod]
    public void EnumExtensions_AreAnyFlagsSet_NegativeValues_AreCorrect()
    {
        SByteFlags.High.AreAnyFlagsSet(SByteFlags.High).Should().BeTrue();
        SByteFlags.High.AreAnyFlagsSet(SByteFlags.Low).Should().BeFalse();
        SByteFlags.AllBits.AreAnyFlagsSet(SByteFlags.High).Should().BeTrue();
        SByteFlags.AllBits.AreAnyFlagsSet(SByteFlags.Low).Should().BeTrue();
        SByteFlags.None.AreAnyFlagsSet(SByteFlags.AllBits).Should().BeFalse();

        SShortFlags.High.AreAnyFlagsSet(SShortFlags.High).Should().BeTrue();
        SShortFlags.High.AreAnyFlagsSet(SShortFlags.Low).Should().BeFalse();
        SShortFlags.AllBits.AreAnyFlagsSet(SShortFlags.High).Should().BeTrue();
        SShortFlags.AllBits.AreAnyFlagsSet(SShortFlags.Low).Should().BeTrue();
        SShortFlags.None.AreAnyFlagsSet(SShortFlags.AllBits).Should().BeFalse();
    }

    [TestMethod]
    public void EnumExtensions_IsOnlyOneFlagSet_NegativeValues_AreCorrect()
    {
        // sbyte: 0x80 ("High") is a single bit
        SByteFlags.High.IsOnlyOneFlagSet(SByteFlags.High).Should().BeTrue();
        // sbyte 0xFF has every bit set, so against itself there is more
        // than one bit AND-ed in.
        SByteFlags.AllBits.IsOnlyOneFlagSet(SByteFlags.AllBits).Should().BeFalse();
        // Masking 0xFF down to a single bit should still report a single bit.
        SByteFlags.AllBits.IsOnlyOneFlagSet(SByteFlags.High).Should().BeTrue();
        SByteFlags.AllBits.IsOnlyOneFlagSet(SByteFlags.Low).Should().BeTrue();

        // short: 0x8000 is a single bit. A buggy signed-promotion path
        // would sign-extend to 0xFFFF8000 and the power-of-two test would
        // incorrectly report false.
        SShortFlags.High.IsOnlyOneFlagSet(SShortFlags.High).Should().BeTrue();
        SShortFlags.AllBits.IsOnlyOneFlagSet(SShortFlags.AllBits).Should().BeFalse();
        SShortFlags.AllBits.IsOnlyOneFlagSet(SShortFlags.High).Should().BeTrue();
        SShortFlags.AllBits.IsOnlyOneFlagSet(SShortFlags.Low).Should().BeTrue();
    }

    [TestMethod]
    public void EnumExtensions_SetFlags_NegativeValues_AreCorrect()
    {
        SByteFlags value = SByteFlags.Low;
        value.SetFlags(SByteFlags.High);
        value.Should().Be(SByteFlags.AllBits & (SByteFlags.Low | SByteFlags.High));

        value = SByteFlags.None;
        value.SetFlags(SByteFlags.AllBits);
        value.Should().Be(SByteFlags.AllBits);

        SShortFlags shortValue = SShortFlags.Low;
        shortValue.SetFlags(SShortFlags.High);
        shortValue.Should().Be(SShortFlags.Low | SShortFlags.High);

        shortValue = SShortFlags.None;
        shortValue.SetFlags(SShortFlags.AllBits);
        shortValue.Should().Be(SShortFlags.AllBits);
    }

    [TestMethod]
    public void EnumExtensions_ClearFlags_NegativeValues_AreCorrect()
    {
        SByteFlags value = SByteFlags.AllBits;
        value.ClearFlags(SByteFlags.High);
        // 0xFF with the sign bit cleared = 0x7F
        ((sbyte)value).Should().Be(0x7F);

        value = SByteFlags.AllBits;
        value.ClearFlags(SByteFlags.AllBits);
        value.Should().Be(SByteFlags.None);

        SShortFlags shortValue = SShortFlags.AllBits;
        shortValue.ClearFlags(SShortFlags.High);
        // 0xFFFF with sign bit cleared = 0x7FFF
        ((short)shortValue).Should().Be(0x7FFF);

        shortValue = SShortFlags.AllBits;
        shortValue.ClearFlags(SShortFlags.AllBits);
        shortValue.Should().Be(SShortFlags.None);
    }

    #endregion

    #region Test Enums

    [Flags]
    public enum ByteFlags : byte
    {
        One = 1,
        Two = 2,
        Four = 4,
        Eight = 8
    }

    [Flags]
    public enum ShortFlags : short
    {
        One = 1,
        Two = 2,
        Four = 4,
        Eight = 8
    }

    [Flags]
    public enum IntFlags : int
    {
        One = 1,
        Two = 2,
        Four = 4,
        Eight = 8
    }

    [Flags]
    public enum LongFlags : long
    {
        One = 1,
        Two = 2,
        Four = 4,
        Eight = 8
    }

    #endregion
}
