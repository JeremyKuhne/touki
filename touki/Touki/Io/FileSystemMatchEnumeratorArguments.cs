// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Snapshots a normalized enumeration root, matcher, and options for constructing a file-system match enumerator.
/// </summary>
internal readonly struct FileSystemMatchEnumeratorArguments
{
    public FileSystemMatchEnumeratorArguments(
        string rootDirectory,
        IFileSystemMatcher matcher,
        EnumerationOptions? options)
    {
        ArgumentNullException.ThrowIfNull(rootDirectory);
        ArgumentNullException.ThrowIfNull(matcher);

        string normalizedRoot = NormalizeRootDirectory(rootDirectory);
        RootDirectory = normalizedRoot;
        Matcher = matcher;
        Options = options is null ? CreateDefaultOptions() : SnapshotOptions(options);
    }

    public static string NormalizeRootDirectory(string rootDirectory)
    {
        string normalizedRoot = Path.GetFullPath(rootDirectory);
        ReadOnlySpan<char> pathRoot = Path.GetPathRoot(normalizedRoot.AsSpan());
        if (Path.EndsInDirectorySeparator(normalizedRoot)
            && normalizedRoot.Length > pathRoot.Length)
        {
            normalizedRoot = normalizedRoot[..^1];
        }

        return normalizedRoot;
    }

    public string RootDirectory { get; }

    public IFileSystemMatcher Matcher { get; }

    public EnumerationOptions Options { get; }

    private static EnumerationOptions CreateDefaultOptions() => new()
    {
        IgnoreInaccessible = true,
        MatchCasing = MatchCasing.PlatformDefault,
        MatchType = MatchType.Simple,
        RecurseSubdirectories = true
    };

    private static EnumerationOptions SnapshotOptions(EnumerationOptions options) => new()
    {
        AttributesToSkip = options.AttributesToSkip,
        BufferSize = options.BufferSize,
        IgnoreInaccessible = options.IgnoreInaccessible,
        MatchCasing = options.MatchCasing,
        MatchType = options.MatchType,
        MaxRecursionDepth = options.MaxRecursionDepth,
        RecurseSubdirectories = options.RecurseSubdirectories,
        ReturnSpecialDirectories = options.ReturnSpecialDirectories
    };

}