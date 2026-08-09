// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

[TestClass]
public class FileSystemMatcherTests
{
    [TestMethod]
    public void MatchesDirectory_DefaultSession_ReturnsConservativeResult()
    {
        using TestSession session = new();

        session.MatchesDirectory("root", "child").Should().Be(DirectoryMatchType.MayContainMatchingFiles);
        session.DirectoryFinished("root");
        session.Dispose();
    }

    [TestMethod]
    public void Create_SessionForwardsSeparateSpans()
    {
        string? actualDirectory = null;
        string? actualFileName = null;
        IFileSystemMatcher matcher = FileSystemMatcher.Create((directory, fileName) =>
        {
            actualDirectory = directory.ToString();
            actualFileName = fileName.ToString();
            return true;
        });
        using IFileSystemMatcherSession session = matcher.CreateSession("root");

        session.MatchesFile("root/sub", "file.cs").Should().BeTrue();

        actualDirectory.Should().Be("root/sub");
        actualFileName.Should().Be("file.cs");
    }

    [TestMethod]
    public void Create_CreateSessionTwice_ReturnsIndependentSessions()
    {
        IFileSystemMatcher matcher = FileSystemMatcher.Create((_, _) => false);
        using IFileSystemMatcherSession first = matcher.CreateSession("root");
        using IFileSystemMatcherSession second = matcher.CreateSession("root");

        first.Should().NotBeSameAs(second);
    }

    [TestMethod]
    public void CreatePath_NestedFile_UsesCanonicalRootRelativePath()
    {
        string root = Path.Combine(Path.GetTempPath(), "matcher-root");
        string currentDirectory = Path.Combine(root, "src", "nested");
        string? actualPath = null;
        IFileSystemMatcher matcher = FileSystemMatcher.CreatePath(path =>
        {
            actualPath = path.ToString();
            return true;
        });
        using IFileSystemMatcherSession session = matcher.CreateSession(root);

        session.MatchesFile(currentDirectory, "file.cs").Should().BeTrue();

        actualPath.Should().Be("src/nested/file.cs");
    }

    [TestMethod]
    public void CreatePath_RootFile_UsesFileNameOnly()
    {
        string root = Path.Combine(Path.GetTempPath(), "matcher-root");
        string? actualPath = null;
        IFileSystemMatcher matcher = FileSystemMatcher.CreatePath(path =>
        {
            actualPath = path.ToString();
            return true;
        });
        using IFileSystemMatcherSession session = matcher.CreateSession(root);

        session.MatchesFile(root, "file.cs").Should().BeTrue();

        actualPath.Should().Be("file.cs");
    }

    private sealed class TestSession : FileSystemMatcherSession
    {
        public override bool MatchesFile(
            ReadOnlySpan<char> currentDirectory,
            ReadOnlySpan<char> fileName) => false;
    }
}