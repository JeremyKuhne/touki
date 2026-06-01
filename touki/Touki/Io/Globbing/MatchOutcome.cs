// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Tri-state result of evaluating a compiled glob against a directory candidate.
/// </summary>
/// <remarks>
///  <para>
///   Returned by <see cref="GlobStrategy.MatchDirectory"/>. Unlike the boolean
///   file-level <see cref="GlobStrategy.MatchCore"/>, the directory query needs a
///   third state so a caller can tell &quot;keep descending&quot; apart from
///   &quot;this subtree is provably excluded by a negation and can be pruned&quot;.
///  </para>
/// </remarks>
internal enum MatchOutcome
{
    /// <summary>
    ///  The candidate is a viable prefix - some descendant could still match, so the
    ///  enumerator must keep descending. This is also the conservative default when
    ///  the pattern carries no negation to reason about.
    /// </summary>
    None = 0,

    /// <summary>
    ///  The candidate directory path is itself a complete match of the pattern.
    /// </summary>
    Positive,

    /// <summary>
    ///  An anchored negation in the pattern excludes one of the candidate's
    ///  segments, so no descendant under it can ever match and the whole subtree may
    ///  be pruned.
    /// </summary>
    Negative,
}
