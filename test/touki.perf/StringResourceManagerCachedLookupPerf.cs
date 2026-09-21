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
///  Measures warmed string lookup on .NET Framework 4.8.1 RyuJIT and modern .NET RyuJIT.
/// </summary>
[MemoryDiagnoser]
public class StringResourceManagerCachedLookupPerf
{
    [AllowNull]
    private ResourceManager _resourceManager;

    [AllowNull]
    private ResourceManager _fileResourceManager;

    [AllowNull]
    private StringResourceManager _assemblyManager;

    [AllowNull]
    private StringResourceManager _fileManager;

    [AllowNull]
    private StringResourceManager _streamManager;

    [AllowNull]
    private string _resourcesFile;

    [GlobalSetup]
    public void Setup()
    {
        Assembly assembly = typeof(StringResourceManagerCachedLookupPerf).Assembly;
        string resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("SatelliteStringResourceManagerPerfStrings.resources", StringComparison.Ordinal));
        string baseName = resourceName[..^".resources".Length];
        _resourcesFile = StringResourceManagerConstructionPerf.WriteResourcesFile(
            nameof(StringResourceManagerCachedLookupPerf));
        string resourcesDirectory = Path.GetDirectoryName(_resourcesFile)
            ?? throw new InvalidOperationException("The benchmark resource path must have a directory.");

        _resourceManager = new(baseName, assembly);
        _fileResourceManager = ResourceManager.CreateFileBasedResourceManager(
            Path.GetFileNameWithoutExtension(_resourcesFile),
            resourcesDirectory,
            usingResourceSet: null);
        _assemblyManager = new(baseName, assembly);
        _fileManager = new(_resourcesFile);
        byte[] resources = System.IO.File.ReadAllBytes(_resourcesFile);
        _streamManager = new(baseName, () => new System.IO.MemoryStream(resources, writable: false));
        _ = _resourceManager.GetString("Greeting", CultureInfo.InvariantCulture);
        _ = _fileResourceManager.GetString("Greeting", CultureInfo.InvariantCulture);
        _ = _assemblyManager.GetString("Greeting", CultureInfo.InvariantCulture);
        _ = _fileManager.GetString("Greeting", CultureInfo.InvariantCulture);
        _ = _streamManager.GetString("Greeting", CultureInfo.InvariantCulture);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _resourceManager.ReleaseAllResources();
        _fileResourceManager.ReleaseAllResources();
        _assemblyManager.ReleaseAllResources();
        _fileManager.ReleaseAllResources();
        _streamManager.ReleaseAllResources();
        System.IO.File.Delete(_resourcesFile);
    }

    [Benchmark(Baseline = true)]
    public int ResourceManager_Assembly() =>
        _resourceManager.GetString("Greeting", CultureInfo.InvariantCulture)?.Length ?? 0;

    [Benchmark]
    public int ResourceManager_ResourcesFile() =>
        _fileResourceManager.GetString("Greeting", CultureInfo.InvariantCulture)?.Length ?? 0;

    [Benchmark]
    public int StringResourceManager_LoadedAssembly() =>
        _assemblyManager.GetString("Greeting", CultureInfo.InvariantCulture)?.Length ?? 0;

    [Benchmark]
    public int StringResourceManager_ResourcesFile() =>
        _fileManager.GetString("Greeting", CultureInfo.InvariantCulture)?.Length ?? 0;

    [Benchmark]
    public int StringResourceManager_StreamFactory() =>
        _streamManager.GetString("Greeting", CultureInfo.InvariantCulture)?.Length ?? 0;
}