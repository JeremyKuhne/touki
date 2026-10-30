// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if NET
namespace Touki.Io;

/// <summary>
///  Configures a demand-only, cached large-file reader.
/// </summary>
public sealed class LargeFileStreamOptions
{
    /// <summary>
    ///  The window size used when no explicit size is supplied.
    /// </summary>
    internal const int DefaultChunkSize = 4 << 20;

    /// <summary>
    ///  Gets the sharing mode used when opening a path.
    /// </summary>
    public FileShare Share { get; init; } = FileShare.Read;

    /// <summary>
    ///  Gets the stream's staging policy.
    /// </summary>
    public LargeFileAccessPattern AccessPattern { get; init; }

    /// <summary>
    ///  Gets the operating system's access hint.
    /// </summary>
    public LargeFileAccessHint AccessHint { get; init; }

    /// <summary>
    ///  Gets whether to reopen the same Windows file with a dedicated synchronous read handle.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   This opt-in avoids overlapped-completion overhead for synchronous consumers while keeping the original
    ///   asynchronous handle for asynchronous reads. Opening fails if the original sharing mode disallows
    ///   another reader. No fallback is performed. This option requires Windows 10 or later.
    ///  </para>
    /// </remarks>
    public bool UseSeparateSynchronousHandle { get; init; }

    /// <summary>
    ///  Gets whether to collect per-request I/O timing and chunk lifetime timing. Defaults to <see langword="false"/>.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Byte, request, short-read, capacity, and outstanding-chunk counters are always collected.
    ///   Timing fields are zero when this option is disabled; Statistics.TimingsEnabled reports the choice.
    ///  </para>
    /// </remarks>
    public bool CollectTimings { get; init; }

    /// <summary>
    ///  Gets the window size. Zero selects 4 MiB for sequential access, or unstaged sparse stream reads.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Chunk reads in unstaged sparse mode explicitly demand a 4 MiB window.
    ///  </para>
    /// </remarks>
    public int ChunkSize { get; init; }

    /// <summary>
    ///  Gets the additional read-ahead depth. The demand-only reader requires zero.
    /// </summary>
    public int ReadAheadDepth { get; init; }

    /// <summary>
    ///  Gets the maximum number of outstanding nonempty chunk leases. Defaults to one.
    /// </summary>
    public int MaxOutstandingChunks { get; init; } = 1;

    /// <summary>
    ///  Gets the payload budget, including the I/O window and all consumer slots. Zero selects 16 MiB.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Consumer slots are allocated only when the chunk API is first used.
    ///  </para>
    /// </remarks>
    public int MaxBufferedBytes { get; init; }

    /// <summary>
    ///  Validates and snapshots the configuration before opening a file.
    /// </summary>
    /// <returns>The effective configuration.</returns>
    internal LargeFileStreamOptions Resolve()
    {
        if (!Enum.IsDefined(AccessPattern))
        {
            throw new ArgumentOutOfRangeException(nameof(AccessPattern));
        }

        if (!Enum.IsDefined(AccessHint))
        {
            throw new ArgumentOutOfRangeException(nameof(AccessHint));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(ChunkSize);
        ArgumentOutOfRangeException.ThrowIfNegative(MaxBufferedBytes);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxOutstandingChunks, 1);
        if (ReadAheadDepth != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ReadAheadDepth), "The demand-only reader requires zero.");
        }

        int size = ChunkSize == 0 ? DefaultChunkSize : ChunkSize;
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 64 << 10, nameof(ChunkSize));
        int budget = MaxBufferedBytes == 0 ? 16 << 20 : MaxBufferedBytes;
        long capacity = checked((long)size * (1L + MaxOutstandingChunks));
        if (capacity > budget)
        {
            throw new ArgumentException("The I/O and consumer slots exceed MaxBufferedBytes.");
        }

        LargeFileAccessHint hint = AccessHint == LargeFileAccessHint.Default
            ? AccessPattern == LargeFileAccessPattern.Sequential
                ? LargeFileAccessHint.Sequential
                : LargeFileAccessHint.Random
            : AccessHint;

        return new()
        {
            Share = Share,
            AccessPattern = AccessPattern,
            AccessHint = hint,
            UseSeparateSynchronousHandle = UseSeparateSynchronousHandle,
            CollectTimings = CollectTimings,
            ChunkSize = AccessPattern == LargeFileAccessPattern.Sparse && ChunkSize == 0 ? 0 : size,
            MaxOutstandingChunks = MaxOutstandingChunks,
            MaxBufferedBytes = budget
        };
    }
}
#endif
