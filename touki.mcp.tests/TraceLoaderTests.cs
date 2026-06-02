// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Mcp.Tracing;

public class TraceLoaderTests
{
    [Test]
    public void Load_MissingFile_ThrowsFileNotFound()
    {
        TraceLoader loader = new();
        string missing = Path.Combine(AppContext.BaseDirectory, "Fixtures", "missing.speedscope.json");

        Action act = () => loader.Load(missing);

        act.Should().Throw<FileNotFoundException>();
    }

    [Test]
    public void Load_UnsupportedExtension_ThrowsNotSupported()
    {
        TraceLoader loader = new();
        string temp = Path.GetTempFileName();
        try
        {
            Action act = () => loader.Load(temp);

            act.Should().Throw<NotSupportedException>();
        }
        finally
        {
            File.Delete(temp);
        }
    }
}
