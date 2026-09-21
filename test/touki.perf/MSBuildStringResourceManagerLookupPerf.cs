// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Resources;
using Touki.Resources;

namespace touki.perf;

/// <summary>
///  Compares BCL and Touki resource-manager lifetimes over the actual Microsoft.Build string tables
///  on .NET Framework 4.8.1 RyuJIT and modern .NET RyuJIT.
/// </summary>
[MemoryDiagnoser]
public class MSBuildStringResourceManagerLookupPerf
{
    /// <summary>
    ///  Representative lookup sequences over the main and shared Microsoft.Build string tables.
    /// </summary>
    public enum LookupScenario
    {
        MainHot1024,
        MainRandom16,
        MainRandom64,
        MainRandom128,
        MainRandom256,
        MainRandom512,
        MainRandom1024,
        SharedHot1024,
        SharedRandom8,
        SharedRandom16,
        SharedRandom32,
        SharedRandom64,
        SharedRandom128,
        SharedRandom256
    }

    [AllowNull]
    private Assembly _assembly;

    [AllowNull]
    private string _baseName;

    [AllowNull]
    private string[] _lookupKeys;

    /// <summary>
    ///  The resource table and lookup sequence to measure.
    /// </summary>
    [Params(
        LookupScenario.MainHot1024,
        LookupScenario.MainRandom16,
        LookupScenario.MainRandom64,
        LookupScenario.MainRandom128,
        LookupScenario.MainRandom256,
        LookupScenario.MainRandom512,
        LookupScenario.MainRandom1024,
        LookupScenario.SharedHot1024,
        LookupScenario.SharedRandom8,
        LookupScenario.SharedRandom16,
        LookupScenario.SharedRandom32,
        LookupScenario.SharedRandom64,
        LookupScenario.SharedRandom128,
        LookupScenario.SharedRandom256)]
    public LookupScenario Scenario { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _assembly = typeof(Microsoft.Build.Evaluation.Project).Assembly;
        (string ResourceName, int LookupCount, bool HotKey) scenario = Scenario switch
        {
            LookupScenario.MainHot1024 => ("Microsoft.Build.Strings.resources", 1024, true),
            LookupScenario.MainRandom16 => ("Microsoft.Build.Strings.resources", 16, false),
            LookupScenario.MainRandom64 => ("Microsoft.Build.Strings.resources", 64, false),
            LookupScenario.MainRandom128 => ("Microsoft.Build.Strings.resources", 128, false),
            LookupScenario.MainRandom256 => ("Microsoft.Build.Strings.resources", 256, false),
            LookupScenario.MainRandom512 => ("Microsoft.Build.Strings.resources", 512, false),
            LookupScenario.MainRandom1024 => ("Microsoft.Build.Strings.resources", 1024, false),
            LookupScenario.SharedHot1024 => ("Microsoft.Build.Strings.shared.resources", 1024, true),
            LookupScenario.SharedRandom8 => ("Microsoft.Build.Strings.shared.resources", 8, false),
            LookupScenario.SharedRandom16 => ("Microsoft.Build.Strings.shared.resources", 16, false),
            LookupScenario.SharedRandom32 => ("Microsoft.Build.Strings.shared.resources", 32, false),
            LookupScenario.SharedRandom64 => ("Microsoft.Build.Strings.shared.resources", 64, false),
            LookupScenario.SharedRandom128 => ("Microsoft.Build.Strings.shared.resources", 128, false),
            LookupScenario.SharedRandom256 => ("Microsoft.Build.Strings.shared.resources", 256, false),
            _ => throw new InvalidOperationException("The lookup scenario is invalid.")
        };

        _baseName = scenario.ResourceName[..^".resources".Length];
        List<string> resourceNames = [];
        using (System.IO.Stream stream = _assembly.GetManifestResourceStream(scenario.ResourceName)
            ?? throw new InvalidOperationException("The Microsoft.Build resource table is missing."))
        using (ResourceReader reader = new(stream))
        {
            IDictionaryEnumerator enumerator = reader.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (enumerator.Key is string name && enumerator.Value is string)
                {
                    resourceNames.Add(name);
                }
            }
        }

        resourceNames.Sort(StringComparer.Ordinal);
        _lookupKeys = new string[scenario.LookupCount];
        Random random = new(0x5EED);
        int previousIndex = -1;
        for (int i = 0; i < _lookupKeys.Length; i++)
        {
            int index;
            if (scenario.HotKey)
            {
                index = 0;
            }
            else
            {
                do
                {
                    index = random.Next(resourceNames.Count);
                }
                while (index == previousIndex);
            }

            _lookupKeys[i] = resourceNames[index];
            previousIndex = index;
        }
    }

    /// <summary>
    ///  Creates a BCL manager, performs the lookup sequence, and releases its resource set.
    /// </summary>
    [Benchmark(Baseline = true)]
    public int ResourceManager_LoadAndLookupBatch()
    {
        ResourceManager manager = new(_baseName, _assembly);
        try
        {
            int totalLength = 0;
            foreach (string key in _lookupKeys)
            {
                string value = manager.GetString(key, CultureInfo.InvariantCulture)
                    ?? throw new InvalidOperationException("The benchmark resource is missing.");
                totalLength += value.Length;
            }

            return totalLength;
        }
        finally
        {
            manager.ReleaseAllResources();
        }
    }

    /// <summary>
    ///  Creates a Touki manager, performs the lookup sequence, and releases its indexed table.
    /// </summary>
    [Benchmark]
    public int StringResourceManager_LoadAndLookupBatch()
    {
        StringResourceManager manager = new(_baseName, _assembly);
        try
        {
            int totalLength = 0;
            foreach (string key in _lookupKeys)
            {
                string value = manager.GetString(key, CultureInfo.InvariantCulture)
                    ?? throw new InvalidOperationException("The benchmark resource is missing.");
                totalLength += value.Length;
            }

            return totalLength;
        }
        finally
        {
            manager.ReleaseAllResources();
        }
    }
}