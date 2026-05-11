// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Exceptions;

public class ExceptionExtensionsTests
{
    [Fact]
    public void InvalidOperationException_Throw_ThrowsWithMessage()
    {
        Action action = () => InvalidOperationException.Throw("boom");
        action.Should().Throw<InvalidOperationException>().WithMessage("boom");
    }

    [Fact]
    public void ArgumentOutOfRangeException_Throw_ThrowsWithParamName()
    {
        Action action = () => ArgumentOutOfRangeException.Throw("count");
        action.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("count");
    }

    [Fact]
    public void ArgumentOutOfRangeException_Throw_ThrowsWithParamNameAndMessage()
    {
        Action action = () => ArgumentOutOfRangeException.Throw("count", "must be positive");

        ArgumentOutOfRangeException ex = action.Should().Throw<ArgumentOutOfRangeException>().Which;
        ex.ParamName.Should().Be("count");
        ex.Message.Should().Contain("must be positive");
    }

    [Fact]
    public void OutOfMemoryException_Throw_Throws()
    {
        Action action = OutOfMemoryException.Throw;
        action.Should().Throw<OutOfMemoryException>();
    }

    [Fact]
    public void NotSupportedException_Throw_DefaultMessage_Throws()
    {
        Action action = () => NotSupportedException.Throw();
        action.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void NotSupportedException_Throw_WithMessage_ThrowsWithMessage()
    {
        Action action = () => NotSupportedException.Throw("not here");
        action.Should().Throw<NotSupportedException>().WithMessage("not here");
    }
}
