// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Touki.TestSupport;

/// <summary>
///  Use (within a using) to eat asserts.
/// </summary>
public sealed class NoAssertContext : IDisposable
{
    // Suppression is tracked with an AsyncLocal depth so it follows the logical flow of execution -
    // across threads and async continuations - rather than being pinned to the thread that created the
    // context. A failure is only swallowed when the ambient flow has a non-zero suppression depth; any
    // other flow (for example a parallel test) is routed to the original listeners and still fails.
    //
    // The custom listener is installed into Trace.Listeners exactly once, from the static constructor.
    // The CLR runs a static constructor a single time, fully serialized, with a happens-before guarantee
    // for every thread that subsequently touches the type, so the listener swap is race-free and visible
    // to all threads without any double-checked-locking or volatile gymnastics. It runs only when a test
    // first creates a NoAssertContext (not for unrelated consumers of this package).
    //
    // All pre-existing listeners are captured and removed so that NoAssertListener is the sole listener.
    // Debug.Fail/Trace invoke every registered listener, so a throwing listener (for example the test
    // framework's own assert-to-exception listener, or touki's ThrowingTraceListener) left in the
    // collection would fire even while we intend to suppress. By making our listener the only one and
    // forwarding to the captured originals only when the ambient depth is zero, suppression is honored
    // and normal behavior (including the throwing listeners) is preserved outside a context.

    // Ambient suppression depth for the current logical flow of execution.
    private static readonly AsyncLocal<int> s_suppressionDepth;

    // The listeners that were present before we installed ours. Forwarded to when not suppressing.
    private static readonly TraceListener[] s_originalListeners;
    private static readonly NoAssertListener s_noAssertListener;

    private bool _disposed;

#pragma warning disable CA1810 // Initialize reference type static fields inline
    // An explicit static constructor is intentional here: it performs the one-time Trace.Listeners swap
    // with side effects and relies on the CLR's guarantee that a static constructor runs exactly once,
    // fully serialized, and happens-before any thread that subsequently touches the type. That ordering
    // is what makes the install race-free without volatile or double-checked locking.
    static NoAssertContext()
#pragma warning restore CA1810
    {
        s_suppressionDepth = new();
        s_noAssertListener = new();

        // Capture every existing listener so we can forward to them when not suppressing.
        s_originalListeners = [.. Trace.Listeners.Cast<TraceListener>()];

        // Hook our custom listener first so we don't lose assertions during the swap, then remove the
        // originals so ours is the only listener that Debug.Fail/Trace will invoke.
        Trace.Listeners.Add(s_noAssertListener);

        foreach (TraceListener listener in s_originalListeners)
        {
            Trace.Listeners.Remove(listener);
        }
    }

    /// <summary>
    ///  Instantiates a context that suppresses asserts for the current flow of execution.
    /// </summary>
    public NoAssertContext()
    {
        // Increase the suppression depth for the current flow. Because this is tracked with an AsyncLocal
        // it follows the execution context across threads and async continuations, so it stays correct
        // even when disposal happens on a different thread than construction. The static constructor has
        // already installed the suppressing listener by the time we get here.
        s_suppressionDepth.Value++;
    }

    /// <summary>
    ///  Disposes the context, restoring assert behavior for the current flow of execution.
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Decrease the suppression depth for the current flow. Nested contexts simply restore the prior
        // depth; the listener stays installed and forwards to the original listeners once depth reaches zero.
        s_suppressionDepth.Value--;
    }

#pragma warning disable CA1821 // Remove empty Finalizers
    /// <summary>
    ///  Finalizer to catch undisposed contexts.
    /// </summary>
    ~NoAssertContext()
#pragma warning restore CA1821
    {
        // We need this class to be used in a using to effectively rationalize about a test.
        throw new InvalidOperationException($"Did not dispose {nameof(NoAssertContext)}");
    }

    private sealed class NoAssertListener : TraceListener
    {
        public NoAssertListener()
            : base(typeof(NoAssertListener).FullName)
        {
        }

        public override void Fail(string? message)
        {
            if (s_suppressionDepth.Value == 0)
            {
                foreach (TraceListener listener in s_originalListeners)
                {
                    listener.Fail(message);
                }
            }
        }

        public override void Fail(string? message, string? detailMessage)
        {
            if (s_suppressionDepth.Value == 0)
            {
                foreach (TraceListener listener in s_originalListeners)
                {
                    listener.Fail(message, detailMessage);
                }
            }
        }

        // Write and WriteLine are virtual

        public override void Write(string? message)
        {
            if (s_suppressionDepth.Value == 0)
            {
                foreach (TraceListener listener in s_originalListeners)
                {
                    listener.Write(message);
                }
            }
        }

        public override void WriteLine(string? message)
        {
            if (s_suppressionDepth.Value == 0)
            {
                foreach (TraceListener listener in s_originalListeners)
                {
                    listener.WriteLine(message);
                }
            }
        }
    }
}
