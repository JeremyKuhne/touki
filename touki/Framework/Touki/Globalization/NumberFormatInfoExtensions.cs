// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Globalization;
using System.Reflection;

namespace Touki.Globalization;

/// <summary>
///  Provides access to .NET Framework number-format group sizes without cloning their backing arrays.
/// </summary>
internal static class NumberFormatInfoExtensions
{
    private static readonly FieldInfo s_numberGroupSizes = typeof(NumberFormatInfo).GetField("numberGroupSizes", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("Failed to find 'numberGroupSizes' field in NumberInfo.");

    private static readonly FieldInfo s_currencyGroupSizes = typeof(NumberFormatInfo).GetField("currencyGroupSizes", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("Failed to find 'currencyGroupSizes' field in NumberInfo.");

    private static readonly FieldInfo s_percentGroupSizes = typeof(NumberFormatInfo).GetField("percentGroupSizes", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("Failed to find 'percentGroupSizes' field in NumberInfo.");

    /// <summary>
    ///  Gets the number group sizes without cloning the backing array.
    /// </summary>
    /// <param name="info">The number format information to inspect.</param>
    /// <returns>The backing array of number group sizes.</returns>
    internal static int[] GetNumberGroupSizes(this NumberFormatInfo info) => (int[])s_numberGroupSizes.GetValue(info)!;

    /// <summary>
    ///  Gets the currency group sizes without cloning the backing array.
    /// </summary>
    /// <param name="info">The number format information to inspect.</param>
    /// <returns>The backing array of currency group sizes.</returns>
    internal static int[] GetCurrencyGroupSizes(this NumberFormatInfo info) => (int[])s_currencyGroupSizes.GetValue(info)!;

    /// <summary>
    ///  Gets the percent group sizes without cloning the backing array.
    /// </summary>
    /// <param name="info">The number format information to inspect.</param>
    /// <returns>The backing array of percent group sizes.</returns>
    internal static int[] GetPercentGroupSizes(this NumberFormatInfo info) => (int[])s_percentGroupSizes.GetValue(info)!;
}
