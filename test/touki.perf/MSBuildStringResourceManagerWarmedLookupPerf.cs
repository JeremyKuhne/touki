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
///  Compares warmed BCL and Touki lookup over the actual Microsoft.Build string tables on .NET
///  Framework 4.8.1 RyuJIT and modern .NET RyuJIT.
/// </summary>
[MemoryDiagnoser]
public class MSBuildStringResourceManagerWarmedLookupPerf
{
    /// <summary>
    ///  Representative warmed lookup sequences over the main and shared Microsoft.Build tables.
    /// </summary>
    public enum LookupScenario
    {
        MainHot,
        MainBurst2,
        MainBurst4,
        MainRandom,
        SharedHot,
        SharedBurst2,
        SharedBurst4,
        SharedRandom
    }

    [AllowNull]
    private ResourceManager _resourceManager;

    [AllowNull]
    private StringResourceManager _stringResourceManager;

    [AllowNull]
    private string[] _lookupKeys;

    /// <summary>
    ///  The resource table and access pattern to measure.
    /// </summary>
    [Params(
        LookupScenario.MainHot,
        LookupScenario.MainBurst2,
        LookupScenario.MainBurst4,
        LookupScenario.MainRandom,
        LookupScenario.SharedHot,
        LookupScenario.SharedBurst2,
        LookupScenario.SharedBurst4,
        LookupScenario.SharedRandom)]
    public LookupScenario Scenario { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        Assembly assembly = typeof(Microsoft.Build.Evaluation.Project).Assembly;
        (string ResourceName, int RunLength) scenario = Scenario switch
        {
            LookupScenario.MainHot => ("Microsoft.Build.Strings.resources", 0),
            LookupScenario.MainBurst2 => ("Microsoft.Build.Strings.resources", 2),
            LookupScenario.MainBurst4 => ("Microsoft.Build.Strings.resources", 4),
            LookupScenario.MainRandom => ("Microsoft.Build.Strings.resources", 1),
            LookupScenario.SharedHot => ("Microsoft.Build.Strings.shared.resources", 0),
            LookupScenario.SharedBurst2 => ("Microsoft.Build.Strings.shared.resources", 2),
            LookupScenario.SharedBurst4 => ("Microsoft.Build.Strings.shared.resources", 4),
            LookupScenario.SharedRandom => ("Microsoft.Build.Strings.shared.resources", 1),
            _ => throw new InvalidOperationException("The lookup scenario is invalid.")
        };

        string baseName = scenario.ResourceName[..^".resources".Length];
        List<string> resourceNames = [];
        using (System.IO.Stream stream = assembly.GetManifestResourceStream(scenario.ResourceName)
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
        _lookupKeys = new string[256];
        Random random = new(0x5EED);
        int previousIndex = -1;
        for (int i = 0; i < _lookupKeys.Length; i++)
        {
            int index;
            if (scenario.RunLength == 0)
            {
                index = 0;
            }
            else if (i % scenario.RunLength != 0)
            {
                index = previousIndex;
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

        int adjacentHits = string.Equals(_lookupKeys[^1], _lookupKeys[0], StringComparison.Ordinal) ? 1 : 0;
        for (int i = 1; i < _lookupKeys.Length; i++)
        {
            if (string.Equals(_lookupKeys[i - 1], _lookupKeys[i], StringComparison.Ordinal))
            {
                adjacentHits++;
            }
        }

        int expectedHits = scenario.RunLength switch
        {
            0 => _lookupKeys.Length,
            1 => 0,
            _ => _lookupKeys.Length - (_lookupKeys.Length / scenario.RunLength)
        };

        if (adjacentHits != expectedHits)
        {
            throw new InvalidOperationException("The lookup sequence does not have the expected locality.");
        }

        _resourceManager = new(baseName, assembly);
        _stringResourceManager = new(baseName, assembly);

        // Populate every BCL value reached by the sequence. Touki intentionally retains only the last value.
        foreach (string key in _lookupKeys)
        {
            _ = _resourceManager.GetString(key, CultureInfo.InvariantCulture);
            _ = _stringResourceManager.GetString(key, CultureInfo.InvariantCulture);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _resourceManager.ReleaseAllResources();
        _stringResourceManager.ReleaseAllResources();
    }

    /// <summary>
    ///  Performs a warmed 256-lookup batch through BCL <see cref="ResourceManager"/>.
    /// </summary>
    [Benchmark(Baseline = true)]
    public int ResourceManager_LookupBatch()
    {
        int totalLength = 0;
        foreach (string key in _lookupKeys)
        {
            string value = _resourceManager.GetString(key, CultureInfo.InvariantCulture)
                ?? throw new InvalidOperationException("The benchmark resource is missing.");

            totalLength += value.Length;
        }

        return totalLength;
    }

    /// <summary>
    ///  Performs the same warmed batch through <see cref="StringResourceManager"/>.
    /// </summary>
    [Benchmark]
    public int StringResourceManager_LookupBatch()
    {
        int totalLength = 0;
        foreach (string key in _lookupKeys)
        {
            string value = _stringResourceManager.GetString(key, CultureInfo.InvariantCulture)
                ?? throw new InvalidOperationException("The benchmark resource is missing.");

            totalLength += value.Length;
        }

        return totalLength;
    }
}