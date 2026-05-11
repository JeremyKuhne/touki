// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

public class TempFolderTests
{
    [Fact]
    public void Constructor_CreatesDirectory()
    {
        using TempFolder folder = new();
        Directory.Exists(folder.TempPath).Should().BeTrue();
    }

    [Fact]
    public void ImplicitConversion_ToString_ReturnsTempPath()
    {
        using TempFolder folder = new();
        string path = folder;
        path.Should().Be(folder.TempPath);
    }

    [Fact]
    public void Dispose_DeletesDirectory()
    {
        TempFolder folder = new();
        string path = folder.TempPath;
        Directory.Exists(path).Should().BeTrue();

        folder.Dispose();

        Directory.Exists(path).Should().BeFalse();
    }

    [Fact]
    public void Dispose_DirectoryAlreadyRemoved_DoesNotThrow()
    {
        TempFolder folder = new();
        Directory.Delete(folder.TempPath, recursive: true);

        Action action = folder.Dispose;
        action.Should().NotThrow();
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        TempFolder folder = new();
        folder.Dispose();

        Action action = folder.Dispose;
        action.Should().NotThrow();
    }
}
