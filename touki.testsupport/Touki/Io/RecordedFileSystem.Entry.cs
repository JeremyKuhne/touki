// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

public sealed partial class RecordedFileSystem
{
    /// <summary>
    ///  A single recorded entry: a file or subdirectory name within a directory.
    /// </summary>
    public readonly struct Entry
    {
        /// <summary>
        ///  Initializes a new instance of the <see cref="Entry"/> struct.
        /// </summary>
        public Entry(string name, bool isDirectory)
        {
            Name = name;
            IsDirectory = isDirectory;
        }

        /// <summary>
        ///  The file or directory name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        ///  <see langword="true"/> when the entry is a directory.
        /// </summary>
        public bool IsDirectory { get; }
    }
}
