// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

/// <summary>
///  Makes <see cref="Value"/> a C# language union via the union member provider pattern.
/// </summary>
/// <remarks>
///  <para>
///   A union type must expose a <c>Value</c> member, but a type named <see cref="Value"/> cannot have a
///   property named <c>Value</c> (CS0542). The union member provider pattern resolves this: the union
///   members live on a nested <see cref="IUnionMembers"/> interface instead of on the type. Case types come
///   from the static <c>Create</c> factory methods (so the existing constructors need not change), and the
///   non-boxing access members (<c>HasValue</c>, <c>TryGetValue</c>) live on the interface too, which keeps
///   pattern matching allocation-free.
///  </para>
///  <para>
///   The case types mirror the non-boxed value types, <see cref="string"/>, and the array/string segments
///   that <see cref="Value"/> stores inline. Enums, nullable value types, and arbitrary reference types are
///   still reached through <see cref="Value.Create{T}(T)"/> / <see cref="As{T}()"/>, but are not union case
///   types (matching <see cref="Value"/>'s open semantics).
///  </para>
/// </remarks>
[Union]
public readonly partial struct Value : Value.IUnionMembers
{
    /// <summary>
    ///  Union member provider for <see cref="Value"/>. The compiler finds the union's case types and access
    ///  members here rather than on <see cref="Value"/> itself.
    /// </summary>
    public interface IUnionMembers
    {
        /// <summary>Creates a <see cref="Value"/> from a <see cref="bool"/> (union case type).</summary>
        static Value Create(bool value) => value;

        /// <summary>Creates a <see cref="Value"/> from a <see cref="byte"/> (union case type).</summary>
        static Value Create(byte value) => value;

        /// <summary>Creates a <see cref="Value"/> from an <see cref="sbyte"/> (union case type).</summary>
        static Value Create(sbyte value) => value;

        /// <summary>Creates a <see cref="Value"/> from a <see cref="char"/> (union case type).</summary>
        static Value Create(char value) => value;

        /// <summary>Creates a <see cref="Value"/> from a <see cref="short"/> (union case type).</summary>
        static Value Create(short value) => value;

        /// <summary>Creates a <see cref="Value"/> from a <see cref="ushort"/> (union case type).</summary>
        static Value Create(ushort value) => value;

        /// <summary>Creates a <see cref="Value"/> from an <see cref="int"/> (union case type).</summary>
        static Value Create(int value) => value;

        /// <summary>Creates a <see cref="Value"/> from a <see cref="uint"/> (union case type).</summary>
        static Value Create(uint value) => value;

        /// <summary>Creates a <see cref="Value"/> from a <see cref="long"/> (union case type).</summary>
        static Value Create(long value) => value;

        /// <summary>Creates a <see cref="Value"/> from a <see cref="ulong"/> (union case type).</summary>
        static Value Create(ulong value) => value;

        /// <summary>Creates a <see cref="Value"/> from a <see cref="float"/> (union case type).</summary>
        static Value Create(float value) => value;

        /// <summary>Creates a <see cref="Value"/> from a <see cref="double"/> (union case type).</summary>
        static Value Create(double value) => value;

        /// <summary>Creates a <see cref="Value"/> from a <see cref="DateTime"/> (union case type).</summary>
        static Value Create(DateTime value) => value;

        /// <summary>Creates a <see cref="Value"/> from a <see cref="DateTimeOffset"/> (union case type).</summary>
        static Value Create(DateTimeOffset value) => value;

        /// <summary>Creates a <see cref="Value"/> from a <see cref="string"/> (union case type).</summary>
        static Value Create(string value) => value;

        /// <summary>Creates a <see cref="Value"/> from an <see cref="ArraySegment{T}"/> of <see cref="byte"/> (union case type).</summary>
        static Value Create(ArraySegment<byte> value) => value;

        /// <summary>Creates a <see cref="Value"/> from an <see cref="ArraySegment{T}"/> of <see cref="char"/> (union case type).</summary>
        static Value Create(ArraySegment<char> value) => value;

        /// <summary>Creates a <see cref="Value"/> from a <see cref="StringSegment"/> (union case type).</summary>
        static Value Create(StringSegment value) => value;

        /// <summary>Gets the contents boxed, or <see langword="null"/> when empty. Mandatory union member.</summary>
        object? Value { get; }

        /// <summary>Gets a value indicating whether the union holds a non-null value.</summary>
        bool HasValue { get; }

        /// <summary>Non-boxing access for the <see cref="bool"/> case.</summary>
        bool TryGetValue(out bool value);

        /// <summary>Non-boxing access for the <see cref="byte"/> case.</summary>
        bool TryGetValue(out byte value);

        /// <summary>Non-boxing access for the <see cref="sbyte"/> case.</summary>
        bool TryGetValue(out sbyte value);

        /// <summary>Non-boxing access for the <see cref="char"/> case.</summary>
        bool TryGetValue(out char value);

        /// <summary>Non-boxing access for the <see cref="short"/> case.</summary>
        bool TryGetValue(out short value);

        /// <summary>Non-boxing access for the <see cref="ushort"/> case.</summary>
        bool TryGetValue(out ushort value);

        /// <summary>Non-boxing access for the <see cref="int"/> case.</summary>
        bool TryGetValue(out int value);

        /// <summary>Non-boxing access for the <see cref="uint"/> case.</summary>
        bool TryGetValue(out uint value);

        /// <summary>Non-boxing access for the <see cref="long"/> case.</summary>
        bool TryGetValue(out long value);

        /// <summary>Non-boxing access for the <see cref="ulong"/> case.</summary>
        bool TryGetValue(out ulong value);

        /// <summary>Non-boxing access for the <see cref="float"/> case.</summary>
        bool TryGetValue(out float value);

        /// <summary>Non-boxing access for the <see cref="double"/> case.</summary>
        bool TryGetValue(out double value);

        /// <summary>Non-boxing access for the <see cref="DateTime"/> case.</summary>
        bool TryGetValue(out DateTime value);

        /// <summary>Non-boxing access for the <see cref="DateTimeOffset"/> case.</summary>
        bool TryGetValue(out DateTimeOffset value);

        /// <summary>Non-boxing access for the <see cref="string"/> case.</summary>
        bool TryGetValue(out string? value);

        /// <summary>Non-boxing access for the <see cref="ArraySegment{T}"/> of <see cref="byte"/> case.</summary>
        bool TryGetValue(out ArraySegment<byte> value);

        /// <summary>Non-boxing access for the <see cref="ArraySegment{T}"/> of <see cref="char"/> case.</summary>
        bool TryGetValue(out ArraySegment<char> value);

        /// <summary>Non-boxing access for the <see cref="StringSegment"/> case.</summary>
        bool TryGetValue(out StringSegment value);
    }

    // A Value is empty precisely when _object is null (see the Type property). These members are on the
    // union pattern-matching path and only need to know whether a value is present, so they test the field
    // directly rather than resolving the full Type (which pattern-matches TypeFlag, calls GetType(), and
    // disambiguates ArraySegment).
    object? IUnionMembers.Value => _object is null ? null : As<object>();

    bool IUnionMembers.HasValue => _object is not null;

    bool IUnionMembers.TryGetValue(out bool value) => TryGetValue(out value);

    bool IUnionMembers.TryGetValue(out byte value) => TryGetValue(out value);

    bool IUnionMembers.TryGetValue(out sbyte value) => TryGetValue(out value);

    bool IUnionMembers.TryGetValue(out char value) => TryGetValue(out value);

    bool IUnionMembers.TryGetValue(out short value) => TryGetValue(out value);

    bool IUnionMembers.TryGetValue(out ushort value) => TryGetValue(out value);

    bool IUnionMembers.TryGetValue(out int value) => TryGetValue(out value);

    bool IUnionMembers.TryGetValue(out uint value) => TryGetValue(out value);

    bool IUnionMembers.TryGetValue(out long value) => TryGetValue(out value);

    bool IUnionMembers.TryGetValue(out ulong value) => TryGetValue(out value);

    bool IUnionMembers.TryGetValue(out float value) => TryGetValue(out value);

    bool IUnionMembers.TryGetValue(out double value) => TryGetValue(out value);

    bool IUnionMembers.TryGetValue(out DateTime value) => TryGetValue(out value);

    bool IUnionMembers.TryGetValue(out DateTimeOffset value) => TryGetValue(out value);

    bool IUnionMembers.TryGetValue(out string? value) => TryGetValue(out value);

    bool IUnionMembers.TryGetValue(out ArraySegment<byte> value) => TryGetValue(out value);

    bool IUnionMembers.TryGetValue(out ArraySegment<char> value) => TryGetValue(out value);

    bool IUnionMembers.TryGetValue(out StringSegment value) => TryGetValue(out value);
}
