// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

internal sealed partial class CompiledGlobStrategy
{
    /// <summary>
    ///  A choice point on the explicit backtrack stack. Captures the walker
    ///  configuration at the choice opcode so each alternative can be tried in
    ///  order and the state restored on backtrack. Per-kind derived data
    ///  (alternative offsets, block bounds, separator-bounded limits) is
    ///  recomputed from the program on revisit to keep the frame small.
    /// </summary>
    private struct Frame
    {
        // The resolved choice kind: the AnyRun/GlobStar opcode, or the resolved
        // alternation kind ('@', '?', '+', '*', '!').
        public char Kind;

        // Index of the choice opcode in the program.
        public int ProgramIndex;

        // inputIndex at the choice point; every alternative restarts from here.
        public int SavedInput;

        // Range-snapshot location in the arena: the configuration head..count
        // captured at entry.
        public int SnapshotOffset;
        public int SnapshotCount;

        // Next-alternative cursor; meaning depends on Kind (next consumed length
        // for AnyRun, next alt index for alternations, next candidate length for
        // negation, started-flag for GlobStar).
        public int Cursor;

        // Per-kind scratch carried across alternatives: AnyRun caches the
        // separator-bounded limit; GlobStar caches the last absorbed length.
        public int Aux;
    }
}
