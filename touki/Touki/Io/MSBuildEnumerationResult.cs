// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Whole-search result returned by <see cref="MSBuildEnumerator.CreateResult(string, string?, string?, MSBuildEnumerationOptions?)"/>.
///  Mirrors the 4-tuple returned by MSBuild's internal <c>FileMatcher.GetFiles</c>.
/// </summary>
/// <remarks>
///  <para>
///   When <see cref="Action"/> is <see cref="MSBuildSearchAction.RunSearch"/>, <see cref="Enumerator"/>
///   is non-<see langword="null"/>; iterate it (and dispose it) to consume matches. For any other action
///   <see cref="Enumerator"/> is <see langword="null"/> and <see cref="GlobFailure"/> or
///   <see cref="FailedExcludeSpec"/> describe why the search was abandoned.
///  </para>
/// </remarks>
public readonly struct MSBuildEnumerationResult
{
    /// <summary>
    ///  Lazy enumerator over matched files. <see langword="null"/> when <see cref="Action"/> is not
    ///  <see cref="MSBuildSearchAction.RunSearch"/>. The caller owns disposal.
    /// </summary>
    public MSBuildEnumerator? Enumerator { get; }

    /// <summary>
    ///  Disposition of the enumeration. <see cref="MSBuildSearchAction.RunSearch"/> for the normal path.
    /// </summary>
    public MSBuildSearchAction Action { get; }

    /// <summary>
    ///  When an exclude spec was responsible for stopping the search, the original spec text; otherwise
    ///  <see langword="null"/>.
    /// </summary>
    public string? FailedExcludeSpec { get; }

    /// <summary>
    ///  Human-readable failure description when the include spec itself could not be evaluated; otherwise
    ///  <see langword="null"/>.
    /// </summary>
    public string? GlobFailure { get; }

    internal MSBuildEnumerationResult(
        MSBuildEnumerator? enumerator,
        MSBuildSearchAction action,
        string? failedExcludeSpec,
        string? globFailure)
    {
        Enumerator = enumerator;
        Action = action;
        FailedExcludeSpec = failedExcludeSpec;
        GlobFailure = globFailure;
    }
}
