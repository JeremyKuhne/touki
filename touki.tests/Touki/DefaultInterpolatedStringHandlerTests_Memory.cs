// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Text;

namespace Touki;

[Collection("SequentialCollection")]
public unsafe class DefaultInterpolatedStringHandlerTests_Memory
{
    // On .NET Framework this is our implementation. On .NET we're getting built-in.
    // Testing both so we can validate behavior and expected allocations.

    // These are theories to avoid compilation optimization removing the code paths.
    // Need to run sequentially to avoid contention with StringBuilder pooling in
    // string formatting (which will give different memory usage results occasionally).

    [Theory()]
    [InlineData(DayOfWeek.Monday)]
    public void ValidateAllocations(DayOfWeek value)
    {
        StringBuilder builder = new(256);
        builder.AppendFormat("Today is {0}.", value);
        builder.Clear();

        using (AssertBytesAllocated tracker = new(80, 184))
        {
            _ = builder.AppendFormat("Today is {0}.", value).ToString();
        }

        _ = $"Today is {value}.";

        using (AssertBytesAllocated tracker = new(56, 256))
        {
            _ = $"Today is {value}.";
        }

        _ = $"Today is {(int)value}.";

        using (AssertBytesAllocated tracker = new(48, 120))
        {
            _ = $"Today is {(int)value}.";
        }

        _ = $"Today is {(float)value}.";

        using (AssertBytesAllocated tracker = new(48, 120))
        {
            _ = $"Today is {(float)value}.";
        }
    }

    [Theory()]
    [InlineData(DayOfWeek.Monday)]
    public void ValidateAllocations2(DayOfWeek value)
    {
        _ = $"Today is {value}.";
    }
}
