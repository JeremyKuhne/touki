// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Resources;
using System.Resources.Extensions;

namespace Touki.Resources;

[TestClass]
public partial class SatelliteStringResourceManagerTests
{
    private static readonly Assembly s_assembly = typeof(SatelliteStringResourceManagerTests).Assembly;

    private sealed class OverrideStringResourceManager : StringResourceManager
    {
        internal OverrideStringResourceManager()
            : base("Unused", () => throw new InvalidOperationException("The overridden manager loaded its source."))
        {
        }

        internal CultureInfo? RequestedCulture { get; private set; }

        public override string? GetString(string name, CultureInfo? culture)
        {
            RequestedCulture = culture;
            return name == "Greeting" ? "Override" : null;
        }
    }

    private sealed class NamedCultureInfo : CultureInfo
    {
        private readonly string _name;

        internal NamedCultureInfo(string name)
            : base("de")
        {
            _name = name;
        }

        public override string Name => _name;
    }

    // The neutral resources are embedded from SatelliteTestStrings.resx. Discover the actual manifest
    // name so the test does not depend on the assembly's root-namespace derivation.
    private static string NeutralBaseName()
    {
        string resourceName = s_assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("SatelliteTestStrings.resources", StringComparison.Ordinal));

        return resourceName[..^".resources".Length];
    }

    private static string SatelliteAssemblyFileName()
    {
        string assemblyName = s_assembly.GetName().Name
            ?? throw new InvalidOperationException("The test assembly must have a name.");

        return $"{assemblyName}.resources.dll";
    }

    private static string CopySatelliteAssembly(string probeRoot, string culture) =>
        CopySatelliteAssembly(probeRoot, culture, culture);

    private static string CopySatelliteAssembly(
        string probeRoot,
        string sourceCulture,
        string destinationCulture) =>
            CopySatelliteAssembly(
                probeRoot,
                sourceCulture,
                destinationCulture,
                SatelliteAssemblyFileName());

    private static string CopySatelliteAssembly(
        string probeRoot,
        string sourceCulture,
        string destinationCulture,
        string destinationFileName)
    {
        string directory = Path.Join(probeRoot, destinationCulture);
        Directory.CreateDirectory(directory);

        string source = Path.Join(AppContext.BaseDirectory, sourceCulture, SatelliteAssemblyFileName());
        string destination = Path.Join(directory, destinationFileName);
        System.IO.File.Copy(source, destination);
        return destination;
    }

    private static string CopyResourceAssembly(string directory)
    {
        string destination = Path.Join(directory, "external-owner.dll");
        System.IO.File.Copy(s_assembly.Location, destination);
        return destination;
    }

    private static void WriteSideFile(
        string probeRoot,
        string culture,
        string baseName,
        params (string Key, object? Value)[] entries)
    {
        string directory = Path.Join(probeRoot, culture);
        Directory.CreateDirectory(directory);
        using ResourceWriter writer = new(Path.Join(directory, $"{baseName}.resources"));
        foreach ((string key, object? value) in entries)
        {
            writer.AddResource(key, value);
        }

        writer.Generate();
    }

    // Writes a default-format file mixing a string with an intrinsic non-string (an int).
    private static void WriteMixedSideFile(string probeRoot, string culture, string baseName)
    {
        string directory = Path.Join(probeRoot, culture);
        Directory.CreateDirectory(directory);
        using ResourceWriter writer = new(Path.Join(directory, $"{baseName}.resources"));
        writer.AddResource("Greeting", "Hallo");
        writer.AddResource("Count", 42);
        writer.Generate();
    }

    // Writes a file via PreserializedResourceWriter containing a resource whose value would require a
    // TypeConverter (reflection) to materialize, which switches the file to the non-default reader type.
    private static void WriteReflectionSideFile(string probeRoot, string culture, string baseName)
    {
        string directory = Path.Join(probeRoot, culture);
        Directory.CreateDirectory(directory);
        using PreserializedResourceWriter writer = new(Path.Join(directory, $"{baseName}.resources"));
        writer.AddResource("Greeting", "Hallo");
        writer.AddResource("Fancy", "10,20", "System.Drawing.Point, System.Drawing.Primitives");
        writer.Generate();
    }

    [TestMethod]
    public void Constructor_ExplicitResourcesDirectory_ReturnsLocalizedValue()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        WriteSideFile(folder.TempPath, "de", baseName, ("Greeting", "Hallo"));

        SatelliteStringResourceManager manager = new(baseName, s_assembly, folder.TempPath);

        manager.GetString("Greeting", new CultureInfo("de")).Should().Be("Hallo");
    }

    [TestMethod]
    public void FromResourcesDirectory_RootIsRelative_CapturesFullPath()
    {
        string relativeRoot = $"relative-{Guid.NewGuid():N}";
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromResourcesDirectory(
            NeutralBaseName(),
            relativeRoot,
            s_assembly);

        string localizedRoot = manager.TestAccessor.Dynamic._localizedRoot;
        localizedRoot.Should().Be(Path.GetFullPath(relativeRoot));
    }

    [TestMethod]
    public void FromResourcesDirectory_BaseNameContainsForwardSlash_ThrowsArgumentException()
    {
        Action action = () => SatelliteStringResourceManager.FromResourcesDirectory(
            "Namespace/Strings",
            Environment.CurrentDirectory,
            s_assembly);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("baseName");
    }

    [TestMethod]
    public void FromSatelliteDirectory_BaseNameContainsBackslash_ThrowsArgumentException()
    {
        Action action = () => SatelliteStringResourceManager.FromSatelliteDirectory(
            "Namespace\\Strings",
            Environment.CurrentDirectory,
            s_assembly);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("baseName");
    }

    [TestMethod]
    public void GetString_CultureNameEscapesRoot_ThrowsArgumentException()
    {
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromResourcesDirectory(
            NeutralBaseName(),
            Environment.CurrentDirectory,
            s_assembly);

        Action action = () => manager.GetString("Greeting", new NamedCultureInfo(".."));

        action.Should().Throw<ArgumentException>()
            .WithParameterName("culture");
    }

    [TestMethod]
    public void GetString_CultureWithSideFile_ReturnsLocalizedValue()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        WriteSideFile(folder.TempPath, "de", baseName, ("Greeting", "Hallo"));

        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromResourcesDirectory(
            baseName,
            folder.TempPath,
            s_assembly);

        manager.GetString("Greeting", new CultureInfo("de")).Should().Be("Hallo");
    }

    [TestMethod]
    public void GetString_CultureWithSatelliteAssembly_ReturnsLocalizedValue()
    {
        string baseName = NeutralBaseName();
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromRuntimeSatellites(
            baseName,
            s_assembly);

        manager.GetString("Greeting", new CultureInfo("de")).Should().Be("Hallo");
    }

    [TestMethod]
    public void GetString_CultureWithDirectSatelliteAssembly_ReturnsLocalizedValue()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        CopySatelliteAssembly(folder.TempPath, "de");
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromSatelliteDirectory(
            baseName,
            folder.TempPath,
            s_assembly);

        manager.GetString("Greeting", new CultureInfo("de")).Should().Be("Hallo");
    }

    [TestMethod]
    public void GetString_ExternalAssemblyFilesExactCulture_ReturnsLocalizedValue()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        string resourceAssemblyFile = CopyResourceAssembly(folder.TempPath);
        CopySatelliteAssembly(folder.TempPath, "de");
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromAssemblyFiles(
            baseName,
            resourceAssemblyFile,
            folder.TempPath);

        manager.GetString("Greeting", new CultureInfo("de")).Should().Be("Hallo");
    }

    [TestMethod]
    public void FromAssemblyFiles_AliasedSimpleNameMatches_CreatesManager()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        string resourceAssemblyFile = CopyResourceAssembly(folder.TempPath);
        string simpleName = s_assembly.GetName().Name
            ?? throw new InvalidOperationException("The test assembly does not have a simple name.");

        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromAssemblyFiles(
            baseName,
            resourceAssemblyFile,
            folder.TempPath,
            s_assembly,
            simpleName,
            StringResourceManagerOptions.ValidateAssemblyIdentity,
            SatelliteStringResourceProbeMode.FallbackOnFailure);

        manager.GetString("Greeting", CultureInfo.InvariantCulture).Should().Be("Hello");
    }

    [TestMethod]
    public void FromAssemblyFiles_AliasedSimpleNameDoesNotMatchByDefault_CreatesManager()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        string resourceAssemblyFile = CopyResourceAssembly(folder.TempPath);

        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromAssemblyFiles(
            baseName,
            resourceAssemblyFile,
            folder.TempPath,
            s_assembly,
            "WrongOwner",
            SatelliteStringResourceProbeMode.FallbackOnFailure);

        manager.GetString("Greeting", CultureInfo.InvariantCulture).Should().Be("Hello");
    }

    [TestMethod]
    public void FromAssemblyFiles_EmptyExternalOwnerSimpleName_ThrowsArgumentException()
    {
        Action action = () => SatelliteStringResourceManager.FromAssemblyFiles(
            NeutralBaseName(),
            s_assembly.Location,
            Environment.CurrentDirectory,
            s_assembly,
            string.Empty,
            SatelliteStringResourceProbeMode.Strict);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("externalOwnerSimpleName");
    }

    [TestMethod]
    public void FromAssemblyFiles_InvalidProbeMode_ThrowsArgumentOutOfRangeException()
    {
        SatelliteStringResourceProbeMode invalidMode = (SatelliteStringResourceProbeMode)(-1);

        Action action = () => SatelliteStringResourceManager.FromAssemblyFiles(
            NeutralBaseName(),
            s_assembly.Location,
            Environment.CurrentDirectory,
            invalidMode);

        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("probeMode");
    }

    [TestMethod]
    public void FromAssemblyFiles_ValidateAssemblyIdentityWithMismatchedAlias_ThrowsFileLoadException()
    {
        using TempFolder folder = new();
        string resourceAssemblyFile = CopyResourceAssembly(folder.TempPath);

        Action action = () => SatelliteStringResourceManager.FromAssemblyFiles(
            NeutralBaseName(),
            resourceAssemblyFile,
            folder.TempPath,
            s_assembly,
            "WrongOwner",
            StringResourceManagerOptions.ValidateAssemblyIdentity,
            SatelliteStringResourceProbeMode.FallbackOnFailure);

        action.Should().Throw<FileLoadException>();
    }

    [TestMethod]
    public void GetString_AliasedOwnerChangedBeforeFirstLookup_ThrowsFileLoadException()
    {
        using TempFolder folder = new();
        string resourceAssemblyFile = CopyResourceAssembly(folder.TempPath);
        string simpleName = s_assembly.GetName().Name
            ?? throw new InvalidOperationException("The test assembly does not have a simple name.");

        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromAssemblyFiles(
            NeutralBaseName(),
            resourceAssemblyFile,
            folder.TempPath,
            s_assembly,
            simpleName,
            StringResourceManagerOptions.ValidateAssemblyIdentity,
            SatelliteStringResourceProbeMode.Strict);

        System.IO.File.Copy(typeof(StringResourceManager).Assembly.Location, resourceAssemblyFile, overwrite: true);

        Action action = () => manager.GetString("Greeting", CultureInfo.InvariantCulture);

        action.Should().Throw<FileLoadException>();
    }

    [TestMethod]
    public void GetString_AliasedOwnerChangedAfterRelease_ThrowsFileLoadException()
    {
        using TempFolder folder = new();
        string resourceAssemblyFile = CopyResourceAssembly(folder.TempPath);
        string simpleName = s_assembly.GetName().Name
            ?? throw new InvalidOperationException("The test assembly does not have a simple name.");

        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromAssemblyFiles(
            NeutralBaseName(),
            resourceAssemblyFile,
            folder.TempPath,
            s_assembly,
            simpleName,
            StringResourceManagerOptions.ValidateAssemblyIdentity,
            SatelliteStringResourceProbeMode.Strict);

        manager.GetString("Greeting", CultureInfo.InvariantCulture).Should().Be("Hello");
        manager.ReleaseAllResources();
        System.IO.File.Copy(typeof(StringResourceManager).Assembly.Location, resourceAssemblyFile, overwrite: true);

        Action action = () => manager.GetString("Greeting", CultureInfo.InvariantCulture);

        action.Should().Throw<FileLoadException>();
    }

    [TestMethod]
    public void GetString_ExternalAssemblyFilesParentCulture_ReturnsParentValue()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        string resourceAssemblyFile = CopyResourceAssembly(folder.TempPath);
        CopySatelliteAssembly(folder.TempPath, "de");
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromAssemblyFiles(
            baseName,
            resourceAssemblyFile,
            folder.TempPath);

        manager.GetString("Greeting", new CultureInfo("de-DE")).Should().Be("Hallo");
    }

    [TestMethod]
    public void GetString_ExternalAssemblyFilesMissingCulture_ReturnsNeutralValue()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        string resourceAssemblyFile = CopyResourceAssembly(folder.TempPath);
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromAssemblyFiles(
            baseName,
            resourceAssemblyFile,
            folder.TempPath);

        manager.GetString("Greeting", new CultureInfo("fr-FR")).Should().Be("Hello");
    }

    [TestMethod]
    public void GetString_ExternalAssemblyFilesNeutralCulture_SkipsSatellite()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        string resourceAssemblyFile = CopyResourceAssembly(folder.TempPath);

        // The file has German identity, so probing it as en-US would fail identity validation.
        CopySatelliteAssembly(folder.TempPath, "de", "en-US");
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromAssemblyFiles(
            baseName,
            resourceAssemblyFile,
            folder.TempPath);

        manager.GetString("Greeting", new CultureInfo("en-US")).Should().Be("Hello");
    }

    [TestMethod]
    public void GetString_ExternalAssemblyFilesSatelliteCultureDoesNotMatch_ThrowsFileLoadException()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        string resourceAssemblyFile = CopyResourceAssembly(folder.TempPath);
        CopySatelliteAssembly(folder.TempPath, "de", "fr");
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromAssemblyFiles(
            baseName,
            resourceAssemblyFile,
            folder.TempPath,
            StringResourceManagerOptions.ValidateAssemblyIdentity,
            SatelliteStringResourceProbeMode.Strict);

        Action action = () => manager.GetString("Greeting", new CultureInfo("fr"));

        action.Should().Throw<FileLoadException>();
    }

    [TestMethod]
    public void GetString_ComposedLoadedAssemblyNeutralResources_ReturnsLocalizedValue()
    {
        string baseName = NeutralBaseName();
        StringResourceManager neutralResources = new(baseName, s_assembly);
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromRuntimeSatellites(
            baseName,
            s_assembly,
            neutralResources);

        manager.GetString("Greeting", new CultureInfo("de")).Should().Be("Hallo");
    }

    [TestMethod]
    public void GetString_SpecificCulture_FallsBackToParentSideFile()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        WriteSideFile(folder.TempPath, "de", baseName, ("Greeting", "Hallo"));

        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromResourcesDirectory(
            baseName,
            folder.TempPath,
            s_assembly);

        // de-DE has no side file; resolution must walk up to the "de" file.
        manager.GetString("Greeting", new CultureInfo("de-DE")).Should().Be("Hallo");
    }

    [TestMethod]
    public void GetString_SpecificCulture_FallsBackToParentSatelliteAssembly()
    {
        string baseName = NeutralBaseName();
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromRuntimeSatellites(
            baseName,
            s_assembly);

        manager.GetString("Greeting", new CultureInfo("de-DE")).Should().Be("Hallo");
    }

    [TestMethod]
    public void GetString_SpecificCulture_FallsBackToParentDirectSatelliteAssembly()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        CopySatelliteAssembly(folder.TempPath, "de");
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromSatelliteDirectory(
            baseName,
            folder.TempPath,
            s_assembly);

        manager.GetString("Greeting", new CultureInfo("de-DE")).Should().Be("Hallo");
    }

    [TestMethod]
    public void GetString_MissingRuntimeSatellite_ReturnsEmbeddedNeutral()
    {
        string baseName = NeutralBaseName();
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromRuntimeSatellites(
            baseName,
            s_assembly);

        manager.GetString("Greeting", new CultureInfo("fr-FR")).Should().Be("Hello");
    }

    [TestMethod]
    public void GetString_MissingDirectSatellite_ReturnsEmbeddedNeutral()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromSatelliteDirectory(
            baseName,
            folder.TempPath,
            s_assembly);

        manager.GetString("Greeting", new CultureInfo("fr-FR")).Should().Be("Hello");
    }

    [TestMethod]
    public void GetString_SpecificCultureOverridesParent()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        WriteSideFile(folder.TempPath, "de", baseName, ("Greeting", "Hallo"), ("Farewell", "Tschuss"));
        WriteSideFile(folder.TempPath, "de-DE", baseName, ("Greeting", "Gruezi"));

        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromResourcesDirectory(
            baseName,
            folder.TempPath,
            s_assembly);

        // de-DE overrides the "de" value for a shared key...
        manager.GetString("Greeting", new CultureInfo("de-DE")).Should().Be("Gruezi");
        // ...and inherits keys it does not itself supply.
        manager.GetString("Farewell", new CultureInfo("de-DE")).Should().Be("Tschuss");
    }

    [TestMethod]
    public void GetString_SpecificCultureValueIsNull_ThrowsBadImageFormatException()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        WriteSideFile(folder.TempPath, "de", baseName, ("Nullable", "Parent"));
        WriteSideFile(folder.TempPath, "de-DE", baseName, ("Nullable", null));
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromResourcesDirectory(
            baseName,
            folder.TempPath,
            s_assembly);

        Action action = () => manager.GetString("Nullable", new CultureInfo("de-DE"));

        action.Should().Throw<BadImageFormatException>();
    }

    [TestMethod]
    public void GetString_ResourcesDirectoryDoesNotMixSatelliteAssembly_UsesLooseFile()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        WriteSideFile(folder.TempPath, "de", baseName, ("Greeting", "Servus"));
        CopySatelliteAssembly(folder.TempPath, "de");

        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromResourcesDirectory(
            baseName,
            folder.TempPath,
            s_assembly);

        manager.GetString("Greeting", new CultureInfo("de")).Should().Be("Servus");
    }

    [TestMethod]
    public void IgnoreCase_Get_ReturnsFalse()
    {
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromRuntimeSatellites(
            NeutralBaseName(),
            s_assembly);

        manager.IgnoreCase.Should().BeFalse();
    }

    [TestMethod]
    public void IgnoreCase_SetFalse_ThrowsNotSupportedException()
    {
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromRuntimeSatellites(
            NeutralBaseName(),
            s_assembly);

        Action action = () => manager.IgnoreCase = false;

        action.Should().Throw<NotSupportedException>();
    }

    [TestMethod]
    public void IgnoreCase_SetTrue_ThrowsNotSupportedException()
    {
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromRuntimeSatellites(
            NeutralBaseName(),
            s_assembly);

        Action action = () => manager.IgnoreCase = true;

        action.Should().Throw<NotSupportedException>();
    }

    [TestMethod]
    public void GetString_NoSideFile_ReturnsEmbeddedNeutral()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();

        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromResourcesDirectory(
            baseName,
            folder.TempPath,
            s_assembly);

        manager.GetString("Greeting", new CultureInfo("de")).Should().Be("Hello");
    }

    [TestMethod]
    public void GetString_ComposedResourcesFileNeutralResources_ReturnsNeutralValue()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        string neutralPath = Path.Join(folder.TempPath, "Neutral.resources");
        using (ResourceWriter writer = new(neutralPath))
        {
            writer.AddResource("Greeting", "Neutral File");
            writer.Generate();
        }

        StringResourceManager neutralResources = new(neutralPath);
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromResourcesDirectory(
            baseName,
            folder.TempPath,
            neutralResources);

        manager.GetString("Greeting", new CultureInfo("de")).Should().Be("Neutral File");
    }

    [TestMethod]
    public void GetString_CallerOwnedNeutralOverride_InvokesVirtualMethodWithCulture()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        OverrideStringResourceManager neutralResources = new();
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromResourcesDirectory(
            baseName,
            folder.TempPath,
            neutralResources);

        CultureInfo culture = new("de-DE");

        manager.GetString("Greeting", culture).Should().Be("Override");
        neutralResources.RequestedCulture.Should().BeSameAs(culture);
    }

    [TestMethod]
    public void GetString_MissingKeyInSideFile_ReturnsEmbeddedNeutral()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        WriteSideFile(folder.TempPath, "de", baseName, ("Farewell", "Tschuss"));

        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromResourcesDirectory(
            baseName,
            folder.TempPath,
            s_assembly);

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

        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromResourcesDirectory(
            baseName,
            folder.TempPath,
            s_assembly);

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

        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromResourcesDirectory(
            baseName,
            folder.TempPath,
            s_assembly);

        manager.GetString("Greeting", new CultureInfo("en-US")).Should().Be("Hello");
    }

    [TestMethod]
    public void GetString_NeutralResourcesStoredInSatellite_ThrowsNotSupportedException()
    {
        AssemblyName assemblyName = new($"SatelliteNeutral-{Guid.NewGuid():N}");
        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        ConstructorInfo constructor = typeof(NeutralResourcesLanguageAttribute).GetConstructor(
            [typeof(string), typeof(UltimateResourceFallbackLocation)])
            ?? throw new InvalidOperationException("The neutral resources attribute constructor is missing.");

        CustomAttributeBuilder attribute = new(
            constructor,
            ["en-US", UltimateResourceFallbackLocation.Satellite]);

        assembly.SetCustomAttribute(attribute);
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromRuntimeSatellites(
            "Strings",
            assembly);

        Action action = () => manager.GetString("Greeting", new CultureInfo("de"));

        action.Should().Throw<NotSupportedException>();
    }

    [TestMethod]
    public void GetString_InvalidSatelliteContractVersion_RuntimeSatellitesThrowsArgumentException()
    {
        AssemblyName assemblyName = new($"InvalidContract-{Guid.NewGuid():N}");
        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        ConstructorInfo constructor = typeof(SatelliteContractVersionAttribute).GetConstructor([typeof(string)])
            ?? throw new InvalidOperationException("The satellite contract attribute constructor is missing.");

        assembly.SetCustomAttribute(new CustomAttributeBuilder(constructor, ["invalid"]));
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromRuntimeSatellites(
            "Strings",
            assembly);

        Action action = () => manager.GetString("Greeting", new CultureInfo("de"));

        action.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void GetString_InvalidSatelliteContractVersion_ResourcesDirectoryIgnoresAttribute()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        WriteSideFile(folder.TempPath, "de", baseName, ("Greeting", "Hallo"));
        AssemblyName assemblyName = new($"InvalidContract-{Guid.NewGuid():N}");
        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        ConstructorInfo constructor = typeof(SatelliteContractVersionAttribute).GetConstructor([typeof(string)])
            ?? throw new InvalidOperationException("The satellite contract attribute constructor is missing.");

        assembly.SetCustomAttribute(new CustomAttributeBuilder(constructor, ["invalid"]));
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromResourcesDirectory(
            baseName,
            folder.TempPath,
            assembly);

        manager.GetString("Greeting", new CultureInfo("de")).Should().Be("Hallo");
    }

    [TestMethod]
    public void GetString_CorruptSideFile_ThrowsArgumentException()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        string directory = Path.Join(folder.TempPath, "de");
        Directory.CreateDirectory(directory);
        System.IO.File.WriteAllBytes(Path.Join(directory, $"{baseName}.resources"), [0x00, 0x01, 0x02, 0x03]);

        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromResourcesDirectory(
            baseName,
            folder.TempPath,
            s_assembly);

        Action action = () => manager.GetString("Greeting", new CultureInfo("de"));

        action.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void GetString_CorruptSideFile_CachesFailureUntilRelease()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        string directory = Path.Join(folder.TempPath, "de");
        Directory.CreateDirectory(directory);
        System.IO.File.WriteAllBytes(
            Path.Join(directory, $"{baseName}.resources"),
            [0x00, 0x01, 0x02, 0x03]);

        int openCount = 0;

        MappedMemoryManager OpenFile(string path)
        {
            openCount++;
            return MappedMemoryManager.CreateFromFile(path);
        }

        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromResourcesDirectory(
            baseName,
            folder.TempPath,
            s_assembly,
            OpenFile);

        Action action = () => manager.GetString("Greeting", new CultureInfo("de"));

        action.Should().Throw<ArgumentException>();
        action.Should().Throw<ArgumentException>();
        openCount.Should().Be(1);

        manager.ReleaseAllResources();

        action.Should().Throw<ArgumentException>();
        openCount.Should().Be(2);
    }

    [TestMethod]
    public void GetString_ResourcesDirectoryDoesNotHideCorruptLooseResource_ThrowsArgumentException()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        string directory = Path.Join(folder.TempPath, "de");
        Directory.CreateDirectory(directory);
        System.IO.File.WriteAllBytes(Path.Join(directory, $"{baseName}.resources"), [0x00, 0x01, 0x02, 0x03]);
        CopySatelliteAssembly(folder.TempPath, "de");

        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromResourcesDirectory(
            baseName,
            folder.TempPath,
            s_assembly);

        Action action = () => manager.GetString("Greeting", new CultureInfo("de"));

        action.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void GetString_CorruptSatelliteAssembly_ThrowsBadImageFormatException()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        string directory = Path.Join(folder.TempPath, "de");
        Directory.CreateDirectory(directory);
        System.IO.File.WriteAllBytes(Path.Join(directory, SatelliteAssemblyFileName()), [0x00, 0x01, 0x02, 0x03]);

        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromSatelliteDirectory(
            baseName,
            folder.TempPath,
            s_assembly);

        Action action = () => manager.GetString("Greeting", new CultureInfo("de"));

        action.Should().Throw<BadImageFormatException>();
    }

    [TestMethod]
    public void GetString_TolerantMalformedSatellite_FallsBackToParent()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        CopySatelliteAssembly(folder.TempPath, "de");
        string directory = Path.Join(folder.TempPath, "de-DE");
        Directory.CreateDirectory(directory);
        System.IO.File.WriteAllBytes(
            Path.Join(directory, SatelliteAssemblyFileName()),
            [0x00, 0x01, 0x02, 0x03]);

        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromSatelliteDirectory(
            baseName,
            folder.TempPath,
            s_assembly,
            SatelliteStringResourceProbeMode.FallbackOnFailure);

        manager.GetString("Greeting", new CultureInfo("de-DE")).Should().Be("Hallo");
    }

    [TestMethod]
    public void GetString_TolerantMissingSatellite_FallsBackToParent()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        CopySatelliteAssembly(folder.TempPath, "de");
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromSatelliteDirectory(
            baseName,
            folder.TempPath,
            s_assembly,
            SatelliteStringResourceProbeMode.FallbackOnFailure);

        manager.GetString("Greeting", new CultureInfo("de-DE")).Should().Be("Hallo");
    }

    [TestMethod]
    public void GetString_TolerantUnreadableSatellite_FallsBackToParent()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        CopySatelliteAssembly(folder.TempPath, "de");
        string unreadablePath = Path.Join(folder.TempPath, "de-DE", SatelliteAssemblyFileName());

        MappedMemoryManager OpenFile(string path)
        {
            if (path == unreadablePath)
            {
                throw new UnauthorizedAccessException("Test candidate is unreadable.");
            }

            return MappedMemoryManager.CreateFromFile(path);
        }

        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromSatelliteDirectory(
            baseName,
            folder.TempPath,
            s_assembly,
            OpenFile,
            probeMode: SatelliteStringResourceProbeMode.FallbackOnFailure);

        manager.GetString("Greeting", new CultureInfo("de-DE")).Should().Be("Hallo");
    }

    [TestMethod]
    public void GetString_TolerantUnsupportedSatellite_FallsBackToParent()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        CopySatelliteAssembly(folder.TempPath, "de");
        string unsupportedPath = Path.Join(folder.TempPath, "de-DE", SatelliteAssemblyFileName());

        MappedMemoryManager OpenFile(string path)
        {
            if (path == unsupportedPath)
            {
                throw new NotSupportedException("Test candidate is unsupported.");
            }

            return MappedMemoryManager.CreateFromFile(path);
        }

        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromSatelliteDirectory(
            baseName,
            folder.TempPath,
            s_assembly,
            OpenFile,
            probeMode: SatelliteStringResourceProbeMode.FallbackOnFailure);

        manager.GetString("Greeting", new CultureInfo("de-DE")).Should().Be("Hallo");
    }

    [TestMethod]
    public void GetString_TolerantIdentityMismatch_FallsBackToParent()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        CopySatelliteAssembly(folder.TempPath, "de");
        CopySatelliteAssembly(folder.TempPath, "de", "de-DE");
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromSatelliteDirectory(
            baseName,
            folder.TempPath,
            s_assembly,
            StringResourceManagerOptions.ValidateAssemblyIdentity,
            SatelliteStringResourceProbeMode.FallbackOnFailure);

        manager.GetString("Greeting", new CultureInfo("de-DE")).Should().Be("Hallo");
    }

    [TestMethod]
    public void GetString_TolerantExternalAssemblyLocalizedFailure_ReturnsExternalNeutral()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        string resourceAssemblyFile = CopyResourceAssembly(folder.TempPath);
        string directory = Path.Join(folder.TempPath, "de");
        Directory.CreateDirectory(directory);
        System.IO.File.WriteAllBytes(
            Path.Join(directory, SatelliteAssemblyFileName()),
            [0x00, 0x01, 0x02, 0x03]);

        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromAssemblyFiles(
            baseName,
            resourceAssemblyFile,
            folder.TempPath,
            SatelliteStringResourceProbeMode.FallbackOnFailure);

        manager.GetString("Greeting", new CultureInfo("de")).Should().Be("Hello");
    }

    [TestMethod]
    public void GetString_TolerantExternalAssemblyMalformedNeutral_ThrowsBadImageFormatException()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        string resourceAssemblyFile = Path.Join(folder.TempPath, "Malformed.dll");
        System.IO.File.WriteAllBytes(resourceAssemblyFile, [0x00, 0x01, 0x02, 0x03]);
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromAssemblyFiles(
            baseName,
            resourceAssemblyFile,
            folder.TempPath,
            SatelliteStringResourceProbeMode.FallbackOnFailure);

        Action action = () => manager.GetString("Greeting", new CultureInfo("de"));

        action.Should().Throw<BadImageFormatException>();
    }

    [TestMethod]
    public void GetString_SatelliteAssemblyWithoutMatchingResource_ThrowsMissingManifestResourceException()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        byte[] image = ReadSatelliteAssembly();
        ReplaceManifestResourceName(
            image,
            $"{baseName}.de.resources",
            $"X{baseName[1..]}.de.resources");

        WriteSatelliteAssembly(folder.TempPath, image);

        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromSatelliteDirectory(
            baseName,
            folder.TempPath,
            s_assembly);

        Action action = () => manager.GetString("Greeting", new CultureInfo("de"));

        action.Should().Throw<MissingManifestResourceException>();
    }

    [TestMethod]
    public void GetString_SatelliteAssemblyIdentityDoesNotMatchByDefault_ReturnsValue()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        const string externalOwnerSimpleName = "ExternalOwner";
        CopySatelliteAssembly(
            folder.TempPath,
            "de",
            "de",
            $"{externalOwnerSimpleName}.resources.dll");

        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromSatelliteDirectory(
            baseName,
            folder.TempPath,
            s_assembly,
            externalOwnerSimpleName,
            SatelliteStringResourceProbeMode.Strict);

        manager.GetString("Greeting", new CultureInfo("de")).Should().Be("Hallo");
    }

    [TestMethod]
    public void GetString_ValidateAssemblyIdentityWithMismatchedSatellite_ThrowsFileLoadException()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        const string externalOwnerSimpleName = "ExternalOwner";
        CopySatelliteAssembly(
            folder.TempPath,
            "de",
            "de",
            $"{externalOwnerSimpleName}.resources.dll");

        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromSatelliteDirectory(
            baseName,
            folder.TempPath,
            s_assembly,
            externalOwnerSimpleName,
            StringResourceManagerOptions.ValidateAssemblyIdentity,
            SatelliteStringResourceProbeMode.Strict);

        Action action = () => manager.GetString("Greeting", new CultureInfo("de"));

        action.Should().Throw<FileLoadException>();
    }

    [TestMethod]
    public void GetString_DefaultOptionsWithNonStringResource_ThrowsNotSupportedException()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        WriteMixedSideFile(folder.TempPath, "de", baseName);

        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromResourcesDirectory(
            baseName,
            folder.TempPath,
            s_assembly);

        Action action = () => manager.GetString("Greeting", new CultureInfo("de"));

        action.Should().Throw<NotSupportedException>();
    }

    [TestMethod]
    public void GetString_IgnoreNonStringResources_TreatsNonStringNameAsMissing()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        WriteMixedSideFile(folder.TempPath, "de", baseName);
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromResourcesDirectory(
            baseName,
            folder.TempPath,
            s_assembly,
            StringResourceManagerOptions.IgnoreNonStringResources);

        manager.GetString("Greeting", new CultureInfo("de")).Should().Be("Hallo");
        manager.GetString("Count", new CultureInfo("de")).Should().BeNull();
    }

    [TestMethod]
    public void GetString_IgnoreNonStringResourcesWithParentString_FallsBackToParent()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        WriteSideFile(folder.TempPath, "de", baseName, ("Count", "Parent"));
        WriteMixedSideFile(folder.TempPath, "de-DE", baseName);
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromResourcesDirectory(
            baseName,
            folder.TempPath,
            s_assembly,
            StringResourceManagerOptions.IgnoreNonStringResources);

        manager.GetString("Count", new CultureInfo("de-DE")).Should().Be("Parent");
    }

    [TestMethod]
    public void GetString_ExtensionsFormatSideFile_IgnoreNonStringResourcesReadsStrings()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        WriteReflectionSideFile(folder.TempPath, "de", baseName);

        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromResourcesDirectory(
            baseName,
            folder.TempPath,
            s_assembly,
            StringResourceManagerOptions.IgnoreNonStringResources);

        manager.GetString("Greeting", new CultureInfo("de")).Should().Be("Hallo");

        manager.GetString("Fancy", new CultureInfo("de")).Should().BeNull();
    }

    [TestMethod]
    public void GetString_TableIsCached_SurvivesFileDeletion()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        WriteSideFile(folder.TempPath, "de", baseName, ("Greeting", "Hallo"));

        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromResourcesDirectory(
            baseName,
            folder.TempPath,
            s_assembly);

        CultureInfo german = new("de");

        manager.GetString("Greeting", german).Should().Be("Hallo");

        // Delete the backing file; the cached table must still answer for the same culture.
        System.IO.File.Delete(Path.Join(folder.TempPath, "de", $"{baseName}.resources"));

        manager.GetString("Greeting", german).Should().Be("Hallo");
    }

    [TestMethod]
    public void GetString_SatelliteTableIsCached_SurvivesFileDeletion()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        string path = CopySatelliteAssembly(folder.TempPath, "de");

        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromSatelliteDirectory(
            baseName,
            folder.TempPath,
            s_assembly);

        CultureInfo german = new("de");

        manager.GetString("Greeting", german).Should().Be("Hallo");
        System.IO.File.Delete(path);

        manager.GetString("Greeting", german).Should().Be("Hallo");
    }

    [TestMethod]
    public void GetString_ResourcesFile_IsOpenedOncePerGeneration()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        WriteSideFile(folder.TempPath, "de", baseName, ("Greeting", "Hallo"), ("Farewell", "Tschuss"));
        int openCount = 0;

        MappedMemoryManager OpenFile(string path)
        {
            openCount++;
            return MappedMemoryManager.CreateFromFile(path);
        }

        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromResourcesDirectory(
            baseName,
            folder.TempPath,
            s_assembly,
            OpenFile);

        CultureInfo german = new("de");

        manager.GetString("Greeting", german).Should().Be("Hallo");
        manager.GetString("Farewell", german).Should().Be("Tschuss");
        openCount.Should().Be(1);

        manager.ReleaseAllResources();

        manager.GetString("Greeting", german).Should().Be("Hallo");
        manager.GetString("Farewell", german).Should().Be("Tschuss");
        openCount.Should().Be(2);
    }

    [TestMethod]
    public void GetString_SatelliteFile_IsOpenedOncePerGeneration()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        CopySatelliteAssembly(folder.TempPath, "de");
        Dictionary<string, int> openCounts = [with(StringComparer.Ordinal)];

        MappedMemoryManager OpenFile(string path)
        {
            openCounts.TryGetValue(path, out int count);
            openCounts[path] = count + 1;
            return MappedMemoryManager.CreateFromFile(path);
        }

        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromSatelliteDirectory(
            baseName,
            folder.TempPath,
            s_assembly,
            OpenFile);

        CultureInfo german = new("de");

        manager.GetString("Greeting", german).Should().Be("Hallo");
        manager.GetString("Farewell", german).Should().Be("Tschuss");
        string satellitePath = Path.Join(folder.TempPath, "de", SatelliteAssemblyFileName());
        openCounts[satellitePath].Should().Be(1);
        openCounts.Should().ContainSingle();

        manager.ReleaseAllResources();

        manager.GetString("Greeting", german).Should().Be("Hallo");
        manager.GetString("Farewell", german).Should().Be("Tschuss");
        openCounts[satellitePath].Should().Be(2);
        openCounts.Should().ContainSingle();
    }

    [TestMethod]
    public void GetString_ParentCultureResourceCandidates_AreOpenedOncePerGeneration()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        WriteSideFile(folder.TempPath, "de", baseName, ("Greeting", "Hallo"), ("Farewell", "Tschuss"));
        Dictionary<string, int> openCounts = [with(StringComparer.Ordinal)];

        MappedMemoryManager OpenFile(string path)
        {
            openCounts.TryGetValue(path, out int count);
            openCounts[path] = count + 1;
            return MappedMemoryManager.CreateFromFile(path);
        }

        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromResourcesDirectory(
            baseName,
            folder.TempPath,
            s_assembly,
            OpenFile);

        CultureInfo german = new("de-DE");

        manager.GetString("Greeting", german).Should().Be("Hallo");
        manager.GetString("Farewell", german).Should().Be("Tschuss");
        string specificPath = Path.Join(folder.TempPath, "de-DE", $"{baseName}.resources");
        string parentPath = Path.Join(folder.TempPath, "de", $"{baseName}.resources");
        openCounts[specificPath].Should().Be(1);
        openCounts[parentPath].Should().Be(1);
        openCounts.Should().HaveCount(2);

        manager.ReleaseAllResources();

        manager.GetString("Greeting", german).Should().Be("Hallo");
        openCounts[specificPath].Should().Be(2);
        openCounts[parentPath].Should().Be(2);
        openCounts.Should().HaveCount(2);
    }

    [TestMethod]
    public async Task GetString_ConcurrentFirstLookup_OpensResourcesFileOnce()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        WriteSideFile(folder.TempPath, "de", baseName, ("Greeting", "Hallo"));
        using CountdownEvent callsReady = new(initialCount: 2);
        using ManualResetEventSlim openStarted = new();
        using ManualResetEventSlim continueOpen = new();
        int openCount = 0;

        MappedMemoryManager OpenFile(string path)
        {
            Interlocked.Increment(ref openCount);
            openStarted.Set();
            continueOpen.Wait();
            return MappedMemoryManager.CreateFromFile(path);
        }

        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromResourcesDirectory(
            baseName,
            folder.TempPath,
            s_assembly,
            OpenFile);

        CultureInfo german = new("de");

        Task<string?> first = Task.Run(GetString);
        Task<string?> second = Task.Run(GetString);
        openStarted.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
        continueOpen.Set();

        string?[] values = await Task.WhenAll(first, second).ConfigureAwait(continueOnCapturedContext: false);
        values.Should().Equal("Hallo", "Hallo");
        openCount.Should().Be(1);

        string? GetString()
        {
            callsReady.Signal();
            callsReady.Wait();
            return manager.GetString("Greeting", german);
        }
    }

    [TestMethod]
    public async Task GetString_ConcurrentFirstExternalAssemblyLookup_OpensEachFileOnce()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        string resourceAssemblyFile = CopyResourceAssembly(folder.TempPath);
        CopySatelliteAssembly(folder.TempPath, "de");
        using CountdownEvent callsReady = new(initialCount: 2);
        int openCount = 0;

        MappedMemoryManager OpenFile(string path)
        {
            Interlocked.Increment(ref openCount);
            return MappedMemoryManager.CreateFromFile(path);
        }

        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromAssemblyFiles(
            baseName,
            resourceAssemblyFile,
            folder.TempPath,
            OpenFile);

        CultureInfo german = new("de");
        Task<string?> first = Task.Run(GetString);
        Task<string?> second = Task.Run(GetString);

        string?[] values = await Task.WhenAll(first, second).ConfigureAwait(continueOnCapturedContext: false);
        values.Should().Equal("Hallo", "Hallo");
        openCount.Should().Be(2);

        string? GetString()
        {
            callsReady.Signal();
            callsReady.Wait();
            return manager.GetString("Greeting", german);
        }
    }

