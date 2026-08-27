// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Original license follows:
//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;

namespace Touki;

public static partial class DisposalTracking
{
    /// <summary>
    ///  Helper base class for tracking undisposed objects. Derive from this (in DEBUG builds only) to track
    ///  construction and proper destruction.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Fires if <see cref="GC.SuppressFinalize(object)"/> is not called on the class and the class is finalized.
    ///   As such you must suppress finalization when disposing to "signal" that you've been disposed properly.
    ///  </para>
    ///  <para>
    ///   The debug only static <see cref="SuppressFinalize(object)"/> can be called when you only derive from this
    ///   class in debug builds.
    ///  </para>
    /// </remarks>
    public abstract class Tracker
    {
        private readonly StackTrace? _originatingStack;
        private readonly bool _throwIfFinalized;

        /// <summary>
        ///  Create a tracker that will throw if finalized without being disposed.
        /// </summary>
        /// <param name="throwIfFinalized">
        ///  <see langword="true"/> to capture the originating stack and throw if finalized; otherwise,
        ///  <see langword="false"/>.
        /// </param>
        public Tracker(bool throwIfFinalized = true)
        {
            _throwIfFinalized = throwIfFinalized;
            _originatingStack = _throwIfFinalized ? new StackTrace() : null;
        }

        /// <summary>
        ///  Finalizer.
        /// </summary>
        ~Tracker()
        {
            if (_throwIfFinalized)
            {
                // Not asserting here as assertions take down test runs.
                throw new InvalidOperationException($"Did not dispose `{GetFriendlyTypeName(GetType())}`. Originating stack:\n{_originatingStack}");
            }
        }

        private static string GetFriendlyTypeName(Type type)
        {
            string friendlyName = type.Name;
            if (type.IsGenericType)
            {
                int backtick = friendlyName.IndexOf('`');
                if (backtick != -1)
                {
                    friendlyName = friendlyName[..backtick];
                }

                friendlyName += $"<{string.Join(",", type.GetGenericArguments().Select(GetFriendlyTypeName))}>";
            }

            return friendlyName;
        }
    }
}
