// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

public sealed partial class GlobSpecification
{
    /// <summary>
    ///  Classifies a source pattern and constructs the cheapest <see cref="GlobStrategy"/>
    ///  implementation that can evaluate it.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   The factory recognizes the following shapes in order of decreasing specificity:
    ///  </para>
    ///  <para>
    ///   - Empty -&gt; <see cref="LiteralGlobStrategy"/> matching <c>""</c>.<br/>
    ///   - All-literal -&gt; <see cref="LiteralGlobStrategy"/>.<br/>
    ///   - All-<c>*</c> -&gt; <see cref="AnyGlobStrategy"/>.<br/>
    ///   - <c>text*</c> -&gt; <see cref="PrefixGlobStrategy"/>.<br/>
    ///   - <c>*text</c> -&gt; <see cref="SuffixGlobStrategy"/>.<br/>
    ///   - <c>*text*</c> -&gt; <see cref="ContainsGlobStrategy"/>.<br/>
    ///   - <c>prefix*suffix</c> -&gt; <see cref="PrefixSuffixGlobStrategy"/>.<br/>
    ///   - Everything else -&gt; <see cref="CompiledGlobStrategy"/>.
    ///  </para>
    /// </remarks>
    private static partial class Factory
    {
        /// <summary>
        ///  Maximum number of characters that a single <see cref="GlobOpCodes.Literal"/> or
        ///  <see cref="GlobOpCodes.Class"/>/<see cref="GlobOpCodes.NegClass"/> body may
        ///  contain. The length is stored in a single <see langword="char"/> header slot, so
        ///  anything beyond <see cref="char.MaxValue"/> can't be encoded faithfully. The
        ///  encoder rejects patterns that would overflow this with
        ///  <see cref="GlobCompileErrorCode.PatternTooLarge"/>.
        /// </summary>
        internal const int MaxOpcodeBodyLength = char.MaxValue;

        /// <summary>
        ///  Maximum nesting depth of extended-glob constructs (<c>?(…)</c>, <c>*(…)</c>,
        ///  <c>+(…)</c>, <c>@(…)</c>, <c>!(…)</c>). Exceeding this raises
        ///  <see cref="GlobCompileErrorCode.FeatureLimitExceeded"/>. The cap exists so
        ///  the interpreter's stack-allocated savepoint buffer stays bounded: with
        ///  this depth and the per-construct alternative cap, simultaneous savepoints
        ///  are guaranteed to fit in the fixed runtime budget.
        /// </summary>
        internal const int MaxExtGlobDepth = 8;

        /// <summary>
        ///  Maximum number of <c>|</c>-separated alternatives in a single extended-glob
        ///  construct. Exceeding this raises
        ///  <see cref="GlobCompileErrorCode.FeatureLimitExceeded"/>.
        /// </summary>
        internal const int MaxExtGlobAlternatives = 32;

        /// <summary>
        ///  Default upper bound (in characters) applied by the
        ///  <see cref="TryCreate(StringSegment, GlobDialect, GlobOptions, GlobPathSeparator, out GlobStrategy?, out GlobCompileError)"/>
        ///  overload that omits an explicit <c>maxPatternLength</c>. Sized to comfortably
        ///  cover real-world path-matching patterns (Linux <c>PATH_MAX</c> is 4096, Windows
        ///  long-path is ~32K, and typical globs are well under 200 chars) while still
        ///  rejecting pathologically large inputs from untrusted sources.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   Callers that need to compile patterns larger than this (e.g. machine-generated
        ///   exclusion lists) should call the
        ///   <see cref="TryCreate(StringSegment, GlobDialect, GlobOptions, GlobPathSeparator, int, out GlobStrategy?, out GlobCompileError)"/>
        ///   overload directly with a larger limit, or pass <c>-1</c> to disable the check.
        ///  </para>
        /// </remarks>
        private const int DefaultMaxPatternLength = 4096;

        /// <summary>
        ///  Convenience overload that applies the <see cref="DefaultMaxPatternLength"/> cap.
        ///  Use this when compiling patterns from untrusted input where the caller has no
        ///  specific size budget in mind; for trusted callers or larger budgets call the
        ///  six-argument overload directly.
        /// </summary>
        public static bool TryCreate(
            StringSegment pattern,
            GlobDialect dialect,
            GlobOptions options,
            GlobPathSeparator separator,
            [NotNullWhen(true)] out GlobStrategy? result,
            out GlobCompileError error) =>
            TryCreate(pattern, dialect, options, separator, DefaultMaxPatternLength, out result, out error);

        /// <summary>
        ///  Attempts to construct the appropriate matcher for <paramref name="pattern"/>.
        ///  When <paramref name="maxPatternLength"/> is non-negative, patterns longer than
        ///  that limit are rejected with <see cref="GlobCompileErrorCode.PatternTooLarge"/>.
        /// </summary>
        public static bool TryCreate(
            StringSegment pattern,
            GlobDialect dialect,
            GlobOptions options,
            GlobPathSeparator separator,
            int maxPatternLength,
            [NotNullWhen(true)] out GlobStrategy? result,
            out GlobCompileError error)
        {
            result = null;

            if (maxPatternLength >= 0 && pattern.Length > maxPatternLength)
            {
                error = new GlobCompileError(
                    GlobCompileErrorCode.PatternTooLarge,
                    position: maxPatternLength,
                    message: $"Pattern length {pattern.Length} exceeds the configured limit of {maxPatternLength}.");

                return false;
            }

            char escape = dialect.GetEscapeChar(options);
            bool allowClasses = dialect.HasCharacterClasses();
            bool pathAware = dialect.IsPathAware();
            char resolvedSeparator = pathAware ? ResolveSeparator(dialect, separator) : '\0';

            // One-time OS-style separator normalization. Mirrors
            // MSBuildSpecification.Normalize: when the resolved separator differs from the
            // characters appearing in the pattern (e.g., MSBuild on Windows with
            // GlobPathSeparator.OSDefault wants '\' but the pattern was written portably
            // with '/'), translate the cross-separator characters at compile time so the
            // matcher's NFA and the file-system enumerator inputs agree without per-call
            // translation. Restricted to dialects with no escape character so we never
            // corrupt a '\'-escape sequence (POSIX, Bash, Git, PowerShell use escapes).
            if (resolvedSeparator != '\0' && escape == '\0')
            {
                char altSeparator = resolvedSeparator == '/' ? '\\' : '/';

                // StringSegment.Replace returns `this` unchanged (zero alloc) when the
                // character isn't present, so the common case where the caller already
                // wrote portable separators pays only one IndexOf.
                pattern = pattern.Replace(altSeparator, resolvedSeparator);
            }

            bool negated = false;
            bool rootAnchored = false;
            bool directoryOnly = false;

            if (dialect == GlobDialect.Git)
            {
                // Gitignore-specific preprocessing: strip leading '!' and '/' and trailing '/'
                // markers and report via flags. The matcher exposes the flags as init
                // properties; <see cref="GlobSpecification.IsMatch"/> wraps with
                (negated, rootAnchored, directoryOnly) = StripGitignoreMarkers(ref pattern);
            }

            if (dialect == GlobDialect.FileSystemGlobbing && resolvedSeparator != '\0')
            {
                // FSG-specific compile-time rewrites that mirror Matcher's own behavior
                // captured by the upstream PatternMatchingTests in dotnet/runtime:
                //
                //  - Leading "/" anchors to the implicit root and is stripped.
                //  - Leading "./" and embedded "/./" dot-segments are normalized away.
                //  - A segment that is exactly "*.*" is equivalent to "*" (StarDotStarIsSameAsStar).
                //  - Trailing "/**" requires at least one path component beyond the prior
                //    literal; rewrite to "/*/**" so the engine enforces the same rule
                //    without a new opcode flag.
                Normalization.FileSystemGlobbing(ref pattern, resolvedSeparator);
            }

            // Dialect-specific normalization of runs of `*` and runs of `/`. The rules
            // captured by the per-dialect oracle tests are:
            //
            //   * `***` and longer:
            //     MSBuild     => pattern never matches anything (sentinel matcher below)
            //     FSG/Simple  => collapse to a single `*`
            //     Posix/Path  => collapse to a single `*`
            //     Git/Bash    => collapse to `**` (globstar)
            //     PowerShell  => leave alone (treated as repeated wildcard)
            //
            //   * `//` and longer (internal):
            //     MSBuild     => collapse to a single `/`; preserve leading double
            //     FSG         => drop leading empties; internal empty => `*` segment
            //     everyone else => leave alone (PosixPath, fnmatch, gitignore etc. all
            //                      treat embedded empty path segments as literal)
            //
            // The MSBuild path also flags `CoalesceInputSeparators = true` so the
            // matcher coalesces runs in inputs at IsMatch time. See the
            // "Sequential-separator behavior" and "Multiple-asterisk-run behavior"
            // sections of docs/globbing-feature-plan.md for the empirical findings
            // that drive each rule.
            //
            // `DisallowEmptyInput` mirrors the documented "empty input never matches"
            // behavior of `FileSystemName.MatchesSimpleExpression`,
            // `Microsoft.Extensions.FileSystemGlobbing.Matcher`, and gitignore (an empty
            // path is undefined; LibGit2Sharp rejects it outright). MSBuild's
            // FileMatcher.IsMatch / MSBuildGlob.IsMatch do match empty input against
            // empty pattern and against `*`, so MSBuild is intentionally not in this
            // list - see the ported FileMatcher rows under
            // touki.tests/Touki/Io/Globbing/PortedTests.MSBuild.cs.
            bool disallowEmptyInput = dialect is
                GlobDialect.Simple
                or GlobDialect.FileSystemGlobbing
                or GlobDialect.Git;

            if (TryNormalizeRuns(ref pattern, dialect, resolvedSeparator, out bool neverMatch, out bool coalesceInputSeparators))
            {
                if (neverMatch)
                {
                    result = new NeverMatchGlobStrategy(dialect, options)
                    {
                        Negated = negated,
                        RootAnchored = rootAnchored,
                        DirectoryOnly = directoryOnly,
                        Separator = resolvedSeparator,
                        CoalesceInputSeparators = coalesceInputSeparators,
                        DisallowEmptyInput = disallowEmptyInput,
                    };

                    error = default;
                    return true;
                }
            }

            // Gitignore non-anchored match-anywhere rule. A Git pattern with no leading `/`
            // and no internal `/` (the trailing `/` for DirectoryOnly was already stripped
            // by StripGitignoreMarkers) matches at any path depth. Prepending `**/` at
            // compile time pushes the work into the compiled program; no runtime
            // suffix-matching is needed. Per the gitignore spec:
            //
            //   "If there is a separator at the beginning or middle (or both) of the
            //    pattern, then the pattern is relative to the directory level of the
            //    particular .gitignore file itself. Otherwise the pattern may also match
            //    at any level below the .gitignore level."
            //
            // Patterns that already begin with `**` followed by the separator or the end
            // of the pattern (typically the output of the multiple-asterisk-run
            // normalization for `***`/`****`, or hand-written globstar) already match at
            // any depth; prepending another `**/` would produce `**/**` which forces a
            // leading directory and breaks bare-root inputs.

            if (dialect == GlobDialect.Git && !rootAnchored && pattern.Length > 0)
            {
                // Git is path-aware so resolvedSeparator is always a real character.
                if (pattern.IndexOf(resolvedSeparator) < 0)
                {
                    bool startsWithGlobStarToken =
                        pattern.Length >= 2
                        && pattern[0] == '*' && pattern[1] == '*'
                        && (pattern.Length == 2 || pattern[2] == resolvedSeparator);

                    if (!startsWithGlobStarToken)
                    {
                        // Compile-path string allocation; not on the match hot path.
                        pattern = $"**{resolvedSeparator}{pattern}";
                    }
                }
            }

            // First pass: validate the pattern, count metacharacters, and locate the single
            // '*' for the PrefixSuffix shape. The scan also fails fast on malformed input
            // (dangling escape, unterminated class) so the encoder can assume well-formed.
            bool allowExtGlob = (options & GlobOptions.AllowExtGlob) != 0;
            if (!Scan(pattern, escape, allowClasses, allowExtGlob, out PatternShape shape, out error))
            {
                return false;
            }

            // Pure literal pattern shortcut: same encoded program for every dialect, no NFA.
            if (shape.HasNoMetacharacters)
            {
                result = new LiteralGlobStrategy(UnescapeToString(pattern, escape), dialect, options)
                {
                    Negated = negated,
                    RootAnchored = rootAnchored,
                    DirectoryOnly = directoryOnly,
                    Separator = resolvedSeparator,
                    CoalesceInputSeparators = coalesceInputSeparators,
                    DisallowEmptyInput = disallowEmptyInput,
                };

                return true;
            }

            // Path-unaware specialized matchers (Prefix/Suffix/Contains/PrefixSuffix/Any).
            // Path-aware dialects route everything through the bytecode interpreter so the
            // separator-aware semantics are honored. Extglob patterns also skip these
            // specializations - they fall through to the general path so the
            // bytecode interpreter handles the alternation.
            if (!pathAware && !shape.HasExtGlob
                && TryCreatePathUnawareSpecialized(pattern, ref shape, dialect, options, escape, out result))
            {
                result.DisallowEmptyInput = disallowEmptyInput;
                return true;
            }

            // Globstar-file-name specialization: when the pattern is exactly
            // `**<sep><segment>` with no further separators in <segment>, the match is
            // equivalent to "file name only" matching against <segment>. This is the
            // canonical real-world glob shape (`**/*.cs`, `**/*.json`, `**/file.cs`,
            // ...) and benefits enormously from bypassing the bytecode interpreter and
            // the per-file path concatenation. Globstar must be enabled (implicitly for
            // MSBuild / FSG / Git, or via GlobOptions.AllowGlobStar). Patterns with an
            // extglob construct in the segment are still routed here - the helper
            // recognizes the `@(*lit1|*lit2|...)` shape and lowers it to
            // <see cref="MultiSuffixGlobStrategy"/>; segments with extglob constructs
            // the helper cannot specialize return false and fall through to the
            // general bytecode path.
            bool allowGlobStar = (options & GlobOptions.AllowGlobStar) != 0 || dialect.GlobStarIsImplicit();
            if (pathAware
                && allowGlobStar
                && TryCreateGlobStarFileNameStrategy(
                    pattern,
                    dialect,
                    options,
                    escape,
                    allowClasses,
                    resolvedSeparator,
                    negated,
                    rootAnchored,
                    directoryOnly,
                    coalesceInputSeparators,
                    disallowEmptyInput,
                    out result))
            {
                return true;
            }

            // General path: encode the pattern into the bytecode-in-a-string program.
            if (TryCreateGeneralMatcher(
                pattern,
                dialect,
                options,
                escape,
                allowClasses,
                allowExtGlob,
                pathAware,
                resolvedSeparator,
                negated,
                rootAnchored,
                directoryOnly,
                coalesceInputSeparators,
                out result,
                out error))
            {
                result.DisallowEmptyInput = disallowEmptyInput;
                return true;
            }

            return false;
        }

