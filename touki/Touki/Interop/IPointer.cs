// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Windows.Support;

/// <summary>
///  Used to indicate ownership of a native resource pointer.
/// </summary>
/// <remarks>
///  <para>
///   This should never be put on a struct.
///  </para>
/// </remarks>
public unsafe interface IPointer<TPointer> where TPointer : unmanaged
{
    /// <summary>
    ///  The pointer to the native resource.
    /// </summary>
    TPointer* Pointer { get; }
}
