// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Text;

namespace Touki;

/// <summary>
///  Provides a strongly-typed value that can be used to store a variety of types without boxing.
/// </summary>
/// <remarks>
///  <para>
///   This type is designed to be used in scenarios where you need to store a value that can be one of many types,
///   such as in a dictionary or a collection. It uses a union-like structure to store the value without boxing it,
///   which can improve performance in certain scenarios.
///  </para>
///  <para>
///   The behavior is meant to match the behavior of <see cref="object"/>.
///  </para>
///  <para>
///   The <see cref="Value"/> type supports storing without boxing a wide range of types, including primitive types,
///   nullable types, <see cref="DateTime"/>, see <see cref="DateTimeOffset"/>, enums, and array segments. It also
///   supports all other types by boxing them into an object.
///  </para>
///  <para>
///   The class contains implicit and explicit operators for converting between <see cref="Value"/> and various types,
///   but it avoids implicit boxing, so it does not implicitly convert from <see langword="object"/> to <see cref="Value"/>.
///   Use <see cref="Create{T}(T)"/> to create instances for types that do not have implicit conversion.
///  </para>
/// </remarks>
public readonly partial struct Value
{
    private readonly Union _union;
    internal readonly object? _object;

    /// <summary>
    ///  Creates a new <see cref="Value"/> instance with the specified value.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   This is private to help ensure that users don't unknowingly box for value types.
    ///  </para>
    /// </remarks>
    /// <param name="value">The value to store.</param>
    private Value(object? value)
    {
        _object = value;
        _union = default;
    }

    // Generally speaking we want to avoid implicit operators for most reference types to avoid accidental boxing from
    // value types that have implicit conversions. In particular this will happen with `object`. `string` is a common
    // reference type that would be passed as a formatting argument and is somewhat unlikely to have implicit
    // conversions from other value types.

    /// <summary>
    ///  Implicitly converts a <see langword="string"/> value to a <see cref="Value"/>.
    /// </summary>
    /// <param name="value">The <see langword="string"/> value to convert.</param>
    /// <returns>A <see cref="Value"/> containing the specified <see langword="string"/> value.</returns>
    public static implicit operator Value(string value) => new((object)value);

    /// <summary>
    ///  Gets the type of the value stored in this instance.
    /// </summary>
    /// <value>
    ///  The <see cref="System.Type"/> of the stored value, or <see langword="null"/> if no value is stored.
    /// </value>
    public readonly Type? Type
    {
        get
        {
            // This must stay aligned with Format logic

            Type? type;
            if (_object is null)
            {
                type = null;
            }
            else if (_object is TypeFlag typeFlag)
            {
                type = typeFlag.Type;
            }
            else
            {
                type = _object.GetType();

                if (_union.UInt64 != 0)
                {
                    Debug.Assert(type.IsArray || type == typeof(string));

                    // We have an ArraySegment
                    if (type == typeof(byte[]))
                    {
                        type = typeof(ArraySegment<byte>);
                    }
                    else if (type == typeof(char[]))
                    {
                        type = typeof(ArraySegment<char>);
                    }
                    else if (type == typeof(string))
                    {
                        type = typeof(StringSegment);
                    }
                    else
                    {
                        Debug.Fail($"Unexpected type {type.Name}.");
                    }
                }
            }

            return type;
        }
    }

    [DoesNotReturn]
    private static void ThrowInvalidCast() => throw new InvalidCastException();

    [DoesNotReturn]
    private static void ThrowArgumentNull(string paramName) => throw new ArgumentNullException(paramName);

    [DoesNotReturn]
    private static void ThrowInvalidOperation() => throw new InvalidOperationException();

    #region Byte
    /// <summary>
    ///  Creates a new <see cref="Value"/> instance with the specified <see langword="byte"/> value.
    /// </summary>
    /// <param name="value">The <see langword="byte"/> value to store.</param>
    private Value(byte value)
    {
        _object = TypeFlags.Byte;
        _union.Byte = value;
    }

    /// <summary>
    ///  Creates a new <see cref="Value"/> instance with the specified nullable <see langword="byte"/> value.
    /// </summary>
    /// <param name="value">The nullable <see langword="byte"/> value to store.</param>
    private Value(byte? value)
    {
        if (value.HasValue)
        {
            _object = TypeFlags.Byte;
            _union.Byte = value.Value;
        }
        else
        {
            _object = null;
        }
    }

    /// <summary>
    ///  Implicitly converts a <see langword="byte"/> value to a <see cref="Value"/>.
    /// </summary>
    /// <param name="value">The <see langword="byte"/> value to convert.</param>
    /// <returns>A <see cref="Value"/> containing the specified <see langword="byte"/> value.</returns>
    public static implicit operator Value(byte value) => new(value);

    /// <summary>
    ///  Explicitly converts a <see cref="Value"/> to a <see langword="byte"/>.
    /// </summary>
    /// <param name="value">The <see cref="Value"/> to convert.</param>
    /// <returns>The <see langword="byte"/> value stored in the <see cref="Value"/>.</returns>
    /// <exception cref="InvalidCastException">The stored value is not a <see langword="byte"/>.</exception>
    public static explicit operator byte(in Value value) => value.As<byte>();

    /// <summary>
    ///  Implicitly converts a nullable <see langword="byte"/> value to a <see cref="Value"/>.
    /// </summary>
    /// <param name="value">The nullable <see langword="byte"/> value to convert.</param>
    /// <returns>A <see cref="Value"/> containing the specified nullable <see langword="byte"/> value.</returns>
    public static implicit operator Value(byte? value) => new(value);

    /// <summary>
    ///  Explicitly converts a <see cref="Value"/> to a nullable <see langword="byte"/>.
    /// </summary>
    /// <param name="value">The <see cref="Value"/> to convert.</param>
    /// <returns>The nullable <see langword="byte"/> value stored in the <see cref="Value"/>.</returns>
    /// <exception cref="InvalidCastException">The stored value is not a nullable <see langword="byte"/>.</exception>
    public static explicit operator byte?(in Value value) => value.As<byte?>();
    #endregion

    #region SByte
    /// <summary>
    ///  Creates a new <see cref="Value"/> instance with the specified <see langword="sbyte"/> value.
    /// </summary>
    /// <param name="value">The <see langword="sbyte"/> value to store.</param>
    private Value(sbyte value)
    {
        _object = TypeFlags.SByte;
        _union.SByte = value;
    }

    /// <summary>
    ///  Creates a new <see cref="Value"/> instance with the specified nullable <see langword="sbyte"/> value.
    /// </summary>
    /// <param name="value">The nullable <see langword="sbyte"/> value to store.</param>
    private Value(sbyte? value)
    {
        if (value.HasValue)
        {
            _object = TypeFlags.SByte;
            _union.SByte = value.Value;
        }
        else
        {
            _object = null;
        }
    }

    /// <summary>
    ///  Implicitly converts an <see langword="sbyte"/> value to a <see cref="Value"/>.
    /// </summary>
    /// <param name="value">The <see langword="sbyte"/> value to convert.</param>
    /// <returns>A <see cref="Value"/> containing the specified <see langword="sbyte"/> value.</returns>
    public static implicit operator Value(sbyte value) => new(value);

    /// <summary>
    ///  Explicitly converts a <see cref="Value"/> to an <see langword="sbyte"/>.
    /// </summary>
    /// <param name="value">The <see cref="Value"/> to convert.</param>
    /// <returns>The <see langword="sbyte"/> value stored in the <see cref="Value"/>.</returns>
    /// <exception cref="InvalidCastException">The stored value is not an <see langword="sbyte"/>.</exception>
    public static explicit operator sbyte(in Value value) => value.As<sbyte>();

    /// <summary>
    ///  Implicitly converts a nullable <see langword="sbyte"/> value to a <see cref="Value"/>.
    /// </summary>
    /// <param name="value">The nullable <see langword="sbyte"/> value to convert.</param>
    /// <returns>A <see cref="Value"/> containing the specified nullable <see langword="sbyte"/> value.</returns>
    public static implicit operator Value(sbyte? value) => new(value);

    /// <summary>
    ///  Explicitly converts a <see cref="Value"/> to a nullable <see langword="sbyte"/>.
    /// </summary>
    /// <param name="value">The <see cref="Value"/> to convert.</param>
    /// <returns>The nullable <see langword="sbyte"/> value stored in the <see cref="Value"/>.</returns>
    /// <exception cref="InvalidCastException">The stored value is not a nullable <see langword="sbyte"/>.</exception>
    public static explicit operator sbyte?(in Value value) => value.As<sbyte?>();
    #endregion

    #region Boolean
    /// <summary>
    ///  Creates a new <see cref="Value"/> instance with the specified <see langword="bool"/> value.
    /// </summary>
    /// <param name="value">The <see langword="bool"/> value to store.</param>
    private Value(bool value)
    {
        _object = TypeFlags.Boolean;
        _union.Boolean = value;
    }

    /// <summary>
    ///  Creates a new <see cref="Value"/> instance with the specified nullable <see langword="bool"/> value.
    /// </summary>
    /// <param name="value">The nullable <see langword="bool"/> value to store.</param>
    private Value(bool? value)
    {
        if (value.HasValue)
        {
            _object = TypeFlags.Boolean;
            _union.Boolean = value.Value;
        }
        else
        {
            _object = null;
        }
    }

    /// <summary>
    ///  Implicitly converts a <see langword="bool"/> value to a <see cref="Value"/>.
    /// </summary>
    /// <param name="value">The <see langword="bool"/> value to convert.</param>
    /// <returns>A <see cref="Value"/> containing the specified <see langword="bool"/> value.</returns>
    public static implicit operator Value(bool value) => new(value);

    /// <summary>
    ///  Explicitly converts a <see cref="Value"/> to a <see langword="bool"/>.
    /// </summary>
    /// <param name="value">The <see cref="Value"/> to convert.</param>
    /// <returns>The <see langword="bool"/> value stored in the <see cref="Value"/>.</returns>
    /// <exception cref="InvalidCastException">The stored value is not a <see langword="bool"/>.</exception>
    public static explicit operator bool(in Value value) => value.As<bool>();

    /// <summary>
    ///  Implicitly converts a nullable <see langword="bool"/> value to a <see cref="Value"/>.
    /// </summary>
    /// <param name="value">The nullable <see langword="bool"/> value to convert.</param>
    /// <returns>A <see cref="Value"/> containing the specified nullable <see langword="bool"/> value.</returns>
    public static implicit operator Value(bool? value) => new(value);

    /// <summary>
    ///  Explicitly converts a <see cref="Value"/> to a nullable <see langword="bool"/>.
    /// </summary>
    /// <param name="value">The <see cref="Value"/> to convert.</param>
    /// <returns>The nullable <see langword="bool"/> value stored in the <see cref="Value"/>.</returns>
    /// <exception cref="InvalidCastException">The stored value is not a nullable <see langword="bool"/>.</exception>
    public static explicit operator bool?(in Value value) => value.As<bool?>();
    #endregion

    #region Char
    /// <summary>
    ///  Creates a new <see cref="Value"/> instance with the specified <see langword="char"/> value.
    /// </summary>
    /// <param name="value">The <see langword="char"/> value to store.</param>
    private Value(char value)
    {
        _object = TypeFlags.Char;
        _union.Char = value;
    }

    /// <summary>
    ///  Creates a new <see cref="Value"/> instance with the specified nullable <see langword="char"/> value.
    /// </summary>
    /// <param name="value">The nullable <see langword="char"/> value to store.</param>
    private Value(char? value)
    {
        if (value.HasValue)
        {
            _object = TypeFlags.Char;
            _union.Char = value.Value;
        }
        else
        {
            _object = null;
        }
    }

    /// <summary>
    ///  Implicitly converts a <see langword="char"/> value to a <see cref="Value"/>.
    /// </summary>
    /// <param name="value">The <see langword="char"/> value to convert.</param>
    /// <returns>A <see cref="Value"/> containing the specified <see langword="char"/> value.</returns>
    public static implicit operator Value(char value) => new(value);

    /// <summary>
    ///  Explicitly converts a <see cref="Value"/> to a <see langword="char"/>.
    /// </summary>
    /// <param name="value">The <see cref="Value"/> to convert.</param>
    /// <returns>The <see langword="char"/> value stored in the <see cref="Value"/>.</returns>
    /// <exception cref="InvalidCastException">The stored value is not a <see langword="char"/>.</exception>
    public static explicit operator char(in Value value) => value.As<char>();

    /// <summary>
    ///  Implicitly converts a nullable <see langword="char"/> value to a <see cref="Value"/>.
    /// </summary>
    /// <param name="value">The nullable <see langword="char"/> value to convert.</param>
    /// <returns>A <see cref="Value"/> containing the specified nullable <see langword="char"/> value.</returns>
    public static implicit operator Value(char? value) => new(value);

    /// <summary>
    ///  Explicitly converts a <see cref="Value"/> to a nullable <see langword="char"/>.
    /// </summary>
    /// <param name="value">The <see cref="Value"/> to convert.</param>
    /// <returns>The nullable <see langword="char"/> value stored in the <see cref="Value"/>.</returns>
    /// <exception cref="InvalidCastException">The stored value is not a nullable <see langword="char"/>.</exception>
    public static explicit operator char?(in Value value) => value.As<char?>();
    #endregion

    #region Int16
    /// <summary>
    ///  Creates a new <see cref="Value"/> instance with the specified <see langword="short"/> value.
    /// </summary>
    /// <param name="value">The <see langword="short"/> value to store.</param>
    private Value(short value)
    {
        _object = TypeFlags.Int16;
        _union.Int16 = value;
    }

    /// <summary>
    ///  Creates a new <see cref="Value"/> instance with the specified nullable <see langword="short"/> value.
    /// </summary>
    /// <param name="value">The nullable <see langword="short"/> value to store.</param>
    private Value(short? value)
    {
        if (value.HasValue)
        {
            _object = TypeFlags.Int16;
            _union.Int16 = value.Value;
        }
        else
        {
            _object = null;
        }
    }

    /// <summary>
    ///  Implicitly converts a <see langword="short"/> value to a <see cref="Value"/>.
    /// </summary>
    /// <param name="value">The <see langword="short"/> value to convert.</param>
    /// <returns>A <see cref="Value"/> containing the specified <see langword="short"/> value.</returns>
    public static implicit operator Value(short value) => new(value);

    /// <summary>
    ///  Explicitly converts a <see cref="Value"/> to a <see langword="short"/>.
    /// </summary>
    /// <param name="value">The <see cref="Value"/> to convert.</param>
    /// <returns>The <see langword="short"/> value stored in the <see cref="Value"/>.</returns>
    /// <exception cref="InvalidCastException">The stored value is not a <see langword="short"/>.</exception>
    public static explicit operator short(in Value value) => value.As<short>();

    /// <summary>
    ///  Implicitly converts a nullable <see langword="short"/> value to a <see cref="Value"/>.
    /// </summary>
    /// <param name="value">The nullable <see langword="short"/> value to convert.</param>
    /// <returns>A <see cref="Value"/> containing the specified nullable <see langword="short"/> value.</returns>
    public static implicit operator Value(short? value) => new(value);

    /// <summary>
    ///  Explicitly converts a <see cref="Value"/> to a nullable <see langword="short"/>.
    /// </summary>
    /// <param name="value">The <see cref="Value"/> to convert.</param>
    /// <returns>The nullable <see langword="short"/> value stored in the <see cref="Value"/>.</returns>
    /// <exception cref="InvalidCastException">The stored value is not a nullable <see langword="short"/>.</exception>
    public static explicit operator short?(in Value value) => value.As<short?>();
    #endregion

    #region Int32
    /// <summary>
    ///  Creates a new <see cref="Value"/> instance with the specified <see langword="int"/> value.
    /// </summary>
    /// <param name="value">The <see langword="int"/> value to store.</param>
    private Value(int value)
    {
        _object = TypeFlags.Int32;
        _union.Int32 = value;
    }

    /// <summary>
    ///  Creates a new <see cref="Value"/> instance with the specified nullable <see langword="int"/> value.
    /// </summary>
    /// <param name="value">The nullable <see langword="int"/> value to store.</param>
    private Value(int? value)
    {
        if (value.HasValue)
        {
            _object = TypeFlags.Int32;
            _union.Int32 = value.Value;
        }
        else
        {
            _object = null;
        }
    }

    /// <summary>
    ///  Implicitly converts an <see langword="int"/> value to a <see cref="Value"/>.
    /// </summary>
    /// <param name="value">The <see langword="int"/> value to convert.</param>
    /// <returns>A <see cref="Value"/> containing the specified <see langword="int"/> value.</returns>
    public static implicit operator Value(int value) => new(value);

    /// <summary>
    ///  Explicitly converts a <see cref="Value"/> to an <see langword="int"/>.
    /// </summary>
    /// <param name="value">The <see cref="Value"/> to convert.</param>
    /// <returns>The <see langword="int"/> value stored in the <see cref="Value"/>.</returns>
    /// <exception cref="InvalidCastException">The stored value is not an <see langword="int"/>.</exception>
    public static explicit operator int(in Value value) => value.As<int>();

    /// <summary>
    ///  Implicitly converts a nullable <see langword="int"/> value to a <see cref="Value"/>.
    /// </summary>
    /// <param name="value">The nullable <see langword="int"/> value to convert.</param>
    /// <returns>A <see cref="Value"/> containing the specified nullable <see langword="int"/> value.</returns>
    public static implicit operator Value(int? value) => new(value);

    /// <summary>
    ///  Explicitly converts a <see cref="Value"/> to a nullable <see langword="int"/>.
    /// </summary>
    /// <param name="value">The <see cref="Value"/> to convert.</param>
    /// <returns>The nullable <see langword="int"/> value stored in the <see cref="Value"/>.</returns>
    /// <exception cref="InvalidCastException">The stored value is not a nullable <see langword="int"/>.</exception>
    public static explicit operator int?(in Value value) => value.As<int?>();
    #endregion

    #region Int64
    /// <summary>
    ///  Creates a new <see cref="Value"/> instance with the specified <see langword="long"/> value.
    /// </summary>
    /// <param name="value">The <see langword="long"/> value to store.</param>
    private Value(long value)
    {
        _object = TypeFlags.Int64;
        _union.Int64 = value;
    }

    /// <summary>
    ///  Creates a new <see cref="Value"/> instance with the specified nullable <see langword="long"/> value.
    /// </summary>
    /// <param name="value">The nullable <see langword="long"/> value to store.</param>
    private Value(long? value)
    {
        if (value.HasValue)
        {
            _object = TypeFlags.Int64;
            _union.Int64 = value.Value;
        }
        else
        {
            _object = null;
        }
    }

    /// <summary>
    ///  Implicitly converts a <see langword="long"/> value to a <see cref="Value"/>.
    /// </summary>
    /// <param name="value">The <see langword="long"/> value to convert.</param>
    /// <returns>A <see cref="Value"/> containing the specified <see langword="long"/> value.</returns>
    public static implicit operator Value(long value) => new(value);

    /// <summary>
    ///  Explicitly converts a <see cref="Value"/> to a <see langword="long"/>.
    /// </summary>
    /// <param name="value">The <see cref="Value"/> to convert.</param>
    /// <returns>The <see langword="long"/> value stored in the <see cref="Value"/>.</returns>
    /// <exception cref="InvalidCastException">The stored value is not a <see langword="long"/>.</exception>
    public static explicit operator long(in Value value) => value.As<long>();

    /// <summary>
    ///  Implicitly converts a nullable <see langword="long"/> value to a <see cref="Value"/>.
    /// </summary>
    /// <param name="value">The nullable <see langword="long"/> value to convert.</param>
    /// <returns>A <see cref="Value"/> containing the specified nullable <see langword="long"/> value.</returns>
    public static implicit operator Value(long? value) => new(value);

    /// <summary>
    ///  Explicitly converts a <see cref="Value"/> to a nullable <see langword="long"/>.
    /// </summary>
    /// <param name="value">The <see cref="Value"/> to convert.</param>
    /// <returns>The nullable <see langword="long"/> value stored in the <see cref="Value"/>.</returns>
    /// <exception cref="InvalidCastException">The stored value is not a nullable <see langword="long"/>.</exception>
    public static explicit operator long?(in Value value) => value.As<long?>();
    #endregion

    #region UInt16
    /// <summary>
    ///  Creates a new <see cref="Value"/> instance with the specified <see langword="ushort"/> value.
    /// </summary>
    /// <param name="value">The <see langword="ushort"/> value to store.</param>
    private Value(ushort value)
    {
        _object = TypeFlags.UInt16;
        _union.UInt16 = value;
    }

    /// <summary>
    ///  Creates a new <see cref="Value"/> instance with the specified nullable <see langword="ushort"/> value.
    /// </summary>
    /// <param name="value">The nullable <see langword="ushort"/> value to store.</param>
    private Value(ushort? value)
    {
        if (value.HasValue)
        {
            _object = TypeFlags.UInt16;
            _union.UInt16 = value.Value;
        }
        else
        {
            _object = null;
        }
    }

    /// <summary>
    ///  Implicitly converts a <see langword="ushort"/> value to a <see cref="Value"/>.
    /// </summary>
    /// <param name="value">The <see langword="ushort"/> value to convert.</param>
    /// <returns>A <see cref="Value"/> containing the specified <see langword="ushort"/> value.</returns>
    public static implicit operator Value(ushort value) => new(value);

    /// <summary>
    ///  Explicitly converts a <see cref="Value"/> to a <see langword="ushort"/>.
    /// </summary>
    /// <param name="value">The <see cref="Value"/> to convert.</param>
    /// <returns>The <see langword="ushort"/> value stored in the <see cref="Value"/>.</returns>
    /// <exception cref="InvalidCastException">The stored value is not a <see langword="ushort"/>.</exception>
    public static explicit operator ushort(in Value value) => value.As<ushort>();

    /// <summary>
    ///  Implicitly converts a nullable <see langword="ushort"/> value to a <see cref="Value"/>.
    /// </summary>
    /// <param name="value">The nullable <see langword="ushort"/> value to convert.</param>
    /// <returns>A <see cref="Value"/> containing the specified nullable <see langword="ushort"/> value.</returns>
    public static implicit operator Value(ushort? value) => new(value);

    /// <summary>
    ///  Explicitly converts a <see cref="Value"/> to a nullable <see langword="ushort"/>.
    /// </summary>
    /// <param name="value">The <see cref="Value"/> to convert.</param>
    /// <returns>The nullable <see langword="ushort"/> value stored in the <see cref="Value"/>.</returns>
    /// <exception cref="InvalidCastException">The stored value is not a nullable <see langword="ushort"/>.</exception>
    public static explicit operator ushort?(in Value value) => value.As<ushort?>();
    #endregion

    #region UInt32
    /// <summary>
    ///  Creates a new <see cref="Value"/> instance with the specified <see langword="uint"/> value.
    /// </summary>
    /// <param name="value">The <see langword="uint"/> value to store.</param>
    private Value(uint value)
    {
        _object = TypeFlags.UInt32;
        _union.UInt32 = value;
    }

    /// <summary>
    ///  Creates a new <see cref="Value"/> instance with the specified nullable <see langword="uint"/> value.
    /// </summary>
    /// <param name="value">The nullable <see langword="uint"/> value to store.</param>
    private Value(uint? value)
    {
        if (value.HasValue)
        {
            _object = TypeFlags.UInt32;
            _union.UInt32 = value.Value;
        }
        else
        {
            _object = null;
        }
    }

    /// <summary>
    ///  Implicitly converts a <see langword="uint"/> value to a <see cref="Value"/>.
    /// </summary>
    /// <param name="value">The <see langword="uint"/> value to convert.</param>
    /// <returns>A <see cref="Value"/> containing the specified <see langword="uint"/> value.</returns>
    public static implicit operator Value(uint value) => new(value);

    /// <summary>
    ///  Explicitly converts a <see cref="Value"/> to a <see langword="uint"/>.
    /// </summary>
    /// <param name="value">The <see cref="Value"/> to convert.</param>
    /// <returns>The <see langword="uint"/> value stored in the <see cref="Value"/>.</returns>
    /// <exception cref="InvalidCastException">The stored value is not a <see langword="uint"/>.</exception>
    public static explicit operator uint(in Value value) => value.As<uint>();

    /// <summary>
    ///  Implicitly converts a nullable <see langword="uint"/> value to a <see cref="Value"/>.
    /// </summary>
    /// <param name="value">The nullable <see langword="uint"/> value to convert.</param>
    /// <returns>A <see cref="Value"/> containing the specified nullable <see langword="uint"/> value.</returns>
    public static implicit operator Value(uint? value) => new(value);

    /// <summary>
    ///  Explicitly converts a <see cref="Value"/> to a nullable <see langword="uint"/>.
    /// </summary>
    /// <param name="value">The <see cref="Value"/> to convert.</param>
    /// <returns>The nullable <see langword="uint"/> value stored in the <see cref="Value"/>.</returns>
    /// <exception cref="InvalidCastException">The stored value is not a nullable <see langword="uint"/>.</exception>
    public static explicit operator uint?(in Value value) => value.As<uint?>();
    #endregion

    #region UInt64
    /// <summary>
    ///  Creates a new <see cref="Value"/> instance with the specified <see langword="ulong"/> value.
    /// </summary>
    /// <param name="value">The <see langword="ulong"/> value to store.</param>
    private Value(ulong value)
    {
        _object = TypeFlags.UInt64;
        _union.UInt64 = value;
    }

    /// <summary>
    ///  Creates a new <see cref="Value"/> instance with the specified nullable <see langword="ulong"/> value.
    /// </summary>
    /// <param name="value">The nullable <see langword="ulong"/> value to store.</param>
    private Value(ulong? value)
    {
        if (value.HasValue)
        {
            _object = TypeFlags.UInt64;
            _union.UInt64 = value.Value;
        }
        else
        {
            _object = null;
        }
    }

    /// <summary>
    ///  Implicitly converts a <see langword="ulong"/> value to a <see cref="Value"/>.
    /// </summary>
    /// <param name="value">The <see langword="ulong"/> value to convert.</param>
    /// <returns>A <see cref="Value"/> containing the specified <see langword="ulong"/> value.</returns>
    public static implicit operator Value(ulong value) => new(value);

    /// <summary>
    ///  Explicitly converts a <see cref="Value"/> to a <see langword="ulong"/>.
    /// </summary>
    /// <param name="value">The <see cref="Value"/> to convert.</param>
    /// <returns>The <see langword="ulong"/> value stored in the <see cref="Value"/>.</returns>
    /// <exception cref="InvalidCastException">The stored value is not a <see langword="ulong"/>.</exception>
    public static explicit operator ulong(in Value value) => value.As<ulong>();

    /// <summary>
    ///  Implicitly converts a nullable <see langword="ulong"/> value to a <see cref="Value"/>.
    /// </summary>
    /// <param name="value">The nullable <see langword="ulong"/> value to convert.</param>
    /// <returns>A <see cref="Value"/> containing the specified nullable <see langword="ulong"/> value.</returns>
    public static implicit operator Value(ulong? value) => new(value);

    /// <summary>
    ///  Explicitly converts a <see cref="Value"/> to a nullable <see langword="ulong"/>.
    /// </summary>
    /// <param name="value">The <see cref="Value"/> to convert.</param>
    /// <returns>The nullable <see langword="ulong"/> value stored in the <see cref="Value"/>.</returns>
    /// <exception cref="InvalidCastException">The stored value is not a nullable <see langword="ulong"/>.</exception>
    public static explicit operator ulong?(in Value value) => value.As<ulong?>();
    #endregion

    #region Single
    /// <summary>
    ///  Creates a new <see cref="Value"/> instance with the specified <see langword="float"/> value.
    /// </summary>
    /// <param name="value">The <see langword="float"/> value to store.</param>
    private Value(float value)
    {
        _object = TypeFlags.Single;
        _union.Single = value;
    }

    /// <summary>
    ///  Creates a new <see cref="Value"/> instance with the specified nullable <see langword="float"/> value.
    /// </summary>
    /// <param name="value">The nullable <see langword="float"/> value to store.</param>
    private Value(float? value)
    {
        if (value.HasValue)
        {
            _object = TypeFlags.Single;
            _union.Single = value.Value;
        }
        else
        {
            _object = null;
        }
    }

    /// <summary>
    ///  Implicitly converts a <see langword="float"/> value to a <see cref="Value"/>.
    /// </summary>
    /// <param name="value">The <see langword="float"/> value to convert.</param>
    /// <returns>A <see cref="Value"/> containing the specified <see langword="float"/> value.</returns>
    public static implicit operator Value(float value) => new(value);

    /// <summary>
    ///  Explicitly converts a <see cref="Value"/> to a <see langword="float"/>.
    /// </summary>
    /// <param name="value">The <see cref="Value"/> to convert.</param>
    /// <returns>The <see langword="float"/> value stored in the <see cref="Value"/>.</returns>
    /// <exception cref="InvalidCastException">The stored value is not a <see langword="float"/>.</exception>
    public static explicit operator float(in Value value) => value.As<float>();

    /// <summary>
    ///  Implicitly converts a nullable <see langword="float"/> value to a <see cref="Value"/>.
    /// </summary>
    /// <param name="value">The nullable <see langword="float"/> value to convert.</param>
    /// <returns>A <see cref="Value"/> containing the specified nullable <see langword="float"/> value.</returns>
    public static implicit operator Value(float? value) => new(value);

    /// <summary>
    ///  Explicitly converts a <see cref="Value"/> to a nullable <see langword="float"/>.
    /// </summary>
    /// <param name="value">The <see cref="Value"/> to convert.</param>
    /// <returns>The nullable <see langword="float"/> value stored in the <see cref="Value"/>.</returns>
    /// <exception cref="InvalidCastException">The stored value is not a nullable <see langword="float"/>.</exception>
    public static explicit operator float?(in Value value) => value.As<float?>();
    #endregion

    #region Double
    /// <summary>
    ///  Creates a new <see cref="Value"/> instance with the specified <see langword="double"/> value.
    /// </summary>
    /// <param name="value">The <see langword="double"/> value to store.</param>
    private Value(double value)
    {
        _object = TypeFlags.Double;
        _union.Double = value;
    }

    /// <summary>
    ///  Creates a new <see cref="Value"/> instance with the specified nullable <see langword="double"/> value.
    /// </summary>
    /// <param name="value">The nullable <see langword="double"/> value to store.</param>
    private Value(double? value)
    {
        if (value.HasValue)
        {
            _object = TypeFlags.Double;
            _union.Double = value.Value;
        }
        else
        {
            _object = null;
        }
    }

    /// <summary>
    ///  Implicitly converts a <see langword="double"/> value to a <see cref="Value"/>.
    /// </summary>
    /// <param name="value">The <see langword="double"/> value to convert.</param>
    /// <returns>A <see cref="Value"/> containing the specified <see langword="double"/> value.</returns>
    public static implicit operator Value(double value) => new(value);

    /// <summary>
    ///  Explicitly converts a <see cref="Value"/> to a <see langword="double"/>.
    /// </summary>
    /// <param name="value">The <see cref="Value"/> to convert.</param>
    /// <returns>The <see langword="double"/> value stored in the <see cref="Value"/>.</returns>
    /// <exception cref="InvalidCastException">The stored value is not a <see langword="double"/>.</exception>
    public static explicit operator double(in Value value) => value.As<double>();

    /// <summary>
    ///  Implicitly converts a nullable <see langword="double"/> value to a <see cref="Value"/>.
    /// </summary>
    /// <param name="value">The nullable <see langword="double"/> value to convert.</param>
    /// <returns>A <see cref="Value"/> containing the specified nullable <see langword="double"/> value.</returns>
    public static implicit operator Value(double? value) => new(value);

    /// <summary>
    ///  Explicitly converts a <see cref="Value"/> to a nullable <see langword="double"/>.
    /// </summary>
    /// <param name="value">The <see cref="Value"/> to convert.</param>
    /// <returns>The nullable <see langword="double"/> value stored in the <see cref="Value"/>.</returns>
    /// <exception cref="InvalidCastException">The stored value is not a nullable <see langword="double"/>.</exception>
    public static explicit operator double?(in Value value) => value.As<double?>();
    #endregion

    #region DateTimeOffset
    /// <summary>
    ///  Creates a new <see cref="Value"/> instance with the specified <see cref="DateTimeOffset"/> value.
    /// </summary>
    /// <param name="value">The <see cref="DateTimeOffset"/> value to store.</param>
    private Value(DateTimeOffset value)
    {
        ref DateTimeOffsetAccessor accessor = ref Unsafe.As<DateTimeOffset, DateTimeOffsetAccessor>(ref value);
        short offsetMinutes = accessor._offsetMinutes;
        ulong ticks = accessor._dateTime._dateTimeData & DateTimeAccessor.TicksMask;

        if (offsetMinutes == 0)
        {
            // This is a UTC time
            _union.Ticks = (long)ticks;
            _object = TypeFlags.DateTimeOffset;
        }
        else if (PackedDateTimeOffset.TryCreate(ticks, offsetMinutes, out var packed))
        {
            _union.PackedDateTimeOffset = packed;
            _object = TypeFlags.PackedDateTimeOffset;
        }
        else
        {
            // Very unusual DateTimeOffset. Just box it.
            _union.UInt64 = 0;
            _object = value;
        }
    }

    [StructLayout(LayoutKind.Auto)]
    private struct DateTimeOffsetAccessor
    {
        internal DateTimeAccessor _dateTime;
        internal short _offsetMinutes;
    }

    [StructLayout(LayoutKind.Auto)]
    private struct DateTimeAccessor
    {
        internal const ulong TicksMask = 0x3FFFFFFFFFFFFFFF;
        internal ulong _dateTimeData;
    }

    /// <summary>
    ///  Creates a new <see cref="Value"/> instance with the specified nullable <see cref="DateTimeOffset"/> value.
    /// </summary>
    /// <param name="value">The nullable <see cref="DateTimeOffset"/> value to store.</param>
    private Value(DateTimeOffset? value)
    {
        if (!value.HasValue)
        {
            _object = null;
        }
        else
        {
            this = new(value.Value);
        }
    }

    /// <summary>
    ///  Implicitly converts a <see cref="DateTimeOffset"/> value to a <see cref="Value"/>.
    /// </summary>
    /// <param name="value">The <see cref="DateTimeOffset"/> value to convert.</param>
    /// <returns>A <see cref="Value"/> containing the specified <see cref="DateTimeOffset"/> value.</returns>
    public static implicit operator Value(DateTimeOffset value) => new(value);

    /// <summary>
    ///  Explicitly converts a <see cref="Value"/> to a <see cref="DateTimeOffset"/>.
    /// </summary>
    /// <param name="value">The <see cref="Value"/> to convert.</param>
    /// <returns>The <see cref="DateTimeOffset"/> value stored in the <see cref="Value"/>.</returns>
    /// <exception cref="InvalidCastException">The stored value is not a <see cref="DateTimeOffset"/>.</exception>
    public static explicit operator DateTimeOffset(in Value value) => value.As<DateTimeOffset>();

    /// <summary>
    ///  Implicitly converts a nullable <see cref="DateTimeOffset"/> value to a <see cref="Value"/>.
    /// </summary>
    /// <param name="value">The nullable <see cref="DateTimeOffset"/> value to convert.</param>
    /// <returns>A <see cref="Value"/> containing the specified nullable <see cref="DateTimeOffset"/> value.</returns>
    public static implicit operator Value(DateTimeOffset? value) => new(value);

    /// <summary>
    ///  Explicitly converts a <see cref="Value"/> to a nullable <see cref="DateTimeOffset"/>.
    /// </summary>
    /// <param name="value">The <see cref="Value"/> to convert.</param>
    /// <returns>The nullable <see cref="DateTimeOffset"/> value stored in the <see cref="Value"/>.</returns>
    /// <exception cref="InvalidCastException">The stored value is not a nullable <see cref="DateTimeOffset"/>.</exception>
    public static explicit operator DateTimeOffset?(in Value value) => value.As<DateTimeOffset?>();
    #endregion

    #region DateTime
    /// <summary>
    ///  Creates a new <see cref="Value"/> instance with the specified <see cref="DateTime"/> value.
    /// </summary>
    /// <param name="value">The <see cref="DateTime"/> value to store.</param>
    private Value(DateTime value)
    {
        _union.DateTime = value;
        _object = TypeFlags.DateTime;
    }

    /// <summary>
    ///  Creates a new <see cref="Value"/> instance with the specified nullable <see cref="DateTime"/> value.
    /// </summary>
    /// <param name="value">The nullable <see cref="DateTime"/> value to store.</param>
    private Value(DateTime? value)
    {
        if (value.HasValue)
        {
            _object = TypeFlags.DateTime;
            _union.DateTime = value.Value;
        }
        else
        {
            _object = value;
        }
    }

    /// <summary>
    ///  Implicitly converts a <see cref="DateTime"/> value to a <see cref="Value"/>.
    /// </summary>
    /// <param name="value">The <see cref="DateTime"/> value to convert.</param>
    /// <returns>A <see cref="Value"/> containing the specified <see cref="DateTime"/> value.</returns>
    public static implicit operator Value(DateTime value) => new(value);

    /// <summary>
    ///  Explicitly converts a <see cref="Value"/> to a <see cref="DateTime"/>.
    /// </summary>
    /// <param name="value">The <see cref="Value"/> to convert.</param>
    /// <returns>The <see cref="DateTime"/> value stored in the <see cref="Value"/>.</returns>
    /// <exception cref="InvalidCastException">The stored value is not a <see cref="DateTime"/>.</exception>
    public static explicit operator DateTime(in Value value) => value.As<DateTime>();

    /// <summary>
    ///  Implicitly converts a nullable <see cref="DateTime"/> value to a <see cref="Value"/>.
    /// </summary>
    /// <param name="value">The nullable <see cref="DateTime"/> value to convert.</param>
    /// <returns>A <see cref="Value"/> containing the specified nullable <see cref="DateTime"/> value.</returns>
    public static implicit operator Value(DateTime? value) => new(value);

    /// <summary>
    ///  Explicitly converts a <see cref="Value"/> to a nullable <see cref="DateTime"/>.
    /// </summary>
    /// <param name="value">The <see cref="Value"/> to convert.</param>
    /// <returns>The nullable <see cref="DateTime"/> value stored in the <see cref="Value"/>.</returns>
    /// <exception cref="InvalidCastException">The stored value is not a nullable <see cref="DateTime"/>.</exception>
    public static explicit operator DateTime?(in Value value) => value.As<DateTime?>();
    #endregion

    #region StringSegment
    /// <summary>
    ///  Creates a new <see cref="Value"/> instance with the specified <see cref="StringSegment"/> value.
    /// </summary>
    /// <param name="segment">The <see cref="StringSegment"/> value to store.</param>
    private Value(in StringSegment segment)
    {
        _object = segment.Value;
        if (segment._startIndex == 0 && segment._length == 0)
        {
            _union.UInt64 = ulong.MaxValue;
        }
        else
        {
            _union.Segment = (segment._startIndex, segment._length);
        }
    }

    /// <summary>
    ///  Implicitly converts a <see cref="StringSegment"/> value to a <see cref="Value"/>.
    /// </summary>
    /// <param name="value">The <see cref="StringSegment"/> value to convert.</param>
    /// <returns>A <see cref="Value"/> containing the specified <see cref="StringSegment"/> value.</returns>
    public static implicit operator Value(in StringSegment value) => new(value);

    /// <summary>
    ///  Explicitly converts a <see cref="Value"/> to a <see cref="StringSegment"/>.
    /// </summary>
    /// <param name="value">The <see cref="Value"/> to convert.</param>
    /// <returns>The <see cref="StringSegment"/> value stored in the <see cref="Value"/>.</returns>
    /// <exception cref="InvalidCastException">The stored value is not an <see cref="StringSegment"/>.</exception>
    public static explicit operator StringSegment(in Value value) => value.As<StringSegment>();
    #endregion

    #region ArraySegment
    /// <summary>
    ///  Creates a new <see cref="Value"/> instance with the specified <see cref="ArraySegment{Byte}"/> value.
    /// </summary>
    /// <param name="segment">The <see cref="ArraySegment{Byte}"/> value to store.</param>
    /// <exception cref="ArgumentNullException">The array of the segment is null.</exception>
    private Value(ArraySegment<byte> segment)
    {
        byte[]? array = segment.Array;
        if (array is null)
        {
            ThrowArgumentNull(nameof(segment));
        }

        _object = array;
        if (segment.Offset == 0 && segment.Count == 0)
        {
            _union.UInt64 = ulong.MaxValue;
        }
        else
        {
            _union.Segment = (segment.Offset, segment.Count);
        }
    }

    /// <summary>
    ///  Implicitly converts an <see cref="ArraySegment{Byte}"/> value to a <see cref="Value"/>.
    /// </summary>
    /// <param name="value">The <see cref="ArraySegment{Byte}"/> value to convert.</param>
    /// <returns>A <see cref="Value"/> containing the specified <see cref="ArraySegment{Byte}"/> value.</returns>
    public static implicit operator Value(ArraySegment<byte> value) => new(value);

    /// <summary>
    ///  Explicitly converts a <see cref="Value"/> to an <see cref="ArraySegment{Byte}"/>.
    /// </summary>
    /// <param name="value">The <see cref="Value"/> to convert.</param>
    /// <returns>The <see cref="ArraySegment{Byte}"/> value stored in the <see cref="Value"/>.</returns>
    /// <exception cref="InvalidCastException">The stored value is not an <see cref="ArraySegment{Byte}"/>.</exception>
    public static explicit operator ArraySegment<byte>(in Value value) => value.As<ArraySegment<byte>>();

    /// <summary>
    ///  Creates a new <see cref="Value"/> instance with the specified <see cref="ArraySegment{Char}"/> value.
    /// </summary>
    /// <param name="segment">The <see cref="ArraySegment{Char}"/> value to store.</param>
    /// <exception cref="ArgumentNullException">The array of the segment is null.</exception>
    private Value(ArraySegment<char> segment)
    {
        char[]? array = segment.Array;
        if (array is null)
        {
            ThrowArgumentNull(nameof(segment));
        }

        _object = array;
        if (segment.Offset == 0 && segment.Count == 0)
        {
            _union.UInt64 = ulong.MaxValue;
        }
        else
        {
            _union.Segment = (segment.Offset, segment.Count);
        }
    }

    /// <summary>
    ///  Implicitly converts an <see cref="ArraySegment{Char}"/> value to a <see cref="Value"/>.
    /// </summary>
    /// <param name="value">The <see cref="ArraySegment{Char}"/> value to convert.</param>
    /// <returns>A <see cref="Value"/> containing the specified <see cref="ArraySegment{Char}"/> value.</returns>
    public static implicit operator Value(ArraySegment<char> value) => new(value);

    /// <summary>
    ///  Explicitly converts a <see cref="Value"/> to an <see cref="ArraySegment{Char}"/>.
    /// </summary>
    /// <param name="value">The <see cref="Value"/> to convert.</param>
    /// <returns>The <see cref="ArraySegment{Char}"/> value stored in the <see cref="Value"/>.</returns>
    /// <exception cref="InvalidCastException">The stored value is not an <see cref="ArraySegment{Char}"/>.</exception>
    public static explicit operator ArraySegment<char>(in Value value) => value.As<ArraySegment<char>>();
    #endregion

    #region Decimal
    /// <summary>
    ///  Implicitly converts a <see langword="decimal"/> value to a <see cref="Value"/>.
    /// </summary>
    /// <param name="value">The <see langword="decimal"/> value to convert.</param>
    /// <returns>A <see cref="Value"/> containing the specified <see langword="decimal"/> value.</returns>
    public static implicit operator Value(decimal value) => new(value);

    /// <summary>
    ///  Explicitly converts a <see cref="Value"/> to a <see langword="decimal"/>.
    /// </summary>
    /// <param name="value">The <see cref="Value"/> to convert.</param>
    /// <returns>The <see langword="decimal"/> value stored in the <see cref="Value"/>.</returns>
    /// <exception cref="InvalidCastException">The stored value is not a <see langword="decimal"/>.</exception>
    public static explicit operator decimal(in Value value) => value.As<decimal>();

    /// <summary>
    ///  Implicitly converts a nullable <see langword="decimal"/> value to a <see cref="Value"/>.
    /// </summary>
    /// <param name="value">The nullable <see langword="decimal"/> value to convert.</param>
    /// <returns>A <see cref="Value"/> containing the specified nullable <see langword="decimal"/> value.</returns>
    public static implicit operator Value(decimal? value) => value.HasValue ? new(value.Value) : new(value);

    /// <summary>
    ///  Explicitly converts a <see cref="Value"/> to a nullable <see langword="decimal"/>.
    /// </summary>
    /// <param name="value">The <see cref="Value"/> to convert.</param>
    /// <returns>The nullable <see langword="decimal"/> value stored in the <see cref="Value"/>.</returns>
    /// <exception cref="InvalidCastException">The stored value is not a nullable <see langword="decimal"/>.</exception>
    public static explicit operator decimal?(in Value value) => value.As<decimal?>();
    #endregion

    #region T
    /// <summary>
    ///  Creates a new <see cref="Value"/> instance with the specified value of type T.
    /// </summary>
    /// <typeparam name="T">The type of the value to store.</typeparam>
    /// <param name="value">The value to store.</param>
    /// <returns>A <see cref="Value"/> containing the specified value.</returns>
    /// <remarks>
    ///  <para>
    ///   This method automatically determines the best storage strategy for the specified type,
    ///   using unboxed storage for supported primitive types and boxing for other types.
    ///  </para>
    /// </remarks>
    public static Value Create<T>(T value)
    {
        // Explicit cast for types we don't box
        if (typeof(T) == typeof(bool))
            return new(Unsafe.As<T, bool>(ref Unsafe.AsRef(in value)));
        if (typeof(T) == typeof(byte))
            return new(Unsafe.As<T, byte>(ref Unsafe.AsRef(in value)));
        if (typeof(T) == typeof(sbyte))
            return new(Unsafe.As<T, sbyte>(ref Unsafe.AsRef(in value)));
        if (typeof(T) == typeof(char))
            return new(Unsafe.As<T, char>(ref Unsafe.AsRef(in value)));
        if (typeof(T) == typeof(short))
            return new(Unsafe.As<T, short>(ref Unsafe.AsRef(in value)));
        if (typeof(T) == typeof(int))
            return new(Unsafe.As<T, int>(ref Unsafe.AsRef(in value)));
        if (typeof(T) == typeof(long))
            return new(Unsafe.As<T, long>(ref Unsafe.AsRef(in value)));
        if (typeof(T) == typeof(ushort))
            return new(Unsafe.As<T, ushort>(ref Unsafe.AsRef(in value)));
        if (typeof(T) == typeof(uint))
            return new(Unsafe.As<T, uint>(ref Unsafe.AsRef(in value)));
        if (typeof(T) == typeof(ulong))
            return new(Unsafe.As<T, ulong>(ref Unsafe.AsRef(in value)));
        if (typeof(T) == typeof(float))
            return new(Unsafe.As<T, float>(ref Unsafe.AsRef(in value)));
        if (typeof(T) == typeof(double))
            return new(Unsafe.As<T, double>(ref Unsafe.AsRef(in value)));
        if (typeof(T) == typeof(DateTime))
            return new(Unsafe.As<T, DateTime>(ref Unsafe.AsRef(in value)));
        if (typeof(T) == typeof(DateTimeOffset))
            return new(Unsafe.As<T, DateTimeOffset>(ref Unsafe.AsRef(in value)));

        if (typeof(T) == typeof(bool?))
            return new(Unsafe.As<T, bool?>(ref Unsafe.AsRef(in value)));
        if (typeof(T) == typeof(byte?))
            return new(Unsafe.As<T, byte?>(ref Unsafe.AsRef(in value)));
        if (typeof(T) == typeof(sbyte?))
            return new(Unsafe.As<T, sbyte?>(ref Unsafe.AsRef(in value)));
        if (typeof(T) == typeof(char?))
            return new(Unsafe.As<T, char?>(ref Unsafe.AsRef(in value)));
        if (typeof(T) == typeof(short?))
            return new(Unsafe.As<T, short?>(ref Unsafe.AsRef(in value)));
        if (typeof(T) == typeof(int?))
            return new(Unsafe.As<T, int?>(ref Unsafe.AsRef(in value)));
        if (typeof(T) == typeof(long?))
            return new(Unsafe.As<T, long?>(ref Unsafe.AsRef(in value)));
        if (typeof(T) == typeof(ushort?))
            return new(Unsafe.As<T, ushort?>(ref Unsafe.AsRef(in value)));
        if (typeof(T) == typeof(uint?))
            return new(Unsafe.As<T, uint?>(ref Unsafe.AsRef(in value)));
        if (typeof(T) == typeof(ulong?))
            return new(Unsafe.As<T, ulong?>(ref Unsafe.AsRef(in value)));
        if (typeof(T) == typeof(float?))
            return new(Unsafe.As<T, float?>(ref Unsafe.AsRef(in value)));
        if (typeof(T) == typeof(double?))
            return new(Unsafe.As<T, double?>(ref Unsafe.AsRef(in value)));
        if (typeof(T) == typeof(DateTime?))
            return new(Unsafe.As<T, DateTime?>(ref Unsafe.AsRef(in value)));
        if (typeof(T) == typeof(DateTimeOffset?))
            return new(Unsafe.As<T, DateTimeOffset?>(ref Unsafe.AsRef(in value)));

        if (typeof(T) == typeof(ArraySegment<byte>))
            return new(Unsafe.As<T, ArraySegment<byte>>(ref Unsafe.AsRef(in value)));
        if (typeof(T) == typeof(ArraySegment<char>))
            return new(Unsafe.As<T, ArraySegment<char>>(ref Unsafe.AsRef(in value)));
        if (typeof(T) == typeof(StringSegment))
            return new(Unsafe.As<T, StringSegment>(ref Unsafe.AsRef(in value)));

        if (typeof(T).IsEnum)
        {
            Debug.Assert(Unsafe.SizeOf<T>() <= sizeof(ulong));

            // There may or may not be extra garbage in the 64 bits with the arg, so we need to check the actual size.
            return Unsafe.SizeOf<T>() switch
            {
                1 => new Value(EnumTypeFlag<T>.Instance, Unsafe.As<T, byte>(ref value)),
                2 => new Value(EnumTypeFlag<T>.Instance, Unsafe.As<T, ushort>(ref value)),
                4 => new Value(EnumTypeFlag<T>.Instance, Unsafe.As<T, uint>(ref value)),
                _ => new Value(EnumTypeFlag<T>.Instance, Unsafe.As<T, ulong>(ref value)),
            };
        }

        return new Value(value);
    }

    /// <summary>
    ///  Use this method to avoid generic instantiation for an unsupported value type (T) on <see cref="Create{T}(T)"/>    /// </summary>
    public static Value Box(object? value) => new Value(value);

    private Value(object o, ulong u)
    {
        _object = o;
        _union.UInt64 = u;
    }

    /// <summary>
    ///  Attempts to retrieve the value as the specified type.
    /// </summary>
    /// <typeparam name="T">The type to retrieve the value as.</typeparam>
    /// <param name="value">When this method returns, contains the value if the conversion succeeded.</param>
    /// <returns><see langword="true"/> if the value was successfully retrieved; otherwise, <see langword="false"/>.</returns>
    public readonly unsafe bool TryGetValue<T>(out T value)
    {
        bool success;

        // Checking the type gets all of the non-relevant compares elided by the JIT
        if (_object is not null && ((typeof(T) == typeof(bool) && _object == TypeFlags.Boolean)
            || (typeof(T) == typeof(byte) && _object == TypeFlags.Byte)
            || (typeof(T) == typeof(char) && _object == TypeFlags.Char)
            || (typeof(T) == typeof(double) && _object == TypeFlags.Double)
            || (typeof(T) == typeof(short) && _object == TypeFlags.Int16)
            || (typeof(T) == typeof(int) && _object == TypeFlags.Int32)
            || (typeof(T) == typeof(long) && _object == TypeFlags.Int64)
            || (typeof(T) == typeof(sbyte) && _object == TypeFlags.SByte)
            || (typeof(T) == typeof(float) && _object == TypeFlags.Single)
            || (typeof(T) == typeof(ushort) && _object == TypeFlags.UInt16)
            || (typeof(T) == typeof(uint) && _object == TypeFlags.UInt32)
            || (typeof(T) == typeof(ulong) && _object == TypeFlags.UInt64)))
        {
            value = Unsafe.As<Union, T>(ref Unsafe.AsRef(in _union));
            success = true;
        }
        else if (typeof(T) == typeof(DateTime) && _object == TypeFlags.DateTime)
        {
            value = Unsafe.As<DateTime, T>(ref Unsafe.AsRef(in _union.DateTime));
            success = true;
        }
        else if (typeof(T) == typeof(DateTimeOffset) && _object == TypeFlags.DateTimeOffset)
        {
            DateTimeOffset dto = new(_union.Ticks, TimeSpan.Zero);
            value = Unsafe.As<DateTimeOffset, T>(ref Unsafe.AsRef(in dto));
            success = true;
        }
        else if (typeof(T) == typeof(DateTimeOffset) && _object == TypeFlags.PackedDateTimeOffset)
        {
            DateTimeOffset dto = _union.PackedDateTimeOffset.Extract();
            value = Unsafe.As<DateTimeOffset, T>(ref Unsafe.AsRef(in dto));
            success = true;
        }
        else
        {
            success = typeof(T).IsValueType ? TryGetValueSlow(out value) : TryGetObjectSlow(out value);
        }

        return success;
    }

    private readonly bool TryGetValueSlow<T>(out T value)
    {
        // Single return has a significant performance benefit.

        bool result = false;

        if (_object is null)
        {
            // A null is stored, it can only be assigned to a reference type or nullable.
            value = default!;
            result = Nullable.GetUnderlyingType(typeof(T)) is not null;
        }
        else if (typeof(T).IsEnum && _object is TypeFlag<T> typeFlag)
        {
            value = typeFlag.To(in this);
            result = true;
        }
        else if (_object is T t)
        {
            value = t;
            result = true;
        }
        else if (typeof(T) == typeof(StringSegment))
        {
            ulong bits = _union.UInt64;
            if (bits != 0 && _object is string str)
            {
                StringSegment segment = bits != ulong.MaxValue
                    ? new(str, _union.Segment.Offset, _union.Segment.Count)
                    : new(str, 0, 0);
                value = Unsafe.As<StringSegment, T>(ref segment);
                result = true;
            }
            else
            {
                value = default!;
            }
        }
        else if (typeof(T) == typeof(ArraySegment<byte>))
        {
            ulong bits = _union.UInt64;
            if (bits != 0 && _object is byte[] byteArray)
            {
                ArraySegment<byte> segment = bits != ulong.MaxValue
                    ? new(byteArray, _union.Segment.Offset, _union.Segment.Count)
                    : new(byteArray, 0, 0);
                value = Unsafe.As<ArraySegment<byte>, T>(ref segment);
                result = true;
            }
            else
            {
                value = default!;
            }
        }
        else if (typeof(T) == typeof(ArraySegment<char>))
        {
            ulong bits = _union.UInt64;
            if (bits != 0 && _object is char[] charArray)
            {
                ArraySegment<char> segment = bits != ulong.MaxValue
                    ? new(charArray, _union.Segment.Offset, _union.Segment.Count)
                    : new(charArray, 0, 0);
                value = Unsafe.As<ArraySegment<char>, T>(ref segment);
                result = true;
            }
            else
            {
                value = default!;
            }
        }
        else if (typeof(T) == typeof(int?) && _object == TypeFlags.Int32)
        {
            int? @int = _union.Int32;
            value = Unsafe.As<int?, T>(ref Unsafe.AsRef(in @int));
            result = true;
        }
        else if (typeof(T) == typeof(long?) && _object == TypeFlags.Int64)
        {
            long? @long = _union.Int64;
            value = Unsafe.As<long?, T>(ref Unsafe.AsRef(in @long));
            result = true;
        }
        else if (typeof(T) == typeof(bool?) && _object == TypeFlags.Boolean)
        {
            bool? @bool = _union.Boolean;
            value = Unsafe.As<bool?, T>(ref Unsafe.AsRef(in @bool));
            result = true;
        }
        else if (typeof(T) == typeof(float?) && _object == TypeFlags.Single)
        {
            float? single = _union.Single;
            value = Unsafe.As<float?, T>(ref Unsafe.AsRef(in single));
            result = true;
        }
        else if (typeof(T) == typeof(double?) && _object == TypeFlags.Double)
        {
            double? @double = _union.Double;
            value = Unsafe.As<double?, T>(ref Unsafe.AsRef(in @double));
            result = true;
        }
        else if (typeof(T) == typeof(uint?) && _object == TypeFlags.UInt32)
        {
            uint? @uint = _union.UInt32;
            value = Unsafe.As<uint?, T>(ref Unsafe.AsRef(in @uint));
            result = true;
        }
        else if (typeof(T) == typeof(ulong?) && _object == TypeFlags.UInt64)
        {
            ulong? @ulong = _union.UInt64;
            value = Unsafe.As<ulong?, T>(ref Unsafe.AsRef(in @ulong));
            result = true;
        }
        else if (typeof(T) == typeof(char?) && _object == TypeFlags.Char)
        {
            char? @char = _union.Char;
            value = Unsafe.As<char?, T>(ref Unsafe.AsRef(in @char));
            result = true;
        }
        else if (typeof(T) == typeof(short?) && _object == TypeFlags.Int16)
        {
            short? @short = _union.Int16;
            value = Unsafe.As<short?, T>(ref Unsafe.AsRef(in @short));
            result = true;
        }
        else if (typeof(T) == typeof(ushort?) && _object == TypeFlags.UInt16)
        {
            ushort? @ushort = _union.UInt16;
            value = Unsafe.As<ushort?, T>(ref Unsafe.AsRef(in @ushort));
            result = true;
        }
        else if (typeof(T) == typeof(byte?) && _object == TypeFlags.Byte)
        {
            byte? @byte = _union.Byte;
            value = Unsafe.As<byte?, T>(ref Unsafe.AsRef(in @byte));
            result = true;
        }
        else if (typeof(T) == typeof(sbyte?) && _object == TypeFlags.SByte)
        {
            sbyte? @sbyte = _union.SByte;
            value = Unsafe.As<sbyte?, T>(ref Unsafe.AsRef(in @sbyte));
            result = true;
        }
        else if (typeof(T) == typeof(DateTime?) && _object == TypeFlags.DateTime)
        {
            DateTime? dateTime = _union.DateTime;
            value = Unsafe.As<DateTime?, T>(ref Unsafe.AsRef(in dateTime));
            result = true;
        }
        else if (typeof(T) == typeof(DateTimeOffset?) && _object == TypeFlags.DateTimeOffset)
        {
            DateTimeOffset? dto = new DateTimeOffset(_union.Ticks, TimeSpan.Zero);
            value = Unsafe.As<DateTimeOffset?, T>(ref Unsafe.AsRef(in dto));
            result = true;
        }
        else if (typeof(T) == typeof(DateTimeOffset?) && _object == TypeFlags.PackedDateTimeOffset)
        {
            DateTimeOffset? dto = _union.PackedDateTimeOffset.Extract();
            value = Unsafe.As<DateTimeOffset?, T>(ref Unsafe.AsRef(in dto));
            result = true;
        }
        else if (Nullable.GetUnderlyingType(typeof(T)) is Type underlyingType
            && underlyingType.IsEnum
            && _object is TypeFlag underlyingTypeFlag
            && underlyingTypeFlag.Type == underlyingType)
        {
            // Asked for a nullable enum and we've got that type.

            // We've got multiple layouts, depending on the size of the enum backing field. We can't use the
            // nullable itself (e.g. default(T)) as a template as it gets treated specially by the runtime.

            int size = Unsafe.SizeOf<T>();

            switch (size)
            {
                case (2):
                    NullableTemplate<byte> byteTemplate = new(_union.Byte);
                    value = Unsafe.As<NullableTemplate<byte>, T>(ref Unsafe.AsRef(in byteTemplate));
                    result = true;
                    break;
                case (4):
                    NullableTemplate<ushort> ushortTemplate = new(_union.UInt16);
                    value = Unsafe.As<NullableTemplate<ushort>, T>(ref Unsafe.AsRef(in ushortTemplate));
                    result = true;
                    break;
                case (8):
                    NullableTemplate<uint> uintTemplate = new(_union.UInt32);
                    value = Unsafe.As<NullableTemplate<uint>, T>(ref Unsafe.AsRef(in uintTemplate));
                    result = true;
                    break;
                case (16):
                    NullableTemplate<ulong> ulongTemplate = new(_union.UInt64);
                    value = Unsafe.As<NullableTemplate<ulong>, T>(ref Unsafe.AsRef(in ulongTemplate));
                    result = true;
                    break;
                default:
                    ThrowInvalidOperation();
                    value = default!;
                    result = false;
                    break;
            }
        }
        else
        {
            value = default!;
            result = false;
        }

        return result;
    }

    private readonly bool TryGetObjectSlow<T>(out T value)
    {
        // Single return has a significant performance benefit.

        bool result = false;

        if (_object is null)
        {
            value = default!;
        }
        else if (typeof(T) == typeof(string))
        {
            if (_union.UInt64 == 0 && _object is string str)
            {
                value = (T)(object)str;
                result = true;
            }
            else
            {
                // Don't allow "implicit" cast to string if we stored a segment.
                value = default!;
                result = false;
            }
        }
        else if (typeof(T) == typeof(char[]))
        {
            if (_union.UInt64 == 0 && _object is char[])
            {
                value = (T)_object;
                result = true;
            }
            else
            {
                // Don't allow "implicit" cast to array if we stored a segment.
                value = default!;
                result = false;
            }
        }
        else if (typeof(T) == typeof(byte[]))
        {
            if (_union.UInt64 == 0 && _object is byte[])
            {
                value = (T)_object;
                result = true;
            }
            else
            {
                // Don't allow "implicit" cast to array if we stored a segment.
                value = default!;
                result = false;
            }
        }
        else if (typeof(T) == typeof(object))
        {
            // This case must also come before the _object is T case to make sure we don't leak our flags.
            if (_object is TypeFlag flag)
            {
                value = (T)flag.ToObject(this);
                result = true;
            }
            else if (_union.UInt64 != 0 && _object is string stringValue)
            {
                value = _union.UInt64 != ulong.MaxValue
                    ? (T)(object)new StringSegment(stringValue, _union.Segment.Offset, _union.Segment.Count)
                    : (T)(object)new StringSegment(stringValue, 0, 0);
                result = true;
            }
            else if (_union.UInt64 != 0 && _object is char[] chars)
            {
                value = _union.UInt64 != ulong.MaxValue
                    ? (T)(object)new ArraySegment<char>(chars, _union.Segment.Offset, _union.Segment.Count)
                    : (T)(object)new ArraySegment<char>(chars, 0, 0);
                result = true;
            }
            else if (_union.UInt64 != 0 && _object is byte[] bytes)
            {
                value = _union.UInt64 != ulong.MaxValue
                    ? (T)(object)new ArraySegment<byte>(bytes, _union.Segment.Offset, _union.Segment.Count)
                    : (T)(object)new ArraySegment<byte>(bytes, 0, 0);
                result = true;
            }
            else
            {
                value = (T)_object;
                result = true;
            }
        }
        else if (_object is T t)
        {
            value = t;
            result = true;
        }
        else
        {
            value = default!;
            result = false;
        }

        return result;
    }

    /// <summary>
    ///  Retrieves the value as the specified type.
    /// </summary>
    /// <typeparam name="T">The type to retrieve the value as.</typeparam>
    /// <returns>The value as the specified type.</returns>
    /// <exception cref="InvalidCastException">The stored value cannot be converted to the specified type.</exception>
    public readonly T As<T>()
    {
        if (!TryGetValue(out T value))
        {
            ThrowInvalidCast();
        }

        return value;
    }
    #endregion T

    #region Format
    /// <summary>
    ///  Format the variant into the given <paramref name="destination"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Format(ref ValueStringBuilder destination, ReadOnlySpan<char> format)
    {
        // This must stay aligned with logic in Type. To get the best performance, we replicate the logic here.

        // Try keeping these checks in expected frequency order. Note that directly casting out of the enum didn't make
        // much of a perf difference over using the As<T>() method. This is explicitly structured to have a single
        // return to simplify the JIT's job of optimizing the code. Also try to keep the method small to allow the JIT
        // to actually inline it.

        if (_object is TypeFlag typeFlag)
        {
            Type type = typeFlag.Type;

            if (type == typeof(int))
            {
                destination.AppendFormatted(_union.Int32, format);
            }
            else if (type == typeof(long))
            {
                destination.AppendFormatted(_union.Int64, format);
            }
            else if (type == typeof(bool))
            {
                destination.AppendFormatted(_union.Boolean, format);
            }
            else if (type == typeof(uint))
            {
                destination.AppendFormatted(_union.UInt32, format);
            }
            else
            {
                // Push some of the logic off to facilitate inlining.
                FormatTypeFlagSlow(ref destination, typeFlag, format);
            }
        }
        else if (_object?.GetType() is Type objectType)
        {
            if (_union.UInt64 == 0)
            {
                destination.AppendFormatted(_object, format);
            }
            else
            {
                // Need to special case ArraySegment<byte>, ArraySegment<char>, and StringSegment

                Debug.Assert(objectType.IsArray || objectType == typeof(StringSegment));

                // We have an ArraySegment or StringSegment
                if (objectType == typeof(byte[]))
                {
                    destination.AppendFormatted(As<ArraySegment<byte>>(), format);
                }
                else if (objectType == typeof(char[]))
                {
                    destination.AppendFormatted(As<ArraySegment<char>>(), format);
                }
                else if (objectType == typeof(string))
                {
                    destination.AppendFormatted(As<StringSegment>(), format);
                }
                else
                {
                    Debug.Fail($"Unexpected type {objectType.Name}.");
                }
            }
        }
    }

    private void FormatTypeFlagSlow(
        ref ValueStringBuilder destination,
        TypeFlag typeFlag,
        ReadOnlySpan<char> format)
    {
        Type type = typeFlag.Type;

        if (type == typeof(ulong))
        {
            destination.AppendFormatted(_union.UInt64, format);
        }
        else if (type == typeof(char))
        {
            destination.AppendFormatted(_union.Char, format);
        }
        else if (type == typeof(byte))
        {
            destination.AppendFormatted(_union.Byte, format);
        }
        else if (type == typeof(DateTime))
        {
            destination.AppendFormatted(_union.DateTime, format);
        }
        else if (type == typeof(DateTimeOffset))
        {
            destination.AppendFormatted(_union.PackedDateTimeOffset.Extract(), format);
        }
        else if (type == typeof(double))
        {
            destination.AppendFormatted(_union.Double, format);
        }
        else if (type == typeof(short))
        {
            destination.AppendFormatted(_union.Int16, format);
        }
        else if (type == typeof(sbyte))
        {
            destination.AppendFormatted(_union.SByte, format);
        }
        else if (type == typeof(float))
        {
            destination.AppendFormatted(_union.Single, format);
        }
        else if (type == typeof(ushort))
        {
            destination.AppendFormatted(_union.UInt16, format);
        }
#if NETFRAMEWORK
        else if (typeFlag is IEnumType enumType
            && format.Length == 0
            && EnumExtensions.GetEnumData(type) is var enumData)
        {
            ulong value = enumType.AsUlong(in this);
            bool signed = enumType.IsSigned;

            if (enumData.IsFlags)
            {
                destination.InternalFlagsFormat(value, signed, enumData);
                return;
            }

            (ulong[] values, string[] names) = enumData.Data;
            int index = Array.BinarySearch(values, value);
            if (index >= 0)
            {
                destination.AppendFormatted(names[index], format);
            }
            else
            {
                if (signed)
                {
                    destination.AppendFormatted((long)value);
                }
                else
                {
                    destination.AppendFormatted(value);
                }
            }
        }
        else
#else
        else
#endif
        {
            destination.AppendFormatted(typeFlag.ToObject(in this), format);
        }
    }
    #endregion
}
