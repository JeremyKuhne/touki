// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  General-purpose glob matcher used for patterns that don't fit a specialized shape.
///  The pattern is encoded into a single program string interpreted at match time;
///  matching is allocation-free.
/// </summary>
/// <remarks>
///  <para>
///   Program encoding (see <see cref="GlobOpCodes"/>):
///  </para>
///  <para>
///   - <c>Any</c>: matches exactly one character.<br/>
///   - <c>AnyRun</c>: matches zero or more characters.<br/>
///   - <c>Literal</c> followed by <c>&lt;len&gt;&lt;chars&gt;</c>: matches the literal run.<br/>
///   - <c>Class</c> / <c>NegClass</c> followed by <c>&lt;len&gt;&lt;body&gt;</c>: matches one character
///     against the (negated) class body.
///  </para>
///  <para>
///   Matching uses the classic two-pointer algorithm with backtracking on the most
///   recent <c>AnyRun</c>. The ordinal and ignore-case paths are compiled into two
///   separate static methods so the case branch is hoisted out of the hot loop.
///  </para>
/// </remarks>
internal sealed partial class CompiledGlobStrategy : GlobStrategy
{
    private readonly string _program;
    private readonly int _nfaProgramLength;
    private readonly int _tailStart;
    private readonly int _tailLength;
    private readonly string _literalPathPrefix;

    /// <summary>
    ///  Compile-time properties of the encoded program (globstar, extglob, negation).
    ///  Captured by the encoder so the match loop can dispatch to the right variant
    ///  and gate optional fast paths behind single field loads. See
    ///  <see cref="GlobTraits"/>.
    /// </summary>
    private readonly GlobTraits _traits;

    /// <summary>
    ///  <see langword="true"/> when the encoded program admits an execution
    ///  path whose first matched input character is a literal <c>'.'</c>.
    ///  Computed once at construction by <see cref="ComputeCanStartWithDot"/>
    ///  so the per-call leading-dot precheck in
    ///  <see cref="MatchCore(ReadOnlySpan{char}, ReadOnlySpan{char})"/> is a
    ///  single field load instead of a bytecode walk. Without the cache the
    ///  walk would re-run on every hidden-file input for extglob programs.
    /// </summary>
    private readonly bool _canStartWithDot;

    /// <summary>
    ///  Constructs a matcher with no trailing-literal anchor (the program is run end-to-end).
    /// </summary>
    public CompiledGlobStrategy(string program, GlobDialect dialect, GlobOptions options)
        : this(program, program.Length, tailStart: -1, tailLength: 0, GlobTraits.None, dialect, options)
    {
    }

    /// <summary>
    ///  Constructs a matcher with a trailing-literal anchor. <paramref name="nfaProgramLength"/>
    ///  is the length of the program portion run by the NFA (excludes the trailing Literal
    ///  op header and payload); <paramref name="tailStart"/> and <paramref name="tailLength"/>
    ///  identify the tail characters within <paramref name="program"/>.
    ///  <paramref name="traits"/> carries the compile-time properties (globstar, extglob,
    ///  negation) the encoder discovered, which select the match loop and gate the
    ///  directory-pruning path.
    /// </summary>
    public CompiledGlobStrategy(
        string program,
        int nfaProgramLength,
        int tailStart,
        int tailLength,
        GlobTraits traits,
        GlobDialect dialect,
        GlobOptions options)
        : base(dialect, options)
    {
        _program = program;
        _nfaProgramLength = nfaProgramLength;
        _tailStart = tailStart;
        _tailLength = tailLength;
        _traits = traits;
        _literalPathPrefix = ComputeLiteralPathPrefix(program.AsSpan(0, nfaProgramLength), Separator);
        _canStartWithDot = ComputeCanStartWithDot(program.AsSpan(0, nfaProgramLength));
    }

    /// <inheritdoc/>
    internal override string LiteralPathPrefix => _literalPathPrefix;

    /// <summary>
    ///  Walks the encoded program looking for a leading <see cref="GlobOpCodes.Literal"/>
    ///  opcode. Returns its body up to and including the last separator character.
    ///  Empty when the program is path-unaware, starts with a non-literal, or the
    ///  leading literal does not contain a separator.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   When the leading <see cref="GlobOpCodes.Literal"/> is immediately followed by
    ///   a <see cref="GlobOpCodes.GlobStar"/> carrying
    ///   <see cref="GlobOpCodes.GlobStarFlagLead"/>, the source pattern had a separator
    ///   between the literal and the <c>**</c> that the GlobStar absorbed. The whole
    ///   literal is therefore a directory-aligned prefix; the helper appends the
    ///   separator manually so callers see <c>bin/Debug/</c> rather than <c>bin/</c>
    ///   for <c>bin/Debug/**/*.cs</c>.
    ///  </para>
    /// </remarks>
    private static string ComputeLiteralPathPrefix(ReadOnlySpan<char> program, char separator)
    {
        if (separator == '\0' || program.Length < 3 || program[0] != GlobOpCodes.Literal)
        {
            return string.Empty;
        }

        int literalLength = program[1];
        if (literalLength <= 0 || 2 + literalLength > program.Length)
        {
            return string.Empty;
        }

        ReadOnlySpan<char> body = program.Slice(2, literalLength);
        int afterLiteral = 2 + literalLength;

        if (afterLiteral + 1 < program.Length
            && program[afterLiteral] == GlobOpCodes.GlobStar
            && (program[afterLiteral + 1] & GlobOpCodes.GlobStarFlagLead) != 0)
        {
            // GlobStar absorbed the trailing separator: the entire literal is a
            // directory-aligned prefix.
            return body.ToString() + separator;
        }

        int lastSeparator = body.LastIndexOf(separator);
        return lastSeparator < 0 ? string.Empty : body[..(lastSeparator + 1)].ToString();
    }