        /// <summary>
        ///  Detects the <c>**&lt;sep&gt;&lt;segment&gt;</c> shape (with no further separators in
        ///  <c>&lt;segment&gt;</c>) and, when the segment fits one of the cheap path-unaware
        ///  specialized matchers, wraps it in a <see cref="GlobStarFileNameStrategy"/> that
        ///  bypasses path concatenation entirely on the per-file hot path.
        /// </summary>
        private static bool TryCreateGlobStarFileNameStrategy(
            ReadOnlySpan<char> pattern,
            GlobDialect dialect,
            GlobOptions options,
            char escape,
            bool allowClasses,
            char separator,
            bool negated,
            bool rootAnchored,
            bool directoryOnly,
            bool coalesceInputSeparators,
            bool disallowEmptyInput,
            [NotNullWhen(true)] out GlobStrategy? result)
        {
            result = null;

            if (separator == '\0'
                || pattern.Length < 4
                || pattern[0] != '*'
                || pattern[1] != '*'
                || pattern[2] != separator)
            {
                return false;
            }

            ReadOnlySpan<char> segment = pattern[3..];
            if (segment.IndexOf(separator) >= 0)
            {
                return false;
            }

            // Scan the segment in isolation. A malformed segment falls through to the
            // general compile path, which produces the canonical error. Extglob is
            // permitted here so the `@(*lit1|*lit2|...)` suffix-set shape can be lowered
            // to <see cref="MultiSuffixGlobStrategy"/>; other extglob shapes flow back
            // to the general bytecode path.
            bool allowExtGlobScan = (options & GlobOptions.AllowExtGlob) != 0;
            if (!Scan(segment, escape, allowClasses, allowExtGlobScan, out PatternShape segmentShape, out _))
            {
                return false;
            }

            GlobStrategy? segmentMatcher;
            if (segmentShape.HasNoMetacharacters)
            {
                segmentMatcher = new LiteralGlobStrategy(UnescapeToString(segment, escape), dialect, options);
            }
            else if (segmentShape.HasExtGlob)
            {
                // Only the `@(*lit1|*lit2|...)` shape is specializable today. Anything
                // else (other kinds, non-suffix alts, nested extglob) falls through to
                // the general bytecode path which already correctly handles it.
                if (!TryCreateMultiSuffixSegmentMatcher(segment, escape, dialect, options, out segmentMatcher))
                {
                    return false;
                }
            }
            else if (!TryCreatePathUnawareSpecialized(segment, ref segmentShape, dialect, options, escape, out segmentMatcher))
            {
                // Segment has classes or question marks; fall back to the general path
                // so the bytecode interpreter handles those.
                return false;
            }

            result = new GlobStarFileNameStrategy(segmentMatcher, dialect, options)
            {
                Negated = negated,
                RootAnchored = rootAnchored,
                DirectoryOnly = directoryOnly,
                Separator = separator,
                CoalesceInputSeparators = coalesceInputSeparators,
                DisallowEmptyInput = disallowEmptyInput,
            };
            return true;
        }

        /// <summary>
        ///  Tries to recognize <c>&#x40;(*lit1|*lit2|...)</c> as a segment matcher.
        ///  When every alternative is a pure <c>*</c> followed by literal characters
        ///  (no nested extglob, no classes, no question marks, no escaping inside the
        ///  alternative), lowers the segment to a <see cref="MultiSuffixGlobStrategy"/>
        ///  that runs a tight <c>EndsWith</c> sweep per file.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   This is the canonical "match any of these extensions" shape produced by
        ///   user code that wants a single compiled spec instead of an N-include
        ///   <c>MatchSet</c>. Specializing it removes the recursive bytecode walker
        ///   from the per-file hot path, which is the dominant cost on
        ///   .NET Framework 4.8.1 RyuJIT (no tail calls for span-bearing helpers, slow
        ///   span indexer). See <see cref="MultiSuffixGlobStrategy"/> for the per-TFM
        ///   rationale.
        ///  </para>
        ///  <para>
        ///   Selection criteria are intentionally narrow: only <c>&#x40;(...)</c>
        ///   (exactly-one semantics) with <c>*literal</c> alternatives. Other kinds
        ///   (<c>?</c>, <c>+</c>, <c>*</c>, <c>!</c>), mixed alternative shapes, and
        ///   anything containing a metacharacter inside the literal portion all
        ///   return <see langword="false"/> so the general bytecode path picks them
        ///   up. Widening the criteria requires re-validating the net481 win -
        ///   each kind has different match semantics and the simple endswith sweep
        ///   does not generalize cleanly.
        ///  </para>
        /// </remarks>
        private static bool TryCreateMultiSuffixSegmentMatcher(
            ReadOnlySpan<char> segment,
            char escape,
            GlobDialect dialect,
            GlobOptions options,
            [NotNullWhen(true)] out GlobStrategy? result)
        {
            result = null;

            // Required shape: `@( ... )` with at least one character between the parens.
            if (segment.Length < 4
                || segment[0] != '@'
                || segment[1] != '('
                || segment[segment.Length - 1] != ')')
            {
                return false;
            }

            ReadOnlySpan<char> body = segment[2..(segment.Length - 1)];

            // Worst case: one suffix per character. Stack-bound by the encoder's
            // MaxExtGlobAlternatives cap (32) which is comfortably below.
            Span<int> altStarts = stackalloc int[64];
            int altCount = 0;
            altStarts[altCount++] = 0;
            for (int i = 0; i < body.Length; i++)
            {
                char current = body[i];
                if (current == escape && i + 1 < body.Length)
                {
                    // Escaped char inside the alt; advance past it. The unescape pass
                    // below normalizes these out.
                    i++;
                    continue;
                }

                if (current == '|')
                {
                    if (altCount >= altStarts.Length)
                    {
                        return false;
                    }

                    altStarts[altCount++] = i + 1;
                }
                else if (current is '?' or '*' or '+' or '@' or '!')
                {
                    // Any of these followed by '(' starts a (possibly nested) extglob
                    // construct. Disqualify so the general path handles it correctly.
                    if (i + 1 < body.Length && body[i + 1] == '(')
                    {
                        return false;
                    }
                }
                else if (current is '[' or ']')
                {
                    // Character classes break the simple `*literal` shape.
                    return false;
                }
            }

            string[] suffixes = new string[altCount];
            for (int j = 0; j < altCount; j++)
            {
                int altStart = altStarts[j];
                int altEnd = (j + 1 < altCount) ? altStarts[j + 1] - 1 : body.Length;
                ReadOnlySpan<char> alt = body[altStart..altEnd];

                // Each alternative must be exactly `*literal` where literal is at least
                // one character. Pure literal alternatives (no `*`) and zero-suffix
                // alternatives (just `*`, which would match anything) are intentionally
                // not specialized today - widening this requires verifying the
                // leading-dot semantics on each shape.
                if (alt.Length < 2 || alt[0] != '*')
                {
                    return false;
                }

                ReadOnlySpan<char> suffixSource = alt[1..];

                // The literal portion must contain no further metacharacters. Scan
                // explicitly here instead of calling Scan to keep the path-aware
                // semantics for `?` / `*` / classes intact even when AllowExtGlob is
                // disabled at the caller site.
                for (int k = 0; k < suffixSource.Length; k++)
                {
                    char c = suffixSource[k];
                    if (c == escape && k + 1 < suffixSource.Length)
                    {
                        k++;
                        continue;
                    }

                    if (c is '*' or '?' or '[' or ']' or '(' or ')' or '|')
                    {
                        return false;
                    }
                }

                suffixes[j] = UnescapeToString(suffixSource, escape);
            }

            result = new MultiSuffixGlobStrategy(suffixes, dialect, options);
            return true;
        }

