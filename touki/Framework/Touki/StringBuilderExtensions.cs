// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Text;

namespace Framework.Touki;

/// <summary>
///  Extensions for <see cref="StringBuilder"/> to provide additional functionality.
/// </summary>
public static unsafe partial class StringBuilderExtensions
{
    /// <summary>
    ///  Appends a <see cref="ReadOnlySpan{T}"/> of characters to the end of the <see cref="StringBuilder"/>.
    /// </summary>
    public static StringBuilder AppendSpan(this StringBuilder builder, ReadOnlySpan<char> value)
    {
        if (!value.IsEmpty)
        {
            fixed (char* pValue = value)
            {
                // Use the StringBuilder's Append method that takes a char pointer and length for better performance.
                builder.Append(pValue, value.Length);
            }
        }

        return builder;
    }

    /// <summary>
    ///  Appends a <see cref="Memory{T}"/> of characters to the end of the <see cref="StringBuilder"/>.
    /// </summary>
    public static StringBuilder AppendSpan(this StringBuilder builder, Memory<char> value) => builder.AppendSpan(value.Span);
}
