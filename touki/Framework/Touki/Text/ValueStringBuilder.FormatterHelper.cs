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
        private static readonly TryFormatDelegate<T> s_tryFormatWithoutBoxing;

        static FormatterHelper()
        {
            if (!typeof(ISpanFormattable).IsAssignableFrom(typeof(T)))
            {
                throw new InvalidOperationException();
            }

            Debug.Assert(typeof(T).IsValueType);

            if (typeof(FormatterHelper<T>).GetMethod(
                nameof(TryFormat),
                BindingFlags.NonPublic | BindingFlags.Static) is not { } method)
            {
                throw new InvalidOperationException();
            }

            method = method.MakeGenericMethod(typeof(T));
            s_tryFormatWithoutBoxing = (TryFormatDelegate<T>)Delegate.CreateDelegate(
                typeof(TryFormatDelegate<T>),
                method);
        }

        /// <summary>
        ///  Delegate that can be used to format a value of type <typeparamref name="T"/> without boxing.
        /// </summary>
        internal static TryFormatDelegate<T> TryFormatWithoutBoxing => s_tryFormatWithoutBoxing;

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
