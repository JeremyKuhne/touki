// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ;

// M0 scaffold CLI. The full verb set - the engine verbs (rank / callers / tree /
// lines / heatmap / diff / export), the family shortcuts (cpu / threadtime /
// alloc / gcstats / ...), and the file ops (convert / clean / trim) - lands in
// M2 over the TraceQ.Core service layer.

if (args.Length == 0)
{
    Console.Error.WriteLine(
        $"traceq ({TraceQCore.Milestone} scaffold): no verbs are implemented yet. "
        + "See docs/traceq-implementation-plan.md for the planned surface.");
    return 1;
}

Console.Error.WriteLine(
    $"traceq: unrecognized verb '{args[0]}'. The verb set arrives in milestone M2.");
return 1;