        /// <summary>
        ///  Strips the gitignore-specific metadata markers from <paramref name="pattern"/>
        /// </summary>
        private static (bool Negated, bool RootAnchored, bool DirectoryOnly) StripGitignoreMarkers(
            ref StringSegment pattern)
        {
            bool negated = false;
            bool rootAnchored = false;
            bool directoryOnly = false;

            if (pattern.IsEmpty)
            {
                return (negated, rootAnchored, directoryOnly);
            }

            if (pattern[0] == '!')
            {
                // Leading '!' negates the match; the '!' is stripped and the flag is reported
                negated = true;
                pattern = pattern[1..];

                if (pattern.IsEmpty)
                {
                    return (negated, rootAnchored, directoryOnly);
                }
            }

            if (pattern[0] == '/')
            {
                // Leading '/' anchors the pattern to the gitignore root; the leading '/' is
                // stripped but the pattern is no longer subject to the "match anywhere" rule.
                rootAnchored = true;
                pattern = pattern[1..];

                if (pattern.IsEmpty)
                {
                    return (negated, rootAnchored, directoryOnly);
                }
            }

            if (pattern[^1] == '/')
            {
                // Trailing '/' marks directory-only; the '/' is stripped and the flag is reported.
                directoryOnly = true;
                pattern = pattern[..^1];
            }

            return (negated, rootAnchored, directoryOnly);
        }

        /// <summary>
        ///  Applies dialect-specific normalization of runs of <c>*</c> (three or more in a
        ///  row) and runs of <see cref="char"/> path-separator characters in the pattern.
        ///  Sets <paramref name="neverMatch"/> when the dialect treats the pattern as
        ///  unmatchable (MSBuild's response to <c>***</c>), and
        ///  <paramref name="coalesceInputSeparators"/> when the dialect normalizes runs of
        ///  separators in <em>inputs</em> at match time.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   See the &quot;Sequential-separator behavior&quot; and &quot;Multiple-asterisk-run
        ///   behavior&quot; sections of <c>docs/globbing-feature-plan.md</c> for the
        ///   per-dialect rule each branch implements, and the oracle tests under
        ///   <c>touki.tests/Touki/Io/Globbing/</c> that pin the rules down.
        ///  </para>
        /// </remarks>
        private static bool TryNormalizeRuns(
            ref StringSegment pattern,
            GlobDialect dialect,
            char separator,
            out bool neverMatch,
            out bool coalesceInputSeparators)
        {
            neverMatch = false;
            coalesceInputSeparators = dialect is GlobDialect.MSBuild or GlobDialect.FileSystemGlobbing;

            if (pattern.IsEmpty)
            {
                return false;
            }

            // The asterisk-run scan is consumed by every dialect except PowerShell (which
            // treats repeated `*` literally and never collapses). Skip the scan there.
            bool hasAsteriskRun = false;
            if (dialect != GlobDialect.PowerShell)
            {
                for (int i = 0; i + 2 < pattern.Length; i++)
                {
                    if (pattern[i] == '*' && pattern[i + 1] == '*' && pattern[i + 2] == '*')
                    {
                        hasAsteriskRun = true;
                        break;
                    }
                }
            }

            // MSBuild: any run of 3+ `*` poisons the entire pattern (oracle says
            // MSBuildGlob accepts such patterns but the resulting glob never matches).
            if (dialect == GlobDialect.MSBuild && hasAsteriskRun)
            {
                neverMatch = true;
                return true;
            }

            // The separator-run scan is consumed only by MSBuild's collapse-runs rule and
            // FSG's drop-leading-and-rewrite-internal rule. Everything else preserves the
            // pattern bytes verbatim so the scan is pure overhead.
            bool hasLeadingSeparatorRun = false;
            bool hasInternalSeparatorRun = false;
            if (separator != '\0'
                && pattern.Length >= 2
                && dialect is GlobDialect.MSBuild or GlobDialect.FileSystemGlobbing)
            {
                if (pattern[0] == separator && pattern[1] == separator)
                {
                    hasLeadingSeparatorRun = true;
                }

                int scanStart = hasLeadingSeparatorRun ? 1 : 0;
                for (int j = scanStart; j + 1 < pattern.Length; j++)
                {
                    if (pattern[j] == separator && pattern[j + 1] == separator
                        && !(hasLeadingSeparatorRun && j == 0))
                    {
                        hasInternalSeparatorRun = true;
                        break;
                    }
                }
            }

            bool needsAsteriskCollapse = hasAsteriskRun && dialect is
                GlobDialect.FileSystemGlobbing
                or GlobDialect.Simple
                or GlobDialect.Posix
                or GlobDialect.PosixPath
                or GlobDialect.Git
                or GlobDialect.Bash;
            bool needsFsgSepTransform =
                dialect == GlobDialect.FileSystemGlobbing
                && (hasLeadingSeparatorRun || hasInternalSeparatorRun);
            bool needsMsbuildSepTransform =
                dialect == GlobDialect.MSBuild
                && (hasInternalSeparatorRun
                    || (hasLeadingSeparatorRun && Path.DirectorySeparatorChar != '\\'));

            if (!needsAsteriskCollapse && !needsFsgSepTransform && !needsMsbuildSepTransform)
            {
                return false;
            }

            ValueStringBuilder builder = new(stackalloc char[256]);
            ReadOnlySpan<char> asteriskReplacement = needsAsteriskCollapse
                ? (dialect is GlobDialect.Git or GlobDialect.Bash ? "**" : "*")
                : default;

            int k = 0;
            if (hasLeadingSeparatorRun)
            {
                if (needsFsgSepTransform)
                {
                    // FSG drops leading empty segments entirely.
                    while (k < pattern.Length && pattern[k] == separator)
                    {
                        k++;
                    }
                }
                else if (needsMsbuildSepTransform)
                {
                    if (Path.DirectorySeparatorChar == '\\')
                    {
                        // Windows: MSBuild preserves the leading double-separator
                        // (UNC anchor) verbatim and only collapses internal/trailing
                        // runs. The MSBuild dialect uses '/' as separator on Windows
                        // too, so this branch fires for '//foo' on Windows.
                        builder.Append(separator);
                        builder.Append(separator);
                        k = 2;
                    }
                    else
                    {
                        // Non-Windows: UNC doesn't apply, so MSBuildGlob coalesces
                        // leading runs to a single separator the same way it
                        // coalesces internal runs.
                        builder.Append(separator);
                    }

                    while (k < pattern.Length && pattern[k] == separator)
                    {
                        k++;
                    }
                }
            }

            while (k < pattern.Length)
            {
                char current = pattern[k];

                if (current == '*' && needsAsteriskCollapse)
                {
                    int runStart = k;
                    while (k < pattern.Length && pattern[k] == '*')
                    {
                        k++;
                    }
                    int runLength = k - runStart;
                    if (runLength >= 3)
                    {
                        builder.Append(asteriskReplacement);
                    }
                    else
                    {
                        builder.Append(pattern.Slice(runStart, runLength));
                    }
                    continue;
                }

                if (current == separator && separator != '\0' && (needsFsgSepTransform || needsMsbuildSepTransform))
                {
                    int runStart = k;
                    while (k < pattern.Length && pattern[k] == separator)
                    {
                        k++;
                    }
                    int runLength = k - runStart;
                    if (runLength >= 2)
                    {
                        if (needsFsgSepTransform)
                        {
                            if (k >= pattern.Length)
                            {
                                // Trailing-only run: collapse to a single `/`. FSG's
                                // input-side normalization makes `a//` equivalent to
                                // `a/` (and `a//` itself matches via the same trailing
                                // normalization).
                                builder.Append(separator);
                            }
                            else
                            {
                                // Internal empty segment becomes a single-`*` segment.
                                // e.g. `a//b` -> `a/*/b`, `a///b` -> `a/*/b`.
                                builder.Append(separator);
                                builder.Append('*');
                                builder.Append(separator);
                            }
                        }
                        else
                        {
                            // MSBuild: collapse runs of `/` to one.
                            builder.Append(separator);
                        }
                    }
                    else
                    {
                        builder.Append(separator);
                    }
                    continue;
                }

                builder.Append(current);
                k++;
            }

            pattern = builder.ToStringAndDispose();
            return true;
        }

