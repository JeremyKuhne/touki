// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Returns an owned matcher session together with the directory from which enumeration can begin.
/// </summary>
/// <param name="session">The owned matcher session.</param>
/// <param name="startDirectory">The directory from which enumeration can begin.</param>
internal readonly struct MSBuildMatchBuildResult(
    IFileSystemMatcherSession session,
    StringSegment startDirectory)
{
    /// <summary>
    ///  Gets the owned matcher session.
    /// </summary>
    public IFileSystemMatcherSession Session { get; } = session;

    /// <summary>
    ///  Gets the directory from which enumeration can begin.
    /// </summary>
    public StringSegment StartDirectory { get; } = startDirectory;
}