    /// <inheritdoc/>
    internal override bool HasNegation => _traits.AreFlagsSet(GlobTraits.Negation);

    /// <inheritdoc/>
    /// <remarks>
    ///  <para>
    ///   Only meaningful when <see cref="HasNegation"/> is set; the caller gates on
    ///   it. Runs the extglob engine in directory mode against the candidate path
    ///   <paramref name="directoryPrefix"/> + <paramref name="directoryName"/>,
    ///   which accepts as soon as any backtracking path consumes the whole candidate
    ///   (a viable prefix). When no path can consume the candidate the anchored
    ///   negation has excluded one of its segments, so the subtree is reported
    ///   <see cref="MatchOutcome.Negative"/> and may be pruned. The tail-anchor
    ///   fast-fail and leading-dot precheck of
    ///   <see cref="MatchCore(ReadOnlySpan{char}, ReadOnlySpan{char})"/> are
    ///   deliberately skipped: a directory name need not end with the pattern's
    ///   trailing literal, and forgoing the dot precheck only costs a pruning
    ///   opportunity.
    ///  </para>
    /// </remarks>
    internal override MatchOutcome MatchDirectory(
        ReadOnlySpan<char> directoryPrefix,
        ReadOnlySpan<char> directoryName)
    {
        if (!_traits.AreFlagsSet(GlobTraits.Negation))
        {
            return MatchOutcome.None;
        }

        ReadOnlySpan<char> program = _program.AsSpan(0, _nfaProgramLength);
        return MatchExtGlobDirectory(directoryPrefix, directoryName, program, Separator, IgnoreCaseKind);
    }

    /// <inheritdoc/>
    /// <remarks>
    ///  <para>
    ///   Walks the virtual concatenation <paramref name="directoryPrefix"/> +
    ///   <paramref name="fileName"/> without copying. Per-char access goes through an
    ///   inline branch; literal-segment comparisons go through
    ///   <see cref="LiteralMatchesAt"/>, which splits the literal at the span boundary
    ///   so each half uses the vectorized <see cref="MemoryExtensions"/>.<c>SequenceEqual</c>
    ///   on a contiguous slice. The caller hands in <paramref name="directoryPrefix"/>
    ///   already separator-translated and separator-terminated (per the
    ///   <see cref="GlobStrategy.MatchCore"/> contract), so the boundary at
    ///   <c>directoryPrefix.Length</c> lines up exactly with the directory/file-name
    ///   split in the bytecode program.
    ///  </para>
    /// </remarks>
    internal override bool MatchCore(
        ReadOnlySpan<char> directoryPrefix,
        ReadOnlySpan<char> fileName)
    {
        ReadOnlySpan<char> first = directoryPrefix;
        ReadOnlySpan<char> second = fileName;
        int firstLength = first.Length;
        int totalLength = firstLength + second.Length;

        // Tail-anchor fast-fail. When the encoded program ends in a Literal op, the
        // factory pre-extracts that literal so we can verify it with a single
        // contiguous compare (vectorized when not straddling) before running the NFA.
        //
        // For non-extglob programs the Literal is the program's last opcode, so
        // matching the tail also consumes it: we trim the tail off the input and
        // run the NFA on the prefix only.
        //
        // For extglob programs the tail is the common literal suffix shared by
        // every alternative the walker could choose. Failing to match the tail
        // proves no alternative can match, but a successful match doesn't tell
        // us which alternative actually applies - the walker still needs the
        // full input. So we EndsWith-check and skip the trim.
        if (_tailLength > 0)
        {
            if (totalLength < _tailLength)
            {
                return false;
            }

            ReadOnlySpan<char> tail = _program.AsSpan(_tailStart, _tailLength);
            int tailStart = totalLength - _tailLength;
            if (!LiteralMatchesAt(first, second, tailStart, tail, IgnoreCaseKind))
            {
                return false;
            }

            if (!_traits.AreFlagsSet(GlobTraits.ExtGlob))
            {
                // Trim the tail off the virtual input. The tail either fits in `second`
                // entirely, fits in `first` entirely (when `second` is empty or short),
                // or straddles the boundary; in each case we slice the appropriate end.
                int trimmed = totalLength - _tailLength;
                if (trimmed >= firstLength)
                {
                    second = second[..(trimmed - firstLength)];
                }
                else
                {
                    first = first[..trimmed];
                    second = default;
                }

                firstLength = first.Length;
                totalLength = trimmed;
            }
        }

        ReadOnlySpan<char> program = _program.AsSpan(0, _nfaProgramLength);

        // Leading-dot rule: if the (post-tail-trim) virtual input starts with '.',
        // the program must admit a leading literal '.' on at least one execution
        // path. `_canStartWithDot` is precomputed at construction (see
        // <see cref="ComputeCanStartWithDot"/>) so this check is a single field
        // load on the per-call hot path; the walk over extglob alternatives that
        // determines the flag happens once at compile time.
        if (!MatchLeadingDot && totalLength > 0)
        {
            char firstChar = firstLength > 0 ? first[0] : second[0];
            if (firstChar == '.' && !_canStartWithDot)
            {
                return false;
            }
        }

        if (_traits.AreFlagsSet(GlobTraits.ExtGlob))
        {
            return MatchExtGlob(first, second, program, Separator, IgnoreCaseKind);
        }

        if (IgnoreCaseKind == IgnoreCaseKind.Off)
        {
            return _traits.AreFlagsSet(GlobTraits.GlobStar)
                ? MatchOrdinal(first, second, program, Separator)
                : MatchOrdinalSimple(first, second, program, Separator);
        }

        return _traits.AreFlagsSet(GlobTraits.GlobStar)
            ? MatchIgnoreCase(first, second, program, IgnoreCaseKind, Separator)
            : MatchIgnoreCaseSimple(first, second, program, IgnoreCaseKind, Separator);
    }

