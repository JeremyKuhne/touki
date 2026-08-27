// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Text;

public ref partial struct ValueStringBuilder
{
    /// <summary>
    ///  Represents a boxing-free span-formatting operation for a value of type <typeparamref name="T"/>.
    /// </summary>
    private delegate bool TryFormatDelegate<T>(
        in T value,
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider);
}
