// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Resources;

/// <summary>
///  Validates resource type codes from the version-2 binary format.
/// </summary>
internal static class ResourceTypeCodeValidator
{
    /// <summary>
    ///  Validates and converts an encoded type value.
    /// </summary>
    /// <param name="value">The encoded type value.</param>
    /// <param name="typeCount">The number of user types declared by the table.</param>
    /// <returns>The validated type code.</returns>
    /// <exception cref="BadImageFormatException">The type value is undefined or out of range.</exception>
    internal static ResourceTypeCode Validate(int value, int typeCount)
    {
        if ((uint)value <= (uint)ResourceTypeCode.LastPrimitive
            || value is (int)ResourceTypeCode.ByteArray or (int)ResourceTypeCode.Stream)
        {
            return (ResourceTypeCode)value;
        }

        if (value >= (int)ResourceTypeCode.StartOfUserTypes
            && value - (int)ResourceTypeCode.StartOfUserTypes < typeCount)
        {
            return (ResourceTypeCode)value;
        }

        throw new BadImageFormatException($"Unsupported resource type code 0x{value:X}.");
    }
}
