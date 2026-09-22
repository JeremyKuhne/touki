// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.IO.MemoryMappedFiles;

namespace Touki.Io;

[TestClass]
public class MappedViewLeaseTests
{
    [TestMethod]
    public void Constructor_DisposedAccessor_DisposesAccessorAndThrows()
    {
        using TempFolder folder = new();
        string path = Path.Join(folder.TempPath, "data.bin");
        System.IO.File.WriteAllBytes(path, [10, 20, 30]);
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using MemoryMappedFile file = MemoryMappedFile.CreateFromFile(
            stream,
            mapName: null,
            capacity: 0,
            MemoryMappedFileAccess.Read,
            HandleInheritability.None,
            leaveOpen: true);

        using MemoryMappedViewAccessor accessor = file.CreateViewAccessor(
            offset: 0,
            size: stream.Length,
            MemoryMappedFileAccess.Read);

        accessor.Dispose();

        Action action = () => _ = new MappedViewLease(accessor);

        action.Should().Throw<ObjectDisposedException>();
        accessor.SafeMemoryMappedViewHandle.IsClosed.Should().BeTrue();
    }
}
