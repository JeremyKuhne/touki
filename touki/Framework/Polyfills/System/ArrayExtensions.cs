// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace System;

/// <summary>
///  <see cref="Array"/> static methods that don't have a direct equivalent in the .NET Framework build.
/// </summary>
public static class ArrayExtensions
{
    extension(Array)
    {
        /// <summary>
        ///  Assigns the given <paramref name="value"/> to each element of the <paramref name="array"/>.
        /// </summary>
        /// <typeparam name="T">The type of the elements of the array.</typeparam>
        /// <param name="array">The array to be filled.</param>
        /// <param name="value">The value to assign to each array element.</param>
        public static void Fill<T>(T[] array, T value)
        {
            ArgumentNullException.ThrowIfNull(array);
            array.AsSpan().Fill(value);
        }

        /// <summary>
        ///  Assigns the given <paramref name="value"/> to each element of the <paramref name="array"/> in the
        ///  specified range.
        /// </summary>
        /// <typeparam name="T">The type of the elements of the array.</typeparam>
        /// <param name="array">The array to be filled.</param>
        /// <param name="value">The value to assign to each array element.</param>
        /// <param name="startIndex">The index at which to begin filling.</param>
        /// <param name="count">The number of elements to fill.</param>
        public static void Fill<T>(T[] array, T value, int startIndex, int count)
        {
            ArgumentNullException.ThrowIfNull(array);
            if ((uint)startIndex > (uint)array.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            }

            if ((uint)count > (uint)(array.Length - startIndex))
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            array.AsSpan(startIndex, count).Fill(value);
        }
    }
}
