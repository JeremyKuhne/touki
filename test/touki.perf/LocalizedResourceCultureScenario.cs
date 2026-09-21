// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace touki.perf;

/// <summary>
///  The culture fallback shape used by localized resource benchmarks.
/// </summary>
public enum LocalizedResourceCultureScenario
{
    /// <summary>
    ///  The requested culture has a localized resource.
    /// </summary>
    Exact,

    /// <summary>
    ///  The requested culture falls back to a localized parent culture.
    /// </summary>
    Parent,

    /// <summary>
    ///  The requested culture has no localized resource in its parent chain.
    /// </summary>
    Missing
}