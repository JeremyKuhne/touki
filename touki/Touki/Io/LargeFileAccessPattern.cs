// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if NET
namespace Touki.Io;

/// <summary>
///  Selects the stream's staging policy independently of the operating system's access hint.
/// </summary>
public enum LargeFileAccessPattern
{
    /// <summary>
    ///  Retains a window for sequential reads and nearby seeks.
    /// </summary>
    Sequential,

    /// <summary>
    ///  Reads only the requested range unless a positive chunk size explicitly enables a retained window.
    /// </summary>
    Sparse
}
#endif
