// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Tracing;

/// <summary>
///  The on-disk format a trace was loaded from.
/// </summary>
internal enum TraceFormat
{
    /// <summary>
    ///  BenchmarkDotNet EventPipe speedscope export (<c>.speedscope.json</c>).
    /// </summary>
    Speedscope,

    /// <summary>
    ///  .NET EventPipe trace (<c>.nettrace</c>).
    /// </summary>
    NetTrace,

    /// <summary>
    ///  Windows ETW trace (<c>.etl</c>), the net481 <c>[EtwProfiler]</c> output.
    /// </summary>
    Etl
}
