// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Matcher used when the compiled program contains
///  <see cref="GlobOpCodes.AltStart"/> opcodes (extended-glob alternation
///  constructs). Trades the iterative two-slot backtrack of the non-extglob
///  fast paths for a "concatenation of program ranges" walker that naturally
///  handles nested alternations.
/// </summary>
/// <remarks>
///  <para>
///   The matcher walks a small list of <see cref="ProgramRange"/> entries; the
///   first entry is the &quot;current&quot; sub-program and any additional
///   entries are the &quot;rest&quot; (typically the slice past the alternation
///   block). On <see cref="GlobOpCodes.AltStart"/> it prepends an alternative's
///   range; for repeating constructs (<c>*(...)</c>, <c>+(...)</c>) it also
///   re-prepends the same alternation block so further iterations can be
///   attempted before falling through to the rest.
///  </para>
///  <para>
///   <b>Iterative engine.</b> Rather than recursing natively for each choice
///   point, the walker (<see cref="ExtGlobEngine"/>) runs a single <c>while</c>
///   loop over an explicit backtrack stack (<see cref="Frame"/>). Each choice
///   point pushes a frame capturing the entry configuration; deterministic
///   opcodes advance the head range in place. This keeps stack depth a heap
///   concern - native recursion no longer grows with the input length, so an
///   adversarial repeating construct (<c>+(...)</c>/<c>*(...)</c> over a long
///   separator-free segment) can no longer trigger an uncatchable
///   <see cref="StackOverflowException"/>. The design mirrors the explicit
///   <c>runstack</c>/<c>runtrack</c> arrays of the .NET regex interpreter.
///  </para>
///  <para>
///   The <c>totalLength</c> parameter threaded through the walker lets callers
///   run the matcher against a clipped input range (matching some prefix of the
///   virtual <c>first + second</c> concatenation rather than the whole thing).
///   The negation handler relies on this to ask &quot;does alternative <i>p</i>
///   consume exactly <i>L</i> input characters?&quot; by re-entering the engine
///   with the clipped length; that re-entry is the only remaining native
///   recursion and is bounded by the encoder's <c>MaxExtGlobDepth</c> cap (the
///   maximum nesting of <c>!(...)</c> constructs).
///  </para>
///  <para>
///   The frame stack and the range-snapshot arena are both seeded from
///   <c>stackalloc</c> and spill to <see cref="ArrayPool{T}"/> only when an
///   adversarial input outgrows the seed, so the common path stays allocation
///   free.
///  </para>
/// </remarks>
internal sealed partial class CompiledGlobStrategy
{
    /// <summary>
    ///  Hard cap on the number of program ranges a single match configuration may
    ///  hold at once. A "range list" is the ordered set of program slices the engine
    ///  is matching in sequence (the active <c>Work</c> list and the <c>Rest</c>
    ///  scratch list an alternative is built into); it also sizes the failure-memo
    ///  <c>Key</c>.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   This is a correctness ceiling, not a growable seed:
    ///   <see cref="BuildRangesWithAlternative"/> and
    ///   <see cref="BuildRangesWithAlternativeAndBlock"/> return
    ///   <see langword="false"/> (failing the match arm) rather than spilling to a
    ///   larger buffer when a list would exceed it, so it must stay at or above the
    ///   worst case any pattern the encoder accepts can reach.
    ///  </para>
    ///  <para>
    ///   The count grows only when an extglob alternative expands into the program:
    ///   entering a construct prepends the alternative body and, for a repeating
    ///   <c>+(...)</c> / <c>*(...)</c> block, a re-entry slice plus the post-block
    ///   tail. That is at most two persistent ranges per enclosing construct, so the
    ///   worst case scales with extglob <em>nesting depth</em> - not with input
    ///   length (<see cref="CompactEmptyRanges"/> stops a repeating construct from
    ///   accumulating one range per iteration). The encoder caps that nesting at
    ///   <see cref="GlobSpecification.MaxExtGlobDepth"/>, so the bound is derived from
    ///   it directly: two ranges per level, plus the initial whole-program range and
    ///   one slot of slack. Raising <see cref="GlobSpecification.MaxExtGlobDepth"/>
    ///   widens this in lock-step; do not hard-code a smaller literal.
    ///  </para>
    /// </remarks>
    private const int MaxRangesDepth = (GlobSpecification.MaxExtGlobDepth * 2) + 2;

