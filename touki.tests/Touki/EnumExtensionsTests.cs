// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public class EnumExtensionsTests
{
    #region AreFlagsSet Tests

    [Theory]
    [InlineData(ByteFlags.One, ByteFlags.One, true)]
    [InlineData(ByteFlags.One | ByteFlags.Two, ByteFlags.One, true)]
    [InlineData(ByteFlags.One | ByteFlags.Two, ByteFlags.One | ByteFlags.Two, true)]
    [InlineData(ByteFlags.One, ByteFlags.Two, false)]
    [InlineData(default(ByteFlags), ByteFlags.One, false)]
    [InlineData(ByteFlags.One, default(ByteFlags), true)] // HasFlag behavior: any value has flag 0
    public void EnumExtensions_AreFlagsSet_ByteFlags(ByteFlags value, ByteFlags flags, bool expected)
    {
        value.AreFlagsSet(flags).Should().Be(expected);
    }

    [Theory]
    [InlineData(ShortFlags.One, ShortFlags.One, true)]
    [InlineData(ShortFlags.One | ShortFlags.Two, ShortFlags.One, true)]
    [InlineData(ShortFlags.One | ShortFlags.Two, ShortFlags.One | ShortFlags.Two, true)]
    [InlineData(ShortFlags.One, ShortFlags.Two, false)]
    [InlineData(default(ShortFlags), ShortFlags.One, false)]
    public void EnumExtensions_AreFlagsSet_ShortFlags(ShortFlags value, ShortFlags flags, bool expected)
    {
        value.AreFlagsSet(flags).Should().Be(expected);
    }

    [Theory]
    [InlineData(IntFlags.One, IntFlags.One, true)]
    [InlineData(IntFlags.One | IntFlags.Two, IntFlags.One, true)]
    [InlineData(IntFlags.One | IntFlags.Two, IntFlags.One | IntFlags.Two, true)]
    [InlineData(IntFlags.One, IntFlags.Two, false)]
    [InlineData(default(IntFlags), IntFlags.One, false)]
    public void EnumExtensions_AreFlagsSet_IntFlags(IntFlags value, IntFlags flags, bool expected)
    {
        value.AreFlagsSet(flags).Should().Be(expected);
    }

    [Theory]
    [InlineData(LongFlags.One, LongFlags.One, true)]
    [InlineData(LongFlags.One | LongFlags.Two, LongFlags.One, true)]
    [InlineData(LongFlags.One | LongFlags.Two, LongFlags.One | LongFlags.Two, true)]
    [InlineData(LongFlags.One, LongFlags.Two, false)]
    [InlineData(default(LongFlags), LongFlags.One, false)]
    public void EnumExtensions_AreFlagsSet_LongFlags(LongFlags value, LongFlags flags, bool expected)
    {
        value.AreFlagsSet(flags).Should().Be(expected);
    }

    #endregion

    #region IsOnlyOneFlagSet Tests

    [Theory]
    [InlineData(ByteFlags.One, ByteFlags.One, true)]
    [InlineData(default(ByteFlags), ByteFlags.One, false)]
    [InlineData(default(ByteFlags), default(ByteFlags), false)]
    [InlineData(ByteFlags.One, default(ByteFlags), false)]
    [InlineData(ByteFlags.One, (ByteFlags)0xFF, true)]
    [InlineData((ByteFlags)0xFF, (ByteFlags)0xFF, false)]
    [InlineData((ByteFlags)0xFF, (ByteFlags)0x00, false)]
    [InlineData((ByteFlags)0x00, (ByteFlags)0xFF, false)]
    [InlineData((ByteFlags)0xFF, (ByteFlags)0b1000_0000, true)]
    [InlineData((ByteFlags)0xFF, (ByteFlags)0b0000_0001, true)]
    [InlineData((ByteFlags)0xFF, (ByteFlags)0b1000_0001, false)]
    public void EnumExtensions_IsOnlyOneFlagSet_ByteFlags(ByteFlags value, ByteFlags flag, bool expected)
    {
        value.IsOnlyOneFlagSet(flag).Should().Be(expected);
    }

    [Theory]
    [InlineData(ShortFlags.One, ShortFlags.One, true)]
    [InlineData(default(ShortFlags), ShortFlags.One, false)]
    [InlineData(default(ShortFlags), default(ShortFlags), false)]
    [InlineData(ShortFlags.One, default(ShortFlags), false)]
    [InlineData(ShortFlags.One | ShortFlags.Two, ShortFlags.One | ShortFlags.Two, false)]
    [InlineData(ShortFlags.One | ShortFlags.Two, ShortFlags.One, true)]
    [InlineData(ShortFlags.One | ShortFlags.Two, ShortFlags.Two, true)]
    public void EnumExtensions_IsOnlyOneFlagSet_ShortFlags(ShortFlags value, ShortFlags flag, bool expected)
    {
        value.IsOnlyOneFlagSet(flag).Should().Be(expected);
    }

    [Theory]
    [InlineData(IntFlags.One, IntFlags.One, true)]
    [InlineData(default(IntFlags), IntFlags.One, false)]
    [InlineData(default(IntFlags), default(IntFlags), false)]
    [InlineData(IntFlags.One, default(IntFlags), false)]
    [InlineData(IntFlags.One | IntFlags.Two, IntFlags.One | IntFlags.Two, false)]
    [InlineData(IntFlags.One | IntFlags.Two, IntFlags.One, true)]
    [InlineData(IntFlags.One | IntFlags.Two, IntFlags.Two, true)]
    public void EnumExtensions_IsOnlyOneFlagSet_IntFlags(IntFlags value, IntFlags flag, bool expected)
    {
        value.IsOnlyOneFlagSet(flag).Should().Be(expected);
    }

    [Theory]
    [InlineData(LongFlags.One, LongFlags.One, true)]
    [InlineData(default(LongFlags), LongFlags.One, false)]
    [InlineData(default(LongFlags), default(LongFlags), false)]
    [InlineData(LongFlags.One, default(LongFlags), false)]
    [InlineData(LongFlags.One | LongFlags.Two, LongFlags.One | LongFlags.Two, false)]
    [InlineData(LongFlags.One | LongFlags.Two, LongFlags.One, true)]
    [InlineData(LongFlags.One | LongFlags.Two, LongFlags.Two, true)]
    public void EnumExtensions_IsOnlyOneFlagSet_LongFlags(LongFlags value, LongFlags flag, bool expected)
    {
        value.IsOnlyOneFlagSet(flag).Should().Be(expected);
    }

    #endregion

    #region AreAnyFlagsSet Tests

    [Theory]
    [InlineData(ByteFlags.One, ByteFlags.One, true)]
    [InlineData(ByteFlags.One | ByteFlags.Two, ByteFlags.One, true)]
    [InlineData(ByteFlags.One | ByteFlags.Two, ByteFlags.Two, true)]
    [InlineData(ByteFlags.One | ByteFlags.Two, ByteFlags.Four, false)]
    [InlineData(ByteFlags.One, ByteFlags.Two, false)]
    [InlineData(default(ByteFlags), ByteFlags.One, false)]
    [InlineData(ByteFlags.One, default(ByteFlags), false)]
    public void EnumExtensions_AreAnyFlagsSet_ByteFlags(ByteFlags value, ByteFlags flags, bool expected)
    {
        value.AreAnyFlagsSet(flags).Should().Be(expected);
    }

    [Theory]
    [InlineData(ShortFlags.One, ShortFlags.One, true)]
    [InlineData(ShortFlags.One | ShortFlags.Two, ShortFlags.One, true)]
    [InlineData(ShortFlags.One | ShortFlags.Two, ShortFlags.Two, true)]
    [InlineData(ShortFlags.One | ShortFlags.Two, ShortFlags.Four, false)]
    [InlineData(ShortFlags.One, ShortFlags.Two, false)]
    [InlineData(default(ShortFlags), ShortFlags.One, false)]
    [InlineData(ShortFlags.One, default(ShortFlags), false)]
    public void EnumExtensions_AreAnyFlagsSet_ShortFlags(ShortFlags value, ShortFlags flags, bool expected)
    {
        value.AreAnyFlagsSet(flags).Should().Be(expected);
    }

    [Theory]
    [InlineData(IntFlags.One, IntFlags.One, true)]
    [InlineData(IntFlags.One | IntFlags.Two, IntFlags.One, true)]
    [InlineData(IntFlags.One | IntFlags.Two, IntFlags.Two, true)]
    [InlineData(IntFlags.One | IntFlags.Two, IntFlags.Four, false)]
    [InlineData(IntFlags.One, IntFlags.Two, false)]
    [InlineData(default(IntFlags), IntFlags.One, false)]
    [InlineData(IntFlags.One, default(IntFlags), false)]
    public void EnumExtensions_AreAnyFlagsSet_IntFlags(IntFlags value, IntFlags flags, bool expected)
    {
        value.AreAnyFlagsSet(flags).Should().Be(expected);
    }

    [Theory]
    [InlineData(LongFlags.One, LongFlags.One, true)]
    [InlineData(LongFlags.One | LongFlags.Two, LongFlags.One, true)]
    [InlineData(LongFlags.One | LongFlags.Two, LongFlags.Two, true)]
    [InlineData(LongFlags.One | LongFlags.Two, LongFlags.Four, false)]
    [InlineData(LongFlags.One, LongFlags.Two, false)]
    [InlineData(default(LongFlags), LongFlags.One, false)]
    [InlineData(LongFlags.One, default(LongFlags), false)]
    public void EnumExtensions_AreAnyFlagsSet_LongFlags(LongFlags value, LongFlags flags, bool expected)
    {
        value.AreAnyFlagsSet(flags).Should().Be(expected);
    }

    #endregion

    #region SetFlags Tests

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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
