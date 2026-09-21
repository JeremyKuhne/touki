// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Touki.Resources;

namespace touki.perf;

/// <summary>
///  Compares generated resource-property caching strategies over representative Microsoft.Build
///  strings on .NET Framework 4.8.1 RyuJIT and modern .NET RyuJIT.
/// </summary>
[MemoryDiagnoser]
public class MSBuildGeneratedResourceWrapperPerf
{
    [GlobalSetup]
    public void Setup()
    {
        BclResources.Culture = CultureInfo.InvariantCulture;
        ToukiResources.Culture = CultureInfo.InvariantCulture;
        CachedToukiResources.Culture = CultureInfo.InvariantCulture;
        CompactCachedToukiResources.Culture = CultureInfo.InvariantCulture;

        _ = ReadBclProperties();
        _ = ReadToukiProperties();
        _ = ReadCachedToukiProperties();
        _ = ReadCompactCachedToukiProperties();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        BclResources.Release();
        ToukiResources.Release();
        CachedToukiResources.Release();
        CompactCachedToukiResources.Release();
    }

    [Benchmark(Baseline = true)]
    public int Bcl_WarmedProperties() => ReadBclProperties();

    [Benchmark]
    public int Touki_WarmedProperties() => ReadToukiProperties();

    [Benchmark]
    public int CachedTouki_WarmedProperties() => ReadCachedToukiProperties();

    [Benchmark]
    public int CompactCachedTouki_WarmedProperties() => ReadCompactCachedToukiProperties();

    [Benchmark]
    public int CachedTouki_FirstProperties()
    {
        CachedToukiResources.ResetCache();
        return ReadCachedToukiProperties();
    }

    [Benchmark]
    public int CompactCachedTouki_FirstProperties()
    {
        CompactCachedToukiResources.ResetCache();
        return ReadCompactCachedToukiProperties();
    }

    private static int ReadBclProperties() =>
        BclResources.AbortingBuild.Length
        + BclResources.BuildCheck_BC0106_MessageFmt.Length
        + BclResources.ConflictingValuesOfMSBuildToolsPath.Length
        + BclResources.IllFormedPropertyCloseParenthesisInCondition.Length
        + BclResources.OM_ItemsOutsideTargetMustHaveIncludeOrUpdateOrRemove.Length
        + BclResources.ProjectImportSkippedExpressionEvaluatedToEmpty.Length
        + BclResources.SdkEnvironmentVariableAlreadySetBySdk.Length
        + BclResources.UsingDifferentToolsVersionFromProjectFile.Length;

    private static int ReadToukiProperties() =>
        ToukiResources.AbortingBuild.Length
        + ToukiResources.BuildCheck_BC0106_MessageFmt.Length
        + ToukiResources.ConflictingValuesOfMSBuildToolsPath.Length
        + ToukiResources.IllFormedPropertyCloseParenthesisInCondition.Length
        + ToukiResources.OM_ItemsOutsideTargetMustHaveIncludeOrUpdateOrRemove.Length
        + ToukiResources.ProjectImportSkippedExpressionEvaluatedToEmpty.Length
        + ToukiResources.SdkEnvironmentVariableAlreadySetBySdk.Length
        + ToukiResources.UsingDifferentToolsVersionFromProjectFile.Length;

    private static int ReadCachedToukiProperties() =>
        CachedToukiResources.AbortingBuild.Length
        + CachedToukiResources.BuildCheck_BC0106_MessageFmt.Length
        + CachedToukiResources.ConflictingValuesOfMSBuildToolsPath.Length
        + CachedToukiResources.IllFormedPropertyCloseParenthesisInCondition.Length
        + CachedToukiResources.OM_ItemsOutsideTargetMustHaveIncludeOrUpdateOrRemove.Length
        + CachedToukiResources.ProjectImportSkippedExpressionEvaluatedToEmpty.Length
        + CachedToukiResources.SdkEnvironmentVariableAlreadySetBySdk.Length
        + CachedToukiResources.UsingDifferentToolsVersionFromProjectFile.Length;

