// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Fuzz;

/// <summary>
///  Thrown by a fuzz target when a structural invariant is violated.
/// </summary>
/// <remarks>
///  <para>
///   SharpFuzz reports any unhandled exception as a crash. A dedicated type makes invariant failures easy
///   to distinguish from incidental framework exceptions when triaging findings.
///  </para>
/// </remarks>
internal sealed class FuzzInvariantException : Exception
{
    public FuzzInvariantException(string message)
        : base(message)
    {
    }
}
