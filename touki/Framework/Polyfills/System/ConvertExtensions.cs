// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Encoding/decoding logic adapted from dotnet/runtime (MIT licensed):
// https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/HexConverter.cs

namespace System;

/// <summary>
///  Polyfill for <see cref="Convert"/> hexadecimal string APIs.
/// </summary>
public static class ConvertExtensions
{
    extension(Convert)
    {
        /// <summary>
        ///  Converts an array of 8-bit unsigned integers to its equivalent
        ///  string representation that is encoded with uppercase hex characters.
        /// </summary>
        public static string ToHexString(byte[] inArray)
        {
            ArgumentNullException.ThrowIfNull(inArray);
            return Convert.ToHexString(new ReadOnlySpan<byte>(inArray));
        }

        /// <summary>
        ///  Converts a subset of an array of 8-bit unsigned integers to its equivalent
        ///  string representation that is encoded with uppercase hex characters.
        /// </summary>
        public static string ToHexString(byte[] inArray, int offset, int length)
        {
            ArgumentNullException.ThrowIfNull(inArray);

            if ((uint)offset > (uint)inArray.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            if ((uint)length > (uint)(inArray.Length - offset))
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            return Convert.ToHexString(new ReadOnlySpan<byte>(inArray, offset, length));
        }

        /// <summary>
        ///  Converts a span of 8-bit unsigned integers to its equivalent string
        ///  representation that is encoded with uppercase hex characters.
        /// </summary>
        public static unsafe string ToHexString(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length == 0)
            {
                return string.Empty;
            }

            if (bytes.Length > int.MaxValue / 2)
            {
                throw new ArgumentOutOfRangeException(nameof(bytes));
            }

            int charCount = bytes.Length * 2;
            string result = new('\0', charCount);
            fixed (char* resultPtr = result)
            {
                EncodeToUtf16(bytes, new Span<char>(resultPtr, charCount), casing: 0);
            }

            return result;
        }

        /// <summary>
        ///  Converts the specified string, which encodes binary data as hex characters,
        ///  to an equivalent 8-bit unsigned integer array.
        /// </summary>
        public static byte[] FromHexString(string s)
        {
            ArgumentNullException.ThrowIfNull(s);
            return Convert.FromHexString(s.AsSpan());
        }

        /// <summary>
        ///  Converts the span, which encodes binary data as hex characters,
        ///  to an equivalent 8-bit unsigned integer array.
        /// </summary>
        public static byte[] FromHexString(ReadOnlySpan<char> chars)
        {
            if (chars.Length == 0)
            {
                return [];
            }

            if ((chars.Length & 1) != 0)
            {
                throw new FormatException("The input is not a valid hexadecimal string as its length is not a multiple of 2.");
            }

            byte[] result = new byte[chars.Length >> 1];
            if (!TryDecodeFromUtf16(chars, result))
            {
                throw new FormatException("The input is not a valid hexadecimal string as it contains a non-hexadecimal character.");
            }

            return result;
        }

        /// <summary>
        ///  Converts a span of 8-bit unsigned integers to its equivalent string representation that is
        ///  encoded with base-64 digits.
        /// </summary>
        public static string ToBase64String(ReadOnlySpan<byte> bytes) =>
            ToBase64String(bytes, Base64FormattingOptions.None);