#if !DEBUG
    [TestMethod]
    public void GetString_CachedResourcesFile_DoesNotAllocate()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        WriteSideFile(folder.TempPath, "de", baseName, ("Greeting", "Hallo"));
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromResourcesDirectory(
            baseName,
            folder.TempPath,
            s_assembly);

        CultureInfo german = new("de");
        manager.GetString("Greeting", german).Should().Be("Hallo");

        string? value;
        using (MemoryWatch.Create)
        {
            value = manager.GetString("Greeting", german);
        }

        value.Should().Be("Hallo");
    }

    [TestMethod]
    public void GetString_CachedDirectSatelliteAssembly_DoesNotAllocate()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        CopySatelliteAssembly(folder.TempPath, "de");
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromSatelliteDirectory(
            baseName,
            folder.TempPath,
            s_assembly);

        CultureInfo german = new("de");
        manager.GetString("Greeting", german).Should().Be("Hallo");

        string? value;
        using (MemoryWatch.Create)
        {
            value = manager.GetString("Greeting", german);
        }

        value.Should().Be("Hallo");
    }

    [TestMethod]
    public void GetString_CachedSatelliteAssembly_DoesNotAllocate()
    {
        string baseName = NeutralBaseName();
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromRuntimeSatellites(
            baseName,
            s_assembly);

        CultureInfo german = new("de");
        manager.GetString("Greeting", german).Should().Be("Hallo");

        string? value;
        using (MemoryWatch.Create)
        {
            value = manager.GetString("Greeting", german);
        }

        value.Should().Be("Hallo");
    }
