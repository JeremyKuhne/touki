// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Resources;
using Touki.Resources;

namespace touki.perf;

/// <summary>
///  Measures cached localized lookups after loading a loose resource file or satellite assembly on
///  .NET Framework 4.8.1 RyuJIT and modern .NET RyuJIT.
/// </summary>
[MemoryDiagnoser]
public class SatelliteStringResourceManagerCachedPerf
{
    [AllowNull]
    private ResourceManager _resourceManager;

    [AllowNull]
    private SatelliteStringResourceManager _runtimeManager;

    [AllowNull]
    private SatelliteStringResourceManager _looseManager;

    [AllowNull]
    private SatelliteStringResourceManager _satelliteManager;

    [AllowNull]
    private CultureInfo _culture;

    [AllowNull]
    private string _looseRoot;

    [AllowNull]
    private string _root;

    /// <summary>
    ///  The localized culture fallback shape.
    /// </summary>
    [ParamsAllValues]
    public LocalizedResourceCultureScenario Scenario { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        Assembly assembly = typeof(SatelliteStringResourceManagerCachedPerf).Assembly;
        string resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("SatelliteStringResourceManagerPerfStrings.resources", StringComparison.Ordinal));
        string baseName = resourceName[..^".resources".Length];
        _culture = Scenario switch
        {
            LocalizedResourceCultureScenario.Exact => new("de"),
            LocalizedResourceCultureScenario.Parent => new("de-DE"),
            LocalizedResourceCultureScenario.Missing => new("fr-FR"),
            _ => throw new InvalidOperationException("The localized culture scenario is invalid.")
        };

        _root = Path.Join(Path.GetTempPath(), $"{nameof(SatelliteStringResourceManagerCachedPerf)}-{Guid.NewGuid():N}");
        _looseRoot = Path.Join(_root, "loose");
        string satelliteRoot = Path.Join(_root, "satellite");

        const string LocalizedCultureName = "de";
        string looseDirectory = Path.Join(_looseRoot, LocalizedCultureName);
        Directory.CreateDirectory(looseDirectory);
        using (ResourceWriter writer = new(Path.Join(looseDirectory, $"{baseName}.resources")))
        {
            writer.AddResource("Greeting", "Hallo");
            writer.AddResource("Farewell", "Tschuss");
            writer.Generate();
        }

        string satelliteDirectory = Path.Join(satelliteRoot, LocalizedCultureName);
        Directory.CreateDirectory(satelliteDirectory);
        string assemblyName = assembly.GetName().Name
            ?? throw new InvalidOperationException("The benchmark assembly must have a name.");
        string satelliteFileName = $"{assemblyName}.resources.dll";
        System.IO.File.Copy(
            Path.Join(AppContext.BaseDirectory, LocalizedCultureName, satelliteFileName),
            Path.Join(satelliteDirectory, satelliteFileName));

        _resourceManager = new(baseName, assembly);
        _runtimeManager = SatelliteStringResourceManager.FromRuntimeSatellites(baseName, assembly);
        _looseManager = SatelliteStringResourceManager.FromResourcesDirectory(
            baseName,
            _looseRoot,
            assembly);
        _satelliteManager = SatelliteStringResourceManager.FromSatelliteDirectory(
            baseName,
            satelliteRoot,
            assembly);
        _ = _resourceManager.GetString("Greeting", _culture);
        _ = _runtimeManager.GetString("Greeting", _culture);
        _ = _looseManager.GetString("Greeting", _culture);
        _ = _satelliteManager.GetString("Greeting", _culture);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _resourceManager.ReleaseAllResources();
        _runtimeManager.ReleaseAllResources();
        _looseManager.ReleaseAllResources();
        _satelliteManager.ReleaseAllResources();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Benchmark(Baseline = true)]
    public int ResourceManager_CachedLookup()
    {
        string? value = _resourceManager.GetString("Greeting", _culture);
        return value?.Length ?? 0;
    }

    [Benchmark]
    public int RuntimeSatellite_CachedLookup()
    {
        string? value = _runtimeManager.GetString("Greeting", _culture);
        return value?.Length ?? 0;
    }

    [Benchmark]
    public int LooseResource_CachedLookup()
    {
        string? value = _looseManager.GetString("Greeting", _culture);
        return value?.Length ?? 0;
    }

    [Benchmark]
    public int SatelliteAssembly_CachedLookup()
    {
        string? value = _satelliteManager.GetString("Greeting", _culture);
        return value?.Length ?? 0;
    }
}