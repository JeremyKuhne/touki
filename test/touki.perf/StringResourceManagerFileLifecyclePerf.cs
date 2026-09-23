// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Globalization;
using System.Resources;
using Touki.Resources;

namespace touki.perf;

/// <summary>
///  Measures a complete file-backed manager lifecycle on .NET Framework 4.8.1 RyuJIT and modern
///  .NET RyuJIT.
/// </summary>
[MemoryDiagnoser]
public class StringResourceManagerFileLifecyclePerf
{
    [AllowNull]
    private string _baseName;

    [AllowNull]
    private string _resourcesDirectory;

    [AllowNull]
    private string _resourcesFile;

    [GlobalSetup]
    public void Setup()
    {
        _resourcesFile = StringResourceManagerConstructionPerf.WriteResourcesFile(
            nameof(StringResourceManagerFileLifecyclePerf));

        _baseName = Path.GetFileNameWithoutExtension(_resourcesFile);
        _resourcesDirectory = Path.GetDirectoryName(_resourcesFile)
            ?? throw new InvalidOperationException("The benchmark resource path must have a directory.");
    }

    [GlobalCleanup]
    public void Cleanup() => System.IO.File.Delete(_resourcesFile);

    [Benchmark(Baseline = true)]
    public int ResourceManager_ResourcesFile()
    {
        ResourceManager manager = ResourceManager.CreateFileBasedResourceManager(
            _baseName,
            _resourcesDirectory,
            usingResourceSet: null);

        try
        {
            return manager.GetString("Greeting", CultureInfo.InvariantCulture)?.Length ?? 0;
        }
        finally
        {
            manager.ReleaseAllResources();
        }
    }

    [Benchmark]
    public int StringResourceManager_ResourcesFile()
    {
        StringResourceManager manager = new(_resourcesFile);
        try
        {
            return manager.GetString("Greeting", CultureInfo.InvariantCulture)?.Length ?? 0;
        }
        finally
        {
            manager.ReleaseAllResources();
        }
    }
}