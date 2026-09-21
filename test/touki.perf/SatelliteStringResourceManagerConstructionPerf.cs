// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Linq;
using System.Reflection;
using System.Resources;
using Touki.Resources;

namespace touki.perf;

/// <summary>
///  Measures localized manager construction on .NET Framework 4.8.1 RyuJIT and modern .NET RyuJIT.
/// </summary>
[MemoryDiagnoser]
public class SatelliteStringResourceManagerConstructionPerf
{
    [AllowNull]
    private Assembly _assembly;

    [AllowNull]
    private string _baseName;

    [AllowNull]
    private string _sourceRoot;

    [GlobalSetup]
    public void Setup()
    {
        _assembly = typeof(SatelliteStringResourceManagerConstructionPerf).Assembly;
        string resourceName = _assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("SatelliteStringResourceManagerPerfStrings.resources", StringComparison.Ordinal));
        _baseName = resourceName[..^".resources".Length];
        _sourceRoot = AppContext.BaseDirectory;
    }

    [Benchmark(Baseline = true)]
    public ResourceManager ResourceManager_RuntimeSatellites() => new(_baseName, _assembly);

    [Benchmark]
    public SatelliteStringResourceManager StringResourceManager_RuntimeSatellites() =>
        SatelliteStringResourceManager.FromRuntimeSatellites(_baseName, _assembly);

    [Benchmark]
    public SatelliteStringResourceManager StringResourceManager_ResourcesDirectory() =>
        SatelliteStringResourceManager.FromResourcesDirectory(_baseName, _sourceRoot, _assembly);

    [Benchmark]
    public SatelliteStringResourceManager StringResourceManager_SatelliteDirectory() =>
        SatelliteStringResourceManager.FromSatelliteDirectory(_baseName, _sourceRoot, _assembly);
}