    /// <summary>
    ///  Compile-time predicate: returns <see langword="true"/> if
    ///  <paramref name="program"/> admits an execution path whose first matched
    ///  input character is a literal <c>'.'</c>. Result is cached in
    ///  <see cref="_canStartWithDot"/> and consumed by the per-call
    ///  leading-dot precheck in
    ///  <see cref="MatchCore(ReadOnlySpan{char}, ReadOnlySpan{char})"/>.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   For a non-extglob program this is the same predicate the previous
    ///   inline check encoded: the leading opcode must be a <c>Literal</c> whose
    ///   first character is <c>'.'</c>. For an extglob program we descend into
    ///   each alternative head and recurse, so explicit-dot alternatives such
    ///   as <c>@(.gitignore|README)</c> can match hidden inputs. Other leading
    ///   opcodes (<c>AnyRun</c>, <c>Any</c>, <c>Class</c>/<c>NegClass</c>,
    ///   <c>GlobStar</c>) cannot consume the leading <c>.</c> under the
    ///   leading-dot rule, so they short-circuit to <see langword="false"/>.
    ///  </para>
    /// </remarks>
    private static bool ComputeCanStartWithDot(ReadOnlySpan<char> program)
    {
        if (program.Length < 1)
        {
            return false;
        }

        char opcode = program[0];
        if (opcode == GlobOpCodes.Literal)
        {
            return program.Length >= 3 && program[2] == '.';
        }

        if (opcode != GlobOpCodes.AltStart)
        {
            // AnyRun / Any / Class / NegClass / GlobStar cannot match a leading
            // '.' when the leading-dot rule is in force; negation (!) is
            // handled the same way as its constituent alternatives (any of
            // which may legitimately start with a dot).
            return false;
        }

        // AltStart header: kind (program[1]), blockLength (program[2]),
        // altCount (program[3]), then one offset-table slot per alternative.
        // Alternatives begin after that header and are AltSep-separated until
        // AltEnd. Walk each alternative's first opcode; recurse to follow
        // nested AltStarts.
        if (program.Length < 4)
        {
            return false;
        }

        int blockLength = program[2];
        int afterEnd = blockLength;
        int altEndIndex = afterEnd - 1;
        if (altEndIndex < 0 || altEndIndex >= program.Length)
        {
            return false;
        }

        int i = 4 + program[3];
        while (i < altEndIndex)
        {
            if (ComputeCanStartWithDot(program[i..altEndIndex]))
            {
                return true;
            }

            // Skip to the next alternative by walking the body of this one so
            // nested AltStart blocks are not mistaken for top-level boundaries.
            i = SkipToNextAlternativeOrEnd(program, i, altEndIndex);
            if (i < altEndIndex && program[i] == GlobOpCodes.AltSep)
            {
                i++;
            }
        }

        return false;
    }

