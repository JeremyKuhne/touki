// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Evaluates exclusion-wins file matching for sessions that consume separate directory and file-name spans.
/// </summary>
/// <param name="includes">The sessions that include paths.</param>
/// <param name="excludes">The sessions that exclude paths.</param>
internal sealed class ExclusionWinsFileSystemMatcherSession(
    IFileSystemMatcherSession[] includes,
    IFileSystemMatcherSession[] excludes) : ExclusionWinsFileSystemMatcherSessionBase(includes, excludes)
{
    public override bool MatchesFile(
        ReadOnlySpan<char> currentDirectory,
        ReadOnlySpan<char> fileName)
    {
        for (int index = 0; index < Excludes.Length; index++)
        {
            if (Excludes[index].MatchesFile(currentDirectory, fileName))
            {
                return false;
            }
        }

        for (int index = 0; index < Includes.Length; index++)
        {
            if (Includes[index].MatchesFile(currentDirectory, fileName))
            {
                return true;
            }
        }

        return false;
    }
}
