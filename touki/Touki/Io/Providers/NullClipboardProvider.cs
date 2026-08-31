// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Providers;

/// <summary>
///  Clipboard provider used when no transport is available (headless Linux, unsupported OS).
/// </summary>
internal sealed class NullClipboardProvider : IClipboardProvider
{
    /// <summary>
    ///  Gets the shared provider instance.
    /// </summary>
    public static NullClipboardProvider Instance { get; } = new();

    private NullClipboardProvider()
    {
    }

    /// <summary>
    ///  Gets a value indicating that no clipboard transport is available.
    /// </summary>
    public bool IsAvailable => false;

    /// <summary>
    ///  Gets a value indicating that no clipboard text is available.
    /// </summary>
    public bool HasText => false;

    /// <summary>
    ///  Reports that clipboard text cannot be read.
    /// </summary>
    /// <param name="text">Receives <see langword="null"/>.</param>
    /// <returns><see langword="false"/>.</returns>
    public bool TryGetText([NotNullWhen(returnValue: true)] out string? text)
    {
        text = null;
        return false;
    }

    /// <summary>
    ///  Reports that clipboard text cannot be set.
    /// </summary>
    /// <param name="text">The text that would be placed on the clipboard.</param>
    /// <returns><see langword="false"/>.</returns>
    public bool TrySetText(ReadOnlySpan<char> text) => false;

    /// <summary>
    ///  Reports that clipboard contents cannot be cleared.
    /// </summary>
    /// <returns><see langword="false"/>.</returns>
    public bool TryClear() => false;
}
