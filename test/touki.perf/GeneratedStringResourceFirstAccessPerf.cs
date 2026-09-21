// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Globalization;
using System.Resources;
using Touki.Resources;

namespace touki.perf;

/// <summary>
///  Measures first generated-field population over a warm manager on modern .NET RyuJIT and
///  .NET Framework 4.8.1 RyuJIT, with cache replacement measured separately.
/// </summary>
[MemoryDiagnoser]
public class GeneratedStringResourceFirstAccessPerf
{
    [GlobalSetup]
    public void Setup()
    {
        SR.Culture = CultureInfo.InvariantCulture;
        _ = SR.ErrorString;
    }

    [GlobalCleanup]
    public void Cleanup() => SR.ResourceManager.ReleaseAllResources();

    [Benchmark(Baseline = true)]
    public int ResetCache()
    {
        SR.Culture = CultureInfo.InvariantCulture;
        return SR.Culture.LCID;
    }

    [Benchmark]
    public int ResetCacheAndRead()
    {
        SR.Culture = CultureInfo.InvariantCulture;
        return SR.ErrorString.Length;
    }

    [Benchmark]
    public int ResetCacheAfterDifferentManagerKeyAndRead()
    {
        SR.Culture = CultureInfo.InvariantCulture;
        string? other = SR.ResourceManager.GetString(
            nameof(SR.Argument_BadFormatSpecifier),
            CultureInfo.InvariantCulture);
        return SR.ErrorString.Length
            + (other?.Length ?? throw new MissingManifestResourceException());
    }
}