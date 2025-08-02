// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;

namespace Framework.Touki;

/// <summary>Provides atomic operations for variables that are shared by multiple threads.</summary>
/// <remarks>Contains the methods .NET Framework does not have in <see cref="Interlocked"/>.</remarks>
public static class Interlock
{
    /// <summary>Increments a specified variable and stores the result, as an atomic operation.</summary>
    /// <param name="location">The variable whose value is to be incremented.</param>
    /// <returns>The incremented value.</returns>
    /// <exception cref="NullReferenceException">The address of location is a null pointer.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Increment(ref uint location) =>
        Add(ref location, 1);

    /// <summary>Increments a specified variable and stores the result, as an atomic operation.</summary>
    /// <param name="location">The variable whose value is to be incremented.</param>
    /// <returns>The incremented value.</returns>
    /// <exception cref="NullReferenceException">The address of location is a null pointer.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Increment(ref ulong location) =>
        Add(ref location, 1);

    /// <summary>Decrements a specified variable and stores the result, as an atomic operation.</summary>
    /// <param name="location">The variable whose value is to be decremented.</param>
    /// <returns>The decremented value.</returns>
    /// <exception cref="NullReferenceException">The address of location is a null pointer.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Decrement(ref uint location) =>
        (uint)Interlocked.Add(ref Unsafe.As<uint, int>(ref location), -1);

    /// <summary>Decrements a specified variable and stores the result, as an atomic operation.</summary>
    /// <param name="location">The variable whose value is to be decremented.</param>
    /// <returns>The decremented value.</returns>
    /// <exception cref="NullReferenceException">The address of location is a null pointer.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Decrement(ref ulong location) =>
        (ulong)Interlocked.Add(ref Unsafe.As<ulong, long>(ref location), -1);

    /// <summary>Sets a 32-bit unsigned integer to a specified value and returns the original value, as an atomic operation.</summary>
    /// <param name="location1">The variable to set to the specified value.</param>
    /// <param name="value">The value to which the <paramref name="location1"/> parameter is set.</param>
    /// <returns>The original value of <paramref name="location1"/>.</returns>
    /// <exception cref="NullReferenceException">The address of location1 is a null pointer.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Exchange(ref uint location1, uint value) =>
        (uint)Interlocked.Exchange(ref Unsafe.As<uint, int>(ref location1), (int)value);

    /// <summary>Sets a 64-bit unsigned integer to a specified value and returns the original value, as an atomic operation.</summary>
    /// <param name="location1">The variable to set to the specified value.</param>
    /// <param name="value">The value to which the <paramref name="location1"/> parameter is set.</param>
    /// <returns>The original value of <paramref name="location1"/>.</returns>
    /// <exception cref="NullReferenceException">The address of location1 is a null pointer.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Exchange(ref ulong location1, ulong value) =>
        (ulong)Interlocked.Exchange(ref Unsafe.As<ulong, long>(ref location1), (long)value);

    /// <summary>Compares two 32-bit unsigned integers for equality and, if they are equal, replaces the first value.</summary>
    /// <param name="location1">The destination, whose value is compared with <paramref name="comparand"/> and possibly replaced.</param>
    /// <param name="value">The value that replaces the destination value if the comparison results in equality.</param>
    /// <param name="comparand">The value that is compared to the value at <paramref name="location1"/>.</param>
    /// <returns>The original value in <paramref name="location1"/>.</returns>
    /// <exception cref="NullReferenceException">The address of <paramref name="location1"/> is a null pointer.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint CompareExchange(ref uint location1, uint value, uint comparand) =>
        (uint)Interlocked.CompareExchange(ref Unsafe.As<uint, int>(ref location1), (int)value, (int)comparand);

