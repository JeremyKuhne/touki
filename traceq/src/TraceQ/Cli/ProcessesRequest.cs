// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Cli;

/// <summary>
///  The validated inputs to a process-inventory run: which trace to load and how to
///  render it. The inventory always reads every process (it exists to list them), so
///  it carries no process-scope option.
/// </summary>
/// <param name="Path">The trace file path.</param>
/// <param name="Format">The render format.</param>
internal sealed record ProcessesRequest(
    string Path,
    OutputFormat Format);