    // Failure-memo key layout: [inputIndex, totalLength, rangeCount] followed by
    // (Start, End, KindOverride) for each range.
    private const int KeyHeaderLength = 3;
    private const int RangeKeyLength = 3;

    // Seed sizes for the explicit backtrack stack and the range-snapshot arena.
    // Common patterns never exceed these, so the engine stays allocation-free;
    // adversarial repeating constructs spill to ArrayPool.
    private const int SeedFrameCount = 32;
    private const int SeedArenaCount = 128;

    // Stack budget for the directory-probe input buffer (candidate path plus a
    // trailing separator). Relative paths longer than this spill to ArrayPool via
    // BufferScope.
    private const int StackInputBufferSize = 256;

    /// <summary>
    ///  Entry point used by <see cref="MatchCore"/> when the program contains
    ///  one or more <see cref="GlobOpCodes.AltStart"/> opcodes.
    /// </summary>
    [SkipLocalsInit]
    private static bool MatchExtGlob(
        ReadOnlySpan<char> first,
        ReadOnlySpan<char> second,
        ReadOnlySpan<char> program,
        char separator,
        IgnoreCaseKind kind)
    {
        int totalLength = first.Length + second.Length;
        EngineInputs inputs = new(first, second, program, separator, kind);
        ExtGlobMatchState state = default;
        try
        {
            // Explicit stackalloc rather than a collection expression: on net481 a
            // [InlineArray]-backed collection expression is unavailable, so the
            // compiler falls back to a heap array. stackalloc stays allocation-free
            // on both target frameworks.
#pragma warning disable IDE0302 // Collection initialization can be simplified - see comment above.
            Span<ProgramRange> initial = stackalloc ProgramRange[1];
#pragma warning restore IDE0302
            initial[0] = new ProgramRange { Start = 0, End = program.Length };
            return RunEngine(in inputs, initial, inputIndex: 0, totalLength, ref state);
        }
        finally
        {
            state.Dispose();
        }
    }

    /// <summary>
    ///  Directory-pruning entry point used by <see cref="MatchDirectory"/>. Runs the
    ///  engine in directory mode against the candidate directory path
    ///  (<paramref name="first"/> + <paramref name="second"/>) and classifies it.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   A trailing <paramref name="separator"/> is appended to the candidate so it
    ///   aligns with the pattern's path-segment boundaries: directory <c>D</c> is a
    ///   viable prefix exactly when the pattern can consume <c>D/</c> as a prefix of
    ///   some <c>D/child...</c> full match. Without it a literal segment
    ///   (<c>src/</c>) or a globstar that absorbs a separator-terminated segment
    ///   would straddle the candidate's end and be misread as a dead end.
    ///  </para>
    ///  <para>
    ///   Directory mode accepts as soon as any backtracking path consumes the whole
    ///   candidate, so the run reports a viable prefix
    ///   (<see cref="MatchOutcome.None"/>) - or a complete match
    ///   (<see cref="MatchOutcome.Positive"/>) - without exhausting the search. When
    ///   no path can consume the candidate, an anchored negation has excluded one of
    ///   its segments and no descendant can match, so the subtree is reported
    ///   <see cref="MatchOutcome.Negative"/> and may be pruned. This is sound: any
    ///   directory with a matching descendant has a full-match run that passes
    ///   through the &quot;whole candidate consumed&quot; state, so it can never be
    ///   reported <see cref="MatchOutcome.Negative"/>.
    ///  </para>
    /// </remarks>
    [SkipLocalsInit]
    private static MatchOutcome MatchExtGlobDirectory(
        ReadOnlySpan<char> first,
        ReadOnlySpan<char> second,
        ReadOnlySpan<char> program,
        char separator,
        IgnoreCaseKind kind)
    {
        // Assemble first + second + a trailing separator into one contiguous buffer
        // so the candidate ends on a path-segment boundary. BufferScope keeps the
        // common case on the stack and falls back to ArrayPool for unusually long
        // relative paths.
        int totalLength = first.Length + second.Length + 1;
        using BufferScope<char> inputBuffer = new(stackalloc char[StackInputBufferSize], totalLength);
        Span<char> input = inputBuffer[..totalLength];
        first.CopyTo(input);
        second.CopyTo(input[first.Length..]);
        input[totalLength - 1] = separator;

        EngineInputs inputs = new(input, default, program, separator, kind);
        ExtGlobMatchState state = default;
        try
        {
#pragma warning disable IDE0302 // Collection initialization can be simplified - see comment in MatchExtGlob.
            Span<ProgramRange> initial = stackalloc ProgramRange[1];
#pragma warning restore IDE0302
            initial[0] = new ProgramRange { Start = 0, End = program.Length };
            return RunEngineDirectory(in inputs, initial, totalLength, ref state);
        }
        finally
        {
            state.Dispose();
        }
    }

