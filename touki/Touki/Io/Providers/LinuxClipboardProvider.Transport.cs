// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if NET

namespace Touki.Io.Providers;

internal sealed partial class LinuxClipboardProvider
{
    private enum Transport
    {
        None,
        WaylandWlCopy,
        X11Xclip,
        X11Xsel,
    }
}

#endif
