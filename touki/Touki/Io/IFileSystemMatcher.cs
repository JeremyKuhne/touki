// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Reusable definition that creates independent matcher sessions for file-system enumerations.
/// </summary>
public interface IFileSystemMatcher
{
    /// <summary>
    ///  Creates a matcher session bound to the specified normalized enumeration root.
    /// </summary>
    /// <param name="rootDirectory">
    ///  The fully qualified root used by the enumeration. A trailing separator is present only when
    ///  the path is itself a file-system root.
    /// </param>
    /// <returns>A new session owned by the caller.</returns>
    IFileSystemMatcherSession CreateSession(string rootDirectory);
}