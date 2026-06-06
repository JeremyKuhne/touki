// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.Diagnostics.Tracing.Etlx;

namespace TraceQ.Tracing.Readers;

/// <summary>
///  Reads a Windows ETW trace (<c>.etl</c>) - the net481 <c>[EtwProfiler]</c>
///  output - into weighted samples. This closes the net481 gap: the same
///  rankings an agent can self-serve from a net10 EventPipe trace become
///  available for Framework profiling.
/// </summary>
internal sealed class EtlReader : TraceLogReader
{
    /// <inheritdoc/>
    public override TraceFormat Format => TraceFormat.Etl;

    /// <inheritdoc/>
    public override bool CanRead(string path) =>
        path.EndsWith(".etl", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    protected override TraceLog OpenTraceLog(string path) =>
        TraceLog.OpenOrConvert(path, new TraceLogOptions { ContinueOnError = true });
}
