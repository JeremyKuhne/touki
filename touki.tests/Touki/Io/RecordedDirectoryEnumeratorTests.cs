// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Io.Globbing;

namespace Touki.Io;

/// <summary>
///  Round-trip tests for <see cref="DirectoryEnumerationRecorder"/> and
///  <see cref="RecordedDirectoryEnumerator"/>: recording a real tree to CSV and replaying it
///  through a matcher must produce the same result set as the corresponding real enumerator.
/// </summary>
public class RecordedDirectoryEnumeratorTests
{
    private const string MSBuildExcludes = "bin/**;obj/**;**/*.user";

    private static readonly string[] s_globExcludes = ["bin/**", "obj/**", "**/*.user"];

    private static TempFolder CreateFixture()
    {
        TempFolder folder = new();
        string root = folder.TempPath;
        Directory.CreateDirectory(Path.Combine(root, "src", "nested"));
        Directory.CreateDirectory(Path.Combine(root, "obj", "Debug"));
        Directory.CreateDirectory(Path.Combine(root, "bin", "Release"));
        Directory.CreateDirectory(Path.Combine(root, "empty"));

        File.WriteAllText(Path.Combine(root, "top.cs"), "");
        File.WriteAllText(Path.Combine(root, "top.txt"), "");
        File.WriteAllText(Path.Combine(root, "src", "a.cs"), "");
        File.WriteAllText(Path.Combine(root, "src", "b.user"), "");
        File.WriteAllText(Path.Combine(root, "src", "nested", "c.cs"), "");
        File.WriteAllText(Path.Combine(root, "obj", "Debug", "obj.cs"), "");
        File.WriteAllText(Path.Combine(root, "bin", "Release", "bin.cs"), "");
        return folder;
    }

    [Test]
    public void Replay_MSBuildMatcher_MatchesRealEnumerator()
    {
        using TempFolder folder = CreateFixture();
        string root = folder.TempPath;

        HashSet<string> expected = [];
        using (MSBuildEnumerator real = MSBuildEnumerator.Create("**/*.cs", MSBuildExcludes, root))
        {
            while (real.MoveNext())
            {
                expected.Add(real.Current);
            }
        }

        RecordedFileSystem fileSystem = RecordRoundTrip(root);

        IEnumerationMatcher matcher = EnumerationMatcherFactory.CreateMSBuild(
            "**/*.cs",
            MSBuildExcludes,
            root,
            out string startDirectory);

        HashSet<string> actual = [];
        using (RecordedDirectoryEnumerator mock = new(fileSystem, matcher, startDirectory, excludeDirectories: true))
        {
            while (mock.MoveNext())
            {
                actual.Add(mock.Current);
            }
        }

        actual.Should().BeEquivalentTo(expected);
        actual.Should().Contain(Path.Combine("src", "a.cs"));
        actual.Should().NotContain(Path.Combine("obj", "Debug", "obj.cs"));
    }

    [Test]
    public void Replay_GlobMatcher_MatchesRealEnumerator()
    {
        using TempFolder folder = CreateFixture();
        string root = folder.TempPath;

        HashSet<string> expected = [];
        using (GlobEnumerator real = GlobEnumerator.Create("**/*.cs", s_globExcludes, root, GlobDialect.MSBuild))
        {
            while (real.MoveNext())
            {
                expected.Add(real.Current);
            }
        }

        RecordedFileSystem fileSystem = RecordRoundTrip(root);

        IEnumerationMatcher matcher = EnumerationMatcherFactory.CreateGlob(
            "**/*.cs",
            s_globExcludes,
            root,
            GlobDialect.MSBuild);

        HashSet<string> actual = [];
        using (RecordedDirectoryEnumerator mock = new(fileSystem, matcher, root))
        {
            while (mock.MoveNext())
            {
                actual.Add(mock.Current);
            }
        }

        actual.Should().BeEquivalentTo(expected);
    }

    [Test]
    public void Record_EmptyDirectory_IsCaptured()
    {
        using TempFolder folder = CreateFixture();
        RecordedFileSystem fileSystem = RecordRoundTrip(folder.TempPath);

        fileSystem.GetEntries(Path.Combine(folder.TempPath, "empty")).Should().BeEmpty();
        fileSystem.DirectoryCount.Should().BeGreaterThan(1);
    }

    [Test]
    public void CsvField_RoundTrips_ValuesNeedingQuoting()
    {
        AssertRoundTrip('D', "simple");
        AssertRoundTrip('F', "with,comma");
        AssertRoundTrip('S', "with\"quote");
        AssertRoundTrip('E', "with\r\nnewline");
        AssertRoundTrip('D', string.Empty);
    }

    private static void AssertRoundTrip(char type, string value)
    {
        System.IO.StringWriter writer = new();
        writer.Write(type);
        writer.Write(',');
        CsvField.Write(writer, value);

        CsvField.TryParse(writer.ToString(), out char parsedType, out string parsedValue).Should().BeTrue();
        parsedType.Should().Be(type);
        parsedValue.Should().Be(value);
    }

    private static RecordedFileSystem RecordRoundTrip(string root)
    {
        System.IO.StringWriter writer = new();
        DirectoryEnumerationRecorder.Record(root, writer);
        return RecordedFileSystem.Load(new System.IO.StringReader(writer.ToString()));
    }
}
