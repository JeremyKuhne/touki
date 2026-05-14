// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Providers;

/// <summary>
///  Clipboard provider used when no transport is available (headless Linux, unsupported OS).
/// </summary>
internal sealed class NullClipboardProvider : IClipboardProvider
{
    public static NullClipboardProvider Instance { get; } = new();

    private NullClipboardProvider()
    {
    }

    public bool IsAvailable => false;

    public bool HasText => false;

    public bool TryGetText([NotNullWhen(true)] out string? text)
    {
        text = null;
        return false;
    }

    public bool TrySetText(ReadOnlySpan<char> text) => false;

    public bool TryClear() => false;
}