    /// <summary>Compares two 64-bit unsigned integers for equality and, if they are equal, replaces the first value.</summary>
    /// <param name="location1">The destination, whose value is compared with <paramref name="comparand"/> and possibly replaced.</param>
    /// <param name="value">The value that replaces the destination value if the comparison results in equality.</param>
    /// <param name="comparand">The value that is compared to the value at <paramref name="location1"/>.</param>
    /// <returns>The original value in <paramref name="location1"/>.</returns>
    /// <exception cref="NullReferenceException">The address of <paramref name="location1"/> is a null pointer.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong CompareExchange(ref ulong location1, ulong value, ulong comparand) =>
        (ulong)Interlocked.CompareExchange(ref Unsafe.As<ulong, long>(ref location1), (long)value, (long)comparand);

    /// <summary>Adds two 32-bit unsigned integers and replaces the first integer with the sum, as an atomic operation.</summary>
    /// <param name="location1">A variable containing the first value to be added. The sum of the two values is stored in <paramref name="location1"/>.</param>
    /// <param name="value">The value to be added to the integer at <paramref name="location1"/>.</param>
    /// <returns>The new value stored at <paramref name="location1"/>.</returns>
    /// <exception cref="NullReferenceException">The address of <paramref name="location1"/> is a null pointer.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Add(ref uint location1, uint value) =>
        (uint)Interlocked.Add(ref Unsafe.As<uint, int>(ref location1), (int)value);

    /// <summary>Adds two 64-bit unsigned integers and replaces the first integer with the sum, as an atomic operation.</summary>
    /// <param name="location1">A variable containing the first value to be added. The sum of the two values is stored in <paramref name="location1"/>.</param>
    /// <param name="value">The value to be added to the integer at <paramref name="location1"/>.</param>
    /// <returns>The new value stored at <paramref name="location1"/>.</returns>
    /// <exception cref="NullReferenceException">The address of <paramref name="location1"/> is a null pointer.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Add(ref ulong location1, ulong value) =>
        (ulong)Interlocked.Add(ref Unsafe.As<ulong, long>(ref location1), (long)value);

    /// <summary>Returns a 64-bit unsigned value, loaded as an atomic operation.</summary>
    /// <param name="location">The 64-bit value to be loaded.</param>
    /// <returns>The loaded value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Read(ref readonly ulong location) => CompareExchange(ref Unsafe.AsRef(in location), 0, 0);

    /// <summary>Bitwise "ands" two 32-bit signed integers and replaces the first integer with the result, as an atomic operation.</summary>
    /// <param name="location1">A variable containing the first value to be combined. The result is stored in <paramref name="location1"/>.</param>
    /// <param name="value">The value to be combined with the integer at <paramref name="location1"/>.</param>
    /// <returns>The original value in <paramref name="location1"/>.</returns>
    /// <exception cref="NullReferenceException">The address of <paramref name="location1"/> is a null pointer.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int And(ref int location1, int value)
    {
        int current = location1;
        while (true)
        {
            int newValue = current & value;
            int oldValue = Interlocked.CompareExchange(ref location1, newValue, current);
            if (oldValue == current)
            {
                return oldValue;
            }
            current = oldValue;
        }
    }

    /// <summary>Bitwise "ands" two 32-bit unsigned integers and replaces the first integer with the result, as an atomic operation.</summary>
    /// <param name="location1">A variable containing the first value to be combined. The result is stored in <paramref name="location1"/>.</param>
    /// <param name="value">The value to be combined with the integer at <paramref name="location1"/>.</param>
    /// <returns>The original value in <paramref name="location1"/>.</returns>
    /// <exception cref="NullReferenceException">The address of <paramref name="location1"/> is a null pointer.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint And(ref uint location1, uint value) =>
        (uint)And(ref Unsafe.As<uint, int>(ref location1), (int)value);

