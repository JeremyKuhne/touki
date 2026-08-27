// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Provides a canonical root-relative file path with directory separators normalized to <c>/</c>,
///  using scoped storage when the directory and file name must be combined.
/// </summary>
[NonCopyable]
internal ref struct CanonicalPathScope
{
    private BufferScope<char> _buffer;

    public CanonicalPathScope(
        Span<char> initialBuffer,
        int rootPrefixLength,
        ReadOnlySpan<char> currentDirectory,
        ReadOnlySpan<char> fileName)
    {
        ReadOnlySpan<char> relativeDirectory = currentDirectory.Length >= rootPrefixLength
            ? currentDirectory[rootPrefixLength..]
            : default;
        if (relativeDirectory.IsEmpty)
        {
            _buffer = default;
            Value = fileName;
            return;
        }

        int pathLength = checked(relativeDirectory.Length + 1 + fileName.Length);
        _buffer = new(initialBuffer, pathLength);
        Span<char> path = _buffer[..pathLength];
        CopyCanonicalDirectory(relativeDirectory, path);
        path[relativeDirectory.Length] = '/';
        fileName.CopyTo(path[(relativeDirectory.Length + 1)..]);
        Value = path;
    }

    public ReadOnlySpan<char> Value { get; }

    public void Dispose() => _buffer.Dispose();

    private static void CopyCanonicalDirectory(
        ReadOnlySpan<char> source,
        Span<char> destination)
    {
        char primarySeparator = Path.DirectorySeparatorChar;
        char alternateSeparator = Path.AltDirectorySeparatorChar;
        ref char sourceReference = ref MemoryMarshal.GetReference(source);
        ref char destinationReference = ref MemoryMarshal.GetReference(destination);
        for (int index = 0; index < source.Length; index++)
        {
            char character = Unsafe.Add(ref sourceReference, index);
            Unsafe.Add(ref destinationReference, index) =
                character == primarySeparator || character == alternateSeparator
                    ? '/'
                    : character;
        }
    }
}
