// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Collections;

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

    /// <summary>
    ///  A choice point on the explicit backtrack stack. Captures the walker
    ///  configuration at the choice opcode so each alternative can be tried in
    ///  order and the state restored on backtrack. Per-kind derived data
    ///  (alternative offsets, block bounds, separator-bounded limits) is
    ///  recomputed from the program on revisit to keep the frame small.
    /// </summary>
    private struct Frame
    {
        // The resolved choice kind: the AnyRun/GlobStar opcode, or the resolved
        // alternation kind ('@', '?', '+', '*', '!').
        public char Kind;

        // Index of the choice opcode in the program.
        public int ProgramIndex;

        // inputIndex at the choice point; every alternative restarts from here.
        public int SavedInput;

        // Range-snapshot location in the arena: the configuration head..count
        // captured at entry.
        public int SnapshotOffset;
        public int SnapshotCount;

        // Next-alternative cursor; meaning depends on Kind (next consumed length
        // for AnyRun, next alt index for alternations, next candidate length for
        // negation, started-flag for GlobStar).
        public int Cursor;

        // Per-kind scratch carried across alternatives: AnyRun caches the
        // separator-bounded limit; GlobStar caches the last absorbed length.
        public int Aux;
    }

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
    ///  The iterative extglob matching engine. Holds the working configuration
    ///  (ranges, input cursor) and an explicit backtrack stack so choice points
    ///  no longer recurse natively.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Deterministic opcodes (<see cref="GlobOpCodes.Literal"/>,
    ///   <see cref="GlobOpCodes.Any"/>, <see cref="GlobOpCodes.Class"/>,
    ///   <see cref="GlobOpCodes.NegClass"/>) and the leading-empty-range skip are
    ///   processed inline in the forward loop so a run of straight opcodes makes
    ///   no stack movement. Only choice points
    ///   (<see cref="GlobOpCodes.AltStart"/>, <see cref="GlobOpCodes.AnyRun"/>,
    ///   <see cref="GlobOpCodes.GlobStar"/>) push a backtrack frame.
    ///  </para>
    ///  <para>
    ///   The failure memo (<see cref="ExtGlobMatchState"/>) engages once the walk
    ///   crosses <see cref="ExtGlobMatchState.EngageThreshold"/> choice visits.
    ///   From then on each choice configuration is checked on entry and, if all
    ///   its alternatives are exhausted without a match, recorded as a failure,
    ///   collapsing pathological backtracking from exponential to polynomial.
    ///   Memoizing only failures is sound: a configuration that cannot match is a
    ///   pure function of the current ranges, input cursor, and total length, and
    ///   keys are compared exactly so a hash collision can never produce a wrong
    ///   answer.
    ///  </para>
    /// </remarks>
    private ref struct ExtGlobEngine
    {
        private readonly ReadOnlySpan<char> _first;
        private readonly ReadOnlySpan<char> _second;
        private readonly ReadOnlySpan<char> _program;
        private readonly char _separator;
        private readonly IgnoreCaseKind _kind;
        private readonly int _totalLength;
        private readonly int _firstLength;
        private readonly bool _directoryMode;

        private Span<Frame> _frames;
        private Span<ProgramRange> _arena;
        private readonly Span<ProgramRange> _work;
        private readonly Span<ProgramRange> _rest;
        private readonly Span<int> _key;

        private Frame[]? _rentedFrames;
        private ProgramRange[]? _rentedArena;

        private int _frameCount;
        private int _arenaTop;

        // Working configuration: the active list of program ranges, the index of
        // the current (head) range, and the input cursor.
        public int WorkCount;
        public int Head;
        public int WorkInput;

        // Directory-mode output: set when the accepting state in directory mode was
        // also a complete match of the candidate (program fully consumed), so the
        // caller can distinguish MatchOutcome.Positive from a viable prefix.
        public bool DirectoryFullMatch;

        public ExtGlobEngine(
            in EngineInputs inputs,
            int totalLength,
            in EngineScratch scratch,
            bool directoryMode = false)
        {
            _first = inputs.First;
            _second = inputs.Second;
            _program = inputs.Program;
            _separator = inputs.Separator;
            _kind = inputs.Kind;
            _totalLength = totalLength;
            _firstLength = inputs.First.Length;
            _directoryMode = directoryMode;
            _frames = scratch.Frames;
            _arena = scratch.Arena;
            _work = scratch.Work;
            _rest = scratch.Rest;
            _key = scratch.Key;
            _rentedFrames = null;
            _rentedArena = null;
            _frameCount = 0;
            _arenaTop = 0;
            WorkCount = 0;
            Head = 0;
            WorkInput = 0;
            DirectoryFullMatch = false;
        }

        /// <summary>
        ///  Returns any pooled storage rented when the seed buffers overflowed.
        /// </summary>
        public readonly void ReturnRented()
        {
            if (_rentedFrames is not null)
            {
                ArrayPool<Frame>.Shared.Return(_rentedFrames);
            }

            if (_rentedArena is not null)
            {
                ArrayPool<ProgramRange>.Shared.Return(_rentedArena);
            }
        }

        /// <summary>
        ///  Runs the engine to completion, returning whether the configured
        ///  ranges match.
        /// </summary>
        public bool Run(ref ExtGlobMatchState state)
        {
            bool forward = true;
            while (true)
            {
                if (forward)
                {
                    char choiceOp = '\0';
                    bool terminal = false;
                    bool accepted = false;

                    // Run deterministic opcodes until a choice point or a
                    // terminal/deadend state.
                    while (true)
                    {
                        while (Head < WorkCount)
                        {
                            ref ProgramRange skip = ref _work[Head];
                            if (skip.Start < skip.End)
                            {
                                break;
                            }

                            Head++;
                        }

                        if (_directoryMode && WorkInput == _totalLength)
                        {
                            // Directory mode: the candidate directory path has been
                            // fully consumed on a live forward path, so it is a viable
                            // prefix - some descendant could still match. Accept
                            // immediately (the caller maps this to "keep descending").
                            // Record whether the program is also exhausted so the
                            // caller can report a complete match (Positive) versus a
                            // proper prefix (None).
                            DirectoryFullMatch = Head == WorkCount;
                            return true;
                        }

                        if (Head == WorkCount)
                        {
                            terminal = true;
                            accepted = WorkInput == _totalLength;
                            break;
                        }

                        // Head is invariant for the remainder of this deterministic
                        // iteration (only the skip loop above advances it), so resolve
                        // the head range reference once instead of re-indexing the work
                        // span on every field read and write below. The net481 slow-span
                        // layout costs ~8 micro-ops per indexer access; a single hoisted
                        // ref collapses that to one address computation.
                        ref ProgramRange head = ref _work[Head];

                        int programIndex = head.Start;
                        char opcode = _program[programIndex];

                        if (opcode == GlobOpCodes.AltStart
                            && head.KindOverride != '\0'
                            && WorkInput <= head.MinProgressInput)
                        {
                            // Progress guard: refuse another iteration of a
                            // repeating block when the previous one consumed no
                            // input. Collapse the block (skip it) and continue
                            // with the rest, avoiding unbounded empty iterations.
                            int guardedBlockLength = _program[programIndex + 2];
                            head.Start = programIndex + guardedBlockLength;
                            head.KindOverride = '\0';
                            continue;
                        }

                        if (opcode == GlobOpCodes.Literal)
                        {
                            int literalLength = _program[programIndex + 1];
                            if (WorkInput + literalLength > _totalLength
                                || !LiteralMatchesAt(_first, _second, WorkInput, _program.Slice(programIndex + 2, literalLength), _kind))
                            {
                                break;
                            }

                            head.Start = programIndex + 2 + literalLength;
                            WorkInput += literalLength;
                            continue;
                        }

                        if (opcode == GlobOpCodes.Any)
                        {
                            if (WorkInput >= _totalLength)
                            {
                                break;
                            }

                            char inputChar = CharAt(_first, _second, _firstLength, WorkInput);
                            if (_separator != '\0' && inputChar == _separator)
                            {
                                break;
                            }

                            head.Start = programIndex + 1;
                            WorkInput++;
                            continue;
                        }

                        if (opcode is GlobOpCodes.Class or GlobOpCodes.NegClass)
                        {
                            int classLength = _program[programIndex + 1];
                            if (WorkInput >= _totalLength)
                            {
                                break;
                            }

                            char inputChar = CharAt(_first, _second, _firstLength, WorkInput);
                            if (_separator != '\0' && inputChar == _separator)
                            {
                                break;
                            }

                            ReadOnlySpan<char> body = _program.Slice(programIndex + 2, classLength);
                            bool inClass = _kind == IgnoreCaseKind.Off
                                ? ClassContainsOrdinal(body, inputChar, opcode == GlobOpCodes.NegClass)
                                : ClassContainsIgnoreCase(body, inputChar, opcode == GlobOpCodes.NegClass);
                            if (!inClass)
                            {
                                break;
                            }

                            head.Start = programIndex + 2 + classLength;
                            WorkInput++;
                            continue;
                        }

                        if (opcode is GlobOpCodes.AltSep or GlobOpCodes.AltEnd)
                        {
                            // These appear only inside an alternation block; the
                            // alternation handler slices the range at AltSep /
                            // AltEnd boundaries so they never reach the walker.
                            Debug.Fail("Encountered AltSep/AltEnd outside an alternation block.");
                            break;
                        }

                        // Choice point.
                        choiceOp = opcode;
                        break;
                    }

                    if (terminal)
                    {
                        if (accepted)
                        {
                            return true;
                        }

                        forward = false;
                        continue;
                    }

                    if (choiceOp == '\0')
                    {
                        // Deterministic mismatch: backtrack.
                        forward = false;
                        continue;
                    }

                    int choiceProgramIndex = _work[Head].Start;

                    // Resolve the frame kind and any push-time scratch.
                    char frameKind;
                    int auxValue = 0;
                    if (choiceOp == GlobOpCodes.AnyRun)
                    {
                        frameKind = choiceOp;

                        // Path-aware AnyRun never crosses a separator.
                        int limit = _totalLength;
                        if (_separator != '\0')
                        {
                            limit = IndexOfSeparator(WorkInput);
                        }

                        auxValue = limit;
                    }
                    else if (choiceOp == GlobOpCodes.GlobStar)
                    {
                        frameKind = choiceOp;
                    }
                    else
                    {
                        frameKind = _work[Head].KindOverride != '\0'
                            ? _work[Head].KindOverride
                            : _program[choiceProgramIndex + 1];
                    }

                    // Failure-memo check (only once engaged).
                    state.Steps++;
                    if (state.Engaged || state.Steps > ExtGlobMatchState.EngageThreshold)
                    {
                        if (!state.Engaged)
                        {
                            state.Engage();
                        }

                        int keyLength = SerializeState(_work[Head..WorkCount], WorkInput, _totalLength, _key);
                        if (state.IsKnownFailure(_key[..keyLength]))
                        {
                            forward = false;
                            continue;
                        }
                    }

                    // Snapshot the choice configuration and push a frame.
                    int snapshotCount = WorkCount - Head;
                    EnsureArena(_arenaTop + snapshotCount);
                    CopyRanges(_work.Slice(Head, snapshotCount), _arena[_arenaTop..], snapshotCount);

                    EnsureFrames(_frameCount + 1);
                    _frames[_frameCount] = new Frame
                    {
                        Kind = frameKind,
                        ProgramIndex = choiceProgramIndex,
                        SavedInput = WorkInput,
                        SnapshotOffset = _arenaTop,
                        SnapshotCount = snapshotCount,
                        Cursor = 0,
                        Aux = auxValue,
                    };
                    _arenaTop += snapshotCount;
                    _frameCount++;

                    if (ProduceAlternative(_frameCount - 1, ref state))
                    {
                        forward = true;
                        continue;
                    }

                    // No alternative produced any candidate: record the failure
                    // and backtrack.
                    if (state.Engaged)
                    {
                        RecordFrameFailure(_frameCount - 1, ref state);
                    }

                    _arenaTop = _frames[_frameCount - 1].SnapshotOffset;
                    _frameCount--;
                    forward = false;
                    continue;
                }
                else
                {
                    // Backtrack: advance the topmost frame to its next
                    // alternative, popping exhausted frames.
                    bool resumed = false;
                    while (_frameCount > 0)
                    {
                        if (ProduceAlternative(_frameCount - 1, ref state))
                        {
                            resumed = true;
                            break;
                        }

                        if (state.Engaged)
                        {
                            RecordFrameFailure(_frameCount - 1, ref state);
                        }

                        _arenaTop = _frames[_frameCount - 1].SnapshotOffset;
                        _frameCount--;
                    }

                    if (!resumed)
                    {
                        return false;
                    }

                    forward = true;
                    continue;
                }
            }
        }

        // Grows the frame stack, preserving existing frames and indices.
        private void EnsureFrames(int needed)
        {
            if (needed <= _frames.Length)
            {
                return;
            }

            int newSize = Math.Max(needed, _frames.Length * 2);
            Frame[] bigger = ArrayPool<Frame>.Shared.Rent(newSize);
            _frames[.._frameCount].CopyTo(bigger);
            if (_rentedFrames is not null)
            {
                ArrayPool<Frame>.Shared.Return(_rentedFrames);
            }

            _rentedFrames = bigger;
            _frames = bigger;
        }

        // Grows the range-snapshot arena, preserving existing snapshots and
        // offsets.
        private void EnsureArena(int needed)
        {
            if (needed <= _arena.Length)
            {
                return;
            }

            int newSize = Math.Max(needed, _arena.Length * 2);
            ProgramRange[] bigger = ArrayPool<ProgramRange>.Shared.Rent(newSize);
            _arena[.._arenaTop].CopyTo(bigger);
            if (_rentedArena is not null)
            {
                ArrayPool<ProgramRange>.Shared.Return(_rentedArena);
            }

            _rentedArena = bigger;
            _arena = bigger;
        }

        // Copies the first `count` ProgramRange values from `source` to
        // `destination`. The backtracking save/restore moves only a few ranges per
        // choice point, and at those tiny lengths the fixed per-call overhead of
        // Span.CopyTo (its Buffer.Memmove length dispatch) dominates the actual
        // copy - this save/restore was the single hottest cluster in the
        // GlobEnumeratorExtGlobSingleWithRoot CPU trace. The common one-to-three
        // range cases are unrolled into direct assignments off a hoisted ref (no
        // bounds check, no Memmove setup); larger snapshots fall back to the bulk
        // copy. Every call site copies between two distinct buffers (work, arena,
        // rest) so the regions never overlap and a forward copy is always correct.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CopyRanges(ReadOnlySpan<ProgramRange> source, Span<ProgramRange> destination, int count)
        {
            if (count <= 0)
            {
                return;
            }

            if (count > 3)
            {
                source[..count].CopyTo(destination);
                return;
            }

            ref ProgramRange src = ref MemoryMarshal.GetReference(source);
            ref ProgramRange dst = ref MemoryMarshal.GetReference(destination);
            dst = src;
            if (count == 1)
            {
                return;
            }

            Unsafe.Add(ref dst, 1) = Unsafe.Add(ref src, 1);
            if (count == 2)
            {
                return;
            }

            Unsafe.Add(ref dst, 2) = Unsafe.Add(ref src, 2);
        }

        // Returns the index of the first separator at or after 'start' in the
        // virtual _first + _second concatenation, clamped to _totalLength when none
        // is found. The path-aware AnyRun choice point calls this once per push to
        // bound its run, and it was the hottest scan in the engine after the literal
        // compare. The previous form walked one character at a time through CharAt,
        // paying the virtual-concatenation branch on every character; this routes
        // each contiguous half through the vectorized span IndexOf instead.
        //
        // _totalLength can be clipped below _first.Length + _second.Length (the
        // negation handler shortens it per candidate), so every search is bounded by
        // it rather than by the raw span lengths.
        private readonly int IndexOfSeparator(int start)
        {
            int total = _totalLength;
            char separator = _separator;

            if (start < _firstLength)
            {
                int firstEnd = Math.Min(_firstLength, total);
                if (start < firstEnd)
                {
                    int relative = _first[start..firstEnd].IndexOf(separator);
                    if (relative >= 0)
                    {
                        return start + relative;
                    }
                }

                if (total > _firstLength)
                {
                    int found = _second[..(total - _firstLength)].IndexOf(separator);
                    if (found >= 0)
                    {
                        return _firstLength + found;
                    }
                }

                return total;
            }

            int secondCount = total - start;
            if (secondCount > 0)
            {
                int found = _second.Slice(start - _firstLength, secondCount).IndexOf(separator);
                if (found >= 0)
                {
                    return start + found;
                }
            }

            return total;
        }

        // Records the choice configuration captured by the given frame as a
        // proven failure.
        private readonly void RecordFrameFailure(int frameIdx, ref ExtGlobMatchState state)
        {
            ReadOnlySpan<ProgramRange> snapshot = _arena.Slice(_frames[frameIdx].SnapshotOffset, _frames[frameIdx].SnapshotCount);
            int keyLength = SerializeState(snapshot, _frames[frameIdx].SavedInput, _totalLength, _key);
            state.RecordFailure(_key[..keyLength]);
        }

        // Advances the given frame to its next alternative, applying it to the
        // working configuration. Returns false when the frame is exhausted.
        private bool ProduceAlternative(int frameIdx, ref ExtGlobMatchState state)
        {
            // Bind the frame once by reference. The body reads and writes its
            // fields many times across the per-kind loops below; a single ref
            // local avoids re-indexing the (bounds-checked) frame span on every
            // access, which is a measurable cost on the net481 RyuJIT.
            ref Frame frame = ref _frames[frameIdx];
            char k = frame.Kind;
            int snapOffset = frame.SnapshotOffset;
            int snapCount = frame.SnapshotCount;
            int savedInput = frame.SavedInput;
            int programIndex = frame.ProgramIndex;
            ReadOnlySpan<ProgramRange> snap = _arena.Slice(snapOffset, snapCount);

            if (k == GlobOpCodes.AnyRun)
            {
                // Alternatives are consumed lengths 0, 1, ... up to the cached
                // separator-bounded limit.
                int consumed = frame.Cursor;
                int limit = frame.Aux;
                if (savedInput + consumed > limit)
                {
                    return false;
                }

                CopyRanges(snap, _work, snapCount);
                WorkCount = snapCount;
                Head = 0;
                _work[0].Start = programIndex + 1;
                WorkInput = savedInput + consumed;
                frame.Cursor = consumed + 1;
                return true;
            }

            if (k == GlobOpCodes.GlobStar)
            {
                // Alternatives are the valid absorbed lengths in increasing
                // order; the run stops once a length would exceed the input.
                int flags = _program[programIndex + 1];
                int absorbed = frame.Cursor == 0
                    ? FirstValidGlobStarLength(_first, _second, savedInput, flags, _separator)
                    : NextValidGlobStarLength(_first, _second, savedInput, frame.Aux, flags, _separator);

                if (absorbed < 0 || savedInput + absorbed > _totalLength)
                {
                    return false;
                }

                CopyRanges(snap, _work, snapCount);
                WorkCount = snapCount;
                Head = 0;
                _work[0].Start = programIndex + 2;
                WorkInput = savedInput + absorbed;
                frame.Aux = absorbed;
                frame.Cursor = 1;
                return true;
            }

            // Alternation: the per-alternative body offsets were baked into the
            // AltStart header at compile time, so read them straight from the
            // offset table instead of re-walking and re-parsing the whole block on
            // every production. Header layout:
            //   [AltStart][kind][blockLen][altCount][off_0..off_{altCount-1}]
            // where off_j is alternative j's body start relative to the AltStart.
            int blockLength = _program[programIndex + 2];
            int afterEnd = programIndex + blockLength;
            int altEndIndex = afterEnd - 1;
            int altCount = _program[programIndex + 3];

            // The per-alternative body offsets live in the AltStart header at
            // [programIndex + 4 + j]. They are read inline at each use site
            // (AltBodyStart) rather than expanded into a scratch buffer: the
            // computation is a single program read, so materializing a table
            // would only add a stack zero and fill loop on every production.
            int altOffsetBase = programIndex + 4;

            switch (k)
            {
                case '@':
                    while (frame.Cursor < altCount)
                    {
                        int j = frame.Cursor;
                        frame.Cursor = j + 1;
                        int altBodyStart = programIndex + _program[altOffsetBase + j];
                        int altBodyEnd = (j + 1 < altCount) ? programIndex + _program[altOffsetBase + j + 1] - 1 : altEndIndex;
                        CopyRanges(snap, _rest, snapCount);
                        _rest[0].Start = afterEnd;
                        _rest[0].KindOverride = '\0';
                        if (BuildRangesWithAlternative(altBodyStart, altBodyEnd, _rest[..snapCount], _work, out WorkCount))
                        {
                            Head = 0;
                            WorkInput = savedInput;
                            return true;
                        }
                    }

                    return false;

                case '?':
                    while (frame.Cursor < altCount)
                    {
                        int j = frame.Cursor;
                        frame.Cursor = j + 1;
                        int altBodyStart = programIndex + _program[altOffsetBase + j];
                        int altBodyEnd = (j + 1 < altCount) ? programIndex + _program[altOffsetBase + j + 1] - 1 : altEndIndex;
                        CopyRanges(snap, _rest, snapCount);
                        _rest[0].Start = afterEnd;
                        _rest[0].KindOverride = '\0';
                        if (BuildRangesWithAlternative(altBodyStart, altBodyEnd, _rest[..snapCount], _work, out WorkCount))
                        {
                            Head = 0;
                            WorkInput = savedInput;
                            return true;
                        }
                    }

                    if (frame.Cursor == altCount)
                    {
                        // Zero-consume: skip the entire alternation block.
                        frame.Cursor = altCount + 1;
                        CopyRanges(snap, _work, snapCount);
                        WorkCount = snapCount;
                        Head = 0;
                        _work[0].Start = afterEnd;
                        _work[0].KindOverride = '\0';
                        WorkInput = savedInput;
                        return true;
                    }

                    return false;

                case '+':
                    while (frame.Cursor < altCount)
                    {
                        int j = frame.Cursor;
                        frame.Cursor = j + 1;
                        int altBodyStart = programIndex + _program[altOffsetBase + j];
                        int altBodyEnd = (j + 1 < altCount) ? programIndex + _program[altOffsetBase + j + 1] - 1 : altEndIndex;
                        CopyRanges(snap, _rest, snapCount);
                        _rest[0].Start = afterEnd;
                        _rest[0].KindOverride = '\0';
                        int restCount = CompactEmptyRanges(_rest, snapCount);

                        if (altBodyStart >= altBodyEnd)
                        {
                            // Empty alternative: the progress guard refuses to
                            // re-enter the block with no input consumed, so this
                            // collapses to matching just the rest.
                            CopyRanges(_rest, _work, restCount);
                            WorkCount = restCount;
                            Head = 0;
                            WorkInput = savedInput;
                            return true;
                        }

                        if (BuildRangesWithAlternativeAndBlock(altBodyStart, altBodyEnd, programIndex, afterEnd, _rest[..restCount], _work, out WorkCount))
                        {
                            _work[1].MinProgressInput = savedInput;
                            Head = 0;
                            WorkInput = savedInput;
                            return true;
                        }
                    }

                    return false;

                case '*':
                    while (frame.Cursor < altCount)
                    {
                        int j = frame.Cursor;
                        frame.Cursor = j + 1;
                        int altBodyStart = programIndex + _program[altOffsetBase + j];
                        int altBodyEnd = (j + 1 < altCount) ? programIndex + _program[altOffsetBase + j + 1] - 1 : altEndIndex;
                        CopyRanges(snap, _rest, snapCount);
                        _rest[0].Start = afterEnd;
                        _rest[0].KindOverride = '\0';
                        int restCount = CompactEmptyRanges(_rest, snapCount);

                        if (altBodyStart >= altBodyEnd)
                        {
                            CopyRanges(_rest, _work, restCount);
                            WorkCount = restCount;
                            Head = 0;
                            WorkInput = savedInput;
                            return true;
                        }

                        if (BuildRangesWithAlternativeAndBlock(altBodyStart, altBodyEnd, programIndex, afterEnd, _rest[..restCount], _work, out WorkCount))
                        {
                            _work[1].MinProgressInput = savedInput;
                            Head = 0;
                            WorkInput = savedInput;
                            return true;
                        }
                    }

                    if (frame.Cursor == altCount)
                    {
                        // Zero iterations: skip the entire alternation block.
                        frame.Cursor = altCount + 1;
                        CopyRanges(snap, _work, snapCount);
                        WorkCount = snapCount;
                        Head = 0;
                        _work[0].Start = afterEnd;
                        _work[0].KindOverride = '\0';
                        WorkInput = savedInput;
                        return true;
                    }

                    return false;

                case '!':
                {
                    // Negation accepts the first candidate length L for which no
                    // alternative matches exactly L characters; the continuation
                    // (rest at savedInput + L) is the produced alternative.
                    int maxL = _totalLength - savedInput;
                    if (_separator != '\0')
                    {
                        for (int j = savedInput; j < _totalLength; j++)
                        {
                            if (CharAt(_first, _second, _firstLength, j) == _separator)
                            {
                                maxL = j - savedInput;
                                break;
                            }
                        }
                    }

                    // Single-literal alternatives are matched against each
                    // candidate length with a cheap comparison rather than a full
                    // engine re-entry (see the loop below); their shape is read
                    // directly from the compiled program, so no parallel table is
                    // needed.
                    Span<ProgramRange> altRange = stackalloc ProgramRange[1];

                    // Probe scratch is consumed only by the non-literal re-entry
                    // path. Rather than zero-filling ~4 KB of stackalloc on every
                    // negation production (the dominant cost in the negation flame
                    // graphs - System.Buffer.ZeroMemoryInternal), the seed buffers
                    // stay unallocated until the candidate loop actually reaches a
                    // non-literal alternative. The common negation shape - every
                    // alternative a single literal, e.g. !(bin|obj) - takes only
                    // the cheap comparison path below and never allocates. When a
                    // non-literal alternative is first encountered the seed buffers
                    // are grown once from the pool (uninitialized: the engine writes
                    // every frame, range, and key slot before reading it, so it does
                    // not depend on zero-init) and reused for every later probe. The
                    // using declarations return each rental to the pool on exit; a
                    // default BufferScope that was never grown disposes as a no-op.
                    using BufferScope<Frame> probeFramesScope = default;
                    using BufferScope<ProgramRange> probeArenaScope = default;
                    using BufferScope<ProgramRange> probeWorkScope = default;
                    using BufferScope<ProgramRange> probeRestScope = default;
                    using BufferScope<int> probeKeyScope = default;
                    EngineScratch probeScratch = default;
                    EngineInputs probeInputs = default;
                    bool probeReady = false;

                    while (frame.Cursor <= maxL)
                    {
                        int candidate = frame.Cursor;
                        frame.Cursor = candidate + 1;

                        bool anyAltMatches = false;
                        for (int j = 0; j < altCount; j++)
                        {
                            int altBodyStart = programIndex + _program[altOffsetBase + j];
                            int altBodyEnd = (j + 1 < altCount) ? programIndex + _program[altOffsetBase + j + 1] - 1 : altEndIndex;

                            // Fast path for a single-literal alternative: its shape
                            // is already encoded in the program (a Literal opcode
                            // plus length char spanning the whole body), so detect it
                            // by reading those bytes directly - no parallel
                            // per-alternative table, no engine re-entry. It matches
                            // exactly 'candidate' characters only when the lengths
                            // agree and the literal compares equal at the cursor.
                            if (IsSingleLiteralAlternative(_program, altBodyStart, altBodyEnd))
                            {
                                int litLen = _program[altBodyStart + 1];
                                if (litLen == candidate
                                    && LiteralMatchesAt(_first, _second, savedInput, _program.Slice(altBodyStart + 2, litLen), _kind))
                                {
                                    anyAltMatches = true;
                                    break;
                                }

                                continue;
                            }

                            altRange[0] = new ProgramRange { Start = altBodyStart, End = altBodyEnd };

                            // First non-literal alternative on this production: grow
                            // the probe seed buffers once from the pool and reuse
                            // them for every later candidate/alternative.
                            if (!probeReady)
                            {
                                int keyLength = KeyHeaderLength + (MaxRangesDepth * RangeKeyLength);
                                probeFramesScope.EnsureCapacity(SeedFrameCount);
                                probeArenaScope.EnsureCapacity(SeedArenaCount);
                                probeWorkScope.EnsureCapacity(MaxRangesDepth);
                                probeRestScope.EnsureCapacity(MaxRangesDepth);
                                probeKeyScope.EnsureCapacity(keyLength);

                                // EnsureCapacity can hand back an oversized pool
                                // bucket, so slice each span back to its logical
                                // seed length. This keeps the probe path under the
                                // same MaxRangesDepth/key-size ceiling as the
                                // stack-backed top-level path, so it can never build
                                // a state the key buffer was not sized to serialize.
                                probeScratch = new(
                                    probeFramesScope.AsSpan()[..SeedFrameCount],
                                    probeArenaScope.AsSpan()[..SeedArenaCount],
                                    probeWorkScope.AsSpan()[..MaxRangesDepth],
                                    probeRestScope.AsSpan()[..MaxRangesDepth],
                                    probeKeyScope.AsSpan()[..keyLength]);
                                probeInputs = new(_first, _second, _program, _separator, _kind);
                                probeReady = true;
                            }

                            // Bounded re-entry: probes whether the alternative
                            // consumes exactly 'candidate' characters. Native
                            // recursion depth here is the negation nesting depth
                            // only.
                            if (RunEngineCore(
                                in probeInputs,
                                altRange,
                                savedInput,
                                savedInput + candidate,
                                in probeScratch,
                                ref state))
                            {
                                anyAltMatches = true;
                                break;
                            }
                        }

                        if (anyAltMatches)
                        {
                            continue;
                        }

                        CopyRanges(snap, _work, snapCount);
                        WorkCount = snapCount;
                        Head = 0;
                        _work[0].Start = afterEnd;
                        _work[0].KindOverride = '\0';
                        WorkInput = savedInput + candidate;
                        return true;
                    }

                    return false;
                }

                default:
                    Debug.Fail($"Unknown extglob kind '{k}'.");
                    return false;
            }
        }

        /// <summary>
        ///  Returns <see langword="true"/> when the alternative body
        ///  <c>[bodyStart, bodyEnd)</c> is a single <see cref="GlobOpCodes.Literal"/>
        ///  opcode spanning the whole body. Such alternatives are matched by a
        ///  direct length-and-compare against each negation candidate length, so
        ///  they never re-enter the engine and need no probe scratch.
        /// </summary>
        private static bool IsSingleLiteralAlternative(ReadOnlySpan<char> program, int bodyStart, int bodyEnd) =>
            bodyStart < bodyEnd
                && program[bodyStart] == GlobOpCodes.Literal
                && bodyStart + 2 + program[bodyStart + 1] == bodyEnd;
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

    /// <summary>
    ///  Lazily-engaged, allocation-free-on-the-common-path guard against
    ///  catastrophic backtracking in <see cref="ExtGlobEngine"/>. Tracks a step
    ///  counter and, once engaged, an exact failure memo of walker entry states
    ///  proven not to match.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   The memo is a <see cref="SequenceSet{T}"/> of the serialized entry
    ///   states (see <see cref="SerializeState"/>). Its single pooled arena is
    ///   rented only when the walker crosses <see cref="EngageThreshold"/> steps
    ///   and is returned by <see cref="Dispose"/>. Keys are compared exactly, so
    ///   a hash collision can never cause a state to be wrongly treated as a
    ///   known failure.
    ///  </para>
    ///  <para>
    ///   A failure memo backs the iterative backtracking walker rather than
    ///   flattening the program to a ReDoS-proof Thompson NFA because negation
    ///   (<c>!(...)</c>) is a non-regular complement over a clipped input window:
    ///   it cannot be expressed as a single NFA and would re-introduce a memo for
    ///   the negation sub-problem anyway. Memoizing failures collapses the
    ///   general case (including negation) to polynomial work without converting
    ///   the walker to an automaton. Exactness of the key is load-bearing for both
    ///   correctness and the polynomial bound: an approximate or linearly-scanned
    ///   structure would either conflate distinct states (wrong answers) or
    ///   degrade lookups to O(distinct) and re-open the denial-of-service.
    ///  </para>
    /// </remarks>
    private struct ExtGlobMatchState
    {
        /// <summary>
        ///  Number of walker steps after which the failure memo engages. Common
        ///  patterns complete in far fewer steps and never pay the memo cost;
        ///  only pathological backtracking crosses this and is then collapsed
        ///  from exponential to polynomial by the memo.
        /// </summary>
        public const int EngageThreshold = 1000;

        // Upper bound on distinct failure states recorded. Far above the
        // distinct-state count of any realistic program; protects memory on a
        // crafted input. Once reached, recording stops (still correct).
        private const int MaxEntries = 1 << 20;

        /// <summary>
        ///  Hard ceiling on the native recursion depth of the engine. Negation
        ///  (<c>!(...)</c>) re-entry is the engine's only native recursion: each
        ///  enclosing negation re-enters <see cref="RunEngineCore"/> one level
        ///  deeper, and the encoder caps extglob nesting at
        ///  <see cref="GlobSpecification.MaxExtGlobDepth"/>, so a validly compiled
        ///  program reaches at most that many re-entries plus the one top-level
        ///  entry. A program that exceeds this could only come from an encoder bug
        ///  that let through deeper nesting, so the guard exists purely to convert
        ///  such a regression into a deterministic, catchable failure instead of a
        ///  stack overflow.
        /// </summary>
        private const int MaxRecursionDepth = GlobSpecification.MaxExtGlobDepth + 1;

        /// <summary>
        ///  Running count of choice-point visits in the current match. Compared
        ///  against <see cref="EngageThreshold"/> to decide when to engage the
        ///  failure memo; never reset within a match.
        /// </summary>
        public long Steps;

        // Current native recursion depth (number of live RunEngineCore frames).
        // Incremented on entry and decremented on exit by EnterRecursion /
        // ExitRecursion; guarded against MaxRecursionDepth.
        private int _depth;

        // Failure memo. Non-null exactly when engaged; lazily created so benign
        // inputs that never cross the threshold pay no allocation.
        private SequenceSet<int>? _failures;

        /// <summary>
        ///  <see langword="true"/> once the failure memo has been created - that is,
        ///  once the walk has crossed <see cref="EngageThreshold"/> steps. Before
        ///  then the common-case path pays no memo allocation or lookup cost.
        /// </summary>
        public readonly bool Engaged => _failures is not null;

        /// <summary>
        ///  Records entry into a <see cref="RunEngineCore"/> frame and throws when
        ///  the native recursion depth would exceed <see cref="MaxRecursionDepth"/>.
        ///  Unreachable for any validly compiled program (the encoder caps nesting
        ///  at <see cref="GlobSpecification.MaxExtGlobDepth"/>); it fires only if a
        ///  logic change breaks that invariant, failing fast and deterministically
        ///  rather than overflowing the stack.
        /// </summary>
        public void EnterRecursion()
        {
            if (++_depth > MaxRecursionDepth)
            {
                throw new InvalidOperationException(
                    $"Extended-glob match recursion exceeded the depth budget of {MaxRecursionDepth}. "
                        + $"The encoder should reject patterns nested deeper than {GlobSpecification.MaxExtGlobDepth} before matching.");
            }
        }

        /// <summary>
        ///  Records exit from a <see cref="RunEngineCore"/> frame, balancing a
        ///  prior <see cref="EnterRecursion"/>.
        /// </summary>
        public void ExitRecursion() => _depth--;

        /// <summary>
        ///  Creates the failure memo. Called once when the step counter first
        ///  crosses <see cref="EngageThreshold"/>.
        /// </summary>
        public void Engage() => _failures = new SequenceSet<int>(minimumCapacity: 1024);

        /// <summary>
        ///  Returns <see langword="true"/> if <paramref name="key"/> has already
        ///  been recorded as a failed state.
        /// </summary>
        public readonly bool IsKnownFailure(ReadOnlySpan<int> key) => _failures!.Contains(key);

        /// <summary>
        ///  Records <paramref name="key"/> as a failed state so future
        ///  occurrences short-circuit. No-op once <see cref="MaxEntries"/> is
        ///  reached.
        /// </summary>
        public readonly void RecordFailure(ReadOnlySpan<int> key)
        {
            if (_failures!.Count < MaxEntries)
            {
                _failures.Add(key);
            }
        }

        /// <summary>
        ///  Returns the memo's pooled storage.
        /// </summary>
        public readonly void Dispose() => _failures?.Dispose();
    }
}
