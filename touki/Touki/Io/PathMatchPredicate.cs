// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Matches a canonical root-relative file path.
/// </summary>
/// <param name="rootRelativePath">The canonical root-relative path.</param>
/// <returns><see langword="true"/> if the path matches; otherwise <see langword="false"/>.</returns>
public delegate bool PathMatchPredicate(ReadOnlySpan<char> rootRelativePath);