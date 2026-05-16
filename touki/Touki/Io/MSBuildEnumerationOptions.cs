// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Configuration for <see cref="MSBuildEnumerator.CreateResult(string, string?, string?, MSBuildEnumerationOptions?)"/>.
///  Composes a standard <see cref="EnumerationOptions"/> with Touki-specific safety flags.
/// </summary>
public sealed class MSBuildEnumerationOptions
{
    /// <summary>
    ///  Underlying filesystem enumeration options (match type, casing, recursion, etc.).
    ///  Each <see cref="MSBuildEnumerationOptions"/> instance gets its own fresh
    ///  <see cref="EnumerationOptions"/> when constructed without an explicit value, so mutating it
    ///  never bleeds into unrelated callers.
    /// </summary>
    public EnumerationOptions EnumerationOptions { get; init; } = CreateDefaultEnumerationOptions();

    /// <summary>
    ///  When <see langword="false"/> (the default) an include that would recursively enumerate an entire
    ///  drive or share (e.g. <c>C:\**</c>, <c>/**</c>, <c>\\server\share\**</c>) is refused; the
    ///  resulting <see cref="MSBuildEnumerationResult.Action"/> is
    ///  <see cref="MSBuildSearchAction.FailBecauseDriveEnumerationIsForbidden"/>. Set to
    ///  <see langword="true"/> to opt in. Matches MSBuild's default behavior of
    ///  <c>MSBUILDDISABLEDRIVEENUMERATIONONWILDCARDS</c>.
    /// </summary>
    public bool AllowDriveEnumeration { get; init; }

    private static EnumerationOptions CreateDefaultEnumerationOptions() => new()
    {
        MatchType = MatchType.Simple,
        MatchCasing = MatchCasing.PlatformDefault,
        IgnoreInaccessible = true,
        RecurseSubdirectories = true
    };
}
