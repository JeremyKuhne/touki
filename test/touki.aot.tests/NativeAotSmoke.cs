// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace Touki.Resources;

/// <summary>
///  Verifies localized resource lookup from the published Native AOT executable.
/// </summary>
internal static class NativeAotSmoke
{
    /// <summary>
    ///  Returns success when the selected source returns the expected localized greeting.
    /// </summary>
    /// <param name="args">The source mode and expected greeting.</param>
    /// <returns>Zero when lookup returns the expected value; otherwise one.</returns>
    private static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("Expected a source mode and the expected greeting.");
            return 1;
        }

        Assembly assembly = typeof(NativeAotSmoke).Assembly;
        string? baseName = null;
        foreach (string name in assembly.GetManifestResourceNames())
        {
            if (name.EndsWith("SatelliteTestStrings.resources", StringComparison.Ordinal))
            {
                baseName = name[..^".resources".Length];
                break;
            }
        }

        if (baseName is null)
        {
            Console.Error.WriteLine("The neutral resource was not embedded.");
            return 1;
        }

        SatelliteStringResourceManager manager;
        if (string.Equals(args[0], "satellite", StringComparison.Ordinal))
        {
            manager = SatelliteStringResourceManager.FromSatelliteDirectory(
                baseName,
                AppContext.BaseDirectory,
                assembly);
        }
        else if (string.Equals(args[0], "resources", StringComparison.Ordinal))
        {
            manager = SatelliteStringResourceManager.FromResourcesDirectory(
                baseName,
                Path.Combine(AppContext.BaseDirectory, "loose"),
                assembly);
        }
        else
        {
            Console.Error.WriteLine($"Unknown source mode '{args[0]}'.");
            return 1;
        }

        string? value = manager.GetString("Greeting", new CultureInfo("de"));
        if (!string.Equals(value, args[1], StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"Expected '{args[1]}', but resource lookup returned '{value}'.");
            return 1;
        }

        return 0;
    }
}