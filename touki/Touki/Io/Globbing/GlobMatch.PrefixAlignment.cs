// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

public sealed partial class GlobMatch
{
    /// <summary>
    ///  Three-valued classification of how a relative directory aligns with the
    ///  specification's <see cref="GlobSpecification.LiteralPathPrefix"/>. Computed
    ///  once per directory at the cache boundary and consumed by every per-file
    ///  call in that directory.
    /// </summary>
    private enum PrefixAlignment : byte
    {
        /// <summary>
        ///  The relative directory is at or beyond the literal prefix (or the
        ///  specification has no literal prefix). Files at this level may match;
        ///  the matcher must be invoked.
        /// </summary>
        Beyond,

        /// <summary>
        ///  The relative directory is a directory-aligned proper prefix of the
        ///  literal prefix. The matcher cannot fire on files at this level (the
        ///  literal prefix is not satisfied yet), but descendant directories
        ///  might still align.
        /// </summary>
        OnPrefix,

        /// <summary>
        ///  The relative directory has diverged from the literal prefix. The
        ///  matcher can never fire on files here or in any descendant directory.
        /// </summary>
        Diverged,
    }
}
