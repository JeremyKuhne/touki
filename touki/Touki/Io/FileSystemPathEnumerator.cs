// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Enumerates canonical root-relative paths selected by a reusable file-system matcher.
/// </summary>
public sealed class FileSystemPathEnumerator : FileSystemMatchEnumerator<string>
{
    private readonly int _rootPrefixLength;

    private FileSystemPathEnumerator(
        string rootDirectory,
        IFileSystemMatcher matcher,
        EnumerationOptions? options)
        : base(rootDirectory, matcher, options)
    {
        _rootPrefixLength = EnumerationRootDirectory.Length
            + (Path.EndsInDirectorySeparator(EnumerationRootDirectory) ? 0 : 1);
    }

    /// <summary>
    ///  Creates an enumerator for files beneath <paramref name="rootDirectory"/>.
    /// </summary>
    public static FileSystemPathEnumerator Create(
        string rootDirectory,
        IFileSystemMatcher matcher,
        EnumerationOptions? options = null) => new(rootDirectory, matcher, options);

    /// <inheritdoc/>
    protected override string TransformEntry(ref FileSystemEntry entry)
    {
        if (entry.Directory.Length <= _rootPrefixLength)
        {
            return entry.FileName.ToString();
        }

        ReadOnlySpan<char> relativeDirectory = entry.Directory[_rootPrefixLength..];
        using ValueStringBuilder builder = new(stackalloc char[256]);
        char separator = Path.DirectorySeparatorChar;
        for (int index = 0; index < relativeDirectory.Length; index++)
        {
            char character = relativeDirectory[index];
            builder.Append(character == separator ? '/' : character);
        }

        builder.Append('/');
        builder.Append(entry.FileName);
        return builder.ToString();
    }
}