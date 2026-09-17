// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Reflection;

namespace Touki.Analyzers;

[TestClass]
public class AnalyzerHostCompatibilityTests
{
    private static readonly Version s_maximumRoslynReferenceVersion = new(4, 14, 0, 0);

    [TestMethod]
    public void GetReferencedAssemblies_ShippedAnalyzers_DoNotRequireNewerRoslyn()
    {
        Assembly[] assemblies =
        [
            typeof(UseTextWriterWriteFormattedAnalyzer).Assembly,
            typeof(UseTextWriterWriteFormattedCodeFixProvider).Assembly
        ];

        foreach (Assembly assembly in assemblies)
        {
            AssemblyName[] roslynReferences =
            [
                .. assembly.GetReferencedAssemblies().Where(reference =>
                    reference.Name?.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal) is true)
            ];

            roslynReferences.Should().NotBeEmpty();
            foreach (AssemblyName reference in roslynReferences)
            {
                Version version = reference.Version
                    ?? throw new InvalidOperationException($"{reference.Name} has no assembly version.");
                version.CompareTo(s_maximumRoslynReferenceVersion).Should().BeLessThanOrEqualTo(0);
            }
        }
    }
}