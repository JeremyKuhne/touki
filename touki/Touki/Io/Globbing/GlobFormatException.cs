// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Thrown by <see cref="GlobSpecification.Compile(StringSegment, GlobDialect, GlobOptions, GlobPathSeparator, int)"/>
///  when the source pattern is not a valid glob for the requested dialect.
/// </summary>
public sealed class GlobFormatException : FormatException
{
    /// <summary>
    ///  Initializes a new instance of the <see cref="GlobFormatException"/> class.
    /// </summary>
    /// <param name="error">The structured description of the failure.</param>
    public GlobFormatException(GlobCompileError error)
        : base(error.Message)
    {
        Error = error;
    }

    /// <summary>
    ///  Gets the structured description of the failure.
    /// </summary>
    public GlobCompileError Error { get; }
}
