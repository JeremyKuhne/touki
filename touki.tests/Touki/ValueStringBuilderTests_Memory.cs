// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

[Collection("SequentialCollection")]
public unsafe class ValueStringBuilderTests_Memory
{
    // These are theories to avoid compilation optimization removing the code paths.
    // Need to run sequentially to avoid contention with StringBuilder pooling in
    // string formatting (which will give different memory usage results occasionally).

    [Theory()]
    [InlineData(DayOfWeek.Monday)]
    public void AsHandler_EnsureNoExtraAllocations(DayOfWeek value)
    {
        // Ensure we've rented from the pool and primed any other data.
        _ = TestFormat($"Today is {(int)value}.");
        _ = TestFormat($"Today is {value}.");
        _ = $"Today is {(int)value}.";
        _ = $"Today is {value}.";

        // Check int formatting first.

        using (AssertBytesAllocated assert = new(48, 80))
        {
            _ = TestFormat($"Today is {(int)value}.");
        }

        // This is 104 without DefaultInterpolatedStringHandler, 80 with it.
        using (AssertBytesAllocated assert = new(48, 80))
        {
            _ = $"Today is {(int)value}.";
        }

        // Check enum formatting allocations.

        using (AssertBytesAllocated assert = new(80, 184))
        {
            _ = TestFormat($"Today is {value}.");
        }

        using (AssertBytesAllocated tracker = new(56, 184))
        {
            _ = $"Today is {value}.";
        }

        // Now try with spans
        using ValueStringBuilder builder = new(stackalloc char[100]);
        builder.Append("This is a test");
        ReadOnlySpan<char> readOnlySpan = builder.AsSpan();

        using (AssertBytesAllocated assert = new(72, 72))
        {
            _ = TestFormat($"Message {readOnlySpan}");
        }

        using (AssertBytesAllocated assert = new(72, 128))
        {
            // You normally *have* to convert to string on .NET Framework.
#if NETFRAMEWORK
            _ = $"Message {readOnlySpan.ToString()}";
#else
            _ = $"Message {readOnlySpan}";
#endif
        }

        using (AssertBytesAllocated assert = new(72, 72))
        {
            _ = $"Message {readOnlySpan}";
        }

        Span<char> span = builder.ToString().ToArray();

        using (AssertBytesAllocated assert = new(72, 72))
        {
            _ = TestFormat($"Message {span}");
        }

        using (AssertBytesAllocated assert = new(72, 128))
        {
            // You *have* to convert to string on .NET Framework.
#if NETFRAMEWORK
            _ = $"Message {span.ToString()}";
#else
            _ = $"Message {span}";
#endif
        }
    }

    private static string TestFormat(ref ValueStringBuilder builder) => builder.ToStringAndClear();
}
