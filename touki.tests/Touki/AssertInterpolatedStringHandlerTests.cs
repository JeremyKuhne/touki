// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if NETFRAMEWORK
using AssertInterpolatedStringHandler = System.Diagnostics.AssertInterpolatedStringHandler;
#else
using AssertInterpolatedStringHandler = System.Diagnostics.Debug.AssertInterpolatedStringHandler;
#endif

namespace Touki;

public class AssertInterpolatedStringHandlerTests
{
    // The built-in handler in .NET 9 does not support calling the append
    // methods when the assertion succeeds, so limit these tests to .NET Framework.

    [Fact]
    public void Constructor_SetsShouldAppend_FalseWhenConditionTrue()
    {
        bool shouldAppend;
        AssertInterpolatedStringHandler handler = new(5, 1, true, out shouldAppend);
        shouldAppend.Should().BeFalse();
#if NETFRAMEWORK
        handler.AppendLiteral("Hello");
        handler.ToStringAndClear().Should().BeEmpty();
#endif
    }

    [Fact]
    public void Constructor_SetsShouldAppend_TrueWhenConditionFalse()
    {
        bool shouldAppend;
        AssertInterpolatedStringHandler handler = new(5, 1, false, out shouldAppend);
        shouldAppend.Should().BeTrue();
#if NETFRAMEWORK
        handler.AppendLiteral("Hello");
        handler.ToStringAndClear().Should().Be("Hello");
#endif
    }
}
