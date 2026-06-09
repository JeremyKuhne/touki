// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Cli;

/// <summary>
///  Which time measure a ranking verb reports.
/// </summary>
internal enum Measure
{
    /// <summary>
    ///  Self time: the weight charged to each frame as the executing leaf, with
    ///  JIT-helper leaves folded into the real method that incurred them.
    /// </summary>
    Self,

    /// <summary>
    ///  Inclusive time: the weight charged to a frame and everything it calls.
    /// </summary>
    Inclusive
}
