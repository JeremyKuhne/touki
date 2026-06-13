// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if !DEBUG
using Touki.Text;

namespace Touki;

/// <summary>
///  Allocation-sensitive <see cref="ValueStringBuilder"/> tests, split out from
///  <see cref="ValueStringBuilderTests"/> so they can opt out of parallel execution.
/// </summary>
/// <remarks>
///  <para>
///   These assert zero allocations via <see cref="MemoryWatch"/>, which reads
///   <see cref="System.GC.GetAllocatedBytesForCurrentThread"/>. The assembly runs tests at
///   method-level parallelism (see TestAssemblyInfo.cs), and a concurrent test can drain a
///   shared ArrayPool bucket between the warmup return and the measured rent while
///   <see cref="ValueStringBuilder"/> grows - turning an otherwise pool-satisfied rent into a
///   real allocation and failing the watch intermittently. Running them sequentially via
///   <see cref="DoNotParallelizeAttribute"/> removes that contention, mirroring
///   <see cref="DefaultInterpolatedStringHandlerTests_Memory"/>.
///  </para>
///  <para>
///   Compiled only in Release; in Debug the allocation profile differs and the watch is not
///   meaningful.
///  </para>
/// </remarks>
[DoNotParallelize]
[TestClass]
public class ValueStringBuilderTests_Memory
{
    [TestMethod]
    public void Append_InterpolatedStringWithLargeContent_DoesNotAllocateIntermediateString()
    {
        string large = new('x', 8192);
        {
            using ValueStringBuilder warmup = new(stackalloc char[32]);
            warmup.Append($"<{large}>");
        }

        using ValueStringBuilder builder = new(stackalloc char[32]);
        using (MemoryWatch.Create)
        {
            builder.Append($"<{large}>");
        }

        builder.ToString().Should().Be("<" + large + ">");
    }

    [TestMethod]
    public void AppendLine_InterpolatedStringWithLargeContent_DoesNotAllocateIntermediateString()
    {
        string large = new('x', 8192);
        {
            using ValueStringBuilder warmup = new(stackalloc char[32]);
            warmup.AppendLine($"<{large}>");
        }

        using ValueStringBuilder builder = new(stackalloc char[32]);
        using (MemoryWatch.Create)
        {
            builder.AppendLine($"<{large}>");
        }

        builder.ToString().Should().Be("<" + large + ">" + Environment.NewLine);
    }
}
#endif
