// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Original license follows:
//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Touki;

/// <summary>
///  Helper for tracking disposal of objects in debug builds.
/// </summary>
// Debug-only diagnostics: SuppressFinalize is [Conditional("DEBUG")] and the Tracker finalizer fires only when an
// object leaks.
[ExcludeFromCodeCoverage]
public static partial class DisposalTracking
{
    /// <summary>
    ///  Used to suppress finalization in debug builds only.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Unfortunately this can only be used when there is a single implicit conversion operator when called from
    ///   a ref struct. C# tries to cast to anything that fits in object, which leads to an ambiguous error.
    ///  </para>
    ///  <para>
    ///   You need to add <see cref="GC.SuppressFinalize"/> under #ifdef when you don't have a single implicit conversion.
    ///  </para>
    /// </remarks>
    [Conditional("DEBUG")]
    public static void SuppressFinalize(object @object)
    {
        GC.SuppressFinalize(@object);
    }
}
