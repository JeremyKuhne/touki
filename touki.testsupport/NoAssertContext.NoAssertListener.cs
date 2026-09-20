// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Touki.TestSupport;

public sealed partial class NoAssertContext
{
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
