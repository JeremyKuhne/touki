// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Resources;

/// <summary>
///  Identifies the type of a value stored in a default-format <c>.resources</c> file.
/// </summary>
/// <remarks>
///  <para>
///   The values are the on-disk contract of the binary <c>.resources</c> format (version 2) and
///   must never change. They mirror the runtime's internal
///   <c>System.Resources.ResourceTypeCode</c>. <see cref="RawResourceReader"/> exposes them so a
///   caller can inspect a resource's stored type without decoding or deserializing the value.
///  </para>
///  <para>
///   Values <c>0x00</c> through <c>0x1F</c> are primitives and reserved values; <c>0x20</c> through
///   <c>0x3F</c> are specially recognized types (<c>byte[]</c> and <see cref="System.IO.Stream"/>);
///   values at or above <see cref="StartOfUserTypes"/> identify serialized user types, whose index
///   into the file's type table is <c>typeCode - StartOfUserTypes</c>.
///  </para>
/// </remarks>
public enum ResourceTypeCode
{
    /// <summary>
    ///  A <see langword="null"/> value.
    /// </summary>
    Null = 0,

    /// <summary>
    ///  A <see cref="string"/>, stored as length-prefixed UTF-8.
    /// </summary>
    String = 1,

    /// <summary>
    ///  A <see cref="bool"/>.
    /// </summary>
    Boolean = 2,

    /// <summary>
    ///  A <see cref="char"/>, stored as a <see cref="ushort"/>.
    /// </summary>
    Char = 3,

    /// <summary>
    ///  A <see cref="byte"/>.
    /// </summary>
    Byte = 4,

    /// <summary>
    ///  An <see cref="sbyte"/>.
    /// </summary>
    SByte = 5,

    /// <summary>
    ///  An <see cref="short"/>.
    /// </summary>
    Int16 = 6,

    /// <summary>
    ///  A <see cref="ushort"/>.
    /// </summary>
    UInt16 = 7,

    /// <summary>
    ///  An <see cref="int"/>.
    /// </summary>
    Int32 = 8,

    /// <summary>
    ///  A <see cref="uint"/>.
    /// </summary>
    UInt32 = 9,

    /// <summary>
    ///  A <see cref="long"/>.
    /// </summary>
    Int64 = 0xA,

    /// <summary>
    ///  A <see cref="ulong"/>.
    /// </summary>
    UInt64 = 0xB,

    /// <summary>
    ///  A <see cref="float"/>.
    /// </summary>
    Single = 0xC,

    /// <summary>
    ///  A <see cref="double"/>.
    /// </summary>
    Double = 0xD,

    /// <summary>
    ///  A <see cref="decimal"/>, stored as four <see cref="int"/> values (16 bytes).
    /// </summary>
    Decimal = 0xE,

    /// <summary>
    ///  A <see cref="System.DateTime"/>, stored as an <see cref="long"/> from
    ///  <see cref="System.DateTime.ToBinary"/>.
    /// </summary>
    DateTime = 0xF,

    /// <summary>
    ///  A <see cref="System.TimeSpan"/>, stored as its <see cref="System.TimeSpan.Ticks"/>.
    /// </summary>
    TimeSpan = 0x10,

    /// <summary>
    ///  The highest primitive type code. Change this if new primitives are added.
    /// </summary>
    LastPrimitive = TimeSpan,

    /// <summary>
    ///  A <c>byte[]</c>, stored as an <see cref="int"/> length followed by the bytes.
    /// </summary>
    ByteArray = 0x20,

    /// <summary>
    ///  A <see cref="System.IO.Stream"/>, stored as an <see cref="int"/> length followed by the bytes.
    /// </summary>
    Stream = 0x21,

    /// <summary>
    ///  The first type code assigned to a serialized user type. The type's name is found in the
    ///  file's type table at index <c>typeCode - StartOfUserTypes</c>.
    /// </summary>
    StartOfUserTypes = 0x40
}