        /// <summary>
        ///  Tries to fit <paramref name="pattern"/> into one of the path-unaware specialized
        ///  matchers (<see cref="AnyGlobStrategy"/>, <see cref="PrefixGlobStrategy"/>,
        ///  <see cref="SuffixGlobStrategy"/>, <see cref="ContainsGlobStrategy"/>, or
        ///  <see cref="PrefixSuffixGlobStrategy"/>). Returns <see langword="false"/> when no
        ///  shape matches; the caller falls through to the general bytecode path.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   Callers must have already confirmed the dialect is path-unaware. The
        ///   gitignore init flags do not apply here because only the path-aware
        ///   <see cref="GlobDialect.Git"/> dialect sets them.
        ///  </para>
        /// </remarks>
        private static bool TryCreatePathUnawareSpecialized(
            ReadOnlySpan<char> pattern,
            ref PatternShape shape,
            GlobDialect dialect,
            GlobOptions options,
            char escape,
            [NotNullWhen(true)] out GlobStrategy? result)
        {
            if (shape.IsAllStars)
            {
                result = new AnyGlobStrategy(dialect, options);
                return true;
            }

            if (!shape.HasClasses && !shape.HasQuestionMarks && shape.StarCount == 1)
            {
                int starIndex = shape.SingleStarSourceIndex;
                ReadOnlySpan<char> prefixSource = pattern[..starIndex];
                ReadOnlySpan<char> suffixSource = pattern[(starIndex + 1)..];

                string? prefix = prefixSource.IsEmpty ? null : UnescapeToString(prefixSource, escape);
                string? suffix = suffixSource.IsEmpty ? null : UnescapeToString(suffixSource, escape);

                if (prefix is null && suffix is null)
                {
                    result = new AnyGlobStrategy(dialect, options);
                }
                else if (prefix is null)
                {
                    result = new SuffixGlobStrategy(suffix!, dialect, options);
                }
                else if (suffix is null)
                {
                    result = new PrefixGlobStrategy(prefix, dialect, options);
                }
                else
                {
                    result = new PrefixSuffixGlobStrategy(prefix, suffix, dialect, options);
                }

                return true;
            }

            if (!shape.HasClasses
                && !shape.HasQuestionMarks
                && shape.StarCount == 2
                && shape.LeadsWithStar
                && shape.EndsWithStar)
            {
                // *text*  -- one literal run surrounded by stars.
                ReadOnlySpan<char> needle = pattern[1..(pattern.Length - 1)];
                result = new ContainsGlobStrategy(UnescapeToString(needle, escape), dialect, options);
                return true;
            }

            result = null;
            return false;
        }

        /// <summary>
        ///  Builds a <see cref="CompiledGlobStrategy"/> for the general bytecode path:
        ///  encodes the pattern into a program string, locates the trailing-literal
        ///  fast-fail anchor, and stamps the four init properties onto the result.
        ///  Returns <see langword="false"/> when the encoder hits an internal size limit
        ///  (a single Literal or Class body exceeding <see cref="MaxOpcodeBodyLength"/>),
        ///  in which case <paramref name="error"/> carries
        ///  <see cref="GlobCompileErrorCode.PatternTooLarge"/>.
        /// </summary>
        private static bool TryCreateGeneralMatcher(
            ReadOnlySpan<char> pattern,
            GlobDialect dialect,
            GlobOptions options,
            char escape,
            bool allowClasses,
            bool allowExtGlob,
            bool pathAware,
            char separator,
            bool negated,
            bool rootAnchored,
            bool directoryOnly,
            bool coalesceInputSeparators,
            [NotNullWhen(true)] out GlobStrategy? result,
            out GlobCompileError error)
        {
            // Globstar is opt-in via GlobOptions.AllowGlobStar for most dialects and
            // implicit for MSBuild / FileSystemGlobbing / Git (see GlobStarIsImplicit).
            // The encoder still requires path-awareness to emit GlobStar opcodes; a
            // path-unaware dialect with the flag set collapses '**' to '*'.
            bool allowGlobStar = (options & GlobOptions.AllowGlobStar) != 0 || dialect.GlobStarIsImplicit();
            if (!TryEncodeProgram(
                pattern,
                escape,
                allowClasses,
                allowGlobStar && pathAware,
                allowExtGlob,
                separator,
                out string program,
                out bool hasGlobStar,
                out bool hasExtGlob,
                out error))
            {
                result = null;
                return false;
            }

            // Tail-anchor optimization: when the program ends in a Literal op, the matcher
            // can EndsWith-fast-fail on the tail before running the NFA. We pass the tail
            // offset/length within the same program string so no extra allocation is needed.
            //
            // For non-extglob programs the tail is SUFFICIENT (matches mean the input ends
            // exactly in this literal) so the matcher both EndsWith-checks and trims.
            //
            // For extglob programs the tail is NECESSARY but not sufficient: when every
            // top-level execution path through the outermost alternation ends in the same
            // literal suffix, an EndsWith check still rules out the (typically large
            // majority of) inputs that don't end that way. We do not trim - the
            // alternation walker still needs the full input to resolve which alternative
            // matches. <see cref="ComputeExtGlobCommonTailSlice"/> returns the longest
            // common literal suffix as a (start, length) slice within the program string
            // (pointing at any one alternative's matching Literal payload tail); it returns
            // (-1, 0) when no common tail exists, in which case we run the walker without
            // a pre-check.
            int nfaProgramLength;
            int tailStart;
            int tailLength;
            if (hasExtGlob)
            {
                nfaProgramLength = program.Length;
                if (!ComputeExtGlobCommonTailSlice(program, out tailStart, out tailLength))
                {
                    tailStart = 0;
                    tailLength = 0;
                }
            }
            else
            {
                FindTrailingLiteral(program, out nfaProgramLength, out tailStart, out tailLength);
            }

            result = new CompiledGlobStrategy(program, nfaProgramLength, tailStart, tailLength, hasGlobStar, hasExtGlob, dialect, options)
            {
                Negated = negated,
                RootAnchored = rootAnchored,
                DirectoryOnly = directoryOnly,
                Separator = separator,
                CoalesceInputSeparators = coalesceInputSeparators,
            };
            return true;
        }

        /// <summary>
        ///  Resolves a <see cref="GlobPathSeparator"/> override against the dialect's
        ///  default. <see cref="GlobPathSeparator.DialectDefault"/> uses the dialect's
        ///  documented separator; the other values force a specific character.
        /// </summary>
        internal static char ResolveSeparator(GlobDialect dialect, GlobPathSeparator separator) => separator switch
        {
            GlobPathSeparator.DialectDefault => dialect.DefaultSeparator(),
            GlobPathSeparator.OSDefault => Path.DirectorySeparatorChar,
            GlobPathSeparator.ForwardSlash => '/',
            GlobPathSeparator.Backslash => '\\',
            _ => dialect.DefaultSeparator(),
        };

        /// <summary>
        ///  Locates the trailing <see cref="GlobOpCodes.Literal"/> op in <paramref name="program"/>,
        ///  if any, and returns the offset/length of its character payload along with the
        ///  length of the prefix portion that the NFA should walk.
        /// </summary>
        private static void FindTrailingLiteral(string program, out int nfaProgramLength, out int tailStart, out int tailLength)
        {
            ReadOnlySpan<char> span = program.AsSpan();
            int i = 0;
            int lastOpcodeStart = -1;
            char lastOpcode = '\0';
            int lastOpcodeLength = 0;

            while (i < span.Length)
            {
                char opcode = span[i];
                lastOpcodeStart = i;
                lastOpcode = opcode;

                if (opcode is GlobOpCodes.AnyRun or GlobOpCodes.Any)
                {
                    i++;
                }
                else if (opcode == GlobOpCodes.GlobStar)
                {
                    // GlobStar = opcode + 1 payload char (flags).
                    i += 2;
                }
                else if (opcode is GlobOpCodes.Literal or GlobOpCodes.Class or GlobOpCodes.NegClass)
                {
                    lastOpcodeLength = span[i + 1];
                    i += 2 + lastOpcodeLength;
                }
                else
                {
                    // Defensive: unknown opcode; bail out without claiming a tail.
                    nfaProgramLength = program.Length;
                    tailStart = -1;
                    tailLength = 0;
                    return;
                }
            }

            if (lastOpcode == GlobOpCodes.Literal && lastOpcodeStart >= 0)
            {
                tailStart = lastOpcodeStart + 2;
                tailLength = lastOpcodeLength;
                nfaProgramLength = lastOpcodeStart;
            }
            else
            {
                nfaProgramLength = program.Length;
                tailStart = -1;
                tailLength = 0;
            }
        }

        /// <summary>
        ///  Compute-time helper for the extglob tail-anchor optimization. Walks an
        ///  encoded program with one or more <see cref="GlobOpCodes.AltStart"/>
        ///  blocks and computes the longest literal suffix common to every
        ///  top-level execution path. Returns <see langword="true"/> with
        ///  <paramref name="tailStart"/> / <paramref name="tailLength"/> pointing
        ///  at a contiguous slice of <paramref name="program"/> containing that
        ///  literal when one exists, otherwise <see langword="false"/>.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   The tail returned here is a <b>necessary</b> condition for a match,
        ///   not a sufficient one: the caller still needs to run the full
        ///   alternation walker on the untrimmed input. See
        ///   <see cref="CompiledGlobStrategy.MatchCore(ReadOnlySpan{char}, ReadOnlySpan{char})"/>
        ///   for the per-call <c>EndsWith</c> pre-check.
        ///  </para>
        ///  <para>
        ///   Common cases handled:
        ///  </para>
        ///  <para>
        ///   - Program ends in a plain <see cref="GlobOpCodes.Literal"/> after
        ///     all alternation blocks. Returns that literal verbatim.<br/>
        ///   - Program ends in an <see cref="GlobOpCodes.AltStart"/> block where
        ///     every alternative ends in a literal and those literals share a
        ///     common trailing run of characters (e.g.
        ///     <c>&#x40;(*.cs|*.cs)</c> &#8594; <c>.cs</c>;
        ///     <c>&#x40;(foo.cs|bar.cs)</c> &#8594; <c>.cs</c>).<br/>
        ///   - Anything else (negation alternation, mixed extensions with no
        ///     shared suffix, alternatives that end in non-literal opcodes)
        ///     returns <see langword="false"/>; the caller falls back to running
        ///     the walker without a pre-check.
        ///  </para>
        ///  <para>
        ///   The negation kind <c>!(...)</c> is treated as &quot;no common tail
        ///   from this block&quot; even when its alternatives share a suffix:
        ///   a negation succeeds on input slices that <i>don't</i> match its
        ///   alternatives, so the alternation's success tail is whatever the
        ///   parent program contributes after the negation block, not the
        ///   alternatives' tails.
        ///  </para>
        /// </remarks>
        private static bool ComputeExtGlobCommonTailSlice(string program, out int tailStart, out int tailLength)
        {
            ReadOnlySpan<char> span = program.AsSpan();
            ReadOnlySpan<char> commonTail = ComputeProgramTailLiteral(span, out int sliceStart);
            if (commonTail.IsEmpty)
            {
                tailStart = -1;
                tailLength = 0;
                return false;
            }

            tailStart = sliceStart;
            tailLength = commonTail.Length;
            return true;
        }

