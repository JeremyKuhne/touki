// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Allocation checks read GC.GetAllocatedBytesForCurrentThread(); keep them Release-only and
// sequential so shared-pool contention under method-level parallelism cannot turn a pooled
// operation into a measured allocation.
#if !DEBUG

namespace Touki;

/// <summary>
///  Zero-allocation checks for <see cref="Value"/>'s union pattern matching. Kept separate and
///  <see cref="DoNotParallelizeAttribute"/> so they do not flake under method-level parallelism.
/// </summary>
[TestClass]
[DoNotParallelize]
public class ValueUnionTests_Memory
{
    [TestMethod]
    public void Match_Int_DoesNotAllocate()
    {
        Value value = Value.Create(42);
        _ = value is int;

        bool matched = false;
        int captured = 0;
        using (MemoryWatch.Create)
        {
            if (value is int result)
            {
                matched = true;
                captured = result;
            }
        }

        matched.Should().BeTrue();
        captured.Should().Be(42);
    }

    [TestMethod]
    public void Match_String_DoesNotAllocate()
    {
        Value value = Value.Create("hello");
        _ = value is string;

        string? captured = null;
        using (MemoryWatch.Create)
        {
            if (value is string result)
            {
                captured = result;
            }
        }

        captured.Should().Be("hello");
    }

    [TestMethod]
    public void Match_DateTimeOffset_DoesNotAllocate()
    {
        DateTimeOffset source = new(2026, 7, 6, 12, 30, 0, TimeSpan.FromHours(-5));
        Value value = Value.Create(source);
        _ = value is DateTimeOffset;

        DateTimeOffset captured = default;
        using (MemoryWatch.Create)
        {
            if (value is DateTimeOffset result)
            {
                captured = result;
            }
        }

        captured.Should().Be(source);
    }
}

#endif
