// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Evaluates ordered rules over sessions that consume separate directory and file-name spans.
/// </summary>
internal sealed class OrderedFileSystemMatcherSession(
    FileSystemMatchRule[] rules,
    IFileSystemMatcherSession[] sessions,
    bool includeUnmatched) : OrderedFileSystemMatcherSessionBase(rules, sessions, includeUnmatched)
{
    public override bool MatchesFile(
        ReadOnlySpan<char> currentDirectory,
        ReadOnlySpan<char> fileName)
    {
        bool included = IncludeUnmatched;
        for (int index = 0; index < Sessions.Length; index++)
        {
            if (Sessions[index].MatchesFile(currentDirectory, fileName))
            {
                included = Rules[index].Action == FileSystemMatchAction.Include;
            }
        }

        return included;
    }
}
