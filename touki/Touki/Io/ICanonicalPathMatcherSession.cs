// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

internal interface ICanonicalPathMatcherSession
{
    bool MatchesPath(ReadOnlySpan<char> rootRelativePath);
}
