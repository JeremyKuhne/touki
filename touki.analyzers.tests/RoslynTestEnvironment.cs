// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.CodeAnalysis.Diagnostics;

namespace Touki.Analyzers;

internal static class RoslynTestEnvironment
{
    private static readonly Lazy<ImmutableArray<MetadataReference>> s_references = new(CreateReferences);
    private static readonly Lazy<ImmutableArray<MetadataReference>> s_net472References = new(CreateNet472References);

    /// <summary>
    ///  Gets metadata references for the trusted platform assemblies of the current test process.
    /// </summary>
    public static ImmutableArray<MetadataReference> References => s_references.Value;

    /// <summary>
    ///  Gets the platform references followed by any test-specific references.
    /// </summary>
    public static ImmutableArray<MetadataReference> GetReferences(
        IReadOnlyCollection<MetadataReference>? additionalReferences) =>
        additionalReferences is null || additionalReferences.Count == 0
            ? References
            : References.AddRange(additionalReferences);

    /// <summary>
    ///  Gets the real net472 reference assemblies and the Microsoft.IO.Redist dependency closure.
    /// </summary>
    public static ImmutableArray<MetadataReference> Net472References => s_net472References.Value;

    /// <summary>
    ///  Creates analyzer options backed by the supplied EditorConfig values.
    /// </summary>
    public static AnalyzerOptions CreateAnalyzerOptions(IReadOnlyDictionary<string, string>? options) =>
        new(
            additionalFiles: [],
            optionsProvider: new TestAnalyzerConfigOptionsProvider(
                options is null ? TestAnalyzerConfigOptions.Empty : new TestAnalyzerConfigOptions(options)));

    /// <summary>
    ///  Applies per-diagnostic reporting options to <paramref name="compilation"/>.
    /// </summary>
    /// <returns>
    ///  The original compilation when no options are supplied; otherwise, a compilation with the options applied.
    /// </returns>
    public static Compilation ApplyDiagnosticOptions(
        Compilation compilation,
        IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions) =>
        diagnosticOptions is null
            ? compilation
            : compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions(diagnosticOptions));

    private static ImmutableArray<MetadataReference> CreateReferences()
    {
        string trustedAssemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        string toukiAssembly = typeof(Touki.Io.TextWriterExtensions).Assembly.Location;

        return
        [
            .. trustedAssemblies
                .Split(Path.PathSeparator)
                .Where(path => path.Length > 0)
                .Where(path => !string.Equals(path, toukiAssembly, StringComparison.OrdinalIgnoreCase))
                .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
        ];
    }

    private static ImmutableArray<MetadataReference> CreateNet472References()
    {
        string packages = typeof(RoslynTestEnvironment).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), inherit: false)
            .Cast<System.Reflection.AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "NuGetPackageRoot")
            .Value!;
        string framework = Path.Join(
            packages,
            "microsoft.netframework.referenceassemblies.net472",
            "1.0.3",
            "build",
            ".NETFramework",
            "v4.7.2");
        string[] paths =
        [
            Path.Join(framework, "mscorlib.dll"),
            Path.Join(framework, "System.dll"),
            Path.Join(framework, "System.Core.dll"),
            Path.Join(packages, "microsoft.io.redist", "6.1.3", "lib", "net472", "Microsoft.IO.Redist.dll"),
            Path.Join(packages, "system.buffers", "4.6.1", "lib", "net462", "System.Buffers.dll"),
            Path.Join(packages, "system.memory", "4.6.3", "lib", "net462", "System.Memory.dll"),
            Path.Join(
                packages,
                "system.numerics.vectors",
                "4.6.1",
                "lib",
                "net462",
                "System.Numerics.Vectors.dll"),
            Path.Join(
                packages,
                "system.runtime.compilerservices.unsafe",
                "6.1.2",
                "lib",
                "net462",
                "System.Runtime.CompilerServices.Unsafe.dll")
        ];

        return [.. paths.Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))];
    }
}