// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System;
using System.Collections.Generic;

namespace Touki.Resources.Generator;

/// <summary>
///  Compares compilation feature sets for incremental generator caching.
/// </summary>
internal sealed class CompilationFeaturesComparer : IEqualityComparer<CompilationFeatures>
{
    /// <summary>
    ///  The shared comparer instance.
    /// </summary>
    internal static readonly CompilationFeaturesComparer s_instance = new();

    /// <inheritdoc/>
    public bool Equals(CompilationFeatures? left, CompilationFeatures? right)
    {
        return ReferenceEquals(left, right)
            || (left is not null
                && right is not null
                && left.AssemblyName == right.AssemblyName
                && left.SupportsNullable == right.SupportsNullable
                && left.HasAggressiveInlining == right.HasAggressiveInlining
                && left.HasNotNullIfNotNull == right.HasNotNullIfNotNull);
    }

    /// <inheritdoc/>
    public int GetHashCode(CompilationFeatures value)
    {
        unchecked
        {
            int hash = value.AssemblyName is null
                ? 0
                : StringComparer.Ordinal.GetHashCode(value.AssemblyName);

            hash = (hash * 397) ^ value.SupportsNullable.GetHashCode();
            hash = (hash * 397) ^ value.HasAggressiveInlining.GetHashCode();
            return (hash * 397) ^ value.HasNotNullIfNotNull.GetHashCode();
        }
    }
}