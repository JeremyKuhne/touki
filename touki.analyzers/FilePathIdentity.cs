// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System;
using System.IO;

namespace Touki.Analyzers;

internal static class FilePathIdentity
{
    public static StringComparer PathComparer { get; } = GetPathComparer(Path.DirectorySeparatorChar);

    public static StringComparer GetPathComparer(char directorySeparator) =>
        directorySeparator == '\\' ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}