        /// <summary>
        ///  Recursively computes the longest literal suffix common to every
        ///  execution path through <paramref name="program"/>. Returns the
        ///  matching slice of <paramref name="program"/> (so the caller can
        ///  reuse the program string as the literal source without allocating)
        ///  via <paramref name="sliceStartInProgram"/>.
        /// </summary>
        private static ReadOnlySpan<char> ComputeProgramTailLiteral(
            ReadOnlySpan<char> program,
            out int sliceStartInProgram)
        {
            sliceStartInProgram = -1;
            if (program.IsEmpty)
            {
                return default;
            }

            // Walk forward to locate the program's last top-level opcode. Top
            // level here means outside any AltStart..AltEnd block; AltStart is
            // treated as the unit (we descend into it only if it's the tail).
            int i = 0;
            int lastTopStart = -1;
            char lastTopOp = '\0';
            while (i < program.Length)
            {
                char opcode = program[i];
                switch (opcode)
                {
                    case GlobOpCodes.Literal:
                    case GlobOpCodes.Class:
                    case GlobOpCodes.NegClass:
                        lastTopStart = i;
                        lastTopOp = opcode;
                        i += 2 + program[i + 1];
                        break;
                    case GlobOpCodes.GlobStar:
                        lastTopStart = i;
                        lastTopOp = opcode;
                        i += 2;
                        break;
                    case GlobOpCodes.AltStart:
                        lastTopStart = i;
                        lastTopOp = opcode;
                        i += program[i + 2];
                        break;
                    case GlobOpCodes.Any:
                    case GlobOpCodes.AnyRun:
                        lastTopStart = i;
                        lastTopOp = opcode;
                        i++;
                        break;
                    default:
                        // AltSep / AltEnd should not appear at top level; bail
                        // without a tail rather than risking a wrong answer.
                        return default;
                }
            }

            if (lastTopOp == GlobOpCodes.Literal)
            {
                int payloadStart = lastTopStart + 2;
                int payloadLength = program[lastTopStart + 1];
                sliceStartInProgram = payloadStart;
                return program.Slice(payloadStart, payloadLength);
            }

            if (lastTopOp != GlobOpCodes.AltStart)
            {
                // Class / NegClass / AnyRun / Any / GlobStar at the tail: no
                // single-literal suffix the EndsWith check can verify.
                return default;
            }

            int blockStart = lastTopStart;
            int blockLen = program[blockStart + 2];
            char altKind = program[blockStart + 1];

            // `?(...)` (zero or one) and `*(...)` (zero or more) can match
            // empty - no guaranteed tail from the block itself. `!(...)`
            // succeeds on input that does NOT match its alternatives, so its
            // success-tail isn't the alternatives' tails either. Only `@(...)`
            // (exactly one) and `+(...)` (one or more) always contribute one
            // alternative's tail to the matched input; restrict the
            // optimization to those kinds.
            if (altKind is not '@' and not '+')
            {
                return default;
            }

            int altsStart = blockStart + 3;
            int altEndIndex = blockStart + blockLen - 1;
            if (altsStart > altEndIndex)
            {
                return default;
            }

            // Walk the alternatives, recursing on each to compute its tail,
            // then take the longest common suffix across all.
            ReadOnlySpan<char> commonSuffix = default;
            int chosenSliceStart = -1;
            int altStart = altsStart;
            int cursor = altsStart;
            bool first = true;
            while (true)
            {
                bool atEnd = cursor >= altEndIndex;
                bool atSep = !atEnd && program[cursor] == GlobOpCodes.AltSep;
                if (atEnd || atSep)
                {
                    ReadOnlySpan<char> altBody = program[altStart..cursor];
                    ReadOnlySpan<char> altTail = ComputeProgramTailLiteral(altBody, out int altSliceOffset);
                    if (altTail.IsEmpty)
                    {
                        return default;
                    }

                    // altSliceOffset is into altBody; translate to program offset.
                    int altSliceInProgram = altStart + altSliceOffset;

                    if (first)
                    {
                        commonSuffix = altTail;
                        chosenSliceStart = altSliceInProgram;
                        first = false;
                    }
                    else
                    {
                        int commonLen = 0;
                        int max = Math.Min(commonSuffix.Length, altTail.Length);
                        while (commonLen < max
                            && commonSuffix[commonSuffix.Length - 1 - commonLen]
                                == altTail[altTail.Length - 1 - commonLen])
                        {
                            commonLen++;
                        }

                        if (commonLen == 0)
                        {
                            return default;
                        }

                        // Trim commonSuffix to the new common length, keeping the
                        // existing slice anchor (its trailing commonLen chars are
                        // exactly the common suffix).
                        if (commonLen < commonSuffix.Length)
                        {
                            chosenSliceStart += commonSuffix.Length - commonLen;
                            commonSuffix = commonSuffix[^commonLen..];
                        }
                    }

                    if (atEnd)
                    {
                        break;
                    }

                    altStart = cursor + 1;
                    cursor++;
                    continue;
                }

                char op = program[cursor];
                if (op == GlobOpCodes.AltStart)
                {
                    cursor += program[cursor + 2];
                }
                else if (op is GlobOpCodes.Literal or GlobOpCodes.Class or GlobOpCodes.NegClass)
                {
                    cursor += 2 + program[cursor + 1];
                }
                else if (op == GlobOpCodes.GlobStar)
                {
                    cursor += 2;
                }
                else
                {
                    cursor++;
                }
            }

            sliceStartInProgram = chosenSliceStart;
            return commonSuffix;
        }

        /// <summary>
        ///  Single-pass validator that classifies the pattern's coarse shape.
        /// </summary>
        /// <param name="escape">
        ///  The escape character to honor (typically <c>\</c>, or <c>`</c> for PowerShell).
        ///  Pass <c>'\0'</c> to disable escape processing.
        /// </param>
        private static bool Scan(
            ReadOnlySpan<char> pattern,
            char escape,
            bool allowClasses,
            bool allowExtGlob,
            out PatternShape shape,
            out GlobCompileError error)
        {
            shape = default;
            error = default;

            bool sawNonStar = false;
            int firstStar = -1;

            for (int i = 0; i < pattern.Length; i++)
            {
                char current = pattern[i];

                // Extended-glob constructs always take precedence over the per-character
                // wildcard meanings of '?' and '*'. `?(`/`*(`/`+(`/`@(`/`!(` open an
                // alternation; the matching ')' and any nested constructs are consumed
                // by the recursive walker. Outside an extglob context '?' is the Any
                // wildcard, '*' is the AnyRun wildcard, and '+'/'@'/'!' are literals,
                // so the lookahead has to happen before any per-character handling.
                if (allowExtGlob
                    && i + 1 < pattern.Length
                    && pattern[i + 1] == '('
                    && current is '?' or '*' or '+' or '@' or '!')
                {
                    sawNonStar = true;
                    if (!TryScanExtGlob(pattern, ref i, escape, allowClasses, depth: 1, ref shape, out error))
                    {
                        return false;
                    }
                    continue;
                }

                if (current == '*')
                {
                    shape.StarCount++;
                    if (firstStar < 0)
                    {
                        firstStar = i;
                    }

                    if (i == 0)
                    {
                        shape.LeadsWithStar = true;
                    }

                    if (i == pattern.Length - 1)
                    {
                        shape.EndsWithStar = true;
                    }

                    continue;
                }

                sawNonStar = true;

                if (current == '?')
                {
                    shape.HasQuestionMarks = true;
                    continue;
                }

                if (allowClasses && current == '[' && HasClassClose(pattern, i))
                {
                    shape.HasClasses = true;
                    if (!SkipClass(pattern, ref i, out error))
                    {
                        return false;
                    }

                    continue;
                }

                if (escape != '\0' && current == escape)
                {
                    if (i + 1 >= pattern.Length)
                    {
                        error = new GlobCompileError(
                            GlobCompileErrorCode.DanglingEscape,
                            position: i,
                            message: $"Pattern ends with an unescaped '{escape}'.");
                        return false;
                    }

                    shape.HasEscapes = true;
                    i++;
                }
            }

            shape.IsAllStars = shape.StarCount > 0 && !sawNonStar;
            shape.HasNoMetacharacters = shape.StarCount == 0
                && !shape.HasQuestionMarks
                && !shape.HasClasses
                && !shape.HasExtGlob;
            if (shape.StarCount == 1)
            {
                shape.SingleStarSourceIndex = firstStar;
            }

            return true;
        }

        /// <summary>
        ///  Walks an extended-glob construct starting at <paramref name="i"/>, which
        ///  must point at the kind character (<c>'?'</c>, <c>'*'</c>, <c>'+'</c>,
        ///  <c>'@'</c>, or <c>'!'</c>) immediately followed by <c>'('</c>. On success
        ///  advances <paramref name="i"/> past the matching <c>')'</c>, sets
        ///  <see cref="PatternShape.HasExtGlob"/>, and returns <see langword="true"/>.
        ///  On failure reports the offending position via <paramref name="error"/>.
        /// </summary>
        /// <param name="depth">
        ///  Current nesting depth (1 at the outermost extglob). The top-level
        ///  <see cref="Scan"/> always passes 1 here; recursive calls increment.
        /// </param>
        private static bool TryScanExtGlob(
            ReadOnlySpan<char> pattern,
            ref int i,
            char escape,
            bool allowClasses,
            int depth,
            ref PatternShape shape,
            out GlobCompileError error)
        {
            error = default;

            if (depth > MaxExtGlobDepth)
            {
                error = new GlobCompileError(
                    GlobCompileErrorCode.FeatureLimitExceeded,
                    position: i,
                    message: $"Extended-glob nesting depth exceeds the limit of {MaxExtGlobDepth}.");
                return false;
            }

            int kindIndex = i;
            // pattern[i] is the kind char; pattern[i+1] is '('.
            i += 2;

            // Empty body `()` matches bash's syntax error: there must be at least
            // one alternative (which may itself be the empty alternative, written
            // as `(|)`).
            if (i < pattern.Length && pattern[i] == ')')
            {
                error = new GlobCompileError(
                    GlobCompileErrorCode.InvalidExtGlobBody,
                    position: kindIndex,
                    message: "Extended-glob construct has an empty body.");
                return false;
            }

            int alternativeCount = 1;
            shape.HasExtGlob = true;

            while (i < pattern.Length)
            {
                char current = pattern[i];

                if (current == ')')
                {
                    i++;
                    return true;
                }

                if (current == '|')
                {
                    alternativeCount++;
                    if (alternativeCount > MaxExtGlobAlternatives)
                    {
                        error = new GlobCompileError(
                            GlobCompileErrorCode.FeatureLimitExceeded,
                            position: i,
                            message:
                                $"Extended-glob alternative count exceeds the limit of {MaxExtGlobAlternatives}.");
                        return false;
                    }

                    i++;
                    continue;
                }

                // Nested extglob.
                if (i + 1 < pattern.Length
                    && pattern[i + 1] == '('
                    && current is '?' or '*' or '+' or '@' or '!')
                {
                    if (!TryScanExtGlob(pattern, ref i, escape, allowClasses, depth + 1, ref shape, out error))
                    {
                        return false;
                    }

                    continue;
                }

                if (allowClasses && current == '[' && HasClassClose(pattern, i))
                {
                    if (!SkipClass(pattern, ref i, out error))
                    {
                        return false;
                    }

                    continue;
                }

                if (escape != '\0' && current == escape)
                {
                    if (i + 1 >= pattern.Length)
                    {
                        error = new GlobCompileError(
                            GlobCompileErrorCode.DanglingEscape,
                            position: i,
                            message: $"Pattern ends with an unescaped '{escape}'.");
                        return false;
                    }

                    i += 2;
                    continue;
                }

                i++;
            }

            error = new GlobCompileError(
                GlobCompileErrorCode.UnterminatedExtGlob,
                position: kindIndex,
                message: "Extended-glob construct is not terminated.");
            return false;
        }

