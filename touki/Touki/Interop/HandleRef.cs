// Copyright (c) 2025 Jeremy W Kuhne
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
///  Adapter to use when owning classes cannot directly implement <see cref="IHandle{T}"/>.
/// </summary>
public readonly struct HandleRef<THandle> : IHandle<THandle>, IEquatable<HandleRef<THandle>>
    where THandle : unmanaged, IEquatable<THandle>
{
    /// <inheritdoc/>
    public required object? Wrapper { get; init; }

    /// <inheritdoc/>
    public required THandle Handle { get; init; }

    /// <summary>
    ///  Initializes a new instance of the <see cref="HandleRef{THandle}"/> struct.
    /// </summary>
    /// <param name="wrapper">The object responsible for the handle lifetime.</param>
    /// <param name="handle">The handle to wrap.</param>
    [SetsRequiredMembers]
    public HandleRef(object? wrapper, THandle handle)
    {
        Wrapper = wrapper;
        Handle = handle;
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref="HandleRef{THandle}"/> struct
    ///  from an existing <see cref="IHandle{THandle}"/> implementation.
    /// </summary>
    /// <param name="handle">The handle implementation to wrap.</param>
    [SetsRequiredMembers]
    public HandleRef(IHandle<THandle>? handle)
    {
        Wrapper = handle;
        Handle = handle?.Handle ?? default;
    }

    /// <inheritdoc/>
    public bool Equals(HandleRef<THandle> other)
        => other.Handle.Equals(Handle) && Equals(other.Wrapper, Wrapper);

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is THandle other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Wrapper?.GetHashCode() ?? 0 ^ Handle.GetHashCode();

    /// <summary>
    ///  Determines whether two specified instances of <see cref="HandleRef{THandle}"/> are equal.
    /// </summary>
    /// <param name="left">The first handle reference to compare.</param>
    /// <param name="right">The second handle reference to compare.</param>
    /// <returns><see langword="true"/> if <paramref name="left"/> and <paramref name="right"/> are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(HandleRef<THandle> left, HandleRef<THandle> right) => left.Equals(right);

    /// <summary>
    ///  Determines whether two specified instances of <see cref="HandleRef{THandle}"/> are not equal.
    /// </summary>
    /// <param name="left">The first handle reference to compare.</param>
    /// <param name="right">The second handle reference to compare.</param>
    /// <returns><see langword="true"/> if <paramref name="left"/> and <paramref name="right"/> are not equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(HandleRef<THandle> left, HandleRef<THandle> right) => !(left == right);

    /// <summary>
    ///  Gets a value indicating whether this handle is a "null" handle.
    /// </summary>
    /// <value>
    ///  <see langword="true"/> if the handle equals the default value; otherwise, <see langword="false"/>.
    /// </value>
    public bool IsNull => Handle.Equals(default);
}
