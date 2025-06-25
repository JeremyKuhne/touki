// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Simple temporary folder implementation that is automatically deleted when disposed.
/// </summary>
public sealed class TempFolder : DisposableBase
{
    /// <summary>
    ///  The path of the temporary folder.
    /// </summary>
    public string TempPath { get; } = CreateTempFolder();

    private static string CreateTempFolder()
    {
        string path = Path.Join(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    ///  Implicit conversion from <see cref="TempFolder"/> to <see cref="string"/> that returns the path of the temporary folder.
    /// </summary>
    public static implicit operator string(TempFolder folder) => folder.TempPath;

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        try
        {
            Directory.Delete(TempPath, recursive: true);
        }
        catch
        {
        }
    }
}
