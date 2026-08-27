// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Matches MSBuild file-name wildcards that require logical trailing-dot handling instead of file-system semantics.
/// </summary>
internal static class MSBuildTrailingDotFileNameMatcher
{
    /// <summary>
    ///  Determines whether a file name matches an MSBuild trailing-dot wildcard pattern.
    /// </summary>
    /// <param name="fileName">The file name to match.</param>
    /// <param name="pattern">The wildcard pattern.</param>
    /// <param name="ignoreCaseKind">The case-folding mode.</param>
    /// <returns><see langword="true"/> if the file name matches; otherwise <see langword="false"/>.</returns>
    public static bool Matches(
        ReadOnlySpan<char> fileName,
        ReadOnlySpan<char> pattern,
        IgnoreCaseKind ignoreCaseKind)
    {
        bool isWindows = IsWindows();
        if (isWindows && IsAll(fileName, '.') && fileName.Length >= 3)
        {
            return IsAll(pattern, '*');
        }

        while (isWindows && fileName.Length > 1 && fileName[^1] == '.')
        {
            fileName = fileName[..^1];
        }

        int stateCount = checked((pattern.Length * 2) + 1);
        using BufferScope<byte> stateBuffer = new(stackalloc byte[256], checked(stateCount * 2));
        Span<byte> activeStates = stateBuffer[..stateCount];
        Span<byte> nextStates = stateBuffer.Slice(stateCount, stateCount);
        activeStates.Clear();
        nextStates.Clear();
        activeStates[0] = 1;
        ApplyStarClosure(activeStates, pattern);

        for (int fileNameIndex = 0; fileNameIndex < fileName.Length; fileNameIndex++)
        {
            char fileNameCharacter = fileName[fileNameIndex];
            bool hasNextState = false;
            nextStates.Clear();

            for (int patternIndex = 0; patternIndex < pattern.Length; patternIndex++)
            {
                int boundaryState = patternIndex * 2;
                char patternCharacter = pattern[patternIndex];
                if (activeStates[boundaryState] != 0)
                {
                    if (patternCharacter == '*')
                    {
                        if (fileNameCharacter != '.')
                        {
                            nextStates[boundaryState] = 1;
                            hasNextState = true;
                        }
                    }
                    else if (patternCharacter == '?')
                    {
                        if (fileNameCharacter != '.')
                        {
                            nextStates[boundaryState + 1] = 1;
                            hasNextState = true;
                        }
                    }
                    else if (CharactersEqual(fileNameCharacter, patternCharacter, ignoreCaseKind))
                    {
                        nextStates[boundaryState + 2] = 1;
                        hasNextState = true;
                    }
                }

                if (activeStates[boundaryState + 1] != 0)
                {
                    nextStates[boundaryState + 2] = 1;
                    hasNextState = true;
                }
            }

            if (!hasNextState)
            {
                return false;
            }

            ApplyStarClosure(nextStates, pattern);
            Span<byte> previousStates = activeStates;
            activeStates = nextStates;
            nextStates = previousStates;
        }

        return activeStates[stateCount - 1] != 0;
    }

    /// <summary>
    ///  Applies Windows trailing-dot normalization before extglob matching.
    /// </summary>
    /// <param name="fileName">The file name to normalize.</param>
    /// <param name="isAllDotInput">Receives whether the input is a Windows all-dot name of at least three dots.</param>
    /// <returns>The normalized file name.</returns>
    internal static ReadOnlySpan<char> NormalizeExtGlobInput(
        ReadOnlySpan<char> fileName,
        out bool isAllDotInput)
    {
        isAllDotInput = false;
        if (!IsWindows())
        {
            return fileName;
        }

        if (IsAll(fileName, '.') && fileName.Length >= 3)
        {
            isAllDotInput = true;
            return fileName;
        }

        while (fileName.Length > 1 && fileName[^1] == '.')
        {
            fileName = fileName[..^1];
        }

        return fileName;
    }

    private static bool IsWindows()
    {
#if NETFRAMEWORK
        return true;
#else
        return OperatingSystem.IsWindows();
#endif
    }

    private static void ApplyStarClosure(Span<byte> states, ReadOnlySpan<char> pattern)
    {
        for (int patternIndex = 0; patternIndex < pattern.Length; patternIndex++)
        {
            int boundaryState = patternIndex * 2;
            if (states[boundaryState] != 0 && pattern[patternIndex] == '*')
            {
                states[boundaryState + 2] = 1;
            }
        }
    }

    private static bool IsAll(ReadOnlySpan<char> value, char character)
    {
        for (int index = 0; index < value.Length; index++)
        {
            if (value[index] != character)
            {
                return false;
            }
        }

        return !value.IsEmpty;
    }

    private static bool CharactersEqual(char left, char right, IgnoreCaseKind ignoreCaseKind) =>
        ignoreCaseKind switch
        {
            IgnoreCaseKind.Ascii => GlobMatcherHelpers.AsciiFoldEquals(left, right),
            IgnoreCaseKind.Unicode => GlobMatcherHelpers.UnicodeFoldEquals(left, right),
            _ => left == right
        };
}