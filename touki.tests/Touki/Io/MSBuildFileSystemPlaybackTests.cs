// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

public class MSBuildFileSystemPlaybackTests
{
    private static string LocateRepoRoot()
    {
        string? directory = AppContext.BaseDirectory;
        while (directory is not null)
        {
            if (System.IO.File.Exists(System.IO.Path.Combine(directory, "touki.slnx")))
            {
                return directory;
            }

            directory = System.IO.Path.GetDirectoryName(directory);
        }

        throw new InvalidOperationException("Could not locate repository root (touki.slnx).");
    }

    private static void AssertDiskMatchesPlayback(string root, string filespec, List<string>? excludes)
    {
        // Record a real traversal while forwarding to disk.
        RecordedMSBuildFileSystem data = new();
        MSBuildFileSystemRecorder recorder = new(data);
        string[] recorded = FileMatcherWrapper.GetFilesSimple(root, filespec, excludes, recorder);

        // Independent disk traversal through the default file system.
        string[] disk = FileMatcherWrapper.GetFilesSimple(root, filespec, excludes);

        // Replaying the in-memory recording must reproduce the disk results exactly.
        MSBuildFileSystemPlayback playback = new(data);
        string[] played = FileMatcherWrapper.GetFilesSimple(root, filespec, excludes, playback);

        // FileMatcher walks the tree in parallel, so result order is not deterministic across
        // runs; the matched set is what must be identical.
        string[] expected = Sorted(disk);
        Sorted(recorded).Should().Equal(expected);
        Sorted(played).Should().Equal(expected);

        // A save/load round trip must replay identically as well.
        string tempFile = System.IO.Path.GetTempFileName();
        try
        {
            data.Save(tempFile);
            RecordedMSBuildFileSystem reloaded = RecordedMSBuildFileSystem.Load(tempFile);
            MSBuildFileSystemPlayback reloadedPlayback = new(reloaded);
            string[] reloadedPlayed = FileMatcherWrapper.GetFilesSimple(root, filespec, excludes, reloadedPlayback);
            Sorted(reloadedPlayed).Should().Equal(expected);
        }
        finally
        {
            System.IO.File.Delete(tempFile);
        }
    }

    private static string[] Sorted(string[] values)
    {
        string[] copy = [.. values];
        Array.Sort(copy, StringComparer.Ordinal);
        return copy;
    }

    [Fact]
    public void GetFiles_RecursiveCSharp_DiskAndPlaybackMatch()
    {
        string root = LocateRepoRoot();
        List<string> excludes = ["bin/**", "obj/**", "artifacts/**", "BenchmarkDotNet.Artifacts/**"];
        AssertDiskMatchesPlayback(root, "**/*.cs", excludes);
    }

    [Fact]
    public void GetFiles_TopLevelMarkdown_DiskAndPlaybackMatch()
    {
        string root = LocateRepoRoot();
        AssertDiskMatchesPlayback(root, "*.md", excludes: null);
    }

    [Fact]
    public void GetFiles_NoExcludes_DiskAndPlaybackMatch()
    {
        string root = LocateRepoRoot();
        AssertDiskMatchesPlayback(System.IO.Path.Combine(root, "touki"), "**/*.cs", excludes: null);
    }
}
