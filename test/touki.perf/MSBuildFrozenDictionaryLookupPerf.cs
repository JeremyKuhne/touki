// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections;
using System.Collections.Frozen;
using System.Reflection;
using System.Resources;

namespace touki.perf;

/// <summary>
///  Compares dictionary lookup over the actual Microsoft.Build string tables on .NET Framework
///  4.8.1 RyuJIT and modern .NET RyuJIT.
/// </summary>
[MemoryDiagnoser]
public class MSBuildFrozenDictionaryLookupPerf
{
    /// <summary>
    ///  The Microsoft.Build resource table to measure.
    /// </summary>
    public enum ResourceTable
    {
        Main,
        Shared
    }

    /// <summary>
    ///  The key sequence to measure.
    /// </summary>
    public enum LookupPattern
    {
        Hot,
        Random
    }

    [AllowNull]
    private Dictionary<string, string> _dictionary;

    [AllowNull]
    private FrozenDictionary<string, string> _frozenDictionary;

    [AllowNull]
    private string[] _lookupKeys;

    [Params(ResourceTable.Main, ResourceTable.Shared)]
    public ResourceTable Table { get; set; }

    [Params(LookupPattern.Hot, LookupPattern.Random)]
    public LookupPattern Pattern { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        Assembly assembly = typeof(Microsoft.Build.Evaluation.Project).Assembly;
        string resourceName = Table switch
        {
            ResourceTable.Main => "Microsoft.Build.Strings.resources",
            ResourceTable.Shared => "Microsoft.Build.Strings.shared.resources",
            _ => throw new InvalidOperationException("The resource table is invalid.")
        };

        _dictionary = [with(StringComparer.Ordinal)];
        using (System.IO.Stream stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("The Microsoft.Build resource table is missing."))
        using (ResourceReader reader = new(stream))
        {
            IDictionaryEnumerator enumerator = reader.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (enumerator.Key is string name && enumerator.Value is string value)
                {
                    _dictionary.Add(name, value);
                }
            }
        }

        _frozenDictionary = _dictionary.ToFrozenDictionary(StringComparer.Ordinal);

        int expectedCount = Table == ResourceTable.Main ? 587 : 64;
        if (_dictionary.Count != expectedCount)
        {
            throw new InvalidOperationException(
                $"The resource table contains {_dictionary.Count} strings instead of {expectedCount}.");
        }

        List<string> resourceNames = [with(_dictionary.Count)];
        foreach (string name in _dictionary.Keys)
        {
            // Model caller-owned names rather than reusing the dictionary's key references.
            resourceNames.Add(new string(name.ToCharArray()));
        }

        resourceNames.Sort(StringComparer.Ordinal);
        _lookupKeys = new string[256];

        Random random = new(0x5EED);
        int previousIndex = -1;
        for (int i = 0; i < _lookupKeys.Length; i++)
        {
            int index;
            if (Pattern == LookupPattern.Hot)
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

    [Benchmark(Baseline = true)]
    public int Dictionary_LookupBatch()
    {
        int totalLength = 0;
        foreach (string key in _lookupKeys)
        {
            totalLength += _dictionary[key].Length;
        }

        return totalLength;
    }

    [Benchmark]
    public int FrozenDictionary_LookupBatch()
    {
        int totalLength = 0;
        foreach (string key in _lookupKeys)
        {
            totalLength += _frozenDictionary[key].Length;
        }

        return totalLength;
    }
}