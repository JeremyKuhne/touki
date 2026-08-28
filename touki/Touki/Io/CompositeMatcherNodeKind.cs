// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Identifies how a flattened matcher node obtains or combines its result.
/// </summary>
internal enum CompositeMatcherNodeKind : byte
{
    /// <summary>
    ///  A node that delegates matching to one matcher session.
    /// </summary>
    Leaf,

    /// <summary>
    ///  A node that combines child results with exclusions taking precedence.
    /// </summary>
    ExclusionWins,

    /// <summary>
    ///  A node that combines child results in matcher order.
    /// </summary>
    Ordered
}
