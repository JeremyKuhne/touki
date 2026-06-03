// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Exceptions;

[TestClass]
public class ExceptionExtensionsTests
{
    [TestMethod]
    public void InvalidOperationException_Throw_ThrowsWithMessage()
    {
        Action action = () => InvalidOperationException.Throw("boom");
        action.Should().Throw<InvalidOperationException>().WithMessage("boom");
    }

    [TestMethod]
    public void ArgumentOutOfRangeException_Throw_ThrowsWithParamName()
    {
        Action action = () => ArgumentOutOfRangeException.Throw("count");
        action.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("count");
    }

    [TestMethod]
    public void ArgumentOutOfRangeException_Throw_ThrowsWithParamNameAndMessage()
    {
        Action action = () => ArgumentOutOfRangeException.Throw("count", "must be positive");

        ArgumentOutOfRangeException ex = action.Should().Throw<ArgumentOutOfRangeException>().Which;
        ex.ParamName.Should().Be("count");
        ex.Message.Should().Contain("must be positive");
    }

    [TestMethod]
    public void OutOfMemoryException_Throw_Throws()
    {
        Action action = OutOfMemoryException.Throw;
        action.Should().Throw<OutOfMemoryException>();
    }

    [TestMethod]
    public void NotSupportedException_Throw_DefaultMessage_Throws()
    {
        Action action = () => NotSupportedException.Throw();
        action.Should().Throw<NotSupportedException>();
    }

    [TestMethod]
    public void NotSupportedException_Throw_WithMessage_ThrowsWithMessage()
    {
        Action action = () => NotSupportedException.Throw("not here");
        action.Should().Throw<NotSupportedException>().WithMessage("not here");
    }
}
