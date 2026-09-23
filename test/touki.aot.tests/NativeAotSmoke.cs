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
    private const string ExternalBaseName = "Touki.Resources.SatelliteTestStrings";

    /// <summary>
    ///  Returns success when the selected source returns the expected localized greeting.
    /// </summary>
    /// <param name="args">The source mode, source arguments, culture, and expected greeting.</param>
    /// <returns>Zero when lookup returns the expected value; otherwise one.</returns>
    private static int Main(string[] args)
    {
        if (args.Length == 1 && string.Equals(args[0], "default", StringComparison.Ordinal))
        {
            if (GeneratedSatelliteTestStrings.ResourceManager is not SatelliteStringResourceManager)
            {
                Console.Error.WriteLine("The default provider did not create a runtime-satellite manager.");
                return 1;
            }

            return 0;
        }

        if (args.Length < 3)
        {
            Console.Error.WriteLine("Expected a source mode, culture, and expected greeting.");
            return 1;
        }

        string cultureName;
        string expected;
        if (string.Equals(args[0], "assemblies", StringComparison.Ordinal))
        {
            if (args.Length != 6)
            {
                Console.Error.WriteLine(
                    "Assembly mode expects an owner assembly, satellite root, external name, culture, and expected greeting.");

                return 1;
            }

            string resourceAssemblyFile = args[1];
            string satelliteDirectory = args[2];
            string externalOwnerSimpleName = args[3];
            Assembly ownerAssembly = typeof(NativeAotSmoke).Assembly;
            StringResourceManager.ValidateAssemblyFile(
                ExternalBaseName,
                resourceAssemblyFile,
                ownerAssembly,
                externalOwnerSimpleName);

            StringResourceManagerProvider.Register(
                (baseName, generatedOwnerAssembly) => SatelliteStringResourceManager.FromAssemblyFiles(
                    baseName,
                    resourceAssemblyFile,
                    satelliteDirectory,
                    generatedOwnerAssembly,
                    externalOwnerSimpleName,
                    StringResourceManagerOptions.ValidateAssemblyIdentity,
                    SatelliteStringResourceProbeMode.FallbackOnFailure));

            cultureName = args[4];
            expected = args[5];
        }
        else if (string.Equals(args[0], "satellite", StringComparison.Ordinal))
        {
            if (args.Length != 4)
            {
                Console.Error.WriteLine(
                    "Satellite mode expects an external owner name, culture, and expected greeting.");

                return 1;
            }

            RegisterResourceManager(args[0], args[1]);
            cultureName = args[2];
            expected = args[3];
        }
        else
        {
            if (args.Length != 3)
            {
                Console.Error.WriteLine("The selected mode expects a culture and expected greeting.");
                return 1;
            }

            RegisterResourceManager(args[0], externalOwnerSimpleName: null);
            cultureName = args[1];
            expected = args[2];
        }

        GeneratedSatelliteTestStrings.Culture = new CultureInfo(cultureName);
        string? value = GeneratedSatelliteTestStrings.Greeting;
        if (!string.Equals(value, expected, StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"Expected '{expected}', but resource lookup returned '{value}'.");
            return 1;
        }

        return 0;
    }

    private static void RegisterResourceManager(
        string sourceMode,
        string? externalOwnerSimpleName)
    {
        if (string.Equals(sourceMode, "embedded", StringComparison.Ordinal))
        {
            StringResourceManagerProvider.RegisterEmbedded();
            return;
        }

        if (string.Equals(sourceMode, "satellite", StringComparison.Ordinal))
        {
            string simpleName = externalOwnerSimpleName
                ?? throw new InvalidOperationException("The external owner simple name was not initialized.");

            StringResourceManagerProvider.Register(
                (registeredBaseName, ownerAssembly) =>
                    SatelliteStringResourceManager.FromSatelliteDirectory(
                        registeredBaseName,
                        Path.Join(AppContext.BaseDirectory, "external-satellites"),
                        ownerAssembly,
                        simpleName,
                        StringResourceManagerOptions.ValidateAssemblyIdentity,
                        SatelliteStringResourceProbeMode.FallbackOnFailure));

            return;
        }

        if (string.Equals(sourceMode, "resources", StringComparison.Ordinal))
        {
            StringResourceManagerProvider.Register(
                (registeredBaseName, ownerAssembly) =>
                    SatelliteStringResourceManager.FromResourcesDirectory(
                        registeredBaseName,
                        Path.Join(AppContext.BaseDirectory, "loose"),
                        ownerAssembly));

            return;
        }

        throw new ArgumentException($"Unknown source mode '{sourceMode}'.", nameof(sourceMode));
    }
}