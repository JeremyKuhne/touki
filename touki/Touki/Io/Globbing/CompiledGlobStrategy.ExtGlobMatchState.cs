// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Collections;

namespace Touki.Io.Globbing;

internal sealed partial class CompiledGlobStrategy
{
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
