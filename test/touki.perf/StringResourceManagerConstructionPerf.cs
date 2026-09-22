// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Linq;
using System.Reflection;
using System.Resources;
using Touki.Resources;

namespace touki.perf;

/// <summary>
///  Measures resource-manager construction on .NET Framework 4.8.1 RyuJIT and modern .NET RyuJIT.
/// </summary>
[MemoryDiagnoser]
public class StringResourceManagerConstructionPerf
{
    [AllowNull]
    private Assembly _assembly;

    [AllowNull]
    private string _baseName;

    [AllowNull]
    private string _fileBaseName;

    [AllowNull]
    private string _resourceName;

    [AllowNull]
    private string _resourcesFile;

    [AllowNull]
    private string _resourcesDirectory;

    [AllowNull]
    private Func<System.IO.Stream> _streamFactory;

    [GlobalSetup]
    public void Setup()
    {
        _assembly = typeof(StringResourceManagerConstructionPerf).Assembly;
        _resourceName = _assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("SatelliteStringResourceManagerPerfStrings.resources", StringComparison.Ordinal));

        _baseName = _resourceName[..^".resources".Length];
        _resourcesFile = WriteResourcesFile(nameof(StringResourceManagerConstructionPerf));
        _fileBaseName = Path.GetFileNameWithoutExtension(_resourcesFile);
        _resourcesDirectory = Path.GetDirectoryName(_resourcesFile)
            ?? throw new InvalidOperationException("The benchmark resource path must have a directory.");

        byte[] resources = System.IO.File.ReadAllBytes(_resourcesFile);
        _streamFactory = () => new System.IO.MemoryStream(resources, writable: false);
    }

    [GlobalCleanup]
    public void Cleanup() => System.IO.File.Delete(_resourcesFile);

    [Benchmark(Baseline = true)]
    public ResourceManager ResourceManager_Assembly() => new(_baseName, _assembly);

    [Benchmark]
    public ResourceManager ResourceManager_ResourcesFile() =>
        ResourceManager.CreateFileBasedResourceManager(
            _fileBaseName,
            _resourcesDirectory,
            usingResourceSet: null);

    [Benchmark]
    public StringResourceManager StringResourceManager_LoadedAssembly() => new(_baseName, _assembly);

    [Benchmark]
    public StringResourceManager StringResourceManager_ResourcesFile() => new(_resourcesFile);

    [Benchmark]
    public StringResourceManager StringResourceManager_StreamFactory() => new(_baseName, _streamFactory);

    internal static string WriteResourcesFile(string owner)
    {
        string path = Path.Join(Path.GetTempPath(), $"{owner}-{Guid.NewGuid():N}.resources");
        using ResourceWriter writer = new(path);
        writer.AddResource("Greeting", "Hello");
        writer.AddResource("Farewell", "Goodbye");
        writer.Generate();
        return path;
    }
}