#endif

    [TestMethod]
    public void GetString_AlternatingCultures_ReturnsRequestedValues()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        WriteSideFile(folder.TempPath, "de", baseName, ("Greeting", "Hallo"));
        WriteSideFile(folder.TempPath, "fr", baseName, ("Greeting", "Bonjour"));

        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromResourcesDirectory(
            baseName,
            folder.TempPath,
            s_assembly);

        manager.GetString("Greeting", new CultureInfo("de")).Should().Be("Hallo");
        manager.GetString("Greeting", new CultureInfo("fr")).Should().Be("Bonjour");
        manager.GetString("Greeting", new CultureInfo("de")).Should().Be("Hallo");
    }

    [TestMethod]
    public async Task GetString_ConcurrentCultures_ReturnRequestedValues()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        WriteSideFile(folder.TempPath, "de", baseName, ("Greeting", "Hallo"));
        WriteSideFile(folder.TempPath, "fr", baseName, ("Greeting", "Bonjour"));
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromResourcesDirectory(
            baseName,
            folder.TempPath,
            s_assembly);

        Task<bool> german = Task.Run(() => RepeatedlyReturns("de", "Hallo"));
        Task<bool> french = Task.Run(() => RepeatedlyReturns("fr", "Bonjour"));

        bool[] results = await Task.WhenAll(german, french).ConfigureAwait(continueOnCapturedContext: false);
        results.Should().OnlyContain(result => result);

        bool RepeatedlyReturns(string cultureName, string expected)
        {
            CultureInfo culture = new(cultureName);
            for (int i = 0; i < 1_000; i++)
            {
                if (!string.Equals(manager.GetString("Greeting", culture), expected, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }
    }

    [TestMethod]
    public void IsNeutralCulture_NameDiffersOnlyByCase_ReturnsTrue() =>
        SatelliteStringResourceManager.IsNeutralCulture("en-US", "en-us").Should().BeTrue();

    [TestMethod]
    public void ReleaseAllResources_SideFileChanged_ReloadsValue()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        WriteSideFile(folder.TempPath, "de", baseName, ("Greeting", "Hallo"));
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromResourcesDirectory(
            baseName,
            folder.TempPath,
            s_assembly);

        CultureInfo german = new("de");
        manager.GetString("Greeting", german).Should().Be("Hallo");

        manager.ReleaseAllResources();
        WriteSideFile(folder.TempPath, "de", baseName, ("Greeting", "Servus"));

        manager.GetString("Greeting", german).Should().Be("Servus");
    }

    [TestMethod]
    public void ReleaseAllResources_CallerOwnedNeutralFileChanged_RetainsCachedValue()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        string neutralPath = Path.Join(folder.TempPath, "Neutral.resources");
        using (ResourceWriter writer = new(neutralPath))
        {
            writer.AddResource("Greeting", "First");
            writer.Generate();
        }

        StringResourceManager neutralResources = new(neutralPath);
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromResourcesDirectory(
            baseName,
            folder.TempPath,
            neutralResources);

        CultureInfo german = new("de");
        manager.GetString("Greeting", german).Should().Be("First");

        System.IO.File.Delete(neutralPath);

        manager.ReleaseAllResources();

        manager.GetString("Greeting", german).Should().Be("First");
    }

    [TestMethod]
    public void ReleaseAllResources_DirectSatelliteCallerOwnedNeutral_RetainsCachedValue()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        string neutralPath = Path.Join(folder.TempPath, "Neutral.resources");
        using (ResourceWriter writer = new(neutralPath))
        {
            writer.AddResource("Greeting", "First");
            writer.Generate();
        }

        StringResourceManager neutralResources = new(neutralPath);
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromSatelliteDirectory(
            baseName,
            folder.TempPath,
            s_assembly,
            neutralResources);

        CultureInfo german = new("de");
        manager.GetString("Greeting", german).Should().Be("First");

        System.IO.File.Delete(neutralPath);

        manager.ReleaseAllResources();

        manager.GetString("Greeting", german).Should().Be("First");
    }

    [TestMethod]
    public async Task ReleaseAllResources_LocalizedLoadInProgress_DoesNotRetainOldGeneration()
    {
        using TempFolder folder = new();
        string baseName = NeutralBaseName();
        string firstRoot = Path.Join(folder.TempPath, "first");
        string secondRoot = Path.Join(folder.TempPath, "second");
        WriteSideFile(firstRoot, "de", baseName, ("Greeting", "First"));
        WriteSideFile(secondRoot, "de", baseName, ("Greeting", "Second"));
        using ManualResetEventSlim loadStarted = new();
        using ManualResetEventSlim continueLoad = new();
        using ManualResetEventSlim releaseWaiting = new();
        int openCount = 0;

        MappedMemoryManager OpenFile(string path)
        {
            int invocation = Interlocked.Increment(ref openCount);
            string selectedPath = Path.Join(
                invocation == 1 ? firstRoot : secondRoot,
                "de",
                $"{baseName}.resources");

            MappedMemoryManager memory = MappedMemoryManager.CreateFromFile(selectedPath);
            if (invocation == 1)
            {
                loadStarted.Set();
                continueLoad.Wait();
            }

            return memory;
        }

        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromResourcesDirectory(
            baseName,
            folder.TempPath,
            s_assembly,
            OpenFile);

        CultureInfo german = new("de");
        Task<string?> lookup = Task.Run(() => manager.GetString("Greeting", german));
        loadStarted.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
        Task release = Task.Run(() => manager.ReleaseAllResources(releaseWaiting.Set));
        releaseWaiting.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
        continueLoad.Set();

        (await lookup.ConfigureAwait(continueOnCapturedContext: false)).Should().BeOneOf("First", "Second");
        await release.ConfigureAwait(continueOnCapturedContext: false);
        manager.GetString("Greeting", german).Should().Be("Second");
        openCount.Should().Be(2);
    }

    [TestMethod]
    public void ReleaseAllResources_InternallyOwnedNeutralManager_ClearsNeutralCache()
    {
        string baseName = NeutralBaseName();
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromRuntimeSatellites(
            baseName,
            s_assembly);

        manager.GetString("Greeting", CultureInfo.InvariantCulture).Should().Be("Hello");
        StringResourceManager neutralResources = manager.TestAccessor.Dynamic._neutralResources;
        ((object?)neutralResources.TestAccessor.Dynamic._cache).Should().NotBeNull();

        manager.ReleaseAllResources();

        ((object?)neutralResources.TestAccessor.Dynamic._cache).Should().BeNull();
    }

    [TestMethod]
    public void BaseName_ReflectsConstructorArgument()
    {
        string baseName = NeutralBaseName();
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromRuntimeSatellites(
            baseName,
            s_assembly);

        manager.BaseName.Should().Be(baseName);
    }
}
