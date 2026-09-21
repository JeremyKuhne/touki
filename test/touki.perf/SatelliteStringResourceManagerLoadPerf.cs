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
///  Measures the first manager-local table load after process-wide runtime binding may be cached on
///  .NET Framework 4.8.1 RyuJIT and modern .NET RyuJIT.
/// </summary>
[MemoryDiagnoser]
public class SatelliteStringResourceManagerLoadPerf
{
    [AllowNull]
    private Assembly _assembly;

    [AllowNull]
    private string _baseName;

    [AllowNull]
    private CultureInfo _culture;

    [AllowNull]
    private string _looseRoot;

    [AllowNull]
    private string _root;

    [AllowNull]
    private string _satelliteRoot;

    /// <summary>
    ///  The localized culture fallback shape.
    /// </summary>
    [ParamsAllValues]
    public LocalizedResourceCultureScenario Scenario { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _assembly = typeof(SatelliteStringResourceManagerLoadPerf).Assembly;
        string resourceName = _assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("SatelliteStringResourceManagerPerfStrings.resources", StringComparison.Ordinal));
        _baseName = resourceName[..^".resources".Length];
        _culture = Scenario switch
        {
            LocalizedResourceCultureScenario.Exact => new("de"),
            LocalizedResourceCultureScenario.Parent => new("de-DE"),
            LocalizedResourceCultureScenario.Missing => new("fr-FR"),
            _ => throw new InvalidOperationException("The localized culture scenario is invalid.")
        };

        _root = Path.Join(Path.GetTempPath(), $"{nameof(SatelliteStringResourceManagerLoadPerf)}-{Guid.NewGuid():N}");
        _looseRoot = Path.Join(_root, "loose");
        _satelliteRoot = Path.Join(_root, "satellite");

        const string LocalizedCultureName = "de";
        string looseDirectory = Path.Join(_looseRoot, LocalizedCultureName);
        Directory.CreateDirectory(looseDirectory);
        using ResourceWriter writer = new(Path.Join(looseDirectory, $"{_baseName}.resources"));
        writer.AddResource("Greeting", "Hallo");
        writer.AddResource("Farewell", "Tschuss");
        writer.Generate();

        string satelliteDirectory = Path.Join(_satelliteRoot, LocalizedCultureName);
        Directory.CreateDirectory(satelliteDirectory);
        string assemblyName = _assembly.GetName().Name
            ?? throw new InvalidOperationException("The benchmark assembly must have a name.");
        string satelliteFileName = $"{assemblyName}.resources.dll";
        System.IO.File.Copy(
            Path.Join(AppContext.BaseDirectory, LocalizedCultureName, satelliteFileName),
            Path.Join(satelliteDirectory, satelliteFileName));
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Benchmark(Baseline = true)]
    public int ResourceManager_RuntimeSatellites_CachedBindFirstLookup()
    {
        ResourceManager manager = new(_baseName, _assembly);
        string? value = manager.GetString("Greeting", _culture);
        return value?.Length ?? 0;
    }

    [Benchmark]
    public int SatelliteStringResourceManager_RuntimeSatellites_CachedBindFirstLookup()
    {
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromRuntimeSatellites(
            _baseName,
            _assembly);
        string? value = manager.GetString("Greeting", _culture);
        return value?.Length ?? 0;
    }

    [Benchmark]
    public int SatelliteStringResourceManager_ResourcesDirectory_FirstLookup()
    {
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromResourcesDirectory(
            _baseName,
            _looseRoot,
            _assembly);
        string? value = manager.GetString("Greeting", _culture);
        return value?.Length ?? 0;
    }

    [Benchmark]
    public int SatelliteStringResourceManager_SatelliteDirectory_FirstLookup()
    {
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromSatelliteDirectory(
            _baseName,
            _satelliteRoot,
            _assembly);
        string? value = manager.GetString("Greeting", _culture);
        return value?.Length ?? 0;
    }
}