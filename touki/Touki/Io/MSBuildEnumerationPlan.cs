// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Carries either a terminal enumeration result or a lazy enumerator with invalid exclude specifications.
/// </summary>
internal readonly struct MSBuildEnumerationPlan
{
    public MSBuildEnumerationPlan(MSBuildEnumerationResult result)
    {
        Result = result;
        Enumerator = null;
        InvalidExcludeSpecifications = [];
    }

    public MSBuildEnumerationPlan(
        MSBuildEnumerator enumerator,
        string[] invalidExcludeSpecifications)
    {
        Result = null;
        Enumerator = enumerator;
        InvalidExcludeSpecifications = invalidExcludeSpecifications;
    }

    public MSBuildEnumerationResult? Result { get; }

    public MSBuildEnumerator? Enumerator { get; }

    public string[] InvalidExcludeSpecifications { get; }
}
