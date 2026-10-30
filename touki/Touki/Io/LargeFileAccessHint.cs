// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if NET
namespace Touki.Io;

/// <summary>
///  Selects the operating system's access hint without changing the stream's staging policy.
/// </summary>
public enum LargeFileAccessHint
{
    /// <summary>
    ///  Resolves to the hint matching the access pattern.
    /// </summary>
    Default,

    /// <summary>
    ///  Sends neither access hint.
    /// </summary>
    None,

    /// <summary>
    ///  Requests sequential access optimization.
    /// </summary>
    Sequential,

    /// <summary>
    ///  Requests random access optimization.
    /// </summary>
    Random
}
#endif
