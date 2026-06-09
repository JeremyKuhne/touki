// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Cli;

/// <summary>
///  The flame-graph file format the <c>export</c> verb writes.
/// </summary>
internal enum ExportFormat
{
    /// <summary>
    ///  The speedscope sampled-profile format, opened at speedscope.app. The default.
    /// </summary>
    Speedscope,

    /// <summary>
    ///  The Chrome Trace Event Format, opened in chrome://tracing or the Perfetto UI.
    /// </summary>
    Chromium
}
