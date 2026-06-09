// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Tracing;

[TestClass]
public sealed class TraceConverterTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    // convert / clean mutate the filesystem (they write and delete the ETLX sidecar),
    // so each test works on a private temp copy of the fixture rather than the shared
    // committed one.
    private static string CopyToTemp(string fixture, out string tempDir)
    {
        tempDir = Path.Combine(Path.GetTempPath(), $"traceq-conv-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string dest = Path.Combine(tempDir, fixture);
        File.Copy(FixturePath(fixture), dest);
        return dest;
    }

    [TestMethod]
    public void Convert_NetTrace_WritesTheEtlxSidecar()
    {
        string trace = CopyToTemp("alloc.nettrace", out string tempDir);
        try
        {
            string etlx = TraceConverter.Convert(trace);

            etlx.Should().Be(trace + ".etlx", "TraceEvent appends .etlx to the trace path");
            File.Exists(etlx).Should().BeTrue();
            new FileInfo(etlx).Length.Should().BeGreaterThan(0);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public void Clean_AfterConvert_RemovesTheSidecar()
    {
        string trace = CopyToTemp("alloc.nettrace", out string tempDir);
        try
        {
            string etlx = TraceConverter.Convert(trace);
            File.Exists(etlx).Should().BeTrue();

            string? removed = TraceConverter.Clean(trace);

            removed.Should().Be(etlx);
            File.Exists(etlx).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public void Clean_WithNoCache_ReturnsNull()
    {
        string trace = CopyToTemp("alloc.nettrace", out string tempDir);
        try
        {
            // No prior convert, so there is no sidecar to remove.
            TraceConverter.Clean(trace).Should().BeNull();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public void Convert_Speedscope_ThrowsNotSupported()
    {
        // A speedscope export is parsed as JSON and has no ETLX cache.
        Action act = () => TraceConverter.Convert(FixturePath("folding.speedscope.json"));

        act.Should().Throw<NotSupportedException>();
    }

    [TestMethod]
    public void Convert_MissingFile_ThrowsFileNotFound()
    {
        Action act = () => TraceConverter.Convert(FixturePath("does-not-exist.nettrace"));

        act.Should().Throw<FileNotFoundException>();
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(null)]
    public void Convert_NullOrEmptyPath_ThrowsArgument(string? path)
    {
        Action act = () => TraceConverter.Convert(path!);

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void EtlxPathFor_AppendsTheExtension()
    {
        TraceConverter.EtlxPathFor("a/b/foo.nettrace").Should().Be("a/b/foo.nettrace.etlx");
    }
}