        /// <summary>
        ///  Returns <see langword="true"/> when <paramref name="pattern"/> contains a
        ///  closing <c>]</c> for the <c>[</c> at <paramref name="openIndex"/>, honoring
        ///  POSIX bracket-expression sub-forms (<c>[:class:]</c>, <c>[=equiv=]</c>,
        ///  <c>[.collate.]</c>) so their inner <c>]</c> is not mistaken for the outer
        ///  close. Used as a pre-check by <see cref="Scan"/> and the encoder loops so
        ///  an unterminated <c>[</c> can fall through to literal handling rather than
        ///  failing the compile - <c>fnmatch</c> and friends treat the trailing <c>[</c>
        ///  as a literal character in that case.
        /// </summary>
        private static bool HasClassClose(ReadOnlySpan<char> pattern, int openIndex)
        {
            int i = openIndex + 1;

            if (i < pattern.Length && (pattern[i] == '!' || pattern[i] == '^'))
            {
                i++;
            }

            bool firstChar = true;
            while (i < pattern.Length)
            {
                char current = pattern[i];
                if (current == ']' && !firstChar)
                {
                    return true;
                }

                if (current == '[' && i + 1 < pattern.Length)
                {
                    char marker = pattern[i + 1];
                    if (marker is ':' or '=' or '.')
                    {
                        int close = FindPosixBracketClose(pattern, i + 2, marker);
                        if (close > 0)
                        {
                            i = close + 2;
                            firstChar = false;
                            continue;
                        }
                    }
                }

                firstChar = false;
                i++;
            }

            return false;
        }

        private static bool SkipClass(ReadOnlySpan<char> pattern, ref int i, out GlobCompileError error)
        {
            error = default;
            int start = i;
            i++;

            if (i < pattern.Length && (pattern[i] == '!' || pattern[i] == '^'))
            {
                i++;
            }

            // A leading ']' is a literal member of the class.
            bool firstChar = true;
            while (i < pattern.Length)
            {
                char current = pattern[i];
                if (current == ']' && !firstChar)
                {
                    return true;
                }

                // Skip POSIX bracket-expression sub-forms ([:class:], [=equiv=], [.collate.])
                // so the inner ']' that terminates them isn't mistaken for the outer class close.
                if (current == '[' && i + 1 < pattern.Length)
                {
                    char marker = pattern[i + 1];
                    if (marker is ':' or '=' or '.')
                    {
                        int close = FindPosixBracketClose(pattern, i + 2, marker);
                        if (close > 0)
                        {
                            i = close + 2;
                            firstChar = false;
                            continue;
                        }
                    }
                }

                firstChar = false;
                i++;
            }

            error = new GlobCompileError(
                GlobCompileErrorCode.UnterminatedClass,
                position: start,
                message: "Character class '[' is not terminated.");
            return false;
        }

        /// <summary>
        ///  Encodes the pattern into the bytecode program consumed by
        ///  <see cref="CompiledGlobStrategy"/>. The result is a single string containing
        ///  opcode markers and inline literal/class payloads.
        /// </summary>
        /// <param name="allowGlobStar">
        ///  When <see langword="true"/>, a <c>**</c> token that occupies an entire path
        ///  segment (preceded by <c>/</c> or start-of-pattern, followed by <c>/</c> or
        ///  end-of-pattern) is encoded as <see cref="GlobOpCodes.GlobStar"/>; the
        ///  surrounding separators are absorbed into the opcode's flag payload so the
        ///  matcher can collapse <c>a/**/b</c> to match <c>a/b</c>. When
        ///  <see langword="false"/> or when the <c>**</c> token is not segment-bounded,
        ///  the run of stars collapses to <see cref="GlobOpCodes.AnyRun"/> exactly as a
        ///  single <c>*</c> would.
        /// </param>
        /// <param name="allowExtGlob">
        ///  When <see langword="true"/>, the encoder emits
        ///  <see cref="GlobOpCodes.AltStart"/> / <see cref="GlobOpCodes.AltSep"/> /
        ///  <see cref="GlobOpCodes.AltEnd"/> for each <c>?(…)</c>, <c>*(…)</c>,
        ///  <c>+(…)</c>, <c>@(…)</c>, <c>!(…)</c> construct encountered. When
        ///  <see langword="false"/> a literal <c>(</c> or <c>)</c> is encoded as a
        ///  literal character.
        /// </param>
        [SkipLocalsInit]
        private static bool TryEncodeProgram(
            ReadOnlySpan<char> pattern,
            char escape,
            bool allowClasses,
            bool allowGlobStar,
            bool allowExtGlob,
            char separator,
            out string program,
            out bool hasGlobStar,
            out bool hasExtGlob,
            out GlobCompileError error)
        {
            // Worst-case encoded length: every character becomes a Literal-of-1 (3 chars)
            // plus class bodies grow by 2 chars each. The stack buffer covers everything
            // up to ~85-char patterns; ValueStringBuilder grows from ArrayPool if exceeded.
            ValueStringBuilder builder = new(stackalloc char[256]);

            // Track the most recent Literal opcode position so we can retroactively strip
            // its trailing separator when the next token is a segment-boundary GlobStar
            // that absorbs the leading separator.
            LiteralCursor lastLiteral = LiteralCursor.None;
            hasGlobStar = false;
            hasExtGlob = false;
            error = default;
            int overflowPosition = -1;

            int i = 0;
            while (i < pattern.Length)
            {
                char current = pattern[i];

                // Extended-glob constructs take precedence over `?`/`*` per-character
                // wildcards and over `+`/`@`/`!` literals when followed by `(`. The
                // scanner has already validated balanced parens, depth, and alt count;
                // here we just emit the bytecode.
                if (allowExtGlob
                    && i + 1 < pattern.Length
                    && pattern[i + 1] == '('
                    && current is '?' or '*' or '+' or '@' or '!')
                {
                    if (!TryEmitExtGlob(
                            pattern,
                            ref i,
                            escape,
                            allowClasses,
                            allowGlobStar,
                            separator,
                            ref builder,
                            ref lastLiteral,
                            ref hasGlobStar,
                            out int extGlobOverflow))
                    {
                        overflowPosition = extGlobOverflow;
                        break;
                    }

                    hasExtGlob = true;
                    continue;
                }

                if (current == '?')
                {
                    builder.Append(GlobOpCodes.Any);
                    lastLiteral = LiteralCursor.None;
                    i++;
                    continue;
                }

                if (allowClasses && current == '[' && HasClassClose(pattern, i))
                {
                    int classStart = i;
                    if (!TryEmitClass(pattern, ref i, ref builder))
                    {
                        overflowPosition = classStart;
                        break;
                    }
                    lastLiteral = LiteralCursor.None;
                    continue;
                }

                if (current == '*')
                {
                    int runEnd = i + 1;
                    while (runEnd < pattern.Length && pattern[runEnd] == '*')
                    {
                        runEnd++;
                    }

                    if (TryEmitGlobStar(pattern, i, runEnd, allowGlobStar, separator, ref builder, ref lastLiteral, out int next))
                    {
                        hasGlobStar = true;
                        i = next;
                        continue;
                    }

                    // Run of '*'s collapses to a single AnyRun.
                    builder.Append(GlobOpCodes.AnyRun);
                    lastLiteral = LiteralCursor.None;
                    i = runEnd;
                    continue;
                }

                int literalStart = i;
                if (!TryEmitLiteralRun(pattern, ref i, escape, allowClasses, allowExtGlob, insideExtGlob: false, ref builder, out lastLiteral))
                {
                    overflowPosition = literalStart;
                    break;
                }
            }

            if (overflowPosition >= 0)
            {
                builder.Dispose();
                program = string.Empty;
                error = new GlobCompileError(
                    GlobCompileErrorCode.PatternTooLarge,
                    position: overflowPosition,
                    message: $"Encoded opcode body would exceed the {MaxOpcodeBodyLength}-character limit.");
                return false;
            }

            program = builder.ToStringAndDispose();
            return true;
        }

