// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Carries either a terminal enumeration result or a lazy enumerator with invalid exclude specifications.
/// </summary>
internal readonly struct MSBuildEnumerationPlan
{
    /// <summary>
    ///  Initializes a plan containing a terminal result.
    /// </summary>
    /// <param name="result">The terminal result.</param>
    public MSBuildEnumerationPlan(MSBuildEnumerationResult result)
    {
        Result = result;
        Enumerator = null;
        InvalidExcludeSpecifications = [];
    }

    /// <summary>
    ///  Initializes a plan containing a lazy enumerator.
    /// </summary>
    /// <param name="enumerator">The owned lazy enumerator.</param>
    /// <param name="invalidExcludeSpecifications">The exclude specifications that could not be parsed.</param>
    public MSBuildEnumerationPlan(
        MSBuildEnumerator enumerator,
        string[] invalidExcludeSpecifications)
    {
        Result = null;
        Enumerator = enumerator;
        InvalidExcludeSpecifications = invalidExcludeSpecifications;
    }

    /// <summary>
    ///  Gets the terminal result, or <see langword="null"/> for a search plan.
    /// </summary>
    public MSBuildEnumerationResult? Result { get; }

    /// <summary>
    ///  Gets the lazy enumerator, or <see langword="null"/> for a terminal result.
    /// </summary>
    public MSBuildEnumerator? Enumerator { get; }

    /// <summary>
    ///  Gets the exclude specifications that could not be parsed.
    /// </summary>
    public string[] InvalidExcludeSpecifications { get; }
}
