// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.TestSupport;

/// <summary>
///  A scoped helper that records the current thread's allocated bytes and
///  asserts that no further managed allocations occur between its creation
///  and disposal.
/// </summary>
/// <remarks>
///  <para>
///   <see cref="MemoryWatch"/> is built around
///   <see cref="GC.GetAllocatedBytesForCurrentThread"/>, which returns a
///   monotonically increasing count of bytes the current thread has
///   allocated on the managed heap. Capturing that value at one point and
///   comparing it later gives a precise, deterministic, single-threaded
///   measurement - unlike GC counters or memory diagnosers, no
///   collection has to run and no warm-up iterations are required.
///  </para>
///  <para>
///   Typical use is a <see langword="using"/> block around a piece of code
///   that must not allocate:
///   <code>
///    using (MemoryWatch.Create)
///    {
///        target.HotPath(value);
///    }
///   </code>
///   When the block exits, <see cref="Dispose"/> calls
///   <see cref="Validate"/>, which throws an
///   <see cref="AllocationException"/> if any bytes were allocated on the
///   current thread while the watch was active. The test fails with the
///   exception (no test-framework dependency).
///  </para>
///  <para>
///   For incremental checks, store the watch and call
///   <see cref="Validate"/> at each checkpoint - the helper resets
///   its baseline after each successful validation so subsequent calls
///   measure only the bytes allocated since the previous call:
///   <code>
///    MemoryWatch watch = MemoryWatch.Create;
///    target.Step1(); watch.Validate();
///    target.Step2(); watch.Validate();
///   </code>
///  </para>
///  <para>
///   <b>JIT warm-up.</b> The first call to a generic method instantiation
///   allocates on the managed heap (the JIT itself allocates while
///   producing code). If your code under test exercises a previously
///   unused generic specialization, take the watch <em>after</em> a
///   warm-up call so the JIT cost is not attributed to the measured
///   path:
///   <code>
///    target.HotPath(value); // warm up
///    using (MemoryWatch.Create)
///    {
///        target.HotPath(value);
///    }
///   </code>
///  </para>
///  <para>
///   <b>Limitations.</b> Only single-threaded code can be measured -
///   <see cref="GC.GetAllocatedBytesForCurrentThread"/> returns a
///   per-thread counter, so allocations on other threads are invisible.
///   Boxing, closure captures, and any reference-type literal in the
///   measured block all count as allocations.
///  </para>
/// </remarks>
public ref struct MemoryWatch
{
    private long _allocations;

    /// <summary>
    ///  Initializes a new <see cref="MemoryWatch"/> with the given baseline
    ///  in bytes. Prefer <see cref="Create"/> over this constructor for
    ///  the normal case of capturing the current thread state.
    /// </summary>
    /// <param name="allocations">
    ///  The byte count to use as the baseline. Subsequent calls to
    ///  <see cref="Validate"/> compare the current thread's allocated
    ///  bytes against this value.
    /// </param>
    public MemoryWatch(long allocations) => _allocations = allocations;

    /// <summary>
    ///  Returns a new <see cref="MemoryWatch"/> whose baseline is the
    ///  current thread's allocated-bytes count at the moment of the call.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   This is the common entry point. Use it inside a
    ///   <see langword="using"/> block to assert that a region of code
    ///   does not allocate.
    ///  </para>
    /// </remarks>
    public static MemoryWatch Create => new(GC.GetAllocatedBytesForCurrentThread());

    /// <summary>
    ///  Asserts that <see cref="Validate"/> has been called by the time
    ///  the watch is disposed.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Disposal happens automatically at the end of a
    ///   <see langword="using"/> block; the call to <see cref="Validate"/>
    ///   surfaces any allocation as an <see cref="AllocationException"/>.
    ///  </para>
    /// </remarks>
    public void Dispose() => Validate();

    /// <summary>
    ///  Throws an <see cref="AllocationException"/> if any bytes have been
    ///  allocated on the current thread since the watch was created or
    ///  since the last successful call to <see cref="Validate"/>.
    /// </summary>
    /// <exception cref="AllocationException">
    ///  Allocations occurred on the current thread while the watch was
    ///  active. The message includes the number of bytes observed.
    /// </exception>
    /// <remarks>
    ///  <para>
    ///   The baseline is reset after a successful check, so a single
    ///   watch can validate multiple sequential checkpoints. The reset
    ///   happens by reading <see cref="GC.GetAllocatedBytesForCurrentThread"/>
    ///   again rather than by reusing the prior baseline plus the
    ///   observed delta - producing the exception message itself
    ///   can allocate, and that allocation must not be counted against
    ///   the next checkpoint.
    ///  </para>
    /// </remarks>
    public void Validate()
    {
        long current = GC.GetAllocatedBytesForCurrentThread();
        long delta = current - _allocations;
        if (delta != 0)
        {
            // Reset the baseline before throwing so the message-building
            // allocation does not become part of the next checkpoint, in
            // case the caller catches and continues.
            _allocations = GC.GetAllocatedBytesForCurrentThread();
            throw new AllocationException(delta);
        }

        _allocations = current;
    }
}

/// <summary>
///  Thrown by <see cref="MemoryWatch.Validate"/> when bytes were allocated
///  on the current thread inside a measured region.
/// </summary>
/// <remarks>
///  <para>
///   The exception carries the number of bytes observed so callers can
///   format their own diagnostics. The exception message is preformatted
///   for display in standard test-runner output.
///  </para>
/// </remarks>
public sealed class AllocationException : Exception
{
    /// <summary>
    ///  The number of bytes allocated on the current thread inside the
    ///  watched region.
    /// </summary>
    public long AllocatedBytes { get; }

    /// <summary>
    ///  Initializes a new <see cref="AllocationException"/> for the
    ///  specified number of bytes.
    /// </summary>
    /// <param name="allocatedBytes">Bytes observed.</param>
    public AllocationException(long allocatedBytes)
        : base($"Expected zero allocations on the current thread, but {allocatedBytes} bytes were allocated.")
        => AllocatedBytes = allocatedBytes;
}
