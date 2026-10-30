// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace touki.perf;

public partial class LargeFileReadTests
{
    [TestMethod]
    [DataRow(-1)]
    [DataRow(2)]
    [DataRow(int.MaxValue)]
    public void ReadExactly_InvalidNativeBufferCount_RejectsBeforeSubmission(int count)
    {
        using TempFolder folder = new();
        string path = Path.Join(folder.TempPath, "pattern.bin");
        LargeFileReadPattern.Create(path, 8, blocks: false);
        using LargeFileReadReferenceReader reader = new(
            path,
            LargeFileReadReference.Raw,
            LargeFileReadScenario.Sequential);

        byte[] destination = [42];
        Action action = () => reader.ReadExactly(0, destination, count);
        action.Should().Throw<ArgumentOutOfRangeException>();
        reader.RequestsIssued.Should().Be(0);
        destination.Should().Equal(42);
    }

    [TestMethod]
    public void ReadExactly_EmptyBufferAtEof_DoesNotPinOrSubmit()
    {
        using TempFolder folder = new();
        string path = Path.Join(folder.TempPath, "empty.bin");
        LargeFileReadPattern.Create(path, 0, blocks: false);
        using LargeFileReadReferenceReader reader = new(
            path,
            LargeFileReadReference.Raw,
            LargeFileReadScenario.Sequential);

        reader.ReadExactly(0, [], 0);
        reader.RequestsIssued.Should().Be(0);
        reader.BackendBytesRead.Should().Be(0);
    }

    [TestMethod]
    public void Fill_OffsetAtLongLimit_RejectsExtentOverflow()
    {
        byte[] destination = [42];
        Action action = () => LargeFileReadPattern.Fill(destination, long.MaxValue, long.MaxValue, blocks: false);
        action.Should().Throw<ArgumentOutOfRangeException>();
        destination.Should().Equal(42);
    }
}
