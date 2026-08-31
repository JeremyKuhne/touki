// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

internal sealed partial class CompiledGlobStrategy
{
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
        private readonly bool _useMSBuildTrailingDotAny;
        private readonly bool _useMSBuildAllDotInput;
        private readonly EffectiveDoubleStarMode _effectiveDoubleStarMode;
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
        public bool WorkSawEffectiveDoubleStar;

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
            _useMSBuildTrailingDotAny = inputs.UseMSBuildTrailingDotAny;
            _useMSBuildAllDotInput = inputs.UseMSBuildAllDotInput;
            _effectiveDoubleStarMode = inputs.EffectiveDoubleStarMode;
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
            WorkSawEffectiveDoubleStar = false;
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
                            accepted = WorkInput == _totalLength
                                && (_effectiveDoubleStarMode != EffectiveDoubleStarMode.RequireAbsent
                                    || !WorkSawEffectiveDoubleStar)
                                && (_effectiveDoubleStarMode != EffectiveDoubleStarMode.RequirePresent
                                    || WorkSawEffectiveDoubleStar);

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

                        if (opcode == GlobOpCodes.Never)
                        {
                            break;
                        }

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
                            if (_useMSBuildAllDotInput
                                && WorkInput + literalLength > _firstLength
                                    || WorkInput + literalLength > _totalLength
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
                            int width = _useMSBuildTrailingDotAny && WorkInput >= _firstLength ? 2 : 1;
                            if (_useMSBuildAllDotInput && WorkInput + width > _firstLength
                                || WorkInput + width > _totalLength)
                            {
                                break;
                            }

                            char inputChar = CharAt(_first, _second, _firstLength, WorkInput);
                            char separator = GetSeparator(WorkInput);
                            if (separator != '\0' && inputChar == separator)
                            {
                                break;
                            }

                            head.Start = programIndex + 1;
                            WorkInput += width;
                            continue;
                        }

                        if (opcode is GlobOpCodes.Class or GlobOpCodes.NegClass)
                        {
                            int classLength = _program[programIndex + 1];
                            if (_useMSBuildAllDotInput && WorkInput >= _firstLength
                                || WorkInput >= _totalLength)
                            {
                                break;
                            }

                            char inputChar = CharAt(_first, _second, _firstLength, WorkInput);
                            char separator = GetSeparator(WorkInput);
                            if (separator != '\0' && inputChar == separator)
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
                    if (choiceOp is GlobOpCodes.AnyRun or GlobOpCodes.EffectiveDoubleStarRun)
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

                        int keyLength = SerializeState(
                            _work[Head..WorkCount],
                            WorkInput,
                            _totalLength,
                            WorkSawEffectiveDoubleStar,
                            _effectiveDoubleStarMode,
                            _key);

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
                        SavedEffectiveDoubleStar = WorkSawEffectiveDoubleStar,
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
            if (!_useMSBuildTrailingDotAny)
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

            for (int index = start; index < _totalLength; index++)
            {
                char separator = GetSeparator(index);
                if (separator != '\0'
                    && CharAt(_first, _second, _firstLength, index) == separator)
                {
                    return index;
                }
            }

            return _totalLength;
        }

        private readonly char GetSeparator(int inputIndex) =>
            _useMSBuildTrailingDotAny && inputIndex >= _firstLength ? '.' : _separator;

        // Records the choice configuration captured by the given frame as a
        // proven failure.
        private readonly void RecordFrameFailure(int frameIdx, ref ExtGlobMatchState state)
        {
            ReadOnlySpan<ProgramRange> snapshot = _arena.Slice(_frames[frameIdx].SnapshotOffset, _frames[frameIdx].SnapshotCount);
            int keyLength = SerializeState(
                snapshot,
                _frames[frameIdx].SavedInput,
                _totalLength,
                _frames[frameIdx].SavedEffectiveDoubleStar,
                _effectiveDoubleStarMode,
                _key);

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

            if (k is GlobOpCodes.AnyRun or GlobOpCodes.EffectiveDoubleStarRun)
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
                WorkSawEffectiveDoubleStar = frame.SavedEffectiveDoubleStar
                    || k == GlobOpCodes.EffectiveDoubleStarRun;

                frame.Cursor = consumed + 1;
                return true;
            }

            WorkSawEffectiveDoubleStar = frame.SavedEffectiveDoubleStar;

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
                    int maxL = _useMSBuildAllDotInput
                        ? Math.Max(0, _firstLength - savedInput)
                        : _totalLength - savedInput;

                    if (_separator != '\0' || _useMSBuildTrailingDotAny)
                    {
                        for (int j = savedInput; j < _totalLength; j++)
                        {
                            if (CharAt(_first, _second, _firstLength, j) == GetSeparator(j))
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
                                if ((!_useMSBuildAllDotInput
                                        || savedInput + litLen <= _firstLength)
                                    && litLen == candidate
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

                                probeInputs = new(
                                    _first,
                                    _second,
                                    _program,
                                    _separator,
                                    _kind,
                                    _useMSBuildTrailingDotAny,
                                    _useMSBuildAllDotInput,
                                    EffectiveDoubleStarMode.Ignore);

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
                                frame.SavedEffectiveDoubleStar,
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
}
