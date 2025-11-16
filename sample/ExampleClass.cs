// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Sample;

/// <summary>
///  Example class showing that we can code using the latest C# features.
/// </summary>
public static class ExampleClass
{
    /// <summary>
    ///  Demonstrates interpolated string handlers which are polyfilled for .NET Framework.
    /// </summary>
    /// <param name="name">The name to include in the message.</param>
    /// <param name="count">The count to include in the message.</param>
    /// <returns>A formatted greeting message.</returns>
    public static string DemoInterpolatedStringHandler(string name, int count)
    {
        // Interpolated string handlers allow efficient string building without allocations
        //
        // The DefaultInterpolatedStringHandler is polyfilled in Touki for .NET Framework, eliminating allocations
        // for primitive types (float, int, etc.).
        return $"Hello {name}, you have {count} items in your collection!";
    }

    /// <summary>
    ///  Demonstrates System.Numerics.BitOperations which are polyfilled for .NET Framework.
    /// </summary>
    /// <param name="value">The value to analyze.</param>
    /// <returns>Information about the bit patterns.</returns>
    public static (int LeadingZeros, int TrailingZeros, int PopCount, bool IsPowerOfTwo) DemoBitOperations(uint value)
    {
        // BitOperations provides intrinsic bit-twiddling operations
        int leadingZeros = BitOperations.LeadingZeroCount(value);
        int trailingZeros = BitOperations.TrailingZeroCount(value);
        int popCount = BitOperations.PopCount(value);
        bool isPowerOfTwo = BitOperations.IsPow2(value);

        return (leadingZeros, trailingZeros, popCount, isPowerOfTwo);
    }

    /// <summary>
    ///  Demonstrates System.Threading.Lock which is polyfilled for .NET Framework.
    /// </summary>
    /// <param name="sharedCounter">A shared counter to increment safely.</param>
    public static void DemoSystemThreadingLock(ref int sharedCounter)
    {
        // The Lock type is polyfilled in Touki for .NET Framework
        Lock lockObj = new();

        lock (lockObj)
        {
            sharedCounter++;
        }
    }

    /// <summary>
    ///  Demonstrates Math.DivRem extension which is polyfilled for .NET Framework.
    /// </summary>
    /// <param name="dividend">The dividend.</param>
    /// <param name="divisor">The divisor.</param>
    /// <returns>The quotient and remainder as a tuple.</returns>
    public static (int Quotient, int Remainder) DemoMathDivRem(int dividend, int divisor)
    {
        // Math.DivRem returns both quotient and remainder efficiently
        // This is an extension method polyfilled for .NET Framework
        return Math.DivRem(dividend, divisor);
    }

    /// <summary>
    ///  Demonstrates Interlocked extensions for unsigned types which are polyfilled for .NET Framework.
    /// </summary>
    /// <param name="counter">The counter to increment atomically.</param>
    /// <returns>The incremented value.</returns>
    public static uint DemoInterlockedExtensions(ref uint counter)
    {
        // Interlocked operations for uint are polyfilled for .NET Framework
        uint _ = Interlocked.Increment(ref counter);
        uint added = Interlocked.Add(ref counter, 10u);
        return added;
    }

    /// <summary>
    ///  Demonstrates char extension methods for ASCII checks which are polyfilled for .NET Framework.
    /// </summary>
    /// <param name="c">The character to check.</param>
    /// <returns>Information about the character's classification.</returns>
    public static (bool IsLetter, bool IsDigit, bool IsHex, bool IsLetterOrDigit) DemoCharExtensions(char c)
    {
        // Char extension methods for ASCII classification are polyfilled for .NET Framework
        bool isLetter = char.IsAsciiLetter(c);
        bool isDigit = char.IsAsciiDigit(c);
        bool isHex = char.IsAsciiHexDigit(c);
        bool isLetterOrDigit = char.IsAsciiLetterOrDigit(c);

        return (isLetter, isDigit, isHex, isLetterOrDigit);
    }

    /// <summary>
    ///  Demonstrates numeric extension methods for floats and doubles which are polyfilled for .NET Framework.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns>Information about the floating-point value.</returns>
    public static (bool IsFinite, bool IsNegative) DemoNumberExtensions(double value)
    {
        // Numeric extension methods for checking finite and negative are polyfilled for .NET Framework
#if NET
        bool isFinite = double.IsFinite(value);
        bool isNegative = double.IsNegative(value);

        return (isFinite, isNegative);
#else
        // TODO
        return (true, true);
#endif
    }

    /// <summary>
    ///  Demonstrates multiple C# 8.0-14.0 features in one method.
    /// </summary>
    /// <param name="values">An array of values.</param>
    /// <param name="threshold">The threshold value.</param>
    /// <returns>A formatted string with analysis results.</returns>
    public static string DemoCombinedFeatures(int[] values, int threshold)
    {
        // Polyfilled ArgumentNullException for .NET Framework
        ArgumentNullException.ThrowIfNull(values);

        List<int> filteredValues = [];

        string category = threshold switch
        {
            < 0 => "negative",
            0 => "zero",
            > 0 and <= 100 => "small positive",
            _ => "large positive"
        };

        foreach (int value in values)
        {
            if (value is > 0 and < 1000)
            {
                filteredValues.Add(value);
            }
        }

        // Get last three elements safely
        int[] lastThree = filteredValues.Count >= 3
            ? [.. filteredValues.GetRange(filteredValues.Count - 3, 3)]
            : [.. filteredValues];

        // Interpolated string handler (polyfilled)
        return $"Category: {category}, Filtered count: {filteredValues.Count}, Last values: {string.Join(", ", lastThree)}";
    }

    /// <summary>
    ///  Demonstrates memory and span operations with ranges.
    /// </summary>
    /// <param name="buffer">A buffer of bytes.</param>
    /// <returns>Statistics about the buffer.</returns>
    public static (int Sum, int FirstFive, int LastFive) DemoMemoryOperations(ReadOnlySpan<byte> buffer)
    {
        int sum = 0;
        foreach (byte b in buffer)
        {
            sum += b;
        }

        // C# 8.0: Using ranges with spans
        ReadOnlySpan<byte> firstFive = buffer[..5];
        ReadOnlySpan<byte> lastFive = buffer[^5..];

        return (sum, firstFive[0], lastFive[0]);
    }
}