    /// <summary>
    ///  Sets up an <see cref="ExtGlobEngine"/> (seeding its frame stack and
    ///  range-snapshot arena from <c>stackalloc</c>) and runs it against the
    ///  concatenation of the program slices in <paramref name="startRanges"/>
    ///  starting at <paramref name="inputIndex"/> and consuming exactly up to
    ///  <paramref name="totalLength"/>.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Runs once per top-level extglob match. The negation handler does not
    ///   re-enter here - it re-enters <see cref="RunEngineCore"/> directly,
    ///   reusing a single set of probe buffers across all of its bounded probes.
    ///  </para>
    ///  <para>
    ///   The five seed buffers are left uninitialized (<see cref="SkipLocalsInitAttribute"/>):
    ///   the engine writes every frame, range, and key slot before reading it
    ///   (high-water-mark counters bound every read to a written region), so it
    ///   does not depend on zero-init. This removes the per-call zero-fill of the
    ///   roughly 3.7 KB seed - on net481 RyuJIT (unvectorized memset) that clear
    ///   dominated the top-level match cost.
    ///  </para>
    /// </remarks>
    [SkipLocalsInit]
    private static bool RunEngine(
        in EngineInputs inputs,
        ReadOnlySpan<ProgramRange> startRanges,
        int inputIndex,
        int totalLength,
        ref ExtGlobMatchState state)
    {
        Span<Frame> frames = stackalloc Frame[SeedFrameCount];
        Span<ProgramRange> arena = stackalloc ProgramRange[SeedArenaCount];
        Span<ProgramRange> work = stackalloc ProgramRange[MaxRangesDepth];
        Span<ProgramRange> rest = stackalloc ProgramRange[MaxRangesDepth];
        Span<int> key = stackalloc int[KeyHeaderLength + (MaxRangesDepth * RangeKeyLength)];
        EngineScratch scratch = new(frames, arena, work, rest, key);

        return RunEngineCore(in inputs, startRanges, inputIndex, totalLength, in scratch, ref state);
    }

