// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Per-platform clipboard transport used by <see cref="Clipboard"/>.
/// </summary>
/// <remarks>
///  <para>
///   Implementations are stateless and shared across the process. Each method returns
///   <see langword="false"/> rather than throwing when the underlying transport is unavailable
///   so callers can probe portability cheaply.
///  </para>
/// </remarks>
internal interface IClipboardProvider
{
    /// <summary>
    ///  Returns <see langword="true"/> if the underlying clipboard transport is reachable.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   This reflects whether the host environment can carry clipboard data at all - not
    ///   whether any particular call will succeed. Concurrent contention from other processes
    ///   can still cause a momentary failure on a fully-supported platform.
    ///  </para>
    /// </remarks>
    bool IsAvailable { get; }

    /// <summary>
    ///  Returns <see langword="true"/> if Unicode text is currently available on the clipboard.
    /// </summary>
    bool HasText { get; }

    /// <summary>
    ///  Attempts to read Unicode text from the clipboard.
    /// </summary>
    bool TryGetText([NotNullWhen(true)] out string? text);

    /// <summary>
    ///  Attempts to place <paramref name="text"/> on the clipboard as Unicode text.
    /// </summary>
    bool TrySetText(ReadOnlySpan<char> text);

    /// <summary>
    ///  Attempts to release the current clipboard contents.
    /// </summary>
    bool TryClear();
}
