// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if NET
using Microsoft.Win32.SafeHandles;
using System.Threading;
using System.Threading.Tasks;

namespace Touki.Io;

/// <summary>
///  Supplies offset-based reads without owning a shared cursor.
/// </summary>
internal interface IFileReadBackend : IDisposable
{
    /// <summary>
    ///  Gets the open file handle.
    /// </summary>
    SafeFileHandle Handle { get; }

    /// <summary>
    ///  Gets the captured file length.
    /// </summary>
    long Length { get; }

    /// <summary>
    ///  Reads into caller-owned memory at an explicit offset.
    /// </summary>
    /// <param name="buffer">The destination.</param>
    /// <param name="fileOffset">The absolute file offset.</param>
    /// <returns>The successful byte count.</returns>
    int Read(Span<byte> buffer, long fileOffset);

    /// <summary>
    ///  Reads asynchronously into caller-owned memory at an explicit offset.
    /// </summary>
    /// <param name="buffer">The destination.</param>
    /// <param name="fileOffset">The absolute file offset.</param>
    /// <param name="cancellationToken">The cancellation token for this request.</param>
    /// <returns>The successful byte count.</returns>
    ValueTask<int> ReadAsync(Memory<byte> buffer, long fileOffset, CancellationToken cancellationToken);
}
#endif
