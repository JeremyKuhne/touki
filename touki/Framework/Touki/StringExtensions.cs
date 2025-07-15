// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

/// <summary>
///  String extensions
/// </summary>
public static class StringExtensions
{
    /// <summary>
    ///  Copies the contents of this string into the destination span.
    /// </summary>
    /// <param name="destination">The span into which to copy this string's contents.</param>
    /// <exception cref="ArgumentException">The destination span is shorter than the source string.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void CopyTo(this string source, Span<char> destination)
    {
        if ((uint)source.Length <= (uint)destination.Length)
        {
            fixed (char* pSource = source)
            fixed (char* pDestination = destination)
            {
                Buffer.MemoryCopy(
                    pSource,
                    pDestination,
                    destination.Length * sizeof(char),
                    source.Length * sizeof(char));
            }
        }
        else
        {
            ThrowHelper.ThrowArgument(nameof(destination));
        }
    }
}
