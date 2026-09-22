// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.TestSupport;

[TestClass]
public class TestAccessorTests
{
    [TestMethod]
    public void Dynamic_OverloadedMethodWithNullArgument_ThrowsArgumentException()
    {
        OverloadedTarget target = new();

        Action action = () => _ = target.TestAccessor.Dynamic.Resolve(null);

        ArgumentException exception = action.Should().Throw<ArgumentException>()
            .WithMessage("Null arguments are not supported when resolving overloaded methods.*")
            .Which;

        exception.ParamName.Should().Be("args");
    }

    private sealed class OverloadedTarget
    {
        internal static string Resolve(string value) => value;

        internal static object Resolve(object value) => value;
    }
}