        /// <summary>
        ///  Emits the bytecode for one extended-glob construct starting at
        ///  <paramref name="i"/>, which must point at the kind character
        ///  (<c>'?'</c>, <c>'*'</c>, <c>'+'</c>, <c>'@'</c>, or <c>'!'</c>)
        ///  immediately followed by <c>'('</c>. On success advances <paramref name="i"/>
        ///  past the matching <c>')'</c> and returns <see langword="true"/>; on
        ///  encoder overflow returns <see langword="false"/> with the offending
        ///  source position via <paramref name="overflowPosition"/>.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   Layout: <c>AltStart{kind, blockLen} alt1 [AltSep alt2 ...] AltEnd</c>
        ///   where <c>blockLen</c> is the total opcode-char count of the block from
        ///   <see cref="GlobOpCodes.AltStart"/> through <see cref="GlobOpCodes.AltEnd"/>
        ///   inclusive. The scanner has already validated balanced parens, the
        ///   <see cref="MaxExtGlobDepth"/> nesting cap, and the
        ///   <see cref="MaxExtGlobAlternatives"/> per-construct alternative cap, so
        ///   no further input validation happens here.
        ///  </para>
        ///  <para>
        ///   Nested extglob constructs recurse into this same helper, each emitting
        ///   their own <see cref="GlobOpCodes.AltStart"/>/<see cref="GlobOpCodes.AltEnd"/>
        ///   pair.
        ///  </para>
        /// </remarks>
        private static bool TryEmitExtGlob(
            ReadOnlySpan<char> pattern,
            ref int i,
            char escape,
            bool allowClasses,
            bool allowGlobStar,
            char separator,
            ref ValueStringBuilder builder,
            ref LiteralCursor lastLiteral,
            ref bool hasGlobStar,
            out int overflowPosition)
        {
            Debug.Assert(i + 1 < pattern.Length && pattern[i + 1] == '(');
            Debug.Assert(pattern[i] is '?' or '*' or '+' or '@' or '!');

            overflowPosition = -1;
            int kindIndex = i;
            char kind = pattern[i];
            int altStartPos = builder.Length;

            // Reserve the AltStart header: opcode + kind + placeholder length.
            // The length payload is back-patched once AltEnd is appended.
            builder.Append(GlobOpCodes.AltStart);
            builder.Append(kind);
            builder.Append('\0');

            i += 2;
            lastLiteral = LiteralCursor.None;

            while (i < pattern.Length)
            {
                char current = pattern[i];

                if (current == ')')
                {
                    builder.Append(GlobOpCodes.AltEnd);
                    int blockLength = builder.Length - altStartPos;
                    if (blockLength > MaxOpcodeBodyLength)
                    {
                        overflowPosition = kindIndex;
                        return false;
                    }

                    // Back-patch the placeholder length slot (third char of the
                    // AltStart header).
                    builder[altStartPos + 2] = (char)blockLength;
                    lastLiteral = LiteralCursor.None;
                    i++;
                    return true;
                }

                if (current == '|')
                {
                    builder.Append(GlobOpCodes.AltSep);
                    lastLiteral = LiteralCursor.None;
                    i++;
                    continue;
                }

                // Nested extglob.
                if (i + 1 < pattern.Length
                    && pattern[i + 1] == '('
                    && current is '?' or '*' or '+' or '@' or '!')
                {
                    if (!TryEmitExtGlob(
                            pattern,
                            ref i,
                            escape,
                            allowClasses,
                            allowGlobStar,
                            separator,
                            ref builder,
                            ref lastLiteral,
                            ref hasGlobStar,
                            out overflowPosition))
                    {
                        return false;
                    }

                    continue;
                }

                if (current == '?')
                {
                    builder.Append(GlobOpCodes.Any);
                    lastLiteral = LiteralCursor.None;
                    i++;
                    continue;
                }

                if (allowClasses && current == '[' && HasClassClose(pattern, i))
                {
                    int classStart = i;
                    if (!TryEmitClass(pattern, ref i, ref builder))
                    {
                        overflowPosition = classStart;
                        return false;
                    }

                    lastLiteral = LiteralCursor.None;
                    continue;
                }

                if (current == '*')
                {
                    int runEnd = i + 1;
                    while (runEnd < pattern.Length && pattern[runEnd] == '*')
                    {
                        runEnd++;
                    }

                    if (TryEmitGlobStar(pattern, i, runEnd, allowGlobStar, separator, ref builder, ref lastLiteral, out int next))
                    {
                        hasGlobStar = true;
                        i = next;
                        continue;
                    }

                    builder.Append(GlobOpCodes.AnyRun);
                    lastLiteral = LiteralCursor.None;
                    i = runEnd;
                    continue;
                }

                int literalStart = i;
                if (!TryEmitLiteralRun(pattern, ref i, escape, allowClasses, allowExtGlob: true, insideExtGlob: true, ref builder, out lastLiteral))
                {
                    overflowPosition = literalStart;
                    return false;
                }
            }

            // The scanner already verified the closing ')'; reaching here would be
            // an encoder/scanner mismatch.
            Debug.Fail("Extglob encoder ran off the end of the pattern; scanner should have rejected this.");
            overflowPosition = kindIndex;
            return false;
        }

        /// <summary>
        ///  Records the in-progress position and length of the most recently emitted
        ///  <see cref="GlobOpCodes.Literal"/> opcode within the encoder's
        ///  <see cref="ValueStringBuilder"/>. <see cref="TryEncodeProgram"/> uses this so the
        ///  <see cref="GlobOpCodes.GlobStar"/> emitter can retroactively strip the trailing
        ///  separator from the prior Literal when a segment-bounded <c>**</c> absorbs it.
        ///  <see cref="None"/> represents "no Literal currently at the tail of the buffer"
        ///  (the most recent opcode is something else, or the buffer is empty).
        /// </summary>
        private struct LiteralCursor
        {
            public int Start;
            public int Length;

            public static LiteralCursor None => new() { Start = -1, Length = 0 };

            public readonly bool IsValid => Start >= 0;
        }

        /// <summary>
        ///  Tries to emit a segment-bounded <see cref="GlobOpCodes.GlobStar"/> for the run of
        ///  <c>*</c> characters at <c>pattern[i..runEnd]</c>. Returns <see langword="true"/>
        ///  and reports the next index to scan via <paramref name="next"/> when the run is
        ///  eligible (at least two stars, preceded by start-of-pattern or separator, followed
        ///  by end-of-pattern or separator). When eligible and preceded by a separator the
        ///  helper also retroactively strips that separator from the most recent Literal
        ///  opcode so the GlobStar owns it via <see cref="GlobOpCodes.GlobStarFlagLead"/>.
        ///  Returns <see langword="false"/> when the run is not eligible; the caller is
        ///  responsible for emitting <see cref="GlobOpCodes.AnyRun"/> in that case.
        /// </summary>
        private static bool TryEmitGlobStar(
            ReadOnlySpan<char> pattern,
            int i,
            int runEnd,
            bool allowGlobStar,
            char separator,
            ref ValueStringBuilder builder,
            ref LiteralCursor lastLiteral,
            out int next)
        {
            next = runEnd;

            if (!allowGlobStar || runEnd - i < 2)
            {
                return false;
            }

            bool leadOk = i == 0 || pattern[i - 1] == separator;
            bool trailOk = runEnd == pattern.Length || pattern[runEnd] == separator;
            if (!leadOk || !trailOk)
            {
                return false;
            }

            int flags = 0;

            // After the early return both `leadOk` and `trailOk` hold, so an `i > 0`
            // automatically means `pattern[i - 1] == separator`; the same simplification
            // applies on the trailing side. No need to re-test the character.
            if (i > 0)
            {
                flags |= GlobOpCodes.GlobStarFlagLead;
                StripTrailingSeparatorFromLastLiteral(ref builder, ref lastLiteral, separator);
            }

            if (runEnd < pattern.Length)
            {
                flags |= GlobOpCodes.GlobStarFlagTrail;
                next = runEnd + 1;
            }

            builder.Append(GlobOpCodes.GlobStar);
            builder.Append((char)flags);
            lastLiteral = LiteralCursor.None;
            return true;
        }

        /// <summary>
        ///  Removes the trailing <paramref name="separator"/> character from the most recent
        ///  Literal opcode, which is being absorbed by an immediately-following segment-bounded
        ///  GlobStar. If the Literal becomes empty its opcode + length header is dropped
        ///  entirely; otherwise the in-place length byte is decremented.
        /// </summary>
        private static void StripTrailingSeparatorFromLastLiteral(
            ref ValueStringBuilder builder,
            ref LiteralCursor lastLiteral,
            char separator)
        {
            Debug.Assert(lastLiteral.IsValid && lastLiteral.Length > 0);
            Debug.Assert(builder[builder.Length - 1] == separator);

            if (lastLiteral.Length == 1)
            {
                // Literal becomes empty; drop the opcode + length header entirely.
                builder.Length = lastLiteral.Start;
                lastLiteral = LiteralCursor.None;
            }
            else
            {
                builder.Length -= 1;
                lastLiteral.Length--;
                builder[lastLiteral.Start + 1] = (char)lastLiteral.Length;
            }
        }

        /// <summary>
        ///  Emits a <see cref="GlobOpCodes.Literal"/> opcode for the run of non-metacharacters
        ///  starting at <paramref name="i"/>. Honors <paramref name="escape"/> by consuming
        ///  the escape character and emitting only the escaped character. Reports the start
        ///  index and length of the emitted Literal back to the caller through
        ///  <paramref name="lastLiteral"/> so the segment-bounded GlobStar emitter can
        ///  retroactively strip a trailing separator if needed. Returns <see langword="false"/>
        ///  if the literal run would exceed <see cref="MaxOpcodeBodyLength"/>; in that case
        ///  <paramref name="builder"/> is left in an indeterminate state and the caller must
        ///  abandon the encode.
        /// </summary>
        private static bool TryEmitLiteralRun(
            ReadOnlySpan<char> pattern,
            ref int i,
            char escape,
            bool allowClasses,
            bool allowExtGlob,
            bool insideExtGlob,
            ref ValueStringBuilder builder,
            out LiteralCursor lastLiteral)
        {
            int literalStart = builder.Length;
            builder.Append(GlobOpCodes.Literal);
            builder.Append('\0'); // length placeholder
            int literalLength = 0;

            while (i < pattern.Length)
            {
                char current = pattern[i];
                if (current == '*' || current == '?'
                    || (allowClasses && current == '[' && HasClassClose(pattern, i)))
                {
                    break;
                }

                // Inside an extglob body, '|' separates alternatives and ')' closes the
                // construct; both terminate the literal run.
                if (insideExtGlob && (current == '|' || current == ')'))
                {
                    break;
                }

                // Extglob constructs start with one of '?'/'*'/'+'/'@'/'!' followed
                // by '('. The '?' / '*' cases already break out above; the remaining
                // three need an explicit check here because they would otherwise be
                // ordinary literal characters. The lookahead is gated on
                // <paramref name="allowExtGlob"/> so unrelated patterns pay nothing.
                if (allowExtGlob
                    && (current == '+' || current == '@' || current == '!')
                    && i + 1 < pattern.Length
                    && pattern[i + 1] == '(')
                {
                    break;
                }

                if (escape != '\0' && current == escape)
                {
                    i++;
                    if (i >= pattern.Length)
                    {
                        break;
                    }

                    current = pattern[i];
                }

                builder.Append(current);
                literalLength++;
                i++;
            }

            if (literalLength > MaxOpcodeBodyLength)
            {
                lastLiteral = LiteralCursor.None;
                return false;
            }

            builder[literalStart + 1] = (char)literalLength;
            lastLiteral = new LiteralCursor { Start = literalStart, Length = literalLength };
            return true;
        }

