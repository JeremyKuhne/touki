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
    private static readonly FieldInfo s_numberGroupSizes = GetRequiredField("numberGroupSizes");
    private static readonly FieldInfo s_currencyGroupSizes = GetRequiredField("currencyGroupSizes");
    private static readonly FieldInfo s_percentGroupSizes = GetRequiredField("percentGroupSizes");

    /// <summary>
    ///  Gets the number group sizes without cloning the backing array.
    /// </summary>
    /// <param name="info">The number format information to inspect.</param>
    /// <returns>The backing array of number group sizes.</returns>
    internal static int[] GetNumberGroupSizes(this NumberFormatInfo info) =>
        s_numberGroupSizes.GetValue(info) as int[] ?? throw new InvalidOperationException();

    /// <summary>
    ///  Gets the currency group sizes without cloning the backing array.
    /// </summary>
    /// <param name="info">The number format information to inspect.</param>
    /// <returns>The backing array of currency group sizes.</returns>
    internal static int[] GetCurrencyGroupSizes(this NumberFormatInfo info) =>
        s_currencyGroupSizes.GetValue(info) as int[] ?? throw new InvalidOperationException();

    /// <summary>
    ///  Gets the percent group sizes without cloning the backing array.
    /// </summary>
    /// <param name="info">The number format information to inspect.</param>
    /// <returns>The backing array of percent group sizes.</returns>
    internal static int[] GetPercentGroupSizes(this NumberFormatInfo info) =>
        s_percentGroupSizes.GetValue(info) as int[] ?? throw new InvalidOperationException();

    private static FieldInfo GetRequiredField(string name)
    {
        if (typeof(NumberFormatInfo).GetField(
            name,
            BindingFlags.NonPublic | BindingFlags.Instance) is not { } field)
        {
            throw new InvalidOperationException($"Failed to find '{name}' field in NumberFormatInfo.");
        }

        return field;
    }
}
