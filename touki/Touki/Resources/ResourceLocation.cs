// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Resources;

/// <summary>
///  Describes a single resource located by <see cref="RawResourceReader"/>: its index, stored type,
///  and the length in bytes of its raw value content.
/// </summary>
public readonly struct ResourceLocation
{
    internal ResourceLocation(int index, ResourceTypeCode typeCode, int byteLength, int contentOffset)
    {
        Index = index;
        TypeCode = typeCode;
        ByteLength = byteLength;
        ContentOffset = contentOffset;
    }

    /// <summary>
    ///  The zero-based index of the resource within the reader.
    /// </summary>
    public int Index { get; }

    /// <summary>
    ///  The stored type of the resource value.
    /// </summary>
    public ResourceTypeCode TypeCode { get; }

    /// <summary>
    ///  The length, in bytes, of the raw value content (with any length prefix already removed). It is
    ///  <c>0</c> for <see cref="ResourceTypeCode.Null"/> and for user types (see
    ///  <see cref="IsUserType"/>).
    /// </summary>
    public int ByteLength { get; }

    /// <summary>
    ///  <see langword="true"/> when the resource is a serialized user type
    ///  (<see cref="TypeCode"/> is at or above <see cref="ResourceTypeCode.StartOfUserTypes"/>), whose
    ///  value bytes are not exposed.
    /// </summary>
    public bool IsUserType => TypeCode >= ResourceTypeCode.StartOfUserTypes;

    /// <summary>
    ///  Absolute offset of the value content within the reader's memory.
    /// </summary>
    internal int ContentOffset { get; }
}
