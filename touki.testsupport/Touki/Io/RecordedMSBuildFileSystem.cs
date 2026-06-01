// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Globalization;

namespace Touki.Io;

/// <summary>
///  An in-memory snapshot of the file-system queries MSBuild's internal <c>FileMatcher</c> issues
///  through its <c>IFileSystem</c> abstraction. Captured by recording the calls a real
///  <c>FileMatcher.GetFiles</c> traversal makes, then replayed to drive the same traversal without
///  touching disk.
/// </summary>
/// <remarks>
///  <para>
///   Two kinds of calls are recorded: directory enumerations
///   (<c>EnumerateFileSystemEntries</c> / <c>EnumerateFiles</c> / <c>EnumerateDirectories</c>),
///   keyed by method, path, search pattern, and search option; and directory-existence checks
///   (<c>DirectoryExists</c>), keyed by method and path. Paths are stored verbatim (absolute), the
///   same way <see cref="DirectoryEnumerationRecorder"/> stores them, so replaying on the same tree
///   at the same location reproduces results exactly.
///  </para>
///  <para>
///   The serialized form is a CSV file. Enumeration records are written as a <c>Q</c> header row
///   (<c>Q,method,path,pattern,option,count</c>) followed by <c>count</c> <c>R</c>
///   result rows; existence checks are written as a single <c>B</c> row
///   (<c>B,method,path,value</c>).
///  </para>
/// </remarks>
public sealed class RecordedMSBuildFileSystem
{
    /// <summary>Method code for <c>EnumerateFileSystemEntries</c>.</summary>
    public const string EnumerateFileSystemEntriesMethod = "FSE";

    /// <summary>Method code for <c>EnumerateFiles</c>.</summary>
    public const string EnumerateFilesMethod = "FILES";

    /// <summary>Method code for <c>EnumerateDirectories</c>.</summary>
    public const string EnumerateDirectoriesMethod = "DIRS";

    /// <summary>Method code for <c>DirectoryExists</c>.</summary>
    public const string DirectoryExistsMethod = "DIR";

    private readonly struct Enumeration
    {
        public Enumeration(string method, string path, string pattern, int option, string[] results)
        {
            Method = method;
            Path = path;
            Pattern = pattern;
            Option = option;
            Results = results;
        }

        public string Method { get; }
        public string Path { get; }
        public string Pattern { get; }
        public int Option { get; }
        public string[] Results { get; }
    }

    private readonly struct Existence
    {
        public Existence(string method, string path, bool value)
        {
            Method = method;
            Path = path;
            Value = value;
        }

        public string Method { get; }
        public string Path { get; }
        public bool Value { get; }
    }

    private readonly Dictionary<string, Enumeration> _enumerations;
    private readonly Dictionary<string, Existence> _existence;
    private readonly System.Threading.Lock _lock = new();

    /// <summary>
    ///  Initializes a new, empty recording.
    /// </summary>
    public RecordedMSBuildFileSystem()
    {
        _enumerations = new(StringComparer.OrdinalIgnoreCase);
        _existence = new(StringComparer.OrdinalIgnoreCase);
        Root = string.Empty;
    }

    private RecordedMSBuildFileSystem(
        Dictionary<string, Enumeration> enumerations,
        Dictionary<string, Existence> existence,
        string root)
    {
        _enumerations = enumerations;
        _existence = existence;
        Root = root;
    }

    /// <summary>
    ///  The first path observed during recording, retained for diagnostics.
    /// </summary>
    public string Root { get; private set; }

    /// <summary>
    ///  The number of recorded directory enumerations.
    /// </summary>
    public int EnumerationCount => _enumerations.Count;

    /// <summary>
    ///  The number of recorded directory-existence checks.
    /// </summary>
    public int ExistenceCount => _existence.Count;

    private static string EnumKey(string method, string path, string pattern, int option) =>
        string.Concat(method, "\0", path, "\0", pattern, "\0", option.ToString(CultureInfo.InvariantCulture));

    private static string ExistenceKey(string method, string path) =>
        string.Concat(method, "\0", path);

    /// <summary>
    ///  Records the result of a directory enumeration.
    /// </summary>
    public void RecordEnumeration(string method, string path, string pattern, int option, IReadOnlyList<string> results)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(results);

