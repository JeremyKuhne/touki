// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public class ArgumentOutOfRangeExtensionsTests
{
    [Fact]
    public void ThrowIfZero_Long_Zero_Throws()
    {
        long value = 0;
        Action action = () => ArgumentOutOfRangeException.ThrowIfZero(value);
        action.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(value));
    }

    [Fact]
    public void ThrowIfZero_Long_NonZero_DoesNotThrow()
    {
        ArgumentOutOfRangeException.ThrowIfZero(1L);
        ArgumentOutOfRangeException.ThrowIfZero(-1L);
    }

    [Fact]
    public void ThrowIfZero_Uint_Zero_Throws()
    {
        uint value = 0;
        Action action = () => ArgumentOutOfRangeException.ThrowIfZero(value);
        action.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(value));
    }

    [Fact]
    public void ThrowIfZero_Double_Zero_Throws()
    {
        double value = 0;
        Action action = () => ArgumentOutOfRangeException.ThrowIfZero(value);
        action.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(value));
    }

    [Fact]
    public void ThrowIfZero_Decimal_Zero_Throws()
    {
        decimal value = 0;
        Action action = () => ArgumentOutOfRangeException.ThrowIfZero(value);
        action.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(value));
    }

    [Fact]
    public void ThrowIfNegative_Long_Negative_Throws()
    {
        long value = -1;
        Action action = () => ArgumentOutOfRangeException.ThrowIfNegative(value);
        action.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(value));
    }

    [Fact]
    public void ThrowIfNegative_Long_ZeroOrPositive_DoesNotThrow()
    {
        ArgumentOutOfRangeException.ThrowIfNegative(0L);
        ArgumentOutOfRangeException.ThrowIfNegative(1L);
    }

    [Fact]
    public void ThrowIfNegative_Double_Negative_Throws()
    {
        double value = -0.1;
        Action action = () => ArgumentOutOfRangeException.ThrowIfNegative(value);
        action.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(value));
    }

    [Fact]
    public void ThrowIfNegative_Decimal_Negative_Throws()
    {
        decimal value = -1m;
        Action action = () => ArgumentOutOfRangeException.ThrowIfNegative(value);
        action.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(value));
    }

    [Fact]
    public void ThrowIfNegativeOrZero_Long_Zero_Throws()
    {
        long value = 0;
        Action action = () => ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        action.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(value));
    }

    [Fact]
    public void ThrowIfNegativeOrZero_Long_Negative_Throws()
    {
        long value = -1;
        Action action = () => ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        action.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(value));
    }

    [Fact]
    public void ThrowIfNegativeOrZero_Long_Positive_DoesNotThrow()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(1L);
    }

    [Fact]
    public void ThrowIfNegativeOrZero_Double_Zero_Throws()
    {
        double value = 0;
        Action action = () => ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        action.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(value));
    }

    [Fact]
    public void ThrowIfNegativeOrZero_Decimal_Zero_Throws()
    {
        decimal value = 0;
        Action action = () => ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        action.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(value));
    }
}
