// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Cli;

/// <summary>
///  The format a verb renders its result in.
/// </summary>
internal enum OutputFormat
{
    /// <summary>
    ///  Dense fixed-width text for a human reading at the terminal. The default.
    /// </summary>
    Text,

    /// <summary>
    ///  Compact, deterministic JSON - the agent-facing wire format.
    /// </summary>
    Json
}
