// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Resources;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Touki.Io;

namespace Touki.Resources;

/// <summary>
///  Managed tests for <see cref="SatelliteStringResourceManager"/> behavior in the AOT validation
///  project. Stock <see cref="ResourceManager"/> cannot load sidecar satellite assemblies under
///  Native AOT; the published executable path is covered by <c>NativeAotSmoke</c>.
/// </summary>
/// <remarks>
///  <para>
///   These methods run under the managed MSTest host and are rooted for Native AOT compilation.
///   Assertions use MSTest's <see cref="Assert"/> rather than a reflection-based assertion library
///   so the project remains trim-clean. <c>NativeAotSmoke</c> executes the localized reader
///   directly from the published native binary.
///  </para>
/// </remarks>
[TestClass]
public class SatelliteStringResourceAotTests
{
    private static readonly Assembly s_assembly = typeof(SatelliteStringResourceAotTests).Assembly;

    // Neutral resources are embedded from Resources/SatelliteTestStrings.resx. Discover the actual
    // manifest name at runtime so the test does not depend on root-namespace derivation.
    private static string NeutralBaseName()
    {
        foreach (string name in s_assembly.GetManifestResourceNames())
        {
            if (name.EndsWith("SatelliteTestStrings.resources", StringComparison.Ordinal))
            {
                return name[..^".resources".Length];
            }
        }

        throw new InvalidOperationException("The neutral test resources were not embedded.");
    }

    private static void WriteSideFile(string probeRoot, string culture, string baseName, string key, string value)
    {
        string directory = Path.Combine(probeRoot, culture);
        Directory.CreateDirectory(directory);
        using ResourceWriter writer = new(Path.Combine(directory, $"{baseName}.resources"));
        writer.AddResource(key, value);
        writer.Generate();
    }

    [TestMethod]
    public void GetString_LocalizedSideFile_LoadsInManagedAotProject()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        WriteSideFile(folder.TempPath, "de", baseName, "Greeting", "Hallo");

        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromResourcesDirectory(
            baseName,
            folder.TempPath,
            s_assembly);

        Assert.AreEqual("Hallo", manager.GetString("Greeting", new CultureInfo("de")));
    }

    [TestMethod]
    public void GetString_RuntimeSatellite_LoadsInManagedAotProject()
    {
        string baseName = NeutralBaseName();
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromRuntimeSatellites(
            baseName,
            s_assembly);

        Assert.AreEqual("Hallo", manager.GetString("Greeting", new CultureInfo("de")));
    }

    [TestMethod]
    public void GetString_ParentCultureWalk_LoadsInManagedAotProject()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        WriteSideFile(folder.TempPath, "de", baseName, "Greeting", "Hallo");

        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromResourcesDirectory(
            baseName,
            folder.TempPath,
            s_assembly);

        // de-DE has no side file; resolution must walk up to the "de" file.
        Assert.AreEqual("Hallo", manager.GetString("Greeting", new CultureInfo("de-DE")));
    }

    [TestMethod]
    public void GetString_NoSideFile_FallsBackToEmbeddedNeutralInManagedAotProject()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();

        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromResourcesDirectory(
            baseName,
            folder.TempPath,
            s_assembly);

        Assert.AreEqual("Hello", manager.GetString("Greeting", new CultureInfo("de")));
    }

    [TestMethod]
    public void GetString_MissingKeyInSideFile_FallsBackToEmbeddedNeutralInManagedAotProject()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        WriteSideFile(folder.TempPath, "de", baseName, "Farewell", "Tschuss");

        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromResourcesDirectory(
            baseName,
            folder.TempPath,
            s_assembly);

        Assert.AreEqual("Tschuss", manager.GetString("Farewell", new CultureInfo("de")));
        Assert.AreEqual("Hello", manager.GetString("Greeting", new CultureInfo("de")));
    }
}
