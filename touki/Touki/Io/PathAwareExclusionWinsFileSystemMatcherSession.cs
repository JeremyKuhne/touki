// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

internal sealed class PathAwareExclusionWinsFileSystemMatcherSession(
    IFileSystemMatcherSession[] includes,
    IFileSystemMatcherSession[] excludes,
    string rootDirectory) : ExclusionWinsFileSystemMatcherSessionBase(includes, excludes)
{
    private const int StackPathBufferSize = 256;

    private readonly int _rootPrefixLength = rootDirectory.Length
        + (Path.EndsInDirectorySeparator(rootDirectory) ? 0 : 1);

    [SkipLocalsInit]
    public override bool MatchesFile(
        ReadOnlySpan<char> currentDirectory,
        ReadOnlySpan<char> fileName)
    {
        for (int index = 0; index < Excludes.Length; index++)
        {
            IFileSystemMatcherSession exclude = Excludes[index];
            if (exclude is not ICanonicalPathMatcherSession
                && exclude.MatchesFile(currentDirectory, fileName))
            {
                return false;
            }
        }

        bool included = false;
        for (int index = 0; index < Includes.Length; index++)
        {
            IFileSystemMatcherSession include = Includes[index];
            if (include is not ICanonicalPathMatcherSession
                && include.MatchesFile(currentDirectory, fileName))
            {
                included = true;
                break;
            }
        }

        using CanonicalPathScope path = new(
            stackalloc char[StackPathBufferSize],
            _rootPrefixLength,
            currentDirectory,
            fileName);
        for (int index = 0; index < Excludes.Length; index++)
        {
            if (Excludes[index] is ICanonicalPathMatcherSession exclude
                && exclude.MatchesPath(path.Value))
            {
                return false;
            }
        }

        if (included)
        {
            return true;
        }

        for (int index = 0; index < Includes.Length; index++)
        {
            if (Includes[index] is ICanonicalPathMatcherSession include
                && include.MatchesPath(path.Value))
            {
                return true;
            }
        }

        return false;
    }
}