        /// <summary>
        ///  Converts a span of 8-bit unsigned integers to its equivalent string representation that is
        ///  encoded with base-64 digits, with optional formatting.
        /// </summary>
        public static string ToBase64String(ReadOnlySpan<byte> bytes, Base64FormattingOptions options)
        {
            if (bytes.IsEmpty)
            {
                return string.Empty;
            }

            byte[] rented = ArrayPool<byte>.Shared.Rent(bytes.Length);
            try
            {
                bytes.CopyTo(rented);
                return Convert.ToBase64String(rented, 0, bytes.Length, options);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        /// <summary>
        ///  Tries to convert the 8-bit unsigned integers inside <paramref name="bytes"/> to their equivalent
        ///  string representation that is encoded with base-64 digits, writing the result into the provided
        ///  character span.
        /// </summary>
        public static bool TryToBase64Chars(ReadOnlySpan<byte> bytes, Span<char> chars, out int charsWritten) =>
            TryToBase64Chars(bytes, chars, out charsWritten, Base64FormattingOptions.None);

        /// <inheritdoc cref="TryToBase64Chars(ReadOnlySpan{byte}, Span{char}, out int)"/>
        /// <param name="bytes">The bytes to convert.</param>
        /// <param name="chars">Destination buffer that receives the encoded chars.</param>
        /// <param name="charsWritten">When this method returns, contains the number of chars written.</param>
        /// <param name="options">Formatting options.</param>
        public static bool TryToBase64Chars(
            ReadOnlySpan<byte> bytes,
            Span<char> chars,
            out int charsWritten,
            Base64FormattingOptions options)
        {
            if (bytes.IsEmpty)
            {
                charsWritten = 0;
                return true;
            }

            string encoded = ToBase64String(bytes, options);
            if (encoded.Length > chars.Length)
            {
                charsWritten = 0;
                return false;
            }

            encoded.AsSpan().CopyTo(chars);
            charsWritten = encoded.Length;
            return true;
        }

        /// <summary>
        ///  Tries to decode the base-64 characters in <paramref name="chars"/> into <paramref name="bytes"/>.
        /// </summary>
        public static bool TryFromBase64Chars(ReadOnlySpan<char> chars, Span<byte> bytes, out int bytesWritten)
        {
            if (chars.IsEmpty)
            {
                bytesWritten = 0;
                return true;
            }

            try
            {
                byte[] decoded = Convert.FromBase64CharArray(chars.ToArray(), 0, chars.Length);
                if (decoded.Length > bytes.Length)
                {
                    bytesWritten = 0;
                    return false;
                }

                decoded.AsSpan().CopyTo(bytes);
                bytesWritten = decoded.Length;
                return true;
            }
            catch (FormatException)
            {
                bytesWritten = 0;
                return false;
            }
        }

        /// <summary>
        ///  Tries to decode the base-64 string <paramref name="s"/> into <paramref name="bytes"/>.
        /// </summary>
        public static bool TryFromBase64String(string s, Span<byte> bytes, out int bytesWritten)
        {
            ArgumentNullException.ThrowIfNull(s);
            return TryFromBase64Chars(s.AsSpan(), bytes, out bytesWritten);
        }
    }

    private static void EncodeToUtf16(ReadOnlySpan<byte> bytes, Span<char> chars, uint casing)
    {
        // casing == 0 -> uppercase, casing == 0x2020 -> lowercase
        ref byte src = ref MemoryMarshal.GetReference(bytes);
        ref char dst = ref MemoryMarshal.GetReference(chars);

        int length = bytes.Length;
        for (int i = 0; i < length; i++)
        {
            uint b = Unsafe.Add(ref src, i);

            // Branchless nibble-to-ASCII conversion (two nibbles packed into one uint).
            uint difference = ((b & 0xF0U) << 4) + (b & 0x0FU) - 0x8989U;
            uint packed = ((((uint)(-(int)difference) & 0x7070U) >> 4) + difference + 0xB9B9U) | casing;

            int j = i * 2;
            Unsafe.Add(ref dst, j) = (char)(packed >> 8);
            Unsafe.Add(ref dst, j + 1) = (char)(packed & 0xFFU);
        }
    }

    private static bool TryDecodeFromUtf16(ReadOnlySpan<char> chars, Span<byte> bytes)
    {
        ref char src = ref MemoryMarshal.GetReference(chars);
        ref byte dst = ref MemoryMarshal.GetReference(bytes);

        int byteCount = bytes.Length;
        for (int i = 0; i < byteCount; i++)
        {
            int hi = Touki.HexConverter.FromChar(Unsafe.Add(ref src, i * 2));
            int lo = Touki.HexConverter.FromChar(Unsafe.Add(ref src, i * 2 + 1));
            if ((hi | lo) == 0xFF)
            {
                return false;
            }

            Unsafe.Add(ref dst, i) = (byte)((hi << 4) | lo);
        }

        return true;
    }
}
