// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

internal sealed class MatcherSessionReferenceComparer : IEqualityComparer<IFileSystemMatcherSession>
{
    public static MatcherSessionReferenceComparer Instance { get; } = new();

    private MatcherSessionReferenceComparer()
    {
    }

    public bool Equals(IFileSystemMatcherSession? left, IFileSystemMatcherSession? right) =>
        ReferenceEquals(left, right);

    public int GetHashCode(IFileSystemMatcherSession session) =>
        RuntimeHelpers.GetHashCode(session);
}
