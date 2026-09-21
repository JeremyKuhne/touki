// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Globalization;
using System.Reflection;
using System.Resources;
using Touki.Resources;

namespace touki.perf;

/// <summary>
///  Compares an actual generated property with BCL and Touki manager lookup on modern .NET
///  RyuJIT and .NET Framework 4.8.1 RyuJIT.
/// </summary>
[MemoryDiagnoser]
public class GeneratedStringResourceAccessorPerf
{
    private const string ResourceName = "ErrorString";
    [AllowNull]
    private ResourceManager _bclManager;
    [AllowNull]
    private StringResourceManager _toukiManager;

    [GlobalSetup]
    public void Setup()
    {
        Assembly resourceAssembly = typeof(StringResourceManager).Assembly;
        _bclManager = new("Touki.Resources.SR", resourceAssembly);
        _toukiManager = new("Touki.Resources.SR", resourceAssembly);
        SR.Culture = CultureInfo.InvariantCulture;

        _ = BclManager_WarmedLookup();
        _ = ToukiManager_WarmedLookup();
        _ = GeneratedProperty_WarmedLookup();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _bclManager.ReleaseAllResources();
        _toukiManager.ReleaseAllResources();
        SR.ResourceManager.ReleaseAllResources();
    }

    [Benchmark(Baseline = true)]
    public int BclManager_WarmedLookup()
    {
        string? value = _bclManager.GetString(ResourceName, CultureInfo.InvariantCulture);
        return value?.Length ?? throw new MissingManifestResourceException();
    }

    [Benchmark]
    public int ToukiManager_WarmedLookup()
    {
        string? value = _toukiManager.GetString(ResourceName, CultureInfo.InvariantCulture);
        return value?.Length ?? throw new MissingManifestResourceException();
    }

    [Benchmark]
    public int GeneratedProperty_WarmedLookup() => SR.ErrorString.Length;
}