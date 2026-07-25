// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

internal sealed partial class CompiledGlobStrategy
{
    /// <summary>
    ///  A contiguous half-open program slice <c>[Start, End)</c>. The optional
    ///  <see cref="KindOverride"/> rewrites the kind of an <see cref="GlobOpCodes.AltStart"/>
    ///  found at <see cref="Start"/>: used so the re-entry of a <c>+(...)</c> block
    ///  during subsequent iterations behaves like <c>*(...)</c> (first iteration was
    ///  already taken; further iterations are optional).
    /// </summary>
    private struct ProgramRange
    {
        public int Start;
        public int End;
        public char KindOverride;

        // For a re-prepended repeating block (KindOverride set), the input index
        // at which this iteration started. The progress guard refuses to take a
        // further iteration unless input advanced past this value, preventing
        // unbounded empty iterations of constructs like *(...) / +(*|*).
        public int MinProgressInput;
    }
}
