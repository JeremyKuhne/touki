// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

internal sealed partial class CompiledGlobStrategy
{
    /// <summary>
    ///  The immutable inputs to an extglob match: the virtual
    ///  <see cref="First"/> + <see cref="Second"/> input, the compiled
    ///  <see cref="Program"/>, and the case/separator policy. Bundled so the
    ///  engine setup and the bounded negation re-entry pass a single documented
    ///  value instead of threading five loose parameters through every call.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Only ever used as a local or an <c>in</c> parameter, never stored as a
    ///   field of <see cref="ExtGlobEngine"/>: a span-bearing ref struct used as
    ///   a <em>field</em> of another ref struct requires the <c>ByRefFields</c>
    ///   runtime feature that net481 lacks. The engine therefore unpacks this
    ///   into its own individual span fields in its constructor.
    ///  </para>
    /// </remarks>
    private readonly ref struct EngineInputs
    {
        /// <summary>Directory-prefix span; the first half of the virtual input.</summary>
        public readonly ReadOnlySpan<char> First;

        /// <summary>File-name span; the second half of the virtual input.</summary>
        public readonly ReadOnlySpan<char> Second;

        /// <summary>The compiled bytecode program (extglob subset).</summary>
        public readonly ReadOnlySpan<char> Program;

        /// <summary>Path separator, or <c>'\0'</c> when the matcher is path-unaware.</summary>
        public readonly char Separator;

        /// <summary>Case-sensitivity policy for literal/class comparisons.</summary>
        public readonly IgnoreCaseKind Kind;

        public EngineInputs(
            ReadOnlySpan<char> first,
            ReadOnlySpan<char> second,
            ReadOnlySpan<char> program,
            char separator,
            IgnoreCaseKind kind)
        {
            First = first;
            Second = second;
            Program = program;
            Separator = separator;
            Kind = kind;
        }
    }
}