        /// <summary>
        ///  Emits a <see cref="GlobOpCodes.Class"/> / <see cref="GlobOpCodes.NegClass"/>
        ///  opcode for a bracket expression starting at <paramref name="i"/>. Recognizes
        ///  the bracket-expression sub-forms defined by
        ///  <see href="https://pubs.opengroup.org/onlinepubs/9799919799/basedefs/V1_chap09.html#tag_09_03_05">
        ///   IEEE Std 1003.1-2024 &#167;9.3.5 (RE Bracket Expression)</see>.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   Grammar (quoting the POSIX standard):
        ///  </para>
        ///  <para>
        ///   - <c>[abc]</c>: matching list. &quot;A matching list expression specifies a list
        ///     that shall match any one of the expressions represented in the list.&quot;<br/>
        ///   - <c>[!abc]</c> / <c>[^abc]</c>: non-matching list. &quot;A non-matching list
        ///     expression begins with a circumflex (<c>'^'</c>), and specifies a list that
        ///     shall match any character except for the expressions represented in the
        ///     list.&quot; (Touki also accepts <c>!</c> for POSIX-shell compatibility.)<br/>
        ///   - <c>[a-z]</c>: range expression. &quot;A range expression represents the set of
        ///     collating elements that fall between two elements in the current collation
        ///     sequence, inclusively.&quot; Encoded inline as the two endpoints surrounding a
        ///     <c>-</c>.<br/>
        ///   - <c>[[:NAME:]]</c>: character class expression. &quot;A character class
        ///     expression is expressed as a character class name enclosed within bracket-colon
        ///     (<c>[:</c>) and colon-bracket (<c>:]</c>) delimiters.&quot; The standard
        ///     character class names are <c>alpha</c>, <c>upper</c>, <c>lower</c>,
        ///     <c>digit</c>, <c>xdigit</c>, <c>alnum</c>, <c>space</c>, <c>blank</c>,
        ///     <c>print</c>, <c>punct</c>, <c>graph</c>, and <c>cntrl</c> - expanded
        ///     inline to their ASCII range equivalent by
        ///     <see cref="AppendPosixNamedClass"/>.<br/>
        ///   - <c>[[=c=]]</c>: equivalence class. &quot;An equivalence class expression
        ///     represents the set of collating elements belonging to an equivalence class.&quot;
        ///     Without locale support the implementation treats the inner characters as a
        ///     literal run (the C-locale fallback equivalence class for <c>c</c> is just
        ///     <c>{ c }</c>).<br/>
        ///   - <c>[[.symbol.]]</c>: collating symbol. &quot;A collating symbol shall be
        ///     interpreted as a single collating element.&quot; Without locale support the
        ///     implementation treats the inner characters as a literal run (matches each
        ///     character separately, which is consistent with the C-locale single-character
        ///     collating elements).
        ///  </para>
        ///  <para>
        ///   Bash extends POSIX bracket expressions: see
        ///   <see href="https://www.gnu.org/software/bash/manual/html_node/Pattern-Matching.html">
        ///    GNU Bash Manual &#167;3.5.8.1 (Pattern Matching)</see>. The bash grammar is a
        ///   superset; what bash documents as &quot;character class&quot; is the same
        ///   <c>[:NAME:]</c> POSIX form recognized here.
        ///  </para>
        /// </remarks>
        private static bool TryEmitClass(ReadOnlySpan<char> pattern, ref int i, ref ValueStringBuilder builder)
        {
            Debug.Assert(pattern[i] == '[');

            // Walk a sliding slice of pattern instead of indexing through it. The body loop
            // re-slices on every step; on exit we recover the new caller-visible index from
            // the residual length.
            ReadOnlySpan<char> remaining = pattern[(i + 1)..];

            bool negated = false;
            if (!remaining.IsEmpty && (remaining[0] == '!' || remaining[0] == '^'))
            {
                negated = true;
                remaining = remaining[1..];
            }

            int headerIndex = builder.Length;
            builder.Append(negated ? GlobOpCodes.NegClass : GlobOpCodes.Class);
            builder.Append('\0'); // length placeholder

            int bodyLength = 0;
            bool firstChar = true;
            while (!remaining.IsEmpty)
            {
                char current = remaining[0];
                if (current == ']' && !firstChar)
                {
                    remaining = remaining[1..];
                    break;
                }

                // POSIX bracket-expression sub-forms ([:class:], [=equiv=], [.collate.]):
                // the helper slices the recognized run off `remaining` and reports how
                // many characters it appended to the class body; otherwise we fall
                // through to per-char handling.
                if (TryEmitPosixSubForm(ref remaining, ref builder, out int appended))
                {
                    bodyLength += appended;
                    firstChar = false;
                    continue;
                }

                builder.Append(current);
                bodyLength++;
                remaining = remaining[1..];
                firstChar = false;
            }

            i = pattern.Length - remaining.Length;
            if (bodyLength > MaxOpcodeBodyLength)
            {
                return false;
            }

            builder[headerIndex + 1] = (char)bodyLength;
            return true;

            // Recognizes a POSIX bracket-expression sub-form at the start of `remaining`.
            // On success advances `remaining` past the consumed run, appends its expansion
            // to `builder`, and reports the number of body characters appended via `appended`.
            // Returns false when no sub-form is recognized, leaving `remaining` and `builder`
            // unchanged and `appended` set to 0.
            static bool TryEmitPosixSubForm(
                ref ReadOnlySpan<char> remaining,
                ref ValueStringBuilder builder,
                out int appended)
            {
                appended = 0;

                // Smallest sub-form is [::] / [==] / [..] = 4 chars.
                if (remaining.Length < 4 || remaining[0] != '[')
                {
                    return false;
                }

                char marker = remaining[1];
                if (marker is not (':' or '=' or '.'))
                {
                    return false;
                }

                int closeMarker = FindPosixBracketClose(remaining, 2, marker);
                if (closeMarker < 0)
                {
                    return false;
                }

                ReadOnlySpan<char> name = remaining[2..closeMarker];

                if (marker == ':')
                {
                    // Character class expression: expand to its ASCII range list. Unknown
                    // class names report zero appended, which signals the caller to fall
                    // through to per-character literal handling (matches the POSIX
                    // implementation-defined-name escape hatch and bash's permissive
                    // treatment of unrecognized class names).
                    appended = AppendPosixNamedClass(name, ref builder);
                    if (appended == 0)
                    {
                        return false;
                    }

                    remaining = remaining[(closeMarker + 2)..];
                    return true;
                }

                // Equivalence class ([=c=]) and collating symbol ([.c.]): without locale
                // support, treat the inner characters as a literal run. In the C/POSIX
                // locale every collating element is a single character and equivalence
                // classes are singletons, so [=e=] matches exactly e and [.ch.] matches c
                // followed by h via two class members.
                for (int j = 0; j < name.Length; j++)
                {
                    builder.Append(name[j]);
                }

                appended = name.Length;
                remaining = remaining[(closeMarker + 2)..];
                return true;
            }
        }

        /// <summary>
        ///  Locates the closing <c>X]</c> (where <c>X</c> is the marker character) for a
        ///  POSIX bracket-expression sub-form (<c>[:class:]</c>, <c>[=equiv=]</c>,
        ///  <c>[.collate.]</c>) starting at <paramref name="startIndex"/>. Returns the index
        ///  of the marker character (so the closing <c>]</c> is at <c>result + 1</c>), or
        ///  <c>-1</c> when no close is found before the outer <c>]</c> or end-of-pattern.
        /// </summary>
        private static int FindPosixBracketClose(ReadOnlySpan<char> pattern, int startIndex, char marker)
        {
            for (int j = startIndex; j < pattern.Length - 1; j++)
            {
                if (pattern[j] == marker && pattern[j + 1] == ']')
                {
                    return j;
                }

                // Don't scan past the outer ']' that closes the enclosing character class.
                if (pattern[j] == ']')
                {
                    return -1;
                }
            }

            return -1;
        }

        /// <summary>
        ///  Appends the ASCII range list equivalent to the named POSIX character class
        ///  <paramref name="name"/> (e.g. <c>alpha</c>, <c>digit</c>) to <paramref name="builder"/>.
        ///  Returns the number of body characters appended, or <c>0</c> when the name is not
        ///  recognized (caller should fall back to literal handling).
        /// </summary>
        private static int AppendPosixNamedClass(ReadOnlySpan<char> name, ref ValueStringBuilder builder)
        {
            // Each named class expands to one or more range pairs encoded as 'a-z'
            // (the class-body walker in CompiledGlobStrategy reads "char (- char)?" tuples).
            // ASCII semantics across the board; locale-aware expansion is out of scope.
            if (name.SequenceEqual("alpha".AsSpan()))
            {
                builder.Append("A-Za-z");
                return 6;
            }

            if (name.SequenceEqual("digit".AsSpan()))
            {
                builder.Append("0-9");
                return 3;
            }

            if (name.SequenceEqual("upper".AsSpan()))
            {
                builder.Append("A-Z");
                return 3;
            }

            if (name.SequenceEqual("lower".AsSpan()))
            {
                builder.Append("a-z");
                return 3;
            }

            if (name.SequenceEqual("alnum".AsSpan()))
            {
                builder.Append("0-9A-Za-z");
                return 9;
            }

            if (name.SequenceEqual("xdigit".AsSpan()))
            {
                builder.Append("0-9A-Fa-f");
                return 9;
            }

            if (name.SequenceEqual("space".AsSpan()))
            {
                // \t \n \v \f \r + space.
                builder.Append("\t-\r ");
                return 4;
            }

            if (name.SequenceEqual("blank".AsSpan()))
            {
                // tab + space.
                builder.Append("\t ");
                return 2;
            }

            if (name.SequenceEqual("cntrl".AsSpan()))
            {
                // 0x00-0x1F and 0x7F. Body is 4 chars: '\0', '-', '\u001F', '\u007F'.
                builder.Append("\0-\u001F\u007F");
                return 4;
            }

            if (name.SequenceEqual("print".AsSpan()))
            {
                // 0x20-0x7E.
                builder.Append(" -~");
                return 3;
            }

            if (name.SequenceEqual("graph".AsSpan()))
            {
                // 0x21-0x7E (printable, no space).
                builder.Append("!-~");
                return 3;
            }

            if (name.SequenceEqual("punct".AsSpan()))
            {
                // ASCII punctuation: !-/, :-@, [-`, {-~.
                builder.Append("!-/:-@[-`{-~");
                return 12;
            }

            return 0;
        }

        [SkipLocalsInit]
        private static string UnescapeToString(ReadOnlySpan<char> source, char escape)
        {
            if (escape == '\0' || source.IndexOf(escape) < 0)
            {
                return source.ToString();
            }

            ValueStringBuilder builder = new(stackalloc char[256]);
            for (int i = 0; i < source.Length; i++)
            {
                char current = source[i];
                if (current == escape && i + 1 < source.Length)
                {
                    i++;
                    current = source[i];
                }

                builder.Append(current);
            }

            return builder.ToStringAndDispose();
        }
    }
}
