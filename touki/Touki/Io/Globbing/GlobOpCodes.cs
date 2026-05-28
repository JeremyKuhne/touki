// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Opcode markers used by <see cref="CompiledGlobStrategy"/>'s bytecode-in-a-string
///  encoding. Values are Unicode noncharacters (<c>U+FDD0..U+FDD8</c>), which are
///  reserved by the Unicode standard for application-internal use and never appear
///  in conforming text.
/// </summary>
internal static class GlobOpCodes
{
    /// <summary>Match exactly one character (<c>?</c>). No payload.</summary>
    public const char Any = '\uFDD0';

    /// <summary>Match zero or more characters (<c>*</c>). No payload.</summary>
    public const char AnyRun = '\uFDD1';

    /// <summary>
    ///  Literal run. Followed by one length character and that many literal characters.
    /// </summary>
    public const char Literal = '\uFDD2';

    /// <summary>
    ///  Positive character class. Followed by one length character and that many body
    ///  characters (the original content between <c>[</c> and <c>]</c>, escapes resolved).
    /// </summary>
    public const char Class = '\uFDD3';

    /// <summary>
    ///  Negated character class. Same shape as <see cref="Class"/>.
    /// </summary>
    public const char NegClass = '\uFDD4';

    /// <summary>
    ///  Globstar (<c>**</c>). Matches zero or more path segments (including the separators
    ///  between them) under <see cref="GlobOptions.AllowGlobStar"/>. Followed by one
    ///  payload char whose low two bits encode which surrounding separators the scanner
    ///  absorbed into this opcode: bit 0 (<see cref="GlobStarFlagLead"/>) means the
    ///  pattern had a separator immediately before the <c>**</c> token; bit 1
    ///  (<see cref="GlobStarFlagTrail"/>) means a separator followed it. The two bits
    ///  together describe four shapes-whole-pattern <c>**</c> (neither),
    ///  leading <c>**/</c> (trail only), trailing <c>/**</c> (lead only), and middle
    ///  <c>/**/</c> (both)-each with their own validity constraints on the
    ///  absorbed input slice.
    /// </summary>
    public const char GlobStar = '\uFDD5';

    /// <summary>
    ///  GlobStar payload bit: a path separator preceded the <c>**</c> in the source
    ///  pattern and was absorbed by this opcode. Equivalent to "the matched slice must
    ///  start with the separator (or be empty when paired with a non-set
    ///  <see cref="GlobStarFlagTrail"/>)".
    /// </summary>
    public const int GlobStarFlagLead = 1;

    /// <summary>
    ///  GlobStar payload bit: a path separator followed the <c>**</c> in the source
    ///  pattern and was absorbed by this opcode. Equivalent to "the matched slice must
    ///  end with the separator (or be empty when paired with a non-set
    ///  <see cref="GlobStarFlagLead"/>)".
    /// </summary>
    public const int GlobStarFlagTrail = 2;

    /// <summary>
    ///  Start of an extglob alternation block (one of <c>?(…)</c>, <c>*(…)</c>,
    ///  <c>+(…)</c>, <c>@(…)</c>, <c>!(…)</c>). Followed by two payload chars:
    ///  the <i>kind</i> character (one of <c>'?'</c>, <c>'*'</c>, <c>'+'</c>,
    ///  <c>'@'</c>, <c>'!'</c>) and the total length (in chars) of the block
    ///  from <see cref="AltStart"/> through the matching <see cref="AltEnd"/>
    ///  inclusive. The block contains zero or more alternative bodies separated
    ///  by <see cref="AltSep"/>, with each body being a self-contained sub-program.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Emitted only when <see cref="GlobOptions.AllowExtGlob"/> is set at compile
    ///   time. Nested alternations encode recursively with their own
    ///   <see cref="AltStart"/>/<see cref="AltEnd"/> pair.
    ///  </para>
    /// </remarks>
    public const char AltStart = '\uFDD6';

    /// <summary>
    ///  Separator between alternatives inside an extglob block. No payload.
    ///  Always paired with an enclosing <see cref="AltStart"/> /
    ///  <see cref="AltEnd"/>.
    /// </summary>
    public const char AltSep = '\uFDD7';

    /// <summary>
    ///  End of an extglob alternation block. No payload. Matches the most
    ///  recent unmatched <see cref="AltStart"/>.
    /// </summary>
    public const char AltEnd = '\uFDD8';
}
