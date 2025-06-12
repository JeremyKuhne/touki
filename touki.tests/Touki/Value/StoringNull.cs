// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.ValueTests;

public class StoringNull
{
    [Fact]
    public void GetIntFromStoredNull()
    {
        Value nullFastValue = new((object?)null);
        Assert.Throws<InvalidCastException>(() => _ = nullFastValue.As<int>());

        bool success = nullFastValue.TryGetValue(out int result);
        Assert.False(success);

        Assert.Equal(default, result);
    }
}
