// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

internal sealed partial class CompiledGlobStrategy
{
    /// <summary>
    ///  The reusable scratch buffers an <see cref="ExtGlobEngine"/> run consumes.
    ///  Bundled so the engine setup and the negation re-entry pass one documented
    ///  value instead of five loose span parameters.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Like <see cref="EngineInputs"/>, only used as a local or an <c>in</c>
    ///   parameter (never a ref-struct field) for net481 compatibility. The seed
    ///   buffers are <c>stackalloc</c>-backed; the engine spills <see cref="Frames"/>
    ///   and <see cref="Arena"/> to <see cref="ArrayPool{T}"/> only when an
    ///   adversarial input outgrows the seed.
    ///  </para>
    /// </remarks>
    private readonly ref struct EngineScratch
    {
        /// <summary>Explicit backtrack stack of choice points.</summary>
        public readonly Span<Frame> Frames;

        /// <summary>Range-snapshot arena backing each frame's saved configuration.</summary>
        public readonly Span<ProgramRange> Arena;

        /// <summary>The active ("work") range list the forward loop advances.</summary>
        public readonly Span<ProgramRange> Work;

        /// <summary>Scratch list used while building an alternative's range list.</summary>
        public readonly Span<ProgramRange> Rest;

        /// <summary>Failure-memo serialization key buffer.</summary>
        public readonly Span<int> Key;

        public EngineScratch(
            Span<Frame> frames,
            Span<ProgramRange> arena,
            Span<ProgramRange> work,
            Span<ProgramRange> rest,
            Span<int> key)
        {
            Frames = frames;
            Arena = arena;
            Work = work;
            Rest = rest;
            Key = key;
        }
    }
}
