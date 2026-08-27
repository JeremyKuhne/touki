// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Identifies how a flattened matcher node obtains or combines its result.
/// </summary>
internal enum CompositeMatcherNodeKind : byte
{
    Leaf,
    ExclusionWins,
    Ordered
}