    /// <summary>
    ///  Directory-mode counterpart of <see cref="RunEngine"/>. Seeds its own scratch
    ///  buffers and runs the engine with directory acceptance enabled, returning the
    ///  classification (<see cref="MatchOutcome.Negative"/> when the candidate cannot
    ///  be consumed at all, <see cref="MatchOutcome.Positive"/> on a complete match,
    ///  <see cref="MatchOutcome.None"/> for a viable prefix).
    /// </summary>
    [SkipLocalsInit]
    private static MatchOutcome RunEngineDirectory(
        in EngineInputs inputs,
        ReadOnlySpan<ProgramRange> startRanges,
        int totalLength,
        ref ExtGlobMatchState state)
    {
        Span<Frame> frames = stackalloc Frame[SeedFrameCount];
        Span<ProgramRange> arena = stackalloc ProgramRange[SeedArenaCount];
        Span<ProgramRange> work = stackalloc ProgramRange[MaxRangesDepth];
        Span<ProgramRange> rest = stackalloc ProgramRange[MaxRangesDepth];
        Span<int> key = stackalloc int[KeyHeaderLength + (MaxRangesDepth * RangeKeyLength)];
        EngineScratch scratch = new(frames, arena, work, rest, key);

        state.EnterRecursion();
        ExtGlobEngine engine = new(in inputs, totalLength, in scratch, directoryMode: true);
        startRanges.CopyTo(scratch.Work);
        engine.WorkCount = startRanges.Length;
        engine.WorkInput = 0;

        try
        {
            if (!engine.Run(ref state))
            {
                return MatchOutcome.Negative;
            }

            return engine.DirectoryFullMatch ? MatchOutcome.Positive : MatchOutcome.None;
        }
        finally
        {
            engine.ReturnRented();
            state.ExitRecursion();
        }
    }

    /// <summary>
    ///  Runs the engine against <paramref name="startRanges"/> using
    ///  caller-supplied scratch buffers. Lets the negation handler reuse one set
    ///  of seed buffers across all of its bounded re-entry probes instead of
    ///  re-seeding (and zeroing) a fresh set per probe.
    /// </summary>
    private static bool RunEngineCore(
        in EngineInputs inputs,
        ReadOnlySpan<ProgramRange> startRanges,
        int inputIndex,
        int totalLength,
        in EngineScratch scratch,
        ref ExtGlobMatchState state)
    {
        // Guard the native recursion depth (negation re-entry is the only native
        // recursion). Throws before recursing past the budget so an encoder/logic
        // regression fails fast instead of overflowing the stack.
        state.EnterRecursion();
        ExtGlobEngine engine = new(in inputs, totalLength, in scratch);
        startRanges.CopyTo(scratch.Work);
        engine.WorkCount = startRanges.Length;
        engine.WorkInput = inputIndex;

        try
        {
            return engine.Run(ref state);
        }
        finally
        {
            engine.ReturnRented();
            state.ExitRecursion();
        }
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
    ///  Compacts the first <paramref name="count"/> ranges of
    ///  <paramref name="ranges"/> in place, dropping any empty
    ///  (<c>Start &gt;= End</c>) range, and returns the number of ranges that
    ///  remain.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   An empty range spans no program bytes and is a pure no-op during the
    ///   walk. When a repeating alternation (<c>*(...)</c> / <c>+(...)</c>)
    ///   re-prepends its own block, the emptied tail of the block's range would
    ///   otherwise be carried into the "rest" on every iteration and accumulate
    ///   one extra empty range per iteration, eventually overflowing the working
    ///   range buffer. Dropping empty ranges keeps the iteration count stable.
    ///  </para>
    /// </remarks>
    private static int CompactEmptyRanges(Span<ProgramRange> ranges, int count)
    {
        int write = 0;
        for (int read = 0; read < count; read++)
        {
            if (ranges[read].Start < ranges[read].End)
            {
                if (write != read)
                {
                    ranges[write] = ranges[read];
                }

                write++;
            }
        }

        return write;
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

    /// <summary>
    ///  Serializes the walker entry state - <paramref name="inputIndex"/>,
    ///  <paramref name="totalLength"/> (which the negation handler clips per
    ///  candidate length, so it varies within a single match), plus the
    ///  contents of <paramref name="ranges"/> - into
    ///  <paramref name="destination"/> as the key used by the failure memo.
    ///  Returns the number of <see cref="int"/> elements written.
    /// </summary>
    private static int SerializeState(ReadOnlySpan<ProgramRange> ranges, int inputIndex, int totalLength, Span<int> destination)
    {
        destination[0] = inputIndex;
        destination[1] = totalLength;
        destination[2] = ranges.Length;
        int written = 3;
        for (int i = 0; i < ranges.Length; i++)
        {
            destination[written++] = ranges[i].Start;
            destination[written++] = ranges[i].End;
            destination[written++] = ranges[i].KindOverride;
        }

        return written;
    }
}
