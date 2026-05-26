// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Describes a compilation failure produced by
///  <see cref="GlobSpecification.TryCompile(StringSegment, GlobDialect, GlobOptions, out GlobSpecification, out GlobCompileError)"/>.
/// </summary>
public readonly struct GlobCompileError
{
    /// <summary>
    ///  Initializes a new instance of the <see cref="GlobCompileError"/> struct.
    /// </summary>
    /// <param name="code">The classification of the failure.</param>
    /// <param name="position">
    ///  Zero-based index into the source pattern where the failure was detected, or
    ///  <c>-1</c> when the failure is not tied to a specific position.
    /// </param>
    /// <param name="message">A human-readable description of the failure.</param>
    public GlobCompileError(GlobCompileErrorCode code, int position, string message)
    {
        Code = code;
        Position = position;
        Message = message;
    }

    /// <inheritdoc cref="GlobCompileError(GlobCompileErrorCode, int, string)"/>
    public GlobCompileError(GlobCompileErrorCode code, string message) : this(code, -1, message) { }

    /// <summary>
    ///  Gets the classification of the failure.
    /// </summary>
    public GlobCompileErrorCode Code { get; }

    /// <summary>
    ///  Gets the zero-based index into the source pattern where the failure was detected,
    ///  or <c>-1</c> when no position is available.
    /// </summary>
    public int Position { get; }

    /// <summary>
    ///  Gets a human-readable description of the failure.
    /// </summary>
    public string Message { get; }

    /// <summary>
    ///  Gets a value indicating whether this error represents a failure.
    /// </summary>
    public bool IsError => Code != GlobCompileErrorCode.None;
}
