// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace System.Text;

/// <summary>
///  Polyfill for <see cref="Encoding"/> span APIs.
/// </summary>
public static class EncodingExtensions
{
    extension(Encoding encoding)
    {
        /// <summary>
        ///  Calculates the number of bytes produced by encoding the specified character span.
        /// </summary>
        public unsafe int GetByteCount(ReadOnlySpan<char> chars)
        {
            ArgumentNullException.ThrowIfNull(encoding);

            if (chars.IsEmpty)
            {
                return 0;
            }

            fixed (char* charPtr = &MemoryMarshal.GetReference(chars))
            {
                return encoding.GetByteCount(charPtr, chars.Length);
            }
        }

        /// <summary>
        ///  Encodes a span of characters into a span of bytes. Returns the number of bytes written.
        /// </summary>
        public unsafe int GetBytes(ReadOnlySpan<char> chars, Span<byte> bytes)
        {
            ArgumentNullException.ThrowIfNull(encoding);

            if (chars.IsEmpty)
            {
                return 0;
            }

            fixed (char* charPtr = &MemoryMarshal.GetReference(chars))
            fixed (byte* bytePtr = &MemoryMarshal.GetReference(bytes))
            {
                return encoding.GetBytes(charPtr, chars.Length, bytePtr, bytes.Length);
            }
        }

        /// <summary>
        ///  Calculates the number of characters produced by decoding the specified byte span.
        /// </summary>
        public unsafe int GetCharCount(ReadOnlySpan<byte> bytes)
        {
            ArgumentNullException.ThrowIfNull(encoding);

            if (bytes.IsEmpty)
            {
                return 0;
            }

            fixed (byte* bytePtr = &MemoryMarshal.GetReference(bytes))
            {
                return encoding.GetCharCount(bytePtr, bytes.Length);
            }
        }

        /// <summary>
        ///  Decodes a span of bytes into a span of characters. Returns the number of characters written.
        /// </summary>
        public unsafe int GetChars(ReadOnlySpan<byte> bytes, Span<char> chars)
        {
            ArgumentNullException.ThrowIfNull(encoding);

            if (bytes.IsEmpty)
            {
                return 0;
            }

            fixed (byte* bytePtr = &MemoryMarshal.GetReference(bytes))
            fixed (char* charPtr = &MemoryMarshal.GetReference(chars))
            {
                return encoding.GetChars(bytePtr, bytes.Length, charPtr, chars.Length);
            }
        }

        /// <summary>
        ///  Decodes the specified byte span into a string.
        /// </summary>
        public unsafe string GetString(ReadOnlySpan<byte> bytes)
        {
            ArgumentNullException.ThrowIfNull(encoding);

            if (bytes.IsEmpty)
            {
                return string.Empty;
            }

            fixed (byte* bytePtr = &MemoryMarshal.GetReference(bytes))
            {
                return encoding.GetString(bytePtr, bytes.Length);
            }
        }
    }
}
