// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  An in-memory snapshot of a directory tree loaded from a CSV file produced by
///  <see cref="DirectoryEnumerationRecorder"/>. Used to replay an enumeration without
///  touching the file system.
/// </summary>
public sealed class RecordedFileSystem
{
    private readonly Dictionary<string, List<Entry>> _directories;

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

    private RecordedFileSystem(Dictionary<string, List<Entry>> directories, string? root)
    {
        _directories = directories;
        Root = root ?? string.Empty;
    }

    /// <summary>
    ///  The first directory header recorded, which is the root that was enumerated.
    /// </summary>
    public string Root { get; }

    /// <summary>
    ///  The number of recorded directories.
    /// </summary>
    public int DirectoryCount => _directories.Count;

    /// <summary>
    ///  Loads a recorded file system from the CSV file at <paramref name="csvFilePath"/>.
    /// </summary>
    public static RecordedFileSystem Load(string csvFilePath)
    {
        ArgumentNullException.ThrowIfNull(csvFilePath);
        using StreamReader reader = new(csvFilePath);
        return Load(reader);
    }

    /// <summary>
    ///  Loads a recorded file system from <paramref name="reader"/>.
    /// </summary>
    public static RecordedFileSystem Load(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        Dictionary<string, List<Entry>> directories = new(StringComparer.OrdinalIgnoreCase);
        string? root = null;
        List<Entry>? current = null;

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (!CsvField.TryParse(line, out char type, out string value))
            {
                continue;
            }

            switch (type)
            {
                case DirectoryEnumerationRecorder.DirectoryHeader:
                    string key = Normalize(value);
                    if (!directories.TryGetValue(key, out current))
                    {
                        current = [];
                        directories[key] = current;
                    }

                    root ??= key;
                    break;
                case DirectoryEnumerationRecorder.FileEntry:
                    current?.Add(new Entry(value, isDirectory: false));
                    break;
                case DirectoryEnumerationRecorder.SubdirectoryEntry:
                    current?.Add(new Entry(value, isDirectory: true));
                    break;
                case DirectoryEnumerationRecorder.DirectoryEnd:
                    current = null;
                    break;
            }
        }

        return new RecordedFileSystem(directories, root);
    }

    /// <summary>
    ///  Gets the recorded entries for <paramref name="directory"/>, or an empty list when the
    ///  directory was not recorded.
    /// </summary>
    public IReadOnlyList<Entry> GetEntries(string directory) =>
        _directories.TryGetValue(Normalize(directory), out List<Entry>? entries)
            ? entries
            : [];

    internal static string Normalize(string path) =>
        Path.TrimEndingDirectorySeparator(path);
}
