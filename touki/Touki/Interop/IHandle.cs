﻿// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Based on code from https://github.com/dotnet/winforms
//
// Original header
// ---------------
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Touki.Interop;

/// <summary>
///  Used to abstract access to classes that contain a potentially owned handle.
/// </summary>
/// <remarks>
///  <para>
///   The key benefit of this is that we can keep the owning class from being collected during interop calls.
///   <see cref="HandleRef"/> wraps arbitrary owners with target handles. Having this interface allows implicit use
///   of the classes that meet this common pattern in interop and encourages correct alignment with the proper owner.
///  </para>
///  <para>
///   Note that keeping objects alive is necessary ONLY when the object has a finalizer that will explicitly
///   close the handle.
///  </para>
///  <para>
///   When implementing P/Invoke wrappers that take this interface they should not directly take
///   <see cref="IHandle{THandle}"/>, but should take a generic "T" that is constrained to IHandle{T}. Doing
///   it this way prevents boxing of structs. The "T" parameters should also be marked as <see langword="in"/>
///   to allow structs to be passed by reference instead of by value.
///  </para>
///  <para>
///   When implementing this on a struct it is important that either the struct itself is marked as readonly
///   or these properties are to avoid extra struct copies.
///  </para>
/// </remarks>
public interface IHandle<THandle> where THandle : unmanaged
{
    /// <summary>
    ///  The handle to the native resource.
    /// </summary>
    THandle Handle { get; }

    /// <summary>
    ///  Owner of the <see cref="Handle"/> that might close it when finalized. Default is the
    ///  <see cref="IHandle{THandle}"/> implementer.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   This allows decoupling the owner from the <typeparamref name="THandle"/> provider and avoids boxing when
    ///   <see cref="IHandle{THandle}"/> is on a struct. See <see cref="HandleRef{THandle}"/> for a concrete usage.
    ///  </para>
    /// </remarks>
#if NETFRAMEWORK
    object? Wrapper { get; }
#else
    object? Wrapper => this;
#endif
}
