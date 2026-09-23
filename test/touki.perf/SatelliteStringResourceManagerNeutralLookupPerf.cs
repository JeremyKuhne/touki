// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Frozen;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Resources;
using Touki.Resources;

namespace touki.perf;

/// <summary>
///  Compares composed neutral fallback lookup with a combined table on .NET Framework 4.8.1 RyuJIT
///  and modern .NET RyuJIT.
/// </summary>
[MemoryDiagnoser]
public class SatelliteStringResourceManagerNeutralLookupPerf
{
    [AllowNull]
    private SatelliteStringResourceManager _fallback;

    [AllowNull]
    private FrozenDictionary<string, string> _combined;

    [AllowNull]
    private CultureInfo _culture;

    [AllowNull]
    private string _root;

    [GlobalSetup]
    public void Setup()
    {
        Assembly assembly = typeof(SatelliteStringResourceManagerNeutralLookupPerf).Assembly;
        string resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("SatelliteStringResourceManagerPerfStrings.resources", StringComparison.Ordinal));

        string baseName = resourceName[..^".resources".Length];
        StringResourceManager neutralResources = new(baseName, assembly);
        Dictionary<string, string> neutralStrings =
            StringResourceTableLoader.LoadStringTableFromAssembly(assembly, resourceName)
                ?? throw new InvalidOperationException("The neutral benchmark resource is missing.");

        _culture = new("de");
        _root = Path.Join(Path.GetTempPath(), $"{nameof(SatelliteStringResourceManagerNeutralLookupPerf)}-{Guid.NewGuid():N}");
        string cultureRoot = Path.Join(_root, _culture.Name);
        Directory.CreateDirectory(cultureRoot);

        using (ResourceWriter writer = new(Path.Join(cultureRoot, $"{baseName}.resources")))
        {
            writer.AddResource("LocalizedOnly", "Localized");
            writer.Generate();
        }

        _fallback = SatelliteStringResourceManager.FromResourcesDirectory(
            baseName,
            _root,
            neutralResources);

        _ = _fallback.GetString("Greeting", _culture);

        Dictionary<string, string> combined = new(neutralStrings, StringComparer.Ordinal)
        {
            ["LocalizedOnly"] = "Localized"
        };

        _combined = combined.ToFrozenDictionary(StringComparer.Ordinal);
    }

    [GlobalCleanup]
    public void Cleanup() => Directory.Delete(_root, recursive: true);

    [Benchmark(Baseline = true)]
    public int CombinedDictionary_NeutralLookup() => _combined["Greeting"].Length;

    [Benchmark]
    public int ComposedNeutral_NeutralLookup() =>
        _fallback.GetString("Greeting", _culture)?.Length ?? 0;

    [Benchmark]
    public int CombinedDictionary_LocalizedLookup() => _combined["LocalizedOnly"].Length;

    [Benchmark]
    public int ComposedNeutral_LocalizedLookup() =>
        _fallback.GetString("LocalizedOnly", _culture)?.Length ?? 0;
}