    private static int ReadCompactCachedToukiProperties() =>
        CompactCachedToukiResources.AbortingBuild.Length
        + CompactCachedToukiResources.BuildCheck_BC0106_MessageFmt.Length
        + CompactCachedToukiResources.ConflictingValuesOfMSBuildToolsPath.Length
        + CompactCachedToukiResources.IllFormedPropertyCloseParenthesisInCondition.Length
        + CompactCachedToukiResources.OM_ItemsOutsideTargetMustHaveIncludeOrUpdateOrRemove.Length
        + CompactCachedToukiResources.ProjectImportSkippedExpressionEvaluatedToEmpty.Length
        + CompactCachedToukiResources.SdkEnvironmentVariableAlreadySetBySdk.Length
        + CompactCachedToukiResources.UsingDifferentToolsVersionFromProjectFile.Length;

    private static class BclResources
    {
        private static ResourceManager? s_resourceManager;

        private static ResourceManager ResourceManager =>
            s_resourceManager ??= new(
                "Microsoft.Build.Strings",
                typeof(Microsoft.Build.Evaluation.Project).Assembly);

        internal static CultureInfo? Culture { get; set; }

        internal static string AbortingBuild => GetResourceString(nameof(AbortingBuild));
        internal static string BuildCheck_BC0106_MessageFmt => GetResourceString(nameof(BuildCheck_BC0106_MessageFmt));
        internal static string ConflictingValuesOfMSBuildToolsPath => GetResourceString(nameof(ConflictingValuesOfMSBuildToolsPath));
        internal static string IllFormedPropertyCloseParenthesisInCondition => GetResourceString(nameof(IllFormedPropertyCloseParenthesisInCondition));
        internal static string OM_ItemsOutsideTargetMustHaveIncludeOrUpdateOrRemove => GetResourceString(nameof(OM_ItemsOutsideTargetMustHaveIncludeOrUpdateOrRemove));
        internal static string ProjectImportSkippedExpressionEvaluatedToEmpty => GetResourceString(nameof(ProjectImportSkippedExpressionEvaluatedToEmpty));
        internal static string SdkEnvironmentVariableAlreadySetBySdk => GetResourceString(nameof(SdkEnvironmentVariableAlreadySetBySdk));
        internal static string UsingDifferentToolsVersionFromProjectFile => GetResourceString(nameof(UsingDifferentToolsVersionFromProjectFile));

        internal static void Release() => s_resourceManager?.ReleaseAllResources();

        private static string GetResourceString(string name) =>
            ResourceManager.GetString(name, Culture)
                ?? throw new InvalidOperationException("The benchmark resource is missing.");
    }

    private static class ToukiResources
    {
        private static StringResourceManager? s_resourceManager;

        private static StringResourceManager ResourceManager =>
            s_resourceManager ??= new(
                "Microsoft.Build.Strings",
                typeof(Microsoft.Build.Evaluation.Project).Assembly);

        internal static CultureInfo? Culture { get; set; }

        internal static string AbortingBuild => GetResourceString(nameof(AbortingBuild));
        internal static string BuildCheck_BC0106_MessageFmt => GetResourceString(nameof(BuildCheck_BC0106_MessageFmt));
        internal static string ConflictingValuesOfMSBuildToolsPath => GetResourceString(nameof(ConflictingValuesOfMSBuildToolsPath));
        internal static string IllFormedPropertyCloseParenthesisInCondition => GetResourceString(nameof(IllFormedPropertyCloseParenthesisInCondition));
        internal static string OM_ItemsOutsideTargetMustHaveIncludeOrUpdateOrRemove => GetResourceString(nameof(OM_ItemsOutsideTargetMustHaveIncludeOrUpdateOrRemove));
        internal static string ProjectImportSkippedExpressionEvaluatedToEmpty => GetResourceString(nameof(ProjectImportSkippedExpressionEvaluatedToEmpty));
        internal static string SdkEnvironmentVariableAlreadySetBySdk => GetResourceString(nameof(SdkEnvironmentVariableAlreadySetBySdk));
        internal static string UsingDifferentToolsVersionFromProjectFile => GetResourceString(nameof(UsingDifferentToolsVersionFromProjectFile));

        internal static void Release() => s_resourceManager?.ReleaseAllResources();

        private static string GetResourceString(string name) =>
            ResourceManager.GetString(name, Culture)
                ?? throw new InvalidOperationException("The benchmark resource is missing.");
    }

    private static class CachedToukiResources
    {
        private static StringResourceManager? s_resourceManager;

        private static StringResourceManager ResourceManager =>
            s_resourceManager ??= new(
                "Microsoft.Build.Strings",
                typeof(Microsoft.Build.Evaluation.Project).Assembly);

        private static Cache s_cache = new(culture: null);

        internal static CultureInfo? Culture
        {
            get => s_cache.Culture;
            set => s_cache = new(value);
        }

        internal static string AbortingBuild
        {
            get => s_cache.AbortingBuild ??= GetResourceString(nameof(AbortingBuild), Culture);
        }

        internal static string BuildCheck_BC0106_MessageFmt
        {
            get => s_cache.BuildCheck_BC0106_MessageFmt ??= GetResourceString(
                nameof(BuildCheck_BC0106_MessageFmt),
                Culture);
        }

        internal static string ConflictingValuesOfMSBuildToolsPath
        {
            get => s_cache.ConflictingValuesOfMSBuildToolsPath ??= GetResourceString(
                nameof(ConflictingValuesOfMSBuildToolsPath),
                Culture);
        }

        internal static string IllFormedPropertyCloseParenthesisInCondition
        {
            get => s_cache.IllFormedPropertyCloseParenthesisInCondition ??= GetResourceString(
                nameof(IllFormedPropertyCloseParenthesisInCondition),
                Culture);
        }

        internal static string OM_ItemsOutsideTargetMustHaveIncludeOrUpdateOrRemove
        {
            get => s_cache.OM_ItemsOutsideTargetMustHaveIncludeOrUpdateOrRemove ??= GetResourceString(
                nameof(OM_ItemsOutsideTargetMustHaveIncludeOrUpdateOrRemove),
                Culture);
        }

        internal static string ProjectImportSkippedExpressionEvaluatedToEmpty
        {
            get => s_cache.ProjectImportSkippedExpressionEvaluatedToEmpty ??= GetResourceString(
                nameof(ProjectImportSkippedExpressionEvaluatedToEmpty),
                Culture);
        }

        internal static string SdkEnvironmentVariableAlreadySetBySdk
        {
            get => s_cache.SdkEnvironmentVariableAlreadySetBySdk ??= GetResourceString(
                nameof(SdkEnvironmentVariableAlreadySetBySdk),
                Culture);
        }

        internal static string UsingDifferentToolsVersionFromProjectFile
        {
            get => s_cache.UsingDifferentToolsVersionFromProjectFile ??= GetResourceString(
                nameof(UsingDifferentToolsVersionFromProjectFile),
                Culture);
        }

        internal static void Release()
        {
            ResetCache();
            s_resourceManager?.ReleaseAllResources();
        }

        internal static void ResetCache() => s_cache = new(Culture);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string GetResourceString(string name, CultureInfo? culture) =>
            ResourceManager.GetString(name, culture)
                ?? throw new MissingManifestResourceException("The benchmark resource is missing.");

        [StructLayout(LayoutKind.Sequential)]
        private sealed class Cache
        {
            internal Cache(CultureInfo? culture) => Culture = culture;

            internal CultureInfo? Culture { get; }

            internal string? AbortingBuild;
#pragma warning disable CS0649 // Padding fields model generated cache slots and intentionally remain null.
            internal Padding72 Padding1;
            internal string? BuildCheck_BC0106_MessageFmt;
            internal Padding72 Padding2;
            internal string? ConflictingValuesOfMSBuildToolsPath;
            internal Padding72 Padding3;
            internal string? IllFormedPropertyCloseParenthesisInCondition;
            internal Padding72 Padding4;
            internal string? OM_ItemsOutsideTargetMustHaveIncludeOrUpdateOrRemove;
            internal Padding72 Padding5;
            internal string? ProjectImportSkippedExpressionEvaluatedToEmpty;
            internal Padding72 Padding6;
            internal string? SdkEnvironmentVariableAlreadySetBySdk;
            internal Padding72 Padding7;
            internal string? UsingDifferentToolsVersionFromProjectFile;
            internal Padding75 Padding8;
#pragma warning restore CS0649
        }

        [StructLayout(LayoutKind.Sequential, Size = 72 * sizeof(long))]
    #pragma warning disable CA1815 // Padding exists only to model generated cache field offsets.
        private struct Padding72
        {
        }

        [StructLayout(LayoutKind.Sequential, Size = 75 * sizeof(long))]
        private struct Padding75
        {
        }
    #pragma warning restore CA1815
    }

    private static class CompactCachedToukiResources
    {
        private static StringResourceManager? s_resourceManager;

        private static StringResourceManager ResourceManager =>
            s_resourceManager ??= new(
                "Microsoft.Build.Strings",
                typeof(Microsoft.Build.Evaluation.Project).Assembly);

        private static Cache s_cache = new(culture: null);

        internal static CultureInfo? Culture
        {
            get => s_cache.Culture;
            set => s_cache = new(value);
        }

        internal static string AbortingBuild
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => GetCachedResourceString(ref s_cache.AbortingBuild, nameof(AbortingBuild));
        }

        internal static string BuildCheck_BC0106_MessageFmt
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => GetCachedResourceString(
                ref s_cache.BuildCheck_BC0106_MessageFmt,
                nameof(BuildCheck_BC0106_MessageFmt));
        }

        internal static string ConflictingValuesOfMSBuildToolsPath
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => GetCachedResourceString(
                ref s_cache.ConflictingValuesOfMSBuildToolsPath,
                nameof(ConflictingValuesOfMSBuildToolsPath));
        }

        internal static string IllFormedPropertyCloseParenthesisInCondition
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => GetCachedResourceString(
                ref s_cache.IllFormedPropertyCloseParenthesisInCondition,
                nameof(IllFormedPropertyCloseParenthesisInCondition));
        }

        internal static string OM_ItemsOutsideTargetMustHaveIncludeOrUpdateOrRemove
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => GetCachedResourceString(
                ref s_cache.OM_ItemsOutsideTargetMustHaveIncludeOrUpdateOrRemove,
                nameof(OM_ItemsOutsideTargetMustHaveIncludeOrUpdateOrRemove));
        }

        internal static string ProjectImportSkippedExpressionEvaluatedToEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => GetCachedResourceString(
                ref s_cache.ProjectImportSkippedExpressionEvaluatedToEmpty,
                nameof(ProjectImportSkippedExpressionEvaluatedToEmpty));
        }

        internal static string SdkEnvironmentVariableAlreadySetBySdk
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => GetCachedResourceString(
                ref s_cache.SdkEnvironmentVariableAlreadySetBySdk,
                nameof(SdkEnvironmentVariableAlreadySetBySdk));
        }

        internal static string UsingDifferentToolsVersionFromProjectFile
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => GetCachedResourceString(
                ref s_cache.UsingDifferentToolsVersionFromProjectFile,
                nameof(UsingDifferentToolsVersionFromProjectFile));
        }

        internal static void Release()
        {
            ResetCache();
            s_resourceManager?.ReleaseAllResources();
        }

        internal static void ResetCache() => s_cache = new(Culture);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string GetCachedResourceString(ref string? value, string name)
        {
            return value ??= ResourceManager.GetString(name, Culture)
                ?? throw new MissingManifestResourceException("The benchmark resource is missing.");
        }

        [StructLayout(LayoutKind.Sequential)]
        private sealed class Cache
        {
            internal Cache(CultureInfo? culture) => Culture = culture;

            internal CultureInfo? Culture { get; }

            internal string? AbortingBuild;
#pragma warning disable CS0649 // Padding fields model generated cache slots and intentionally remain null.
            internal Padding72 Padding1;
            internal string? BuildCheck_BC0106_MessageFmt;
            internal Padding72 Padding2;
            internal string? ConflictingValuesOfMSBuildToolsPath;
            internal Padding72 Padding3;
            internal string? IllFormedPropertyCloseParenthesisInCondition;
            internal Padding72 Padding4;
            internal string? OM_ItemsOutsideTargetMustHaveIncludeOrUpdateOrRemove;
            internal Padding72 Padding5;
            internal string? ProjectImportSkippedExpressionEvaluatedToEmpty;
            internal Padding72 Padding6;
            internal string? SdkEnvironmentVariableAlreadySetBySdk;
            internal Padding72 Padding7;
            internal string? UsingDifferentToolsVersionFromProjectFile;
            internal Padding75 Padding8;
#pragma warning restore CS0649
        }

        [StructLayout(LayoutKind.Sequential, Size = 72 * sizeof(long))]
    #pragma warning disable CA1815 // Padding exists only to model generated cache field offsets.
        private struct Padding72
        {
        }

        [StructLayout(LayoutKind.Sequential, Size = 75 * sizeof(long))]
        private struct Padding75
        {
        }
    #pragma warning restore CA1815
    }
}