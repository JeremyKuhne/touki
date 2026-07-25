// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

public sealed partial class GlobSpecification
{
    /// <summary>
    ///  Maximum number of <c>|</c>-separated alternatives in a single extended-glob
    ///  construct. Exceeding this raises
    ///  <see cref="GlobCompileErrorCode.FeatureLimitExceeded"/>.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   This is the single source of truth for the alternative cap. The encoder
    ///   enforces it and sizes the offset table it bakes into the
    ///   <see cref="GlobOpCodes.AltStart"/> header by it; the matcher's
    ///   fixed-size offset-read buffer in <see cref="CompiledGlobStrategy"/>
    ///   references this same constant so the two sides can never drift.
    ///  </para>
    /// </remarks>
    internal const int MaxExtGlobAlternatives = 32;

    /// <summary>
    ///  Maximum nesting depth of extended-glob constructs (<c>?(…)</c>, <c>*(…)</c>,
    ///  <c>+(…)</c>, <c>@(…)</c>, <c>!(…)</c>). Exceeding this raises
    ///  <see cref="GlobCompileErrorCode.FeatureLimitExceeded"/>. The cap exists so
    ///  the interpreter's stack-allocated savepoint buffer stays bounded: with
    ///  this depth and the per-construct alternative cap, simultaneous savepoints
    ///  are guaranteed to fit in the fixed runtime budget.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   This is the single source of truth for the nesting cap. The encoder
    ///   enforces it; the matcher derives its fixed range-list ceiling
    ///   (<see cref="CompiledGlobStrategy"/>'s <c>MaxRangesDepth</c>) from it so the
    ///   two sides can never drift.
    ///  </para>
    /// </remarks>
    internal const int MaxExtGlobDepth = 8;
}
