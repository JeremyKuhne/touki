// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

[TestClass]
public class StoringNull
{
    [TestMethod]
    public void GetIntFromStoredNull()
    {
        Value nullFastValue = Value.Create((object?)null);
        Assert.Throws<InvalidCastException>(() => _ = nullFastValue.As<int>());

        bool success = nullFastValue.TryGetValue(out int result);
        success.Should().BeFalse();

        result.Should().Be(default);
    }
}
