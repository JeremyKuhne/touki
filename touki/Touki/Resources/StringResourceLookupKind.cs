// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Resources;

/// <summary>
///  The result of looking up a name in a string resource table.
/// </summary>
internal enum StringResourceLookupKind
{
    /// <summary>
    ///  The name does not exist in the resource table.
    /// </summary>
    Missing,

    /// <summary>
    ///  The name identifies an intrinsic string.
    /// </summary>
    Found,

    /// <summary>
    ///  The table was retired while the lookup was waiting to access its backing.
    /// </summary>
    Stale
}