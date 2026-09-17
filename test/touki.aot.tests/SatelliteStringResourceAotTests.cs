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
///  End-to-end proof that <see cref="SatelliteStringResourceManager"/> loads localized strings from
///  loose <c>.resources</c> files when this assembly is published with Native AOT. Stock
///  <see cref="ResourceManager"/> cannot do this because the AOT runtime has no satellite
///  assembly loader; see docs/aot-localized-resources-plan.md.
/// </summary>
/// <remarks>
///  <para>
///   Assertions use MSTest's <see cref="Assert"/> rather than a reflection-based assertion
///   library so the published binary stays trim-clean. The side files are produced at runtime
///   with <see cref="ResourceWriter"/> (its string path is AOT-safe) and read back through the
///   manager, exercising both the writer and reader under Native AOT.
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
    public void GetString_LocalizedSideFile_LoadsUnderAot()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        WriteSideFile(folder.TempPath, "de", baseName, "Greeting", "Hallo");

        SatelliteStringResourceManager manager = new(baseName, s_assembly, folder.TempPath);

        Assert.AreEqual("Hallo", manager.GetString("Greeting", new CultureInfo("de")));
    }

    [TestMethod]
    public void GetString_ParentCultureWalk_LoadsUnderAot()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        WriteSideFile(folder.TempPath, "de", baseName, "Greeting", "Hallo");

        SatelliteStringResourceManager manager = new(baseName, s_assembly, folder.TempPath);

        // de-DE has no side file; resolution must walk up to the "de" file.
        Assert.AreEqual("Hallo", manager.GetString("Greeting", new CultureInfo("de-DE")));
    }

    [TestMethod]
    public void GetString_NoSideFile_FallsBackToEmbeddedNeutralUnderAot()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();

        SatelliteStringResourceManager manager = new(baseName, s_assembly, folder.TempPath);

        Assert.AreEqual("Hello", manager.GetString("Greeting", new CultureInfo("de")));
    }

    [TestMethod]
    public void GetString_MissingKeyInSideFile_FallsBackToEmbeddedNeutralUnderAot()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        WriteSideFile(folder.TempPath, "de", baseName, "Farewell", "Tschuss");

        SatelliteStringResourceManager manager = new(baseName, s_assembly, folder.TempPath);

        Assert.AreEqual("Tschuss", manager.GetString("Farewell", new CultureInfo("de")));
        Assert.AreEqual("Hello", manager.GetString("Greeting", new CultureInfo("de")));
    }
}
