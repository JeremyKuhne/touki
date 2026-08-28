// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

public sealed partial class GlobSpecification
{
    private static partial class Factory
    {
        /// <summary>
        ///  Records the in-progress position and length of the most recently emitted
        ///  <see cref="GlobOpCodes.Literal"/> opcode within the encoder's
        ///  <see cref="ValueStringBuilder"/>. <see cref="TryEncodeProgram"/> uses this so the
        ///  <see cref="GlobOpCodes.GlobStar"/> emitter can retroactively strip the trailing
        ///  separator from the prior Literal when a segment-bounded <c>**</c> absorbs it.
        ///  <see cref="None"/> represents "no Literal currently at the tail of the buffer"
        ///  (the most recent opcode is something else, or the buffer is empty).
        ///  <see cref="GlobStar"/> tags the cursor as "most recent opcode is a GlobStar
        ///  whose flag byte is at <c>Start</c>"; used by the GlobStar emitter to collapse
        ///  adjacent `**/**` runs without relying on a fragile buffer-tail char peek.
        /// </summary>
        private struct LiteralCursor
        {
            public int Start;
            public int Length;

            public static LiteralCursor None => new() { Start = -1, Length = 0 };

            /// <summary>
            ///  Tags the cursor as "the most recently emitted opcode is a
            ///  <see cref="GlobOpCodes.GlobStar"/> with its flag byte at
            ///  <paramref name="flagIndex"/>". Read back via
            ///  <see cref="IsGlobStar"/> and <see cref="GlobStarFlagIndex"/>.
            ///  Uses <c>Length = -1</c> as the sentinel so the existing
            ///  <see cref="IsValid"/> check (<c>Start &gt;= 0</c>) keeps reporting
            ///  "not a Literal".
            /// </summary>
            /// <param name="flagIndex">The index of the globstar flag byte in the encoded program.</param>
            /// <returns>The tagged cursor.</returns>
            public static LiteralCursor GlobStar(int flagIndex) => new() { Start = flagIndex, Length = -1 };

            public readonly bool IsValid => Start >= 0 && Length >= 0;

            public readonly bool IsGlobStar => Start >= 0 && Length == -1;

            public readonly int GlobStarFlagIndex => Start;
        }
    }
}
