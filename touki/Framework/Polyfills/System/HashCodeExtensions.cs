// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Buffers.Binary;

namespace System;

/// <summary>
///  Polyfills for <see cref="HashCode"/>.
/// </summary>
public static class HashCodeExtensions
{
    extension(ref HashCode hash)
    {
        /// <summary>
        ///  Adds a span of bytes to the hash code.
        /// </summary>
        public void AddBytes(ReadOnlySpan<byte> value)
        {
            // Hash 4 bytes at a time as int. Tail bytes hashed individually.
            while (value.Length >= sizeof(int))
            {
                hash.Add(BinaryPrimitives.ReadInt32LittleEndian(value));
                value = value[sizeof(int)..];
            }

            for (int i = 0; i < value.Length; i++)
            {
                hash.Add(value[i]);
            }
        }
    }
}