        // FileMatcher queries the file system from parallel threads, so recording must be guarded.
        lock (_lock)
        {
            if (Root.Length == 0)
            {
                Root = path;
            }

            _enumerations[EnumKey(method, path, pattern, option)] = new Enumeration(method, path, pattern, option, [.. results]);
        }
    }

    /// <summary>
    ///  Records the result of a directory-existence check.
    /// </summary>
    public void RecordExistence(string method, string path, bool value)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(path);

        lock (_lock)
        {
            _existence[ExistenceKey(method, path)] = new Existence(method, path, value);
        }
    }

    /// <summary>
    ///  Gets the recorded result of a directory enumeration.
    /// </summary>
    public bool TryGetEnumeration(string method, string path, string pattern, int option, out string[] results)
    {
        if (_enumerations.TryGetValue(EnumKey(method, path, pattern, option), out Enumeration enumeration))
        {
            results = enumeration.Results;
            return true;
        }

        results = [];
        return false;
    }

    /// <summary>
    ///  Gets the recorded result of a directory-existence check.
    /// </summary>
    public bool TryGetExistence(string method, string path, out bool value)
    {
        if (_existence.TryGetValue(ExistenceKey(method, path), out Existence existence))
        {
            value = existence.Value;
            return true;
        }

        value = false;
        return false;
    }

    /// <summary>
    ///  Saves the recording to the CSV file at <paramref name="csvFilePath"/>.
    /// </summary>
    public void Save(string csvFilePath)
    {
        ArgumentNullException.ThrowIfNull(csvFilePath);
        using StreamWriter writer = new(csvFilePath, append: false)
        {
            NewLine = "\n"
        };

        Save(writer);
    }

    /// <summary>
    ///  Saves the recording to <paramref name="writer"/>.
    /// </summary>
    public void Save(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        foreach (Enumeration enumeration in _enumerations.Values)
        {
            CsvField.WriteRecord(
                writer,
                "Q",
                enumeration.Method,
                enumeration.Path,
                enumeration.Pattern,
                enumeration.Option.ToString(CultureInfo.InvariantCulture),
                enumeration.Results.Length.ToString(CultureInfo.InvariantCulture));

            foreach (string result in enumeration.Results)
            {
                CsvField.WriteRecord(writer, "R", result);
            }
        }

        foreach (Existence existence in _existence.Values)
        {
            CsvField.WriteRecord(writer, "B", existence.Method, existence.Path, existence.Value ? "1" : "0");
        }
    }

    /// <summary>
    ///  Loads a recording from the CSV file at <paramref name="csvFilePath"/>.
    /// </summary>
    public static RecordedMSBuildFileSystem Load(string csvFilePath)
    {
        ArgumentNullException.ThrowIfNull(csvFilePath);
        using StreamReader reader = new(csvFilePath);
        return Load(reader);
    }

    /// <summary>
    ///  Loads a recording from <paramref name="reader"/>.
    /// </summary>
    public static RecordedMSBuildFileSystem Load(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        Dictionary<string, Enumeration> enumerations = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, Existence> existence = new(StringComparer.OrdinalIgnoreCase);
        string root = string.Empty;

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            List<string> fields = CsvField.ParseRecord(line);
            if (fields.Count == 0)
            {
                continue;
            }

            switch (fields[0])
            {
                case "Q":
                    string method = fields[1];
                    string path = fields[2];
                    string pattern = fields[3];
                    int option = int.Parse(fields[4], CultureInfo.InvariantCulture);
                    int count = int.Parse(fields[5], CultureInfo.InvariantCulture);

                    string[] results = new string[count];
                    for (int i = 0; i < count; i++)
                    {
                        List<string> resultFields = CsvField.ParseRecord(reader.ReadLine() ?? string.Empty);
                        results[i] = resultFields.Count > 1 ? resultFields[1] : string.Empty;
                    }

                    if (root.Length == 0)
                    {
                        root = path;
                    }

                    enumerations[EnumKey(method, path, pattern, option)] = new Enumeration(method, path, pattern, option, results);
                    break;
                case "B":
                    existence[ExistenceKey(fields[1], fields[2])] = new Existence(fields[1], fields[2], fields[3] == "1");
                    break;
            }
        }

        return new RecordedMSBuildFileSystem(enumerations, existence, root);
    }
}
