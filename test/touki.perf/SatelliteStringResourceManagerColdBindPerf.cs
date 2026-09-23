// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Resources;
using BenchmarkDotNet.Engines;
using Touki.Resources;

namespace touki.perf;

/// <summary>
///  Measures one localized lookup with a cold process-wide satellite bind on .NET Framework 4.8.1
///  RyuJIT and modern .NET RyuJIT. Each measured invocation runs in a fresh process.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.ColdStart, launchCount: 15, warmupCount: 0, iterationCount: 1, invocationCount: 1)]
public class SatelliteStringResourceManagerColdBindPerf
{
    [AllowNull]
    private Assembly _assembly;

    [AllowNull]
    private string _baseName;

    [AllowNull]
    private CultureInfo _culture;

    [AllowNull]
    private string _resourcesRoot;

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
        _assembly = typeof(SatelliteStringResourceManagerColdBindPerf).Assembly;
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

        _root = Path.Join(Path.GetTempPath(), $"{nameof(SatelliteStringResourceManagerColdBindPerf)}-{Guid.NewGuid():N}");
        _resourcesRoot = Path.Join(_root, "resources");
        _satelliteRoot = Path.Join(_root, "satellite");

        const string LocalizedCultureName = "de";
        string resourcesDirectory = Path.Join(_resourcesRoot, LocalizedCultureName);
        Directory.CreateDirectory(resourcesDirectory);
        using (ResourceWriter writer = new(Path.Join(resourcesDirectory, $"{_baseName}.resources")))
        {
            writer.AddResource("Greeting", "Hallo");
            writer.AddResource("Farewell", "Tschuss");
            writer.Generate();
        }

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
    public void Cleanup() => Directory.Delete(_root, recursive: true);

    [Benchmark(Baseline = true)]
    public int ResourceManager_RuntimeSatellites_ColdFirstLookup()
    {
        ResourceManager manager = new(_baseName, _assembly);
        return manager.GetString("Greeting", _culture)?.Length ?? 0;
    }

    [Benchmark]
    public int SatelliteStringResourceManager_RuntimeSatellites_ColdFirstLookup()
    {
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromRuntimeSatellites(
            _baseName,
            _assembly);

        return manager.GetString("Greeting", _culture)?.Length ?? 0;
    }

    [Benchmark]
    public int SatelliteStringResourceManager_ResourcesDirectory_ColdFirstLookup()
    {
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromResourcesDirectory(
            _baseName,
            _resourcesRoot,
            _assembly);

        return manager.GetString("Greeting", _culture)?.Length ?? 0;
    }

    [Benchmark]
    public int SatelliteStringResourceManager_SatelliteDirectory_ColdFirstLookup()
    {
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromSatelliteDirectory(
            _baseName,
            _satelliteRoot,
            _assembly);

        return manager.GetString("Greeting", _culture)?.Length ?? 0;
    }
}