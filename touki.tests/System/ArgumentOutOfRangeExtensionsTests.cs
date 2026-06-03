// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

[TestClass]
public class ArgumentOutOfRangeExtensionsTests
{
    [TestMethod]
    public void ThrowIfZero_Long_Zero_Throws()
    {
        long value = 0;
        Action action = () => ArgumentOutOfRangeException.ThrowIfZero(value);
        action.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(value));
    }

    [TestMethod]
    public void ThrowIfZero_Long_NonZero_DoesNotThrow()
    {
        ArgumentOutOfRangeException.ThrowIfZero(1L);
        ArgumentOutOfRangeException.ThrowIfZero(-1L);
    }

    [TestMethod]
    public void ThrowIfZero_Uint_Zero_Throws()
    {
        uint value = 0;
        Action action = () => ArgumentOutOfRangeException.ThrowIfZero(value);
        action.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(value));
    }

    [TestMethod]
    public void ThrowIfZero_Ulong_Zero_Throws()
    {
        ulong value = 0;
        Action action = () => ArgumentOutOfRangeException.ThrowIfZero(value);
        action.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(value));
    }

    [TestMethod]
    public void ThrowIfZero_Nint_Zero_Throws()
    {
        nint value = 0;
        Action action = () => ArgumentOutOfRangeException.ThrowIfZero(value);
        action.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(value));
    }

    [TestMethod]
    public void ThrowIfZero_Nuint_Zero_Throws()
    {
        nuint value = 0;
        Action action = () => ArgumentOutOfRangeException.ThrowIfZero(value);
        action.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(value));
    }

    [TestMethod]
    public void ThrowIfZero_Float_Zero_Throws()
    {
        float value = 0f;
        Action action = () => ArgumentOutOfRangeException.ThrowIfZero(value);
        action.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(value));
    }

    [TestMethod]
    public void ThrowIfZero_Double_Zero_Throws()
    {
        double value = 0;
        Action action = () => ArgumentOutOfRangeException.ThrowIfZero(value);
        action.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(value));
    }

    [TestMethod]
    public void ThrowIfZero_Decimal_Zero_Throws()
    {
        decimal value = 0;
        Action action = () => ArgumentOutOfRangeException.ThrowIfZero(value);
        action.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(value));
    }

    [TestMethod]
    public void ThrowIfNegative_Long_Negative_Throws()
    {
        long value = -1;
        Action action = () => ArgumentOutOfRangeException.ThrowIfNegative(value);
        action.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(value));
    }

    [TestMethod]
    public void ThrowIfNegative_Long_ZeroOrPositive_DoesNotThrow()
    {
        ArgumentOutOfRangeException.ThrowIfNegative(0L);
        ArgumentOutOfRangeException.ThrowIfNegative(1L);
    }

    [TestMethod]
    public void ThrowIfNegative_Nint_Negative_Throws()
    {
        nint value = -1;
        Action action = () => ArgumentOutOfRangeException.ThrowIfNegative(value);
        action.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(value));
    }

    [TestMethod]
    public void ThrowIfNegative_Float_Negative_Throws()
    {
        float value = -0.1f;
        Action action = () => ArgumentOutOfRangeException.ThrowIfNegative(value);
        action.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(value));
    }

    [TestMethod]
    public void ThrowIfNegative_Double_Negative_Throws()
    {
        double value = -0.1;
        Action action = () => ArgumentOutOfRangeException.ThrowIfNegative(value);
        action.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(value));
    }

    [TestMethod]
    public void ThrowIfNegative_Decimal_Negative_Throws()
    {
        decimal value = -1m;
        Action action = () => ArgumentOutOfRangeException.ThrowIfNegative(value);
        action.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(value));
    }

    [TestMethod]
    public void ThrowIfNegativeOrZero_Long_Zero_Throws()
    {
        long value = 0;
        Action action = () => ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        action.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(value));
    }

    [TestMethod]
    public void ThrowIfNegativeOrZero_Long_Negative_Throws()
    {
        long value = -1;
        Action action = () => ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        action.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(value));
    }

    [TestMethod]
    public void ThrowIfNegativeOrZero_Long_Positive_DoesNotThrow()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(1L);
    }

    [TestMethod]
    public void ThrowIfNegativeOrZero_Nint_Zero_Throws()
    {
        nint value = 0;
        Action action = () => ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        action.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(value));
    }

    [TestMethod]
    public void ThrowIfNegativeOrZero_Float_Negative_Throws()
    {
        float value = -1f;
        Action action = () => ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        action.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(value));
    }

    [TestMethod]
    public void ThrowIfNegativeOrZero_Double_Zero_Throws()
    {
        double value = 0;
        Action action = () => ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        action.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(value));
    }

    [TestMethod]
    public void ThrowIfNegativeOrZero_Decimal_Zero_Throws()
    {
        decimal value = 0;
        Action action = () => ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        action.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(value));
    }
}
