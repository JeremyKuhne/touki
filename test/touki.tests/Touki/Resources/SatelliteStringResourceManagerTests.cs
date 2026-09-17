// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Resources.Extensions;

namespace Touki.Resources;

[TestClass]
public class SatelliteStringResourceManagerTests
{
    private static readonly Assembly s_assembly = typeof(SatelliteStringResourceManagerTests).Assembly;

    // The neutral resources are embedded from SatelliteTestStrings.resx. Discover the actual manifest
    // name so the test does not depend on the assembly's root-namespace derivation.
    private static string NeutralBaseName()
    {
        string resourceName = s_assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("SatelliteTestStrings.resources", StringComparison.Ordinal));
        return resourceName[..^".resources".Length];
    }

    private static void WriteSideFile(
        string probeRoot,
        string culture,
        string baseName,
        params (string Key, string Value)[] entries)
    {
        string directory = Path.Combine(probeRoot, culture);
        Directory.CreateDirectory(directory);
        using ResourceWriter writer = new(Path.Combine(directory, $"{baseName}.resources"));
        foreach ((string key, string value) in entries)
        {
            writer.AddResource(key, value);
        }

        writer.Generate();
    }

    // Writes a default-format file mixing a string with an intrinsic non-string (an int).
    private static void WriteMixedSideFile(string probeRoot, string culture, string baseName)
    {
        string directory = Path.Combine(probeRoot, culture);
        Directory.CreateDirectory(directory);
        using ResourceWriter writer = new(Path.Combine(directory, $"{baseName}.resources"));
        writer.AddResource("Greeting", "Hallo");
        writer.AddResource("Count", 42);
        writer.Generate();
    }

    // Writes a file via PreserializedResourceWriter containing a resource whose value would require a
    // TypeConverter (reflection) to materialize, which switches the file to the non-default reader type.
    private static void WriteReflectionSideFile(string probeRoot, string culture, string baseName)
    {
        string directory = Path.Combine(probeRoot, culture);
        Directory.CreateDirectory(directory);
        using PreserializedResourceWriter writer = new(Path.Combine(directory, $"{baseName}.resources"));
        writer.AddResource("Greeting", "Hallo");
        writer.AddResource("Fancy", "10,20", "System.Drawing.Point, System.Drawing.Primitives");
        writer.Generate();
    }

    [TestMethod]
    public void GetString_CultureWithSideFile_ReturnsLocalizedValue()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        WriteSideFile(folder.TempPath, "de", baseName, ("Greeting", "Hallo"));

        SatelliteStringResourceManager manager = new(baseName, s_assembly, folder.TempPath);

        manager.GetString("Greeting", new CultureInfo("de")).Should().Be("Hallo");
    }

    [TestMethod]
    public void GetString_SpecificCulture_FallsBackToParentSideFile()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        WriteSideFile(folder.TempPath, "de", baseName, ("Greeting", "Hallo"));

        SatelliteStringResourceManager manager = new(baseName, s_assembly, folder.TempPath);

        // de-DE has no side file; resolution must walk up to the "de" file.
        manager.GetString("Greeting", new CultureInfo("de-DE")).Should().Be("Hallo");
    }

    [TestMethod]
    public void GetString_SpecificCultureOverridesParent()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        WriteSideFile(folder.TempPath, "de", baseName, ("Greeting", "Hallo"), ("Farewell", "Tschuss"));
        WriteSideFile(folder.TempPath, "de-DE", baseName, ("Greeting", "Gruezi"));

        SatelliteStringResourceManager manager = new(baseName, s_assembly, folder.TempPath);

        // de-DE overrides the "de" value for a shared key...
        manager.GetString("Greeting", new CultureInfo("de-DE")).Should().Be("Gruezi");
        // ...and inherits keys it does not itself supply.
        manager.GetString("Farewell", new CultureInfo("de-DE")).Should().Be("Tschuss");
    }

    [TestMethod]
    public void GetString_NoSideFile_ReturnsEmbeddedNeutral()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();

        SatelliteStringResourceManager manager = new(baseName, s_assembly, folder.TempPath);

        manager.GetString("Greeting", new CultureInfo("de")).Should().Be("Hello");
    }

    [TestMethod]
    public void GetString_MissingKeyInSideFile_ReturnsEmbeddedNeutral()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        WriteSideFile(folder.TempPath, "de", baseName, ("Farewell", "Tschuss"));

        SatelliteStringResourceManager manager = new(baseName, s_assembly, folder.TempPath);

        // Present in the side file.
        manager.GetString("Farewell", new CultureInfo("de")).Should().Be("Tschuss");

        // Absent from the side file; falls back to the embedded neutral value.
        manager.GetString("Greeting", new CultureInfo("de")).Should().Be("Hello");
    }

    [TestMethod]
    public void GetString_InvariantCulture_ReturnsEmbeddedNeutral()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        WriteSideFile(folder.TempPath, "de", baseName, ("Greeting", "Hallo"));

        SatelliteStringResourceManager manager = new(baseName, s_assembly, folder.TempPath);

        manager.GetString("Greeting", CultureInfo.InvariantCulture).Should().Be("Hello");
    }

    [TestMethod]
    public void GetString_NeutralCulture_SkipsSideFileAndUsesEmbedded()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();

        // The assembly's neutral culture is en-US (see NeutralLanguage in touki.tests.csproj). A side
        // file for the neutral culture must be ignored: the neutral resources are embedded, so no
        // probing happens for it.
        WriteSideFile(folder.TempPath, "en-US", baseName, ("Greeting", "SHOULD_BE_IGNORED"));

        SatelliteStringResourceManager manager = new(baseName, s_assembly, folder.TempPath);

        manager.GetString("Greeting", new CultureInfo("en-US")).Should().Be("Hello");
    }

    [TestMethod]
    public void GetString_CorruptSideFile_ReturnsEmbeddedNeutral()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        string directory = Path.Combine(folder.TempPath, "de");
        Directory.CreateDirectory(directory);
        System.IO.File.WriteAllBytes(Path.Combine(directory, $"{baseName}.resources"), [0x00, 0x01, 0x02, 0x03]);

        SatelliteStringResourceManager manager = new(baseName, s_assembly, folder.TempPath);

        // A corrupt side file is treated as absent, so the key resolves to the embedded neutral value.
        manager.GetString("Greeting", new CultureInfo("de")).Should().Be("Hello");
    }

    [TestMethod]
    public void GetString_BuiltInNonStringResource_IsSkipped()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        WriteMixedSideFile(folder.TempPath, "de", baseName);

        SatelliteStringResourceManager manager = new(baseName, s_assembly, folder.TempPath);

        // The string entry still loads despite the non-string sibling...
        manager.GetString("Greeting", new CultureInfo("de")).Should().Be("Hallo");
        // ...and the non-string (int) entry is ignored, so it falls back to neutral (absent -> null).
        manager.GetString("Count", new CultureInfo("de")).Should().BeNull();
    }

    [TestMethod]
    public void GetString_ExtensionsFormatSideFile_ReadsStringsSkipsUserTypes()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        WriteReflectionSideFile(folder.TempPath, "de", baseName);

        SatelliteStringResourceManager manager = new(baseName, s_assembly, folder.TempPath);

        // The file was written by PreserializedResourceWriter (Extensions format). RawResourceReader
        // parses it structurally: the intrinsic string entry is read...
        manager.GetString("Greeting", new CultureInfo("de")).Should().Be("Hallo");
        // ...while the serialized user-type entry is skipped by its type code alone - never
        // deserialized - so it falls back to the embedded neutral resources (absent -> null).
        manager.GetString("Fancy", new CultureInfo("de")).Should().BeNull();
    }

    [TestMethod]
    public void GetString_TableIsCached_SurvivesFileDeletion()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        WriteSideFile(folder.TempPath, "de", baseName, ("Greeting", "Hallo"));

        SatelliteStringResourceManager manager = new(baseName, s_assembly, folder.TempPath);
        CultureInfo german = new("de");

        manager.GetString("Greeting", german).Should().Be("Hallo");

        // Delete the backing file; the cached table must still answer for the same culture.
        System.IO.File.Delete(Path.Combine(folder.TempPath, "de", $"{baseName}.resources"));

        manager.GetString("Greeting", german).Should().Be("Hallo");
    }

    [TestMethod]
    public void GetString_CultureChange_RebuildsCache()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        WriteSideFile(folder.TempPath, "de", baseName, ("Greeting", "Hallo"));
        WriteSideFile(folder.TempPath, "fr", baseName, ("Greeting", "Bonjour"));

        SatelliteStringResourceManager manager = new(baseName, s_assembly, folder.TempPath);

        manager.GetString("Greeting", new CultureInfo("de")).Should().Be("Hallo");
        // Switching culture must rebuild the single-entry cache...
        manager.GetString("Greeting", new CultureInfo("fr")).Should().Be("Bonjour");
        // ...and switching back rebuilds it again.
        manager.GetString("Greeting", new CultureInfo("de")).Should().Be("Hallo");
    }

    [TestMethod]
    public void ProbeRoot_ReflectsConstructorArgument()
    {
        using TempFolder folder = new();
        SatelliteStringResourceManager manager = new(NeutralBaseName(), s_assembly, folder.TempPath);
        manager.ProbeRoot.Should().Be(folder.TempPath);
    }
}
