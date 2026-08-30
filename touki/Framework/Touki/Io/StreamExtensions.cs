// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

public static partial class StreamExtensions
{
    /// <summary>
    ///  Compatibility entry point for writing a byte span to a stream on .NET Framework.
    /// </summary>
    /// <param name="stream">The target stream.</param>
    /// <param name="buffer">The bytes to write.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void Write(Stream stream, ReadOnlySpan<byte> buffer)
    {
        ArgumentNullException.ThrowIfNull(stream);
        stream.Write(buffer);
    }
}
