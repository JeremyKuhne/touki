// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

[TestClass]
public class MappedMemoryManagerTests
{
    [TestMethod]
    public void CreateFromFile_MapsFileContents()
    {
        byte[] bytes = [10, 20, 30, 40, 50];
        using TempFolder folder = new();
        string path = System.IO.Path.Combine(folder.TempPath, "data.bin");
        System.IO.File.WriteAllBytes(path, bytes);

        using MappedMemoryManager manager = MappedMemoryManager.CreateFromFile(path);

        manager.Memory.Length.Should().Be(bytes.Length);
        manager.Memory.Span.SequenceEqual(bytes).Should().BeTrue();
        manager.GetSpan().SequenceEqual(bytes).Should().BeTrue();
    }

    [TestMethod]
    public void CreateFromFile_NullPath_ThrowsArgumentNullException()
    {
        Action act = () => _ = MappedMemoryManager.CreateFromFile(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void CreateFromFile_EmptyFile_ThrowsIOException()
    {
        using TempFolder folder = new();
        string path = System.IO.Path.Combine(folder.TempPath, "empty.bin");
        System.IO.File.WriteAllBytes(path, []);

        Action act = () => _ = MappedMemoryManager.CreateFromFile(path);
        act.Should().Throw<System.IO.IOException>();
    }

    [TestMethod]
    public void Dispose_IsIdempotent()
    {
        byte[] bytes = [1, 2, 3];
        using TempFolder folder = new();
        string path = System.IO.Path.Combine(folder.TempPath, "data.bin");
        System.IO.File.WriteAllBytes(path, bytes);

        MappedMemoryManager manager = MappedMemoryManager.CreateFromFile(path);
        ((IDisposable)manager).Dispose();
        ((IDisposable)manager).Dispose();
    }
}
