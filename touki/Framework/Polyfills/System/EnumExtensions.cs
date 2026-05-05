// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki;

namespace System;

/// <summary>
///  <see cref="Enum"/> static methods that don't have a direct equivalent in the .NET Framework build.
/// </summary>
public static class EnumExtensions
{
    extension(Enum)
    {
        /// <summary>
        ///  Retrieves an array of the values of the constants in the specified enumeration type.
        /// </summary>
        /// <typeparam name="TEnum">An enumeration type.</typeparam>
        /// <returns>An array that contains the values of the constants in <typeparamref name="TEnum"/>.</returns>
        public static TEnum[] GetValues<TEnum>() where TEnum : struct, Enum =>
            (TEnum[])Enum.GetValues(typeof(TEnum));

        /// <summary>
        ///  Retrieves an array of the names of the constants in the specified enumeration type.
        /// </summary>
        /// <typeparam name="TEnum">An enumeration type.</typeparam>
        /// <returns>A string array of the names of the constants in <typeparamref name="TEnum"/>.</returns>
        public static string[] GetNames<TEnum>() where TEnum : struct, Enum
        {
            return Enum.GetNames(typeof(TEnum));
        }

        /// <summary>
        ///  Returns the name of the constant in the specified enumeration type that has the specified value, or
        ///  <see langword="null"/> if no such constant is found.
        /// </summary>
        /// <typeparam name="TEnum">An enumeration type.</typeparam>
        /// <param name="value">The value of a particular enumerated constant in terms of its underlying type.</param>
        public static string? GetName<TEnum>(TEnum value) where TEnum : struct, Enum =>
            Enum.GetName(typeof(TEnum), value);

        /// <summary>
        ///  Returns a Boolean telling whether a given integral value, or its name as a string, exists in a specified
        ///  enumeration.
        /// </summary>
        /// <typeparam name="TEnum">An enumeration type.</typeparam>
        /// <param name="value">The value or name of a constant in <typeparamref name="TEnum"/>.</param>
        public static bool IsDefined<TEnum>(TEnum value) where TEnum : struct, Enum =>
            Enum.IsDefined(typeof(TEnum), value);

        /// <summary>
        ///  Converts the span representation of the name or numeric value of one or more enumerated constants to an
        ///  equivalent enumerated object.
        /// </summary>
        public static TEnum Parse<TEnum>(ReadOnlySpan<char> value) where TEnum : struct, Enum =>
            (TEnum)Enum.Parse(typeof(TEnum), value.ToString());

        /// <inheritdoc cref="Parse{TEnum}(ReadOnlySpan{char})"/>
        /// <param name="value">The span representation of the name or numeric value to convert.</param>
        /// <param name="ignoreCase">
        ///  <see langword="true"/> to read <paramref name="value"/> in case insensitive mode;
        ///  <see langword="false"/> to read in case sensitive mode.
        /// </param>
        public static TEnum Parse<TEnum>(ReadOnlySpan<char> value, bool ignoreCase) where TEnum : struct, Enum =>
            (TEnum)Enum.Parse(typeof(TEnum), value.ToString(), ignoreCase);

        /// <summary>
        ///  Converts the span representation of the name or numeric value of one or more enumerated constants to an
        ///  equivalent enumerated object. The return value indicates whether the conversion succeeded.
        /// </summary>
        public static bool TryParse<TEnum>(ReadOnlySpan<char> value, out TEnum result) where TEnum : struct, Enum =>
            Enum.TryParse(value.ToString(), out result);

        /// <inheritdoc cref="TryParse{TEnum}(ReadOnlySpan{char}, out TEnum)"/>
        /// <param name="value">The span representation of the name or numeric value to convert.</param>
        /// <param name="ignoreCase">
        ///  <see langword="true"/> to ignore case; <see langword="false"/> to consider case.
        /// </param>
        /// <param name="result">When this method returns, contains the parsed value, or default on failure.</param>
        public static bool TryParse<TEnum>(ReadOnlySpan<char> value, bool ignoreCase, out TEnum result)
            where TEnum : struct, Enum =>
            Enum.TryParse(value.ToString(), ignoreCase, out result);

        /// <summary>
        ///  Converts the string representation of the name or numeric value of one or more enumerated constants to an
        ///  equivalent enumerated object. A parameter specifies whether the operation is case-insensitive.
        /// </summary>
        public static object Parse(Type enumType, ReadOnlySpan<char> value) =>
            Enum.Parse(enumType, value.ToString());

        /// <inheritdoc cref="Parse(Type, ReadOnlySpan{char})"/>
        /// <param name="enumType">An enumeration type.</param>
        /// <param name="value">The span representation of the name or numeric value to convert.</param>
        /// <param name="ignoreCase">
        ///  <see langword="true"/> to ignore case; <see langword="false"/> to consider case.
        /// </param>
        public static object Parse(Type enumType, ReadOnlySpan<char> value, bool ignoreCase) =>
            Enum.Parse(enumType, value.ToString(), ignoreCase);

        /// <summary>
        ///  Tries to convert the span representation of the name or numeric value of one or more enumerated constants
        ///  to an equivalent enumerated object.
        /// </summary>
        public static bool TryParse(Type enumType, ReadOnlySpan<char> value, out object? result)
        {
            try
            {
                result = Enum.Parse(enumType, value.ToString());
                return true;
            }
            catch
            {
                result = null;
                return false;
            }
        }

        /// <inheritdoc cref="TryParse(Type, ReadOnlySpan{char}, out object?)"/>
        /// <param name="enumType">An enumeration type.</param>
        /// <param name="value">The span representation of the name or numeric value to convert.</param>
        /// <param name="ignoreCase">
        ///  <see langword="true"/> to ignore case; <see langword="false"/> to consider case.
        /// </param>
        /// <param name="result">When this method returns, contains the parsed value, or <see langword="null"/> on failure.</param>
        public static bool TryParse(Type enumType, ReadOnlySpan<char> value, bool ignoreCase, out object? result)
        {
            try
            {
                result = Enum.Parse(enumType, value.ToString(), ignoreCase);
                return true;
            }
            catch
            {
                result = null;
                return false;
            }
        }
    }
}
