// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Cli;

// The M2 CLI head dispatches the verb set over the TraceQ.Core service layer,
// parsed by ConsoleAppFramework (a source generator, no runtime dependency). The
// engine 'rank' verb and the 'cpu' provider shortcut are the first slice; the
// remaining verbs (callers / tree / lines / heatmap / diff / export, the other
// family shortcuts, and the file ops convert / clean / trim) register into the
// same TraceCommands surface as they land.
return CliApp.Run(args);
