// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

[TestClass]
public class DebuggingTests
{
    [TestMethod]
    public void Assert_ExpressionNotEvaluated_WhenConditionTrue()
    {
        int value = 0;
        Debugging.Assert(condition: true, $"Value {++value}");
        value.Should().Be(0);
    }

#if !DEBUG
    [TestMethod]
    public void Assert_Elided_InRelease()
    {
        int value = 0;
        Debugging.Assert(condition: true, $"Value {++value}");
        value.Should().Be(0);
    }
#endif
}
