// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Reflection;

namespace Touki.Text;

public ref partial struct ValueStringBuilder
{
    /// <summary>
    ///  Creates and caches a delegate for formatting <see cref="ISpanFormattable"/> value types without boxing.
    /// </summary>
    private static class FormatterHelper<T>
    {
        private static TryFormatDelegate<T>? s_tryFormatWithoutBoxing;

        /// <summary>
        ///  Delegate that can be used to format a value of type <typeparamref name="T"/> without boxing.
        /// </summary>
        internal static TryFormatDelegate<T>? TryFormatWithoutBoxing => s_tryFormatWithoutBoxing ??= Init();

        private static TryFormatDelegate<T>? Init()
        {
            // Dynamically check if T implements ISpanFormattable (e.g., via reflection or a known flag).
            if (!typeof(ISpanFormattable).IsAssignableFrom(typeof(T)))
            {
                return null;
            }

            // Shouldn't be using this for reference types.
            Debug.Assert(typeof(T).IsValueType);

            MethodInfo method = typeof(FormatterHelper<T>).GetMethod(
                nameof(TryFormat),
                BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(typeof(T));

            return (TryFormatDelegate<T>)Delegate.CreateDelegate(typeof(TryFormatDelegate<T>), method);
        }

        private static bool TryFormat<TFormat>(
            in TFormat value,
            Span<char> destination,
            out int charsWritten,
            ReadOnlySpan<char> format,
            IFormatProvider? provider) where TFormat : struct, ISpanFormattable
        {
            return value.TryFormat(destination, out charsWritten, format, provider);
        }
    }
}
