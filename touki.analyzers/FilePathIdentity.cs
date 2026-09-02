// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System;
using System.Runtime.InteropServices;

namespace Touki.Analyzers;

internal static class FilePathIdentity
{
    public static StringComparer PathComparer { get; } = GetPathComparer(
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows),
        RuntimeInformation.IsOSPlatform(OSPlatform.OSX));

    public static StringComparer GetPathComparer(bool isWindows, bool isMacOS) =>
        isWindows || isMacOS ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}