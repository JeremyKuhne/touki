// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public class ArgumentExceptionExtensionsTests
{
    [Test]
    public void ThrowIfNullOrEmpty_Null_ThrowsArgumentNull()
    {
        string? value = null;
        Action action = () => ArgumentException.ThrowIfNullOrEmpty(value);
        action.Should().Throw<ArgumentNullException>().WithParameterName(nameof(value));
    }

    [Test]
    public void ThrowIfNullOrEmpty_Empty_ThrowsArgument()
    {
        string value = string.Empty;
        Action action = () => ArgumentException.ThrowIfNullOrEmpty(value);
        action.Should().Throw<ArgumentException>()
            .Which.ParamName.Should().Be(nameof(value));
    }

    [Test]
    public void ThrowIfNullOrEmpty_WhiteSpace_DoesNotThrow()
    {
        ArgumentException.ThrowIfNullOrEmpty("  ");
    }

    [Test]
    public void ThrowIfNullOrEmpty_NonEmpty_DoesNotThrow()
    {
        ArgumentException.ThrowIfNullOrEmpty("x");
    }

    [Test]
    public void ThrowIfNullOrWhiteSpace_Null_ThrowsArgumentNull()
    {
        string? value = null;
        Action action = () => ArgumentException.ThrowIfNullOrWhiteSpace(value);
        action.Should().Throw<ArgumentNullException>().WithParameterName(nameof(value));
    }

    [Test]
    public void ThrowIfNullOrWhiteSpace_Empty_ThrowsArgument()
    {
        string value = string.Empty;
        Action action = () => ArgumentException.ThrowIfNullOrWhiteSpace(value);
        action.Should().Throw<ArgumentException>()
            .Which.ParamName.Should().Be(nameof(value));
    }

    [Test]
    public void ThrowIfNullOrWhiteSpace_WhiteSpaceOnly_ThrowsArgument()
    {
        string value = " \t\r\n";
        Action action = () => ArgumentException.ThrowIfNullOrWhiteSpace(value);
        action.Should().Throw<ArgumentException>()
            .Which.ParamName.Should().Be(nameof(value));
    }

    [Test]
    public void ThrowIfNullOrWhiteSpace_NonWhiteSpace_DoesNotThrow()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(" x ");
    }
}
