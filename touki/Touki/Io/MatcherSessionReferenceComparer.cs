// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Compares matcher sessions by object identity so compositions can detect shared session ownership.
/// </summary>
internal sealed class MatcherSessionReferenceComparer : IEqualityComparer<IFileSystemMatcherSession>
{
    /// <summary>
    ///  Gets the shared comparer instance.
    /// </summary>
    public static MatcherSessionReferenceComparer Instance { get; } = new();

    private MatcherSessionReferenceComparer()
    {
    }

    /// <summary>
    ///  Determines whether two matcher sessions are the same object.
    /// </summary>
    /// <param name="left">The first session.</param>
    /// <param name="right">The second session.</param>
    /// <returns><see langword="true"/> if the sessions are identical; otherwise <see langword="false"/>.</returns>
    public bool Equals(IFileSystemMatcherSession? left, IFileSystemMatcherSession? right) =>
        ReferenceEquals(left, right);

    /// <summary>
    ///  Gets the identity-based hash code for a matcher session.
    /// </summary>
    /// <param name="session">The matcher session.</param>
    /// <returns>The identity-based hash code.</returns>
    public int GetHashCode(IFileSystemMatcherSession session) =>
        RuntimeHelpers.GetHashCode(session);
}
