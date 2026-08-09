// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Matches a canonical root-relative file path.
/// </summary>
public delegate bool PathMatchPredicate(ReadOnlySpan<char> rootRelativePath);