// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Touki.Analyzers;

/// <summary>
///  Dictionary-backed <see cref="AnalyzerConfigOptions"/> that stands in for the values an
///  <c>.editorconfig</c> would supply.
/// </summary>
internal sealed class TestAnalyzerConfigOptions(IReadOnlyDictionary<string, string> options) : AnalyzerConfigOptions
{
    public static TestAnalyzerConfigOptions Empty { get; } = new(new Dictionary<string, string>());

    public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) =>
        options.TryGetValue(key, out value);
}
