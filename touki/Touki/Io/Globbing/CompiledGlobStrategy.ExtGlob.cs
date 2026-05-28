// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Recursive matcher used when the compiled program contains
///  <see cref="GlobOpCodes.AltStart"/> opcodes (extended-glob alternation
///  constructs). Trades the iterative two-slot backtrack of the non-extglob
///  fast paths for a recursive "concatenation of program ranges" walker that
///  naturally handles nested alternations.
/// </summary>
/// <remarks>
///  <para>
///   The matcher walks a small <see cref="Span{T}"/> of <see cref="ProgramRange"/>
///   entries; the first entry is the &quot;current&quot; sub-program and any
///   additional entries are the &quot;rest&quot; (typically the slice past the
///   alternation block). On <see cref="GlobOpCodes.AltStart"/> the matcher
///   prepends an alternative's range and recurses; for repeating constructs
///   (<c>*(...)</c>, <c>+(...)</c>) it also re-prepends the same alternation block
///   so further iterations can be attempted before falling through to the rest.
///  </para>
///  <para>
///   The <c>totalLength</c> parameter threaded through the walker
///   lets callers run the matcher against a clipped input range (matching some
///   prefix of the virtual <c>first + second</c> concatenation rather than the
///   whole thing). The negation handler relies on this to ask &quot;does
///   alternative <i>p</i> consume exactly <i>L</i> input characters?&quot;.
///  </para>
///  <para>
///   Recursion depth is bounded by the encoder's <c>MaxExtGlobDepth</c> cap
///   plus the maximum number of pending iterations (also bounded by the input
///   length). The ranges span is always stack-allocated.
///  </para>
/// </remarks>
internal sealed partial class CompiledGlobStrategy
{
    private const int MaxRangesDepth = 32;

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
    }

    /// <summary>
    ///  Entry point used by <see cref="MatchCore"/> when the program contains
    ///  one or more <see cref="GlobOpCodes.AltStart"/> opcodes.
    /// </summary>
    private bool MatchExtGlob(
        ReadOnlySpan<char> first,
        ReadOnlySpan<char> second,
        ReadOnlySpan<char> program,
        char separator,
        IgnoreCaseKind kind)
    {
        Span<ProgramRange> ranges = stackalloc ProgramRange[MaxRangesDepth];
        ranges[0] = new ProgramRange { Start = 0, End = program.Length };
        int totalLength = first.Length + second.Length;
        return TryMatchRanges(first, second, program, ranges[..1], inputIndex: 0, totalLength, separator, kind);
    }

    /// <summary>
    ///  Returns <see langword="true"/> if the concatenation of the program
    ///  slices in <paramref name="ranges"/> matches <paramref name="first"/> +
    ///  <paramref name="second"/> starting at <paramref name="inputIndex"/>
    ///  and consuming exactly up to <paramref name="totalLength"/>.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Deterministic opcodes (<see cref="GlobOpCodes.Literal"/>,
    ///   <see cref="GlobOpCodes.Any"/>, <see cref="GlobOpCodes.Class"/>,
    ///   <see cref="GlobOpCodes.NegClass"/>) and the leading-empty-range skip
    ///   are inlined into the top-level <c>while</c> loop so a run of straight
    ///   opcodes processes in a single stack frame. Only choice points
    ///   (<see cref="GlobOpCodes.AltStart"/>, <see cref="GlobOpCodes.AnyRun"/>,
    ///   <see cref="GlobOpCodes.GlobStar"/>) tail-recurse, because they need to
    ///   try alternatives and backtrack on failure.
    ///  </para>
    ///  <para>
    ///   <b>net481 design note.</b> The inline loop matters for .NET Framework
    ///   4.8.1 RyuJIT, which does not tail-call methods that take
    ///   <see cref="Span{T}"/> / <see cref="ReadOnlySpan{T}"/> parameters and
    ///   pays a per-frame cost to re-materialize those spans on entry. Folding
    ///   the deterministic advances into a <c>while</c> loop removed the
    ///   majority of the recursive frames for typical patterns
    ///   (e.g. <c>**/@(*.cs|*.md|...)</c> processes each file in 1-3
    ///   frames instead of one frame per opcode); on the enumeration benchmark
    ///   this closed the bulk of the net481 throughput gap. Modern .NET RyuJIT
    ///   handles span tail-calls efficiently and sees only a small win, but the
    ///   form is identical so there is no <c>#if</c> split.
    ///  </para>
    /// </remarks>
    private static bool TryMatchRanges(
        ReadOnlySpan<char> first,
        ReadOnlySpan<char> second,
        ReadOnlySpan<char> program,
        Span<ProgramRange> ranges,
        int inputIndex,
        int totalLength,
        char separator,
        IgnoreCaseKind kind)
    {
        int firstLength = first.Length;

        while (true)
        {
            // Skip any leading empty ranges; an alternation's "rest" range may
            // be empty when the alternation is the final construct in the
            // program. Inlined into the dispatch loop so a string of empties
            // does not recurse.
            while (ranges.Length > 0 && ranges[0].Start >= ranges[0].End)
            {
                ranges = ranges[1..];
            }

            if (ranges.Length == 0)
            {
                return inputIndex == totalLength;
            }

            int programIndex = ranges[0].Start;
            char opcode = program[programIndex];

            switch (opcode)
            {
                case GlobOpCodes.Literal:
                    {
                        int literalLength = program[programIndex + 1];
                        if (inputIndex + literalLength > totalLength)
                        {
                            return false;
                        }

                        if (!LiteralMatchesAt(first, second, inputIndex, program.Slice(programIndex + 2, literalLength), kind))
                        {
                            return false;
                        }

                        // Deterministic advance: rewrite the head range in place
                        // and loop instead of recursing. This is the dominant
                        // form on net481 (e.g. the literal tail of every alt body
                        // in `@(*.cs|*.md|...)` extensions).
                        ranges[0].Start = programIndex + 2 + literalLength;
                        inputIndex += literalLength;
                        continue;
                    }

                case GlobOpCodes.Any:
                    {
                        if (inputIndex >= totalLength)
                        {
                            return false;
                        }

                        char inputChar = CharAt(first, second, firstLength, inputIndex);
                        if (separator != '\0' && inputChar == separator)
                        {
                            return false;
                        }

                        ranges[0].Start = programIndex + 1;
                        inputIndex++;
                        continue;
                    }

                case GlobOpCodes.Class:
                case GlobOpCodes.NegClass:
                    {
                        int classLength = program[programIndex + 1];
                        if (inputIndex >= totalLength)
                        {
                            return false;
                        }

                        char inputChar = CharAt(first, second, firstLength, inputIndex);
                        if (separator != '\0' && inputChar == separator)
                        {
                            return false;
                        }

                        ReadOnlySpan<char> body = program.Slice(programIndex + 2, classLength);
                        bool inClass = kind == IgnoreCaseKind.Off
                            ? ClassContainsOrdinal(body, inputChar, opcode == GlobOpCodes.NegClass)
                            : ClassContainsIgnoreCase(body, inputChar, opcode == GlobOpCodes.NegClass);
                        if (!inClass)
                        {
                            return false;
                        }

                        ranges[0].Start = programIndex + 2 + classLength;
                        inputIndex++;
                        continue;
                    }

                case GlobOpCodes.AltStart:
                    return DispatchAlternation(first, second, program, ranges, inputIndex, totalLength, separator, kind);

                case GlobOpCodes.AnyRun:
                    {
                        // Choice point: try every consumed length from 0 to either
                        // the next separator (path-aware) or the clipped input
                        // end. Stays a recursive call: the recursion lets us
                        // restart from the saved head on each retry without
                        // tracking program state across iterations.
                        ProgramRange savedHead = ranges[0];
                        int limit = totalLength;
                        if (separator != '\0')
                        {
                            for (int j = inputIndex; j < totalLength; j++)
                            {
                                if (CharAt(first, second, firstLength, j) == separator)
                                {
                                    limit = j;
                                    break;
                                }
                            }
                        }

                        for (int consumed = 0; inputIndex + consumed <= limit; consumed++)
                        {
                            ranges[0] = savedHead;
                            ranges[0].Start = programIndex + 1;
                            if (TryMatchRanges(first, second, program, ranges, inputIndex + consumed, totalLength, separator, kind))
                            {
                                return true;
                            }
                        }

                        return false;
                    }

                case GlobOpCodes.GlobStar:
                    {
                        int flags = program[programIndex + 1];

                        // Choice point. The recursive call can mutate ranges[0]
                        // (DispatchAlternation advances Start past the alternation
                        // block, for example). Snapshot and restore across each
                        // retry so every iteration starts from the same
                        // "program continues at programIndex+2" state.
                        ProgramRange savedHead = ranges[0];
                        int absorbed = FirstValidGlobStarLength(first, second, inputIndex, flags, separator);
                        while (absorbed >= 0)
                        {
                            if (inputIndex + absorbed > totalLength)
                            {
                                break;
                            }

                            ranges[0] = savedHead;
                            ranges[0].Start = programIndex + 2;
                            if (TryMatchRanges(first, second, program, ranges, inputIndex + absorbed, totalLength, separator, kind))
                            {
                                return true;
                            }

                            absorbed = NextValidGlobStarLength(first, second, inputIndex, absorbed, flags, separator);
                        }

                        return false;
                    }

                case GlobOpCodes.AltSep:
                case GlobOpCodes.AltEnd:
                    // These appear only inside an alternation block; the
                    // alternation handler slices the range at AltSep / AltEnd
                    // boundaries so they never reach the top-level walker.
                    Debug.Fail("Encountered AltSep/AltEnd outside an alternation block.");
                    return false;

                default:
                    Debug.Fail($"Unexpected opcode '{opcode:X4}' in extglob program.");
                    return false;
            }
        }
    }

    /// <summary>
    ///  Handles an <see cref="GlobOpCodes.AltStart"/> at the head of
    ///  <paramref name="ranges"/>: locates the alternatives, dispatches to the
    ///  kind-specific match strategy, and stitches the &quot;rest&quot; of the
    ///  program back onto the call.
    /// </summary>
    private static bool DispatchAlternation(
        ReadOnlySpan<char> first,
        ReadOnlySpan<char> second,
        ReadOnlySpan<char> program,
        Span<ProgramRange> ranges,
        int inputIndex,
        int totalLength,
        char separator,
        IgnoreCaseKind kind)
    {
        int altStartIndex = ranges[0].Start;
        char altKind = ranges[0].KindOverride != '\0'
            ? ranges[0].KindOverride
            : program[altStartIndex + 1];
        int blockLength = program[altStartIndex + 2];
        int afterEnd = altStartIndex + blockLength;
        int altsStart = altStartIndex + 3;
        int altEndIndex = afterEnd - 1;

        // The "rest" of the current head range starts past AltEnd. Reset the
        // KindOverride so subsequent walks past this alternation see the
        // bytecode kind unmodified.
        ranges[0].Start = afterEnd;
        ranges[0].KindOverride = '\0';

        Span<int> altStarts = stackalloc int[32];
        int altCount = SplitAlternatives(program, altsStart, altEndIndex, altStarts);

        switch (altKind)
        {
            case '@':
                return TryAlternativeOnce(first, second, program, ranges, altStarts[..altCount], altEndIndex, inputIndex, totalLength, separator, kind);

            case '?':
                if (TryAlternativeOnce(first, second, program, ranges, altStarts[..altCount], altEndIndex, inputIndex, totalLength, separator, kind))
                {
                    return true;
                }

                // Zero-consume: skip the entire alternation block.
                return TryMatchRanges(first, second, program, ranges, inputIndex, totalLength, separator, kind);

            case '+':
                return TryAlternativeRepeating(first, second, program, ranges, altStarts[..altCount], altEndIndex, altStartIndex, afterEnd, inputIndex, totalLength, separator, kind, requireAtLeastOne: true);

            case '*':
                return TryAlternativeRepeating(first, second, program, ranges, altStarts[..altCount], altEndIndex, altStartIndex, afterEnd, inputIndex, totalLength, separator, kind, requireAtLeastOne: false);

            case '!':
                return TryNegation(first, second, program, ranges, altStarts[..altCount], altEndIndex, inputIndex, totalLength, separator, kind);

            default:
                Debug.Fail($"Unknown extglob kind '{altKind}'.");
                return false;
        }
    }

    /// <summary>
    ///  For <c>?(...)</c> / <c>@(...)</c>: try matching each alternative
    ///  followed by the program's &quot;rest&quot; <paramref name="ranges"/>.
    ///  Returns <see langword="true"/> on the first successful alternative.
    /// </summary>
    private static bool TryAlternativeOnce(
        ReadOnlySpan<char> first,
        ReadOnlySpan<char> second,
        ReadOnlySpan<char> program,
        Span<ProgramRange> ranges,
        ReadOnlySpan<int> altStarts,
        int altEndIndex,
        int inputIndex,
        int totalLength,
        char separator,
        IgnoreCaseKind kind)
    {
        Span<ProgramRange> newRanges = stackalloc ProgramRange[MaxRangesDepth];
        for (int j = 0; j < altStarts.Length; j++)
        {
            int altBodyStart = altStarts[j];
            int altBodyEnd = (j + 1 < altStarts.Length) ? altStarts[j + 1] - 1 : altEndIndex;

            if (!BuildRangesWithAlternative(altBodyStart, altBodyEnd, ranges, newRanges, out int newRangeCount))
            {
                continue;
            }

            if (TryMatchRanges(first, second, program, newRanges[..newRangeCount], inputIndex, totalLength, separator, kind))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///  For <c>*(...)</c> / <c>+(...)</c>: try matching one alternative
    ///  followed by another invocation of the same alternation block (for
    ///  additional iterations) and then the program's &quot;rest&quot;
    ///  <paramref name="ranges"/>. When
    ///  <paramref name="requireAtLeastOne"/> is <see langword="false"/>, also
    ///  tries matching just the &quot;rest&quot; with no iterations consumed.
    /// </summary>
    private static bool TryAlternativeRepeating(
        ReadOnlySpan<char> first,
        ReadOnlySpan<char> second,
        ReadOnlySpan<char> program,
        Span<ProgramRange> ranges,
        ReadOnlySpan<int> altStarts,
        int altEndIndex,
        int blockStart,
        int blockEnd,
        int inputIndex,
        int totalLength,
        char separator,
        IgnoreCaseKind kind,
        bool requireAtLeastOne)
    {
        Span<ProgramRange> newRanges = stackalloc ProgramRange[MaxRangesDepth];
        for (int j = 0; j < altStarts.Length; j++)
        {
            int altBodyStart = altStarts[j];
            int altBodyEnd = (j + 1 < altStarts.Length) ? altStarts[j + 1] - 1 : altEndIndex;

            if (!BuildRangesWithAlternativeAndBlock(
                    altBodyStart,
                    altBodyEnd,
                    blockStart,
                    blockEnd,
                    ranges,
                    newRanges,
                    out int newRangeCount))
            {
                continue;
            }

            if (TryMatchRangesProgressGuarded(
                    first,
                    second,
                    program,
                    newRanges[..newRangeCount],
                    inputIndex,
                    minimumProgressInputIndex: inputIndex + 1,
                    totalLength,
                    separator,
                    kind))
            {
                return true;
            }
        }

        if (requireAtLeastOne)
        {
            return false;
        }

        // Zero iterations: just match the rest.
        return TryMatchRanges(first, second, program, ranges, inputIndex, totalLength, separator, kind);
    }

    /// <summary>
    ///  Variant of <see cref="TryMatchRanges"/> that, on re-entering a repeating
    ///  alternation block via <see cref="TryAlternativeRepeating"/>, refuses to
    ///  try another iteration unless the previous iteration consumed at least
    ///  one input character (<paramref name="minimumProgressInputIndex"/>). This
    ///  avoids infinite recursion on constructs that admit empty matches such
    ///  as <c>*(|)</c>. The repeating-block range is identified by its
    ///  <see cref="ProgramRange.KindOverride"/> being set.
    /// </summary>
    private static bool TryMatchRangesProgressGuarded(
        ReadOnlySpan<char> first,
        ReadOnlySpan<char> second,
        ReadOnlySpan<char> program,
        Span<ProgramRange> ranges,
        int inputIndex,
        int minimumProgressInputIndex,
        int totalLength,
        char separator,
        IgnoreCaseKind kind)
    {
        while (ranges.Length > 0 && ranges[0].Start >= ranges[0].End)
        {
            ranges = ranges[1..];
        }

        if (ranges.Length > 0
            && ranges[0].KindOverride != '\0'
            && inputIndex < minimumProgressInputIndex
            && program[ranges[0].Start] == GlobOpCodes.AltStart)
        {
            int altStartIdx = ranges[0].Start;
            int blockLen = program[altStartIdx + 2];
            ranges[0].Start = altStartIdx + blockLen;
            ranges[0].KindOverride = '\0';
        }

        return TryMatchRanges(first, second, program, ranges, inputIndex, totalLength, separator, kind);
    }

    /// <summary>
    ///  For <c>!(...)</c>: succeed iff there exists a consumed length <c>L</c>
    ///  in <c>[0, maxL]</c> such that no alternative matches the input slice
    ///  <c>input[inputIndex..inputIndex+L]</c> exactly, AND the program's
    ///  &quot;rest&quot; <paramref name="ranges"/> matches the remainder.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   When path-aware, <c>maxL</c> is bounded by the next path separator
    ///   (same constraint as <see cref="GlobOpCodes.AnyRun"/>): a negation in
    ///   bash's pathname-expansion context never crosses <c>/</c>.
    ///  </para>
    ///  <para>
    ///   "Alternative matches exactly L chars" is implemented by calling
    ///   <see cref="TryMatchRanges"/> with a single alt-body range and the
    ///   <c>totalLength</c> clipped to <c>inputIndex + L</c>; the recursion
    ///   succeeds only when the alt body consumes the whole clipped slice.
    ///  </para>
    /// </remarks>
    private static bool TryNegation(
        ReadOnlySpan<char> first,
        ReadOnlySpan<char> second,
        ReadOnlySpan<char> program,
        Span<ProgramRange> ranges,
        ReadOnlySpan<int> altStarts,
        int altEndIndex,
        int inputIndex,
        int totalLength,
        char separator,
        IgnoreCaseKind kind)
    {
        int firstLength = first.Length;
        int maxL = totalLength - inputIndex;
        if (separator != '\0')
        {
            for (int j = inputIndex; j < totalLength; j++)
            {
                if (CharAt(first, second, firstLength, j) == separator)
                {
                    maxL = j - inputIndex;
                    break;
                }
            }
        }

        Span<ProgramRange> altRanges = stackalloc ProgramRange[1];

        // Snapshot the caller's ranges so that each L iteration starts from the
        // same "rest of program after !()" state. The recursive TryMatchRanges
        // call below can advance ranges[0].Start as it consumes the rest, and
        // we'd otherwise carry that mutation into the next L.
        Span<ProgramRange> savedRanges = stackalloc ProgramRange[MaxRangesDepth];
        ranges.CopyTo(savedRanges);
        int savedCount = ranges.Length;

        for (int L = 0; L <= maxL; L++)
        {
            bool anyAltMatches = false;
            for (int j = 0; j < altStarts.Length; j++)
            {
                int altBodyStart = altStarts[j];
                int altBodyEnd = (j + 1 < altStarts.Length) ? altStarts[j + 1] - 1 : altEndIndex;

                altRanges[0] = new ProgramRange { Start = altBodyStart, End = altBodyEnd };
                if (TryMatchRanges(first, second, program, altRanges, inputIndex, inputIndex + L, separator, kind))
                {
                    anyAltMatches = true;
                    break;
                }
            }

            if (anyAltMatches)
            {
                continue;
            }

            // No alternative matches exactly L chars. The negation accepts;
            // try matching the program's rest against the remaining input.
            savedRanges[..savedCount].CopyTo(ranges);
            if (TryMatchRanges(first, second, program, ranges, inputIndex + L, totalLength, separator, kind))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///  Builds a new ranges list: [alt body range] followed by the existing
    ///  <paramref name="rest"/>. Returns <see langword="false"/> if the result
    ///  would exceed <see cref="MaxRangesDepth"/>.
    /// </summary>
    private static bool BuildRangesWithAlternative(
        int altBodyStart,
        int altBodyEnd,
        ReadOnlySpan<ProgramRange> rest,
        Span<ProgramRange> destination,
        out int count)
    {
        if (1 + rest.Length > destination.Length)
        {
            count = 0;
            return false;
        }

        destination[0] = new ProgramRange { Start = altBodyStart, End = altBodyEnd };
        rest.CopyTo(destination[1..]);
        count = 1 + rest.Length;
        return true;
    }

    /// <summary>
    ///  Builds a new ranges list: [alt body range], [whole alternation block range
    ///  with kind overridden to <c>'*'</c>], followed by the existing
    ///  <paramref name="rest"/>. Used by repeating alternations
    ///  (<c>*(...)</c> / <c>+(...)</c>) to expand one iteration followed by
    ///  another invocation of the same block; the override makes the re-entered
    ///  block behave like <c>*</c> regardless of the bytecode kind, so a
    ///  <c>+(...)</c> after its mandatory first iteration only optionally takes
    ///  further iterations.
    /// </summary>
    private static bool BuildRangesWithAlternativeAndBlock(
        int altBodyStart,
        int altBodyEnd,
        int blockStart,
        int blockEnd,
        ReadOnlySpan<ProgramRange> rest,
        Span<ProgramRange> destination,
        out int count)
    {
        if (2 + rest.Length > destination.Length)
        {
            count = 0;
            return false;
        }

        destination[0] = new ProgramRange { Start = altBodyStart, End = altBodyEnd };
        destination[1] = new ProgramRange { Start = blockStart, End = blockEnd, KindOverride = '*' };
        rest.CopyTo(destination[2..]);
        count = 2 + rest.Length;
        return true;
    }

    /// <summary>
    ///  Splits the alternation body at <c>AltSep</c> boundaries, writing the
    ///  start index of each alternative body to <paramref name="altStarts"/>.
    ///  Returns the number of alternatives found.
    /// </summary>
    private static int SplitAlternatives(
        ReadOnlySpan<char> program,
        int altsStart,
        int altEndIndex,
        Span<int> altStarts)
    {
        int count = 0;
        int i = altsStart;
        altStarts[count++] = i;

        while (i < altEndIndex)
        {
            char op = program[i];
            if (op == GlobOpCodes.AltStart)
            {
                int nestedBlockLen = program[i + 2];
                i += nestedBlockLen;
                continue;
            }

            if (op == GlobOpCodes.AltSep)
            {
                if (count < altStarts.Length)
                {
                    altStarts[count] = i + 1;
                }

                count++;
                i++;
                continue;
            }

            if (op is GlobOpCodes.Literal or GlobOpCodes.Class or GlobOpCodes.NegClass)
            {
                int bodyLength = program[i + 1];
                i += 2 + bodyLength;
                continue;
            }

            if (op == GlobOpCodes.GlobStar)
            {
                i += 2;
                continue;
            }

            i++;
        }

        return count;
    }

    /// <summary>
    ///  Returns the character at <paramref name="inputIndex"/> across the virtual
    ///  <paramref name="first"/> + <paramref name="second"/> concatenation. The
    ///  caller is expected to have verified <c>inputIndex &lt; firstLength + second.Length</c>.
    /// </summary>
    private static char CharAt(
        ReadOnlySpan<char> first,
        ReadOnlySpan<char> second,
        int firstLength,
        int inputIndex) =>
        inputIndex < firstLength ? first[inputIndex] : second[inputIndex - firstLength];
}
