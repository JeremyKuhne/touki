// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Frozen;
using System.Linq;
using System.Reflection;
using Touki.Resources;

namespace touki.perf;

/// <summary>
///  Compares neutral-fallback composition with eager table combination on .NET Framework 4.8.1
///  RyuJIT and modern .NET RyuJIT.
/// </summary>
[MemoryDiagnoser]
public class SatelliteStringResourceManagerNeutralConstructionPerf
{
    [AllowNull]
    private Assembly _assembly;

    [AllowNull]
    private string _baseName;

    [AllowNull]
    private StringResourceManager _neutralResources;

    [AllowNull]
    private Dictionary<string, string> _neutralStrings;

    [AllowNull]
    private FrozenDictionary<string, string> _localized;

    [GlobalSetup]
    public void Setup()
    {
        _assembly = typeof(SatelliteStringResourceManagerNeutralConstructionPerf).Assembly;
        string resourceName = _assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("SatelliteStringResourceManagerPerfStrings.resources", StringComparison.Ordinal));
        _baseName = resourceName[..^".resources".Length];
        _neutralResources = new(_baseName, _assembly);
        _ = _neutralResources.GetString("Greeting");
        _neutralStrings = StringResourceTableLoader.LoadStringTableFromAssembly(_assembly, resourceName)
            ?? throw new InvalidOperationException("The neutral benchmark resource is missing.");
        _localized = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["LocalizedOnly"] = "Localized"
        }.ToFrozenDictionary(StringComparer.Ordinal);
    }

    [Benchmark(Baseline = true)]
    public SatelliteStringResourceManager AssemblyConvenience_Construction() =>
        SatelliteStringResourceManager.FromRuntimeSatellites(_baseName, _assembly);

    [Benchmark]
    public SatelliteStringResourceManager ComposedNeutral_Construction() =>
        SatelliteStringResourceManager.FromRuntimeSatellites(_baseName, _assembly, _neutralResources);

    [Benchmark]
    public (SatelliteStringResourceManager Manager, FrozenDictionary<string, string> Strings)
        ComposedNeutralAndCombinedDictionary_Construction()
    {
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromRuntimeSatellites(
            _baseName,
            _assembly,
            _neutralResources);
        Dictionary<string, string> combined = new(_neutralStrings, StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> entry in _localized)
        {
            combined[entry.Key] = entry.Value;
        }

        return (manager, combined.ToFrozenDictionary(StringComparer.Ordinal));
    }
}