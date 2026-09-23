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
///  Measures construction plus the first lookup on .NET Framework 4.8.1 RyuJIT and modern .NET
///  RyuJIT.
/// </summary>
/// <remarks>
///  <para>
///   Manager release is intentionally excluded from the measured operation. BenchmarkDotNet forces
///   collection between iterations so abandoned resource sets and file handles do not carry into the
///   next iteration. Complete release cost is measured by <see cref="StringResourceManagerFileLifecyclePerf"/>.
///  </para>
/// </remarks>
[MemoryDiagnoser]
[GcForce(value: true)]
public class StringResourceManagerFirstLookupPerf
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
        _assembly = typeof(StringResourceManagerFirstLookupPerf).Assembly;
        _resourceName = _assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("SatelliteStringResourceManagerPerfStrings.resources", StringComparison.Ordinal));

        _baseName = _resourceName[..^".resources".Length];
        _resourcesFile = StringResourceManagerConstructionPerf.WriteResourcesFile(
            nameof(StringResourceManagerFirstLookupPerf));

        _fileBaseName = Path.GetFileNameWithoutExtension(_resourcesFile);
        _resourcesDirectory = Path.GetDirectoryName(_resourcesFile)
            ?? throw new InvalidOperationException("The benchmark resource path must have a directory.");

        byte[] resources = System.IO.File.ReadAllBytes(_resourcesFile);
        _streamFactory = () => new System.IO.MemoryStream(resources, writable: false);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        System.IO.File.Delete(_resourcesFile);
    }

    [Benchmark(Baseline = true)]
    public int ResourceManager_Assembly()
    {
        ResourceManager manager = new(_baseName, _assembly);
        return manager.GetString("Greeting", CultureInfo.InvariantCulture)?.Length ?? 0;
    }

    [Benchmark]
    public int ResourceManager_ResourcesFile()
    {
        ResourceManager manager = ResourceManager.CreateFileBasedResourceManager(
            _fileBaseName,
            _resourcesDirectory,
            usingResourceSet: null);

        return manager.GetString("Greeting", CultureInfo.InvariantCulture)?.Length ?? 0;
    }

    [Benchmark]
    public int StringResourceManager_LoadedAssembly()
    {
        StringResourceManager manager = new(_baseName, _assembly);
        return manager.GetString("Greeting", CultureInfo.InvariantCulture)?.Length ?? 0;
    }

    [Benchmark]
    public int StringResourceManager_ResourcesFile()
    {
        StringResourceManager manager = new(_resourcesFile);
        return manager.GetString("Greeting", CultureInfo.InvariantCulture)?.Length ?? 0;
    }

    [Benchmark]
    public int StringResourceManager_StreamFactory()
    {
        StringResourceManager manager = new(_baseName, _streamFactory);
        return manager.GetString("Greeting", CultureInfo.InvariantCulture)?.Length ?? 0;
    }
}