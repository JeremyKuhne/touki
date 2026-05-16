// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Reflection;
using Microsoft.Build.Globbing;

namespace Touki.Io;

/// <summary>
///  Reflection-based access to MSBuild's internal <c>EscapingUtilities.UnescapeAll</c>. Used by
///  parity tests in <see cref="EscapingUtilitiesOracleTests"/> to confirm touki's
///  <see cref="MSBuildSpecification.Unescape"/> matches MSBuild's behavior for valid and invalid
///  <c>%XX</c> escape sequences.
/// </summary>
public static class EscapingUtilitiesWrapper
{
    private static readonly MethodInfo s_unescapeAllMethod = ResolveUnescapeAll();

    private static MethodInfo ResolveUnescapeAll()
    {
        Type escapingUtilities = typeof(MSBuildGlob).Assembly.GetType("Microsoft.Build.Shared.EscapingUtilities")
            ?? throw new InvalidOperationException("Could not find EscapingUtilities type.");

        return escapingUtilities.GetMethod(
            "UnescapeAll",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            types: [typeof(string), typeof(bool)],
            modifiers: null)
            ?? throw new InvalidOperationException("Could not find UnescapeAll(string, bool) method.");
    }

    /// <summary>
    ///  Calls MSBuild's <c>EscapingUtilities.UnescapeAll(escaped, trim: false)</c> and returns the
    ///  result.
    /// </summary>
    public static string UnescapeAll(string escaped)
    {
        ArgumentNullException.ThrowIfNull(escaped);
        try
        {
            return (string)s_unescapeAllMethod.Invoke(null, [escaped, false])!;
        }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            throw tie.InnerException;
        }
    }
}
