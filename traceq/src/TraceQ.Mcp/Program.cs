// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ;

// M0 scaffold for the MCP shim. The curated tool surface (trace_info,
// trace_rank with a metric selector, trace_callers, trace_diff, trace_export,
// trace_trim, ...) is built in M3 over the shared TraceQ.Core service layer,
// with stderr-only logging and the stdout-purity guarantee. This stub exists so
// the ModelContextProtocol + Hosting dependencies resolve and the shim is wired
// to the same core assembly the CLI uses.

Console.Error.WriteLine(
    $"traceq MCP facade ({TraceQCore.Milestone} scaffold): not implemented until milestone M3.");
return 0;
