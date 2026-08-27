// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Returns an owned matcher session together with the directory from which enumeration can begin.
/// </summary>
internal readonly struct MSBuildMatchBuildResult(
    IFileSystemMatcherSession session,
    StringSegment startDirectory)
{
    public IFileSystemMatcherSession Session { get; } = session;

    public StringSegment StartDirectory { get; } = startDirectory;
}