    /// <summary>Bitwise "ands" two 64-bit signed integers and replaces the first integer with the result, as an atomic operation.</summary>
    /// <param name="location1">A variable containing the first value to be combined. The result is stored in <paramref name="location1"/>.</param>
    /// <param name="value">The value to be combined with the integer at <paramref name="location1"/>.</param>
    /// <returns>The original value in <paramref name="location1"/>.</returns>
    /// <exception cref="NullReferenceException">The address of <paramref name="location1"/> is a null pointer.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long And(ref long location1, long value)
    {
        long current = location1;
        while (true)
        {
            long newValue = current & value;
            long oldValue = Interlocked.CompareExchange(ref location1, newValue, current);
            if (oldValue == current)
            {
                return oldValue;
            }
            current = oldValue;
        }
    }

    /// <summary>Bitwise "ands" two 64-bit unsigned integers and replaces the first integer with the result, as an atomic operation.</summary>
    /// <param name="location1">A variable containing the first value to be combined. The result is stored in <paramref name="location1"/>.</param>
    /// <param name="value">The value to be combined with the integer at <paramref name="location1"/>.</param>
    /// <returns>The original value in <paramref name="location1"/>.</returns>
    /// <exception cref="NullReferenceException">The address of <paramref name="location1"/> is a null pointer.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong And(ref ulong location1, ulong value) =>
        (ulong)And(ref Unsafe.As<ulong, long>(ref location1), (long)value);

    /// <summary>Bitwise "ors" two 32-bit signed integers and replaces the first integer with the result, as an atomic operation.</summary>
    /// <param name="location1">A variable containing the first value to be combined. The result is stored in <paramref name="location1"/>.</param>
    /// <param name="value">The value to be combined with the integer at <paramref name="location1"/>.</param>
    /// <returns>The original value in <paramref name="location1"/>.</returns>
    /// <exception cref="NullReferenceException">The address of <paramref name="location1"/> is a null pointer.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Or(ref int location1, int value)
    {
        int current = location1;
        while (true)
        {
            int newValue = current | value;
            int oldValue = Interlocked.CompareExchange(ref location1, newValue, current);
            if (oldValue == current)
            {
                return oldValue;
            }
            current = oldValue;
        }
    }

    /// <summary>Bitwise "ors" two 32-bit unsigned integers and replaces the first integer with the result, as an atomic operation.</summary>
    /// <param name="location1">A variable containing the first value to be combined. The result is stored in <paramref name="location1"/>.</param>
    /// <param name="value">The value to be combined with the integer at <paramref name="location1"/>.</param>
    /// <returns>The original value in <paramref name="location1"/>.</returns>
    /// <exception cref="NullReferenceException">The address of <paramref name="location1"/> is a null pointer.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Or(ref uint location1, uint value) =>
        (uint)Or(ref Unsafe.As<uint, int>(ref location1), (int)value);

    /// <summary>Bitwise "ors" two 64-bit signed integers and replaces the first integer with the result, as an atomic operation.</summary>
    /// <param name="location1">A variable containing the first value to be combined. The result is stored in <paramref name="location1"/>.</param>
    /// <param name="value">The value to be combined with the integer at <paramref name="location1"/>.</param>
    /// <returns>The original value in <paramref name="location1"/>.</returns>
    /// <exception cref="NullReferenceException">The address of <paramref name="location1"/> is a null pointer.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long Or(ref long location1, long value)
    {
        long current = location1;
        while (true)
        {
            long newValue = current | value;
            long oldValue = Interlocked.CompareExchange(ref location1, newValue, current);
            if (oldValue == current)
            {
                return oldValue;
            }
            current = oldValue;
        }
    }

    /// <summary>Bitwise "ors" two 64-bit unsigned integers and replaces the first integer with the result, as an atomic operation.</summary>
    /// <param name="location1">A variable containing the first value to be combined. The result is stored in <paramref name="location1"/>.</param>
    /// <param name="value">The value to be combined with the integer at <paramref name="location1"/>.</param>
    /// <returns>The original value in <paramref name="location1"/>.</returns>
    /// <exception cref="NullReferenceException">The address of <paramref name="location1"/> is a null pointer.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Or(ref ulong location1, ulong value) =>
        (ulong)Or(ref Unsafe.As<ulong, long>(ref location1), (long)value);
}
