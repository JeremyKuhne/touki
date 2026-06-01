// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Records a directory enumeration performed through a <see cref="FileSystemEnumerator{TResult}"/>
///  to a CSV file so it can be replayed later without touching the file system.
/// </summary>
/// <remarks>
///  <para>
///   The recorder walks the entire visible tree under a root directory (recursing into every
///   directory regardless of any matcher) and writes every entry it observes. Replaying through
///   <see cref="RecordedDirectoryEnumerator"/> then lets a matcher make its own pruning decisions
///   over the recorded universe of files and directories, which is what makes deterministic,
///   file-system-free testing and performance evaluation possible.
///  </para>
///  <para>
///   The CSV uses a two-column <c>Type,Value</c> schema with one row per event. The directory
///   being scanned only changes when a directory-finished event fires, so the containing
///   directory is written once as a header row rather than being repeated on every entry:
///  </para>
///  <para>
///   <list type="table">
///    <listheader>
///     <term>Type</term>
///     <description>Meaning of <c>Value</c></description>
///    </listheader>
///    <item>
///     <term>D</term>
///     <description>Full path of the directory now being scanned (header).</description>
///    </item>
///    <item>
///     <term>F</term>
///     <description>Name of a file in the current directory.</description>
///    </item>
///    <item>
///     <term>S</term>
///     <description>Name of a subdirectory in the current directory.</description>
///    </item>
///    <item>
///     <term>E</term>
///     <description>Full path of the directory that just finished scanning.</description>
///    </item>
///   </list>
///  </para>
/// </remarks>
public sealed class DirectoryEnumerationRecorder : FileSystemEnumerator<string>
{
    private readonly TextWriter _writer;
    private string _currentDirectory = string.Empty;

    /// <summary>
    ///  Record type marker for a directory header row.
    /// </summary>
    internal const char DirectoryHeader = 'D';

    /// <summary>
    ///  Record type marker for a file entry row.
    /// </summary>
    internal const char FileEntry = 'F';

    /// <summary>
    ///  Record type marker for a subdirectory entry row.
    /// </summary>
    internal const char SubdirectoryEntry = 'S';

    /// <summary>
    ///  Record type marker for a directory-finished row.
    /// </summary>
    internal const char DirectoryEnd = 'E';

    /// <summary>
    ///  Default options used when recording: recurse into everything and ignore inaccessible
    ///  entries. The skip attributes mirror the enumerators being mocked (the
    ///  <see cref="EnumerationOptions"/> default of <see cref="FileAttributes.Hidden"/> |
    ///  <see cref="FileAttributes.System"/>) so the recording captures the same universe of
    ///  entries a real <see cref="GlobEnumerator"/> or <see cref="MSBuildEnumerator"/> would see.
    /// </summary>
    public static EnumerationOptions DefaultRecordingOptions { get; } = new()
    {
        MatchType = MatchType.Simple,
        MatchCasing = MatchCasing.PlatformDefault,
        IgnoreInaccessible = true,
        RecurseSubdirectories = true
    };

    /// <summary>
    ///  Initializes a new instance of the <see cref="DirectoryEnumerationRecorder"/> class.
    /// </summary>
    /// <param name="directory">The root directory to enumerate and record.</param>
    /// <param name="writer">The writer that receives the CSV rows.</param>
    /// <param name="options">
    ///  Enumeration options. When <see langword="null"/>, <see cref="DefaultRecordingOptions"/> is used.
    /// </param>
    public DirectoryEnumerationRecorder(string directory, TextWriter writer, EnumerationOptions? options = null)
        : base(directory, options ?? DefaultRecordingOptions)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(writer);
        _writer = writer;
    }

    /// <summary>
    ///  Records the full enumeration of <paramref name="directory"/> to the CSV file at
    ///  <paramref name="csvFilePath"/>.
    /// </summary>
    /// <param name="directory">The root directory to enumerate.</param>
    /// <param name="csvFilePath">The CSV file to create or overwrite.</param>
    /// <param name="options">
    ///  Enumeration options. When <see langword="null"/>, <see cref="DefaultRecordingOptions"/> is used.
    /// </param>
    public static void Record(string directory, string csvFilePath, EnumerationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(csvFilePath);

        using StreamWriter writer = new(csvFilePath, append: false)
        {
            NewLine = "\n"
        };

        Record(directory, writer, options);
    }

    /// <summary>
    ///  Records the full enumeration of <paramref name="directory"/> to <paramref name="writer"/>.
    /// </summary>
    /// <param name="directory">The root directory to enumerate.</param>
    /// <param name="writer">The writer that receives the CSV rows.</param>
    /// <param name="options">
    ///  Enumeration options. When <see langword="null"/>, <see cref="DefaultRecordingOptions"/> is used.
    /// </param>
    public static void Record(string directory, TextWriter writer, EnumerationOptions? options = null)
    {
        using DirectoryEnumerationRecorder recorder = new(directory, writer, options);

        // ShouldIncludeEntry always returns false, so a single MoveNext walks and records the
        // entire tree before returning false. The loop is defensive only.
        while (recorder.MoveNext())
        {
        }
    }

    /// <inheritdoc/>
    protected override bool ShouldRecurseIntoEntry(ref FileSystemEntry entry) => true;

    /// <inheritdoc/>
    protected override bool ShouldIncludeEntry(ref FileSystemEntry entry)
    {
        string directory = entry.Directory.ToString();
        if (!string.Equals(directory, _currentDirectory, StringComparison.Ordinal))
        {
            WriteRow(DirectoryHeader, directory);
            _currentDirectory = directory;
        }

        WriteRow(entry.IsDirectory ? SubdirectoryEntry : FileEntry, entry.FileName.ToString());

        // Never yield: we only want to record, not return results.
        return false;
    }

    /// <inheritdoc/>
    protected override void OnDirectoryFinished(ReadOnlySpan<char> directory)
    {
        string finished = directory.ToString();

        // An empty directory produces no entries, so its header was never written. Write it now
        // so the replay knows the directory exists (with no children).
        if (!string.Equals(finished, _currentDirectory, StringComparison.Ordinal))
        {
            WriteRow(DirectoryHeader, finished);
        }

        WriteRow(DirectoryEnd, finished);

        // Reset so the next directory writes a fresh header.
        _currentDirectory = string.Empty;
    }

    /// <inheritdoc/>
    protected override string TransformEntry(ref FileSystemEntry entry) => string.Empty;

    private void WriteRow(char type, string value)
    {
        _writer.Write(type);
        _writer.Write(',');
        CsvField.Write(_writer, value);
        _writer.Write('\n');
    }
}
