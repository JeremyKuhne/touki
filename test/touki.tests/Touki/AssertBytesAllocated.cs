// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

internal ref struct AssertBytesAllocated
{
    private readonly long _startBytes;
#pragma warning disable IDE0052 // Field is never assigned to
    private readonly long _expectedBytes;
    private readonly long _expectedBytesFramework;
#pragma warning restore IDE0052

    public long BytesAllocated { get; private set; }

    public AssertBytesAllocated(long expectedBytes, long expectedBytesFramework)
    {
        _startBytes = GC.GetAllocatedBytesForCurrentThread();
        _expectedBytes = expectedBytes;
        _expectedBytesFramework = expectedBytesFramework;
    }

    public readonly void Dispose()
    {
        long bytesAllocated = GC.GetAllocatedBytesForCurrentThread() - _startBytes;
#if NETFRAMEWORK
        bytesAllocated.Should().Be(_expectedBytesFramework);
#else
        bytesAllocated.Should().Be(_expectedBytes);
#endif
    }
}
