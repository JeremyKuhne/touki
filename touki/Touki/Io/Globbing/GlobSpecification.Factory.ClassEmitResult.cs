// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

public sealed partial class GlobSpecification
{
    private static partial class Factory
    {
        /// <summary>
        ///  Outcome of <see cref="TryEmitClass"/>: a real bracket-class was emitted,
        ///  the <c>[</c> was unterminated and should be treated as a literal, or the
        ///  class body exceeded <see cref="MaxOpcodeBodyLength"/>.
        /// </summary>
        private enum ClassEmitResult
        {
            Emitted,
            NotClass,
            Overflow,
        }
    }
}