    /// <summary>
    ///  Walks an alternative body in the AltStart bytecode block, returning the
    ///  index of the next <see cref="GlobOpCodes.AltSep"/> or the
    ///  <paramref name="altEndIndex"/> bound. Skips nested <c>AltStart</c>
    ///  blocks via their pre-recorded <c>blockLength</c>.
    /// </summary>
    private static int SkipToNextAlternativeOrEnd(ReadOnlySpan<char> program, int i, int altEndIndex)
    {
        while (i < altEndIndex)
        {
            char op = program[i];
            if (op == GlobOpCodes.AltSep)
            {
                return i;
            }

            if (op == GlobOpCodes.AltStart)
            {
                int nestedBlockLen = program[i + 2];
                i += nestedBlockLen;
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

        return altEndIndex;
    }

    /// <summary>
    ///  Compares the virtual <paramref name="first"/> + <paramref name="second"/> slice
    ///  starting at <paramref name="inputIndex"/> against <paramref name="literal"/> under
    ///  the matcher's case-fold rule. Splits the literal at the span boundary so each
    ///  half stays contiguous on its source span and uses vectorized routines.
    /// </summary>
    private static bool LiteralMatchesAt(
        ReadOnlySpan<char> first,
        ReadOnlySpan<char> second,
        int inputIndex,
        ReadOnlySpan<char> literal,
        IgnoreCaseKind kind)
    {
        int firstLength = first.Length;
        int literalLength = literal.Length;

        if (inputIndex + literalLength <= firstLength)
        {
            return LiteralMatch(first.Slice(inputIndex, literalLength), literal, kind);
        }

        if (inputIndex >= firstLength)
        {
            return LiteralMatch(second.Slice(inputIndex - firstLength, literalLength), literal, kind);
        }

        int leftLength = firstLength - inputIndex;
        return LiteralMatch(first.Slice(inputIndex, leftLength), literal[..leftLength], kind)
            && LiteralMatch(second[..(literalLength - leftLength)], literal[leftLength..], kind);
    }

    /// <summary>
    ///  Ordinal match path for programs without <see cref="GlobOpCodes.GlobStar"/>.
    ///  Single AnyRun savepoint, no shared backtrack helper - keeps the JIT happy and
    ///  avoids the two-slot backtrack overhead of <see cref="MatchOrdinal"/> on the
    ///  globstar-free common case. Walks the virtual
    ///  <paramref name="first"/> + <paramref name="second"/> concatenation.
    /// </summary>
    private static bool MatchOrdinalSimple(
        ReadOnlySpan<char> first,
        ReadOnlySpan<char> second,
        ReadOnlySpan<char> program,
        char separator)
    {
        int firstLength = first.Length;
        int totalLength = firstLength + second.Length;
        int programIndex = 0;
        int inputIndex = 0;
        int anyRunProgramIndex = -1;
        int anyRunInputIndex = 0;

        while (inputIndex < totalLength)
        {
            if (programIndex < program.Length)
            {
                char opcode = program[programIndex];

                if (opcode == GlobOpCodes.AnyRun)
                {
                    anyRunProgramIndex = programIndex;
                    anyRunInputIndex = inputIndex;
                    programIndex++;
                    continue;
                }

                char inputChar = inputIndex < firstLength
                    ? first[inputIndex]
                    : second[inputIndex - firstLength];

                if (opcode == GlobOpCodes.Any)
                {
                    if (separator == '\0' || inputChar != separator)
                    {
                        inputIndex++;
                        programIndex++;
                        continue;
                    }
                }
                else if (opcode == GlobOpCodes.Literal)
                {
                    int literalLength = program[programIndex + 1];
                    if (inputIndex + literalLength <= totalLength
                        && LiteralMatchesAt(first, second, inputIndex, program.Slice(programIndex + 2, literalLength), IgnoreCaseKind.Off))
                    {
                        inputIndex += literalLength;
                        programIndex += 2 + literalLength;
                        continue;
                    }
                }
                else if (opcode is GlobOpCodes.Class or GlobOpCodes.NegClass)
                {
                    int classLength = program[programIndex + 1];
                    if ((separator == '\0' || inputChar != separator)
                        && ClassContainsOrdinal(program.Slice(programIndex + 2, classLength), inputChar, opcode == GlobOpCodes.NegClass))
                    {
                        inputIndex++;
                        programIndex += 2 + classLength;
                        continue;
                    }
                }
            }

            if (anyRunProgramIndex >= 0)
            {
                // Path-aware AnyRun cannot extend across the separator.
                if (separator != '\0' && anyRunInputIndex < totalLength)
                {
                    char anyRunChar = anyRunInputIndex < firstLength
                        ? first[anyRunInputIndex]
                        : second[anyRunInputIndex - firstLength];
                    if (anyRunChar == separator)
                    {
                        return false;
                    }
                }

                programIndex = anyRunProgramIndex + 1;
                anyRunInputIndex++;
                inputIndex = anyRunInputIndex;
                continue;
            }

            return false;
        }

        while (programIndex < program.Length && program[programIndex] == GlobOpCodes.AnyRun)
        {
            programIndex++;
        }

        return programIndex == program.Length;
    }

    /// <summary>
    ///  Ignore-case companion to <see cref="MatchOrdinalSimple"/>.
    /// </summary>
    private static bool MatchIgnoreCaseSimple(
        ReadOnlySpan<char> first,
        ReadOnlySpan<char> second,
        ReadOnlySpan<char> program,
        IgnoreCaseKind kind,
        char separator)
    {
        int firstLength = first.Length;
        int totalLength = firstLength + second.Length;
        int programIndex = 0;
        int inputIndex = 0;
        int anyRunProgramIndex = -1;
        int anyRunInputIndex = 0;

        while (inputIndex < totalLength)
        {
            if (programIndex < program.Length)
            {
                char opcode = program[programIndex];

                if (opcode == GlobOpCodes.AnyRun)
                {
                    anyRunProgramIndex = programIndex;
                    anyRunInputIndex = inputIndex;
                    programIndex++;
                    continue;
                }

                char inputChar = inputIndex < firstLength
                    ? first[inputIndex]
                    : second[inputIndex - firstLength];

                if (opcode == GlobOpCodes.Any)
                {
                    if (separator == '\0' || inputChar != separator)
                    {
                        inputIndex++;
                        programIndex++;
                        continue;
                    }
                }
                else if (opcode == GlobOpCodes.Literal)
                {
                    int literalLength = program[programIndex + 1];
                    if (inputIndex + literalLength <= totalLength
                        && LiteralMatchesAt(first, second, inputIndex, program.Slice(programIndex + 2, literalLength), kind))
                    {
                        inputIndex += literalLength;
                        programIndex += 2 + literalLength;
                        continue;
                    }
                }
                else if (opcode is GlobOpCodes.Class or GlobOpCodes.NegClass)
                {
                    int classLength = program[programIndex + 1];
                    if ((separator == '\0' || inputChar != separator)
                        && ClassContainsIgnoreCase(program.Slice(programIndex + 2, classLength), inputChar, opcode == GlobOpCodes.NegClass))
                    {
                        inputIndex++;
                        programIndex += 2 + classLength;
                        continue;
                    }
                }
            }

            if (anyRunProgramIndex >= 0)
            {
                if (separator != '\0' && anyRunInputIndex < totalLength)
                {
                    char anyRunChar = anyRunInputIndex < firstLength
                        ? first[anyRunInputIndex]
                        : second[anyRunInputIndex - firstLength];
                    if (anyRunChar == separator)
                    {
                        return false;
                    }
                }

                programIndex = anyRunProgramIndex + 1;
                anyRunInputIndex++;
                inputIndex = anyRunInputIndex;
                continue;
            }

            return false;
        }

        while (programIndex < program.Length && program[programIndex] == GlobOpCodes.AnyRun)
        {
            programIndex++;
        }

        return programIndex == program.Length;
    }

    /// <summary>
    ///  Ordinal match path. Walks the virtual <paramref name="first"/> +
    ///  <paramref name="second"/> concatenation; uses <see cref="LiteralMatchesAt"/>
    ///  for literal runs (vectorized when not straddling the span boundary).
    /// </summary>
    private static bool MatchOrdinal(
        ReadOnlySpan<char> first,
        ReadOnlySpan<char> second,
        ReadOnlySpan<char> program,
        char separator)
    {
        BacktrackState state = default;
        state.AnyRunProgramIndex = -1;
        state.GlobStarProgramIndex = -1;

        int firstLength = first.Length;
        int totalLength = firstLength + second.Length;

        while (state.InputIndex < totalLength)
        {
            if (state.ProgramIndex < program.Length)
            {
                char opcode = program[state.ProgramIndex];

                if (opcode == GlobOpCodes.AnyRun)
                {
                    state.AnyRunProgramIndex = state.ProgramIndex;
                    state.AnyRunInputIndex = state.InputIndex;
                    state.ProgramIndex++;
                    continue;
                }

                if (opcode == GlobOpCodes.GlobStar)
                {
                    int flags = program[state.ProgramIndex + 1];
                    int absorbedLength = FirstValidGlobStarLength(first, second, state.InputIndex, flags, separator);
                    if (absorbedLength >= 0)
                    {
                        if (state.AnyRunProgramIndex > state.ProgramIndex)
                        {
                            state.AnyRunProgramIndex = -1;
                        }

                        state.GlobStarProgramIndex = state.ProgramIndex;
                        state.GlobStarInitialInput = state.InputIndex;
                        state.GlobStarInputIndex = state.InputIndex + absorbedLength;
                        state.GlobStarFlags = flags;
                        state.ProgramIndex += 2;
                        state.InputIndex = state.GlobStarInputIndex;
                        continue;
                    }
                }
                else
                {
                    char inputChar = state.InputIndex < firstLength
                        ? first[state.InputIndex]
                        : second[state.InputIndex - firstLength];

                    if (opcode == GlobOpCodes.Any)
                    {
                        if (separator == '\0' || inputChar != separator)
                        {
                            state.InputIndex++;
                            state.ProgramIndex++;
                            continue;
                        }
                    }
                    else if (opcode == GlobOpCodes.Literal)
                    {
                        int length = program[state.ProgramIndex + 1];
                        if (state.InputIndex + length <= totalLength
                            && LiteralMatchesAt(first, second, state.InputIndex, program.Slice(state.ProgramIndex + 2, length), IgnoreCaseKind.Off))
                        {
                            state.InputIndex += length;
                            state.ProgramIndex += 2 + length;
                            continue;
                        }
                    }
                    else if (opcode is GlobOpCodes.Class or GlobOpCodes.NegClass)
                    {
                        int length = program[state.ProgramIndex + 1];
                        if ((separator == '\0' || inputChar != separator)
                            && ClassContainsOrdinal(program.Slice(state.ProgramIndex + 2, length), inputChar, opcode == GlobOpCodes.NegClass))
                        {
                            state.InputIndex++;
                            state.ProgramIndex += 2 + length;
                            continue;
                        }
                    }
                }
            }

            if (!Backtrack(first, second, separator, ref state))
            {
                return false;
            }
        }

        return ConsumeTrailingEmpty(program, state.ProgramIndex);
    }

    /// <summary>
    ///  Ignore-case match path. ASCII-fold both sides per compare. The exact case-fold rule
    ///  is selected by <paramref name="kind"/>: <see cref="IgnoreCaseKind.Ascii"/> uses strict
    ///  ASCII semantics (matches POSIX/bash/git), <see cref="IgnoreCaseKind.Unicode"/> uses
    ///  full Unicode ordinal (matches MSBuild/FileSystemGlobbing). Note: bracket-class
    ///  membership currently always uses ASCII fold; full Unicode class membership is a
    ///  follow-up if needed.
    /// </summary>
    private static bool MatchIgnoreCase(
        ReadOnlySpan<char> first,
        ReadOnlySpan<char> second,
        ReadOnlySpan<char> program,
        IgnoreCaseKind kind,
        char separator)
    {
        BacktrackState state = default;
        state.AnyRunProgramIndex = -1;
        state.GlobStarProgramIndex = -1;

        int firstLength = first.Length;
        int totalLength = firstLength + second.Length;

        while (state.InputIndex < totalLength)
        {
            if (state.ProgramIndex < program.Length)
            {
                char opcode = program[state.ProgramIndex];

                if (opcode == GlobOpCodes.AnyRun)
                {
                    state.AnyRunProgramIndex = state.ProgramIndex;
                    state.AnyRunInputIndex = state.InputIndex;
                    state.ProgramIndex++;
                    continue;
                }

                if (opcode == GlobOpCodes.GlobStar)
                {
                    int flags = program[state.ProgramIndex + 1];
                    int absorbedLength = FirstValidGlobStarLength(first, second, state.InputIndex, flags, separator);
                    if (absorbedLength >= 0)
                    {
                        if (state.AnyRunProgramIndex > state.ProgramIndex)
                        {
                            state.AnyRunProgramIndex = -1;
                        }

                        state.GlobStarProgramIndex = state.ProgramIndex;
                        state.GlobStarInitialInput = state.InputIndex;
                        state.GlobStarInputIndex = state.InputIndex + absorbedLength;
                        state.GlobStarFlags = flags;
                        state.ProgramIndex += 2;
                        state.InputIndex = state.GlobStarInputIndex;
                        continue;
                    }
                }
                else
                {
                    char inputChar = state.InputIndex < firstLength
                        ? first[state.InputIndex]
                        : second[state.InputIndex - firstLength];

                    if (opcode == GlobOpCodes.Any)
                    {
                        if (separator == '\0' || inputChar != separator)
                        {
                            state.InputIndex++;
                            state.ProgramIndex++;
                            continue;
                        }
                    }
                    else if (opcode == GlobOpCodes.Literal)
                    {
                        int length = program[state.ProgramIndex + 1];
                        if (state.InputIndex + length <= totalLength
                            && LiteralMatchesAt(first, second, state.InputIndex, program.Slice(state.ProgramIndex + 2, length), kind))
                        {
                            state.InputIndex += length;
                            state.ProgramIndex += 2 + length;
                            continue;
                        }
                    }
                    else if (opcode is GlobOpCodes.Class or GlobOpCodes.NegClass)
                    {
                        int length = program[state.ProgramIndex + 1];
                        if ((separator == '\0' || inputChar != separator)
                            && ClassContainsIgnoreCase(program.Slice(state.ProgramIndex + 2, length), inputChar, opcode == GlobOpCodes.NegClass))
                        {
                            state.InputIndex++;
                            state.ProgramIndex += 2 + length;
                            continue;
                        }
                    }
                }
            }

            if (!Backtrack(first, second, separator, ref state))
            {
                return false;
            }
        }

        return ConsumeTrailingEmpty(program, state.ProgramIndex);
    }

    /// <summary>
    ///  Consumes trailing ops whose empty match is valid (<see cref="GlobOpCodes.AnyRun"/>
    ///  and any <see cref="GlobOpCodes.GlobStar"/> that is not <c>GS_LT</c>). Returns
    ///  <see langword="true"/> iff the program is fully consumed.
    /// </summary>
    private static bool ConsumeTrailingEmpty(ReadOnlySpan<char> program, int programIndex)
    {
        while (programIndex < program.Length)
        {
            char opcode = program[programIndex];
            if (opcode == GlobOpCodes.AnyRun)
            {
                programIndex++;
                continue;
            }

            if (opcode == GlobOpCodes.GlobStar)
            {
                int flags = program[programIndex + 1];
                // GS_LT requires a non-empty absorbed slice; empty match invalid.
                if ((flags & GlobOpCodes.GlobStarFlagLead) != 0
                    && (flags & GlobOpCodes.GlobStarFlagTrail) != 0)
                {
                    return false;
                }

                programIndex += 2;
                continue;
            }

            return false;
        }

        return true;
    }

    /// <summary>
    ///  Mutable matcher state passed by reference into <see cref="Backtrack"/> instead of
    ///  individual <c>ref int</c> parameters. Cuts <see cref="Backtrack"/>'s argument list
    ///  from ten to three, which on net481 RyuJIT measurably reduces call-site overhead and
    ///  improves inlining behavior. Holds the active program/input cursors plus both
    ///  savepoint slots (AnyRun and GlobStar) and the per-GlobStar invariants.
    /// </summary>
    private ref struct BacktrackState
    {
        public int ProgramIndex;
        public int InputIndex;
        public int AnyRunProgramIndex;
        public int AnyRunInputIndex;
        public int GlobStarProgramIndex;
        public int GlobStarInputIndex;
        public int GlobStarInitialInput;
        public int GlobStarFlags;
    }

    /// <summary>
    ///  Backtracks to whichever savepoint (AnyRun or GlobStar) is more recent in
    ///  program flow; on exhaustion, falls through to the other. Returns
    ///  <see langword="false"/> when both slots are exhausted.
    /// </summary>
    private static bool Backtrack(
        ReadOnlySpan<char> first,
        ReadOnlySpan<char> second,
        char separator,
        ref BacktrackState state)
    {
        int firstLength = first.Length;
        int totalLength = firstLength + second.Length;

        while (true)
        {
            bool tryGlobStar = state.GlobStarProgramIndex >= 0
                && (state.AnyRunProgramIndex < 0 || state.GlobStarProgramIndex >= state.AnyRunProgramIndex);

            if (tryGlobStar)
            {
                int currentAbsorbed = state.GlobStarInputIndex - state.GlobStarInitialInput;
                int nextAbsorbed = NextValidGlobStarLength(
                    first,
                    second,
                    state.GlobStarInitialInput,
                    currentAbsorbed,
                    state.GlobStarFlags,
                    separator);
                if (nextAbsorbed < 0)
                {
                    state.GlobStarProgramIndex = -1;
                    continue;
                }

                state.GlobStarInputIndex = state.GlobStarInitialInput + nextAbsorbed;
                state.ProgramIndex = state.GlobStarProgramIndex + 2;
                state.InputIndex = state.GlobStarInputIndex;
                return true;
            }

            if (state.AnyRunProgramIndex >= 0)
            {
                // Path-aware AnyRun cannot extend across the separator.
                if (separator != '\0' && state.AnyRunInputIndex < totalLength)
                {
                    char anyRunChar = state.AnyRunInputIndex < firstLength
                        ? first[state.AnyRunInputIndex]
                        : second[state.AnyRunInputIndex - firstLength];
                    if (anyRunChar == separator)
                    {
                        state.AnyRunProgramIndex = -1;
                        continue;
                    }
                }

                state.AnyRunInputIndex++;
                if (state.AnyRunInputIndex > totalLength)
                {
                    state.AnyRunProgramIndex = -1;
                    continue;
                }

                if (state.GlobStarProgramIndex > state.AnyRunProgramIndex)
                {
                    state.GlobStarProgramIndex = -1;
                }

                state.ProgramIndex = state.AnyRunProgramIndex + 1;
                state.InputIndex = state.AnyRunInputIndex;
                return true;
            }

            return false;
        }
    }

    /// <summary>
    ///  Returns the smallest valid absorbed length at which a
    ///  <see cref="GlobOpCodes.GlobStar"/> with the given flag bits may commit, given the
    ///  absorbed input slice <c>input[initial..initial+length]</c>. Returns <c>-1</c> when
    ///  no valid length exists (e.g., <c>GS_LT</c> at a position where the input character
    ///  at <c>initial</c> is not the separator).
    /// </summary>
    private static int FirstValidGlobStarLength(
        ReadOnlySpan<char> first,
        ReadOnlySpan<char> second,
        int initialInputIndex,
        int flags,
        char separator)
    {
        bool hasLead = (flags & GlobOpCodes.GlobStarFlagLead) != 0;
        bool hasTrail = (flags & GlobOpCodes.GlobStarFlagTrail) != 0;
        int firstLength = first.Length;
        int totalLength = firstLength + second.Length;

        if (hasLead && hasTrail)
        {
            if (initialInputIndex < totalLength)
            {
                char inputChar = initialInputIndex < firstLength
                    ? first[initialInputIndex]
                    : second[initialInputIndex - firstLength];
                if (inputChar == separator)
                {
                    return 1;
                }
            }

            return -1;
        }

        // GS_None / GS_R / GS_L: empty match (length 0) is always valid.
        return 0;
    }

    /// <summary>
    ///  Returns the smallest valid absorbed length greater than
    ///  <paramref name="currentAbsorbed"/> for a <see cref="GlobOpCodes.GlobStar"/>
    ///  backtrack, or <c>-1</c> when exhausted.
    /// </summary>
    private static int NextValidGlobStarLength(
        ReadOnlySpan<char> first,
        ReadOnlySpan<char> second,
        int initialInputIndex,
        int currentAbsorbed,
        int flags,
        char separator)
    {
        bool hasLead = (flags & GlobOpCodes.GlobStarFlagLead) != 0;
        bool hasTrail = (flags & GlobOpCodes.GlobStarFlagTrail) != 0;
        int firstLength = first.Length;
        int totalLength = firstLength + second.Length;
        int maxAbsorbed = totalLength - initialInputIndex;

        if (hasTrail)
        {
            // (GS_R or GS_LT.) Need length > currentAbsorbed with the input character at
            // (initial + length - 1) equal to the separator. Scan upward.
            for (int position = initialInputIndex + currentAbsorbed; position < totalLength; position++)
            {
                char inputChar = position < firstLength
                    ? first[position]
                    : second[position - firstLength];
                if (inputChar == separator)
                {
                    return position - initialInputIndex + 1;
                }
            }

            return -1;
        }

        if (hasLead)
        {
            // GS_L. Length 0 was the initial empty match; length >= 1 requires the input
            // character at `initial` to be the separator. Beyond length 1, no further
            // constraint.
            int next = currentAbsorbed + 1;
            if (next > maxAbsorbed)
            {
                return -1;
            }

            if (next == 1)
            {
                if (initialInputIndex < totalLength)
                {
                    char inputChar = initialInputIndex < firstLength
                        ? first[initialInputIndex]
                        : second[initialInputIndex - firstLength];
                    if (inputChar == separator)
                    {
                        return 1;
                    }
                }

                return -1;
            }

            return next;
        }

        // GS_None: no constraint.
        {
            int next = currentAbsorbed + 1;
            return next > maxAbsorbed ? -1 : next;
        }
    }

    /// <summary>
    ///  Selects the literal-segment compare for the active <see cref="IgnoreCaseKind"/>.
    ///  Caller guarantees <c>a.Length == b.Length</c> (the NFA slices both sides to <c>length</c>).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool LiteralMatch(ReadOnlySpan<char> a, ReadOnlySpan<char> b, IgnoreCaseKind kind) => kind switch
    {
        IgnoreCaseKind.Ascii => a.EqualsAsciiLetterIgnoreCase(b),
        IgnoreCaseKind.Unicode => a.EqualsOrdinalIgnoreCase(b),
        _ => a.SequenceEqual(b),
    };

    private static bool ClassContainsOrdinal(ReadOnlySpan<char> body, char character, bool negated)
    {
        bool hit = false;

        int i = 0;
        while (i < body.Length)
        {
            char low = body[i];
            char high = low;

            if (i + 2 < body.Length && body[i + 1] == '-')
            {
                high = body[i + 2];
                i += 3;
            }
            else
            {
                i++;
            }

            if (character >= low && character <= high)
            {
                hit = true;
                break;
            }
        }

        return hit ^ negated;
    }

    private static bool ClassContainsIgnoreCase(ReadOnlySpan<char> body, char character, bool negated)
    {
        char target = GlobMatcherHelpers.AsciiFold(character);
        bool hit = false;

        int i = 0;
        while (i < body.Length)
        {
            char low = GlobMatcherHelpers.AsciiFold(body[i]);
            char high = low;

            if (i + 2 < body.Length && body[i + 1] == '-')
            {
                high = GlobMatcherHelpers.AsciiFold(body[i + 2]);
                i += 3;
            }
            else
            {
                i++;
            }

            if (low > high)
            {
                (low, high) = (high, low);
            }

            if (target >= low && target <= high)
            {
                hit = true;
                break;
            }
        }

        return hit ^ negated;
    }
}
