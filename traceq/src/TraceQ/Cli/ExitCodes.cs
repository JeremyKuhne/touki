// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Cli;

/// <summary>
///  The process exit codes the CLI verbs return.
/// </summary>
/// <remarks>
///  <para>
///   These are the codes the M2 head returns (implementation-plan section 4.4.6):
///   <see cref="Success"/> for a completed query, <see cref="UsageError"/> for a
///   bad command line, <see cref="InputError"/> when the trace could not be
///   loaded, and <see cref="StrictGate"/> when an otherwise successful run tripped
///   the <c>--strict</c> symbol-resolution gate.
///  </para>
/// </remarks>
internal static class ExitCodes
{
    /// <summary>
    ///  The verb completed and produced a result.
    /// </summary>
    public const int Success = 0;

    /// <summary>
    ///  The command line was malformed (unknown verb, missing or invalid option).
    /// </summary>
    public const int UsageError = 1;

    /// <summary>
    ///  The trace could not be loaded (missing file or unrecognized format).
    /// </summary>
    public const int InputError = 2;

    /// <summary>
    ///  The run succeeded but <c>--strict</c> was set and symbol resolution was
    ///  below the trusted threshold.
    /// </summary>
    public const int StrictGate = 3;
}
