// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  An <see cref="IEnumerationMatcher"/> binding of a <see cref="GlobSpecification"/>
///  to an enumeration root.
/// </summary>
/// <remarks>
///  <para>
///   Owns the per-directory cache that powers the prune-by-literal-prefix
///   optimization; the underlying specification is unchanged and may be shared
///   with other concurrent <see cref="GlobMatch"/> instances.
///  </para>
///  <para>
///   Single-threaded against <see cref="MatchesFile"/>, <see cref="MatchesDirectory"/>,
///   and <see cref="DirectoryFinished"/>. Compose multi-pattern enumerations via
///   <see cref="MatchSet"/> / <see cref="OrderedMatchSet"/>.
///  </para>
/// </remarks>
public sealed partial class GlobMatch : DisposableBase, IEnumerationMatcher
{
    // Stack budget for the translated-prefix scratch buffer used by
    // MatchesFile / MatchesDirectory. 256 chars (512 bytes) covers the vast
    // majority of relative directory paths (MAX_PATH is 260 on legacy Windows,
    // typical real-world relative paths are well under that). BufferScope rents
    // from ArrayPool for the rare longer path so the hot path never allocates
    // on the managed heap and never silently rejects long inputs.
    private const int StackBufferSize = 256;

    // Per-directory cache. Mirrors MatchMSBuild: invalidated by DirectoryFinished,
    // refreshed by the next MatchesFile/MatchesDirectory call. Holds the alignment
    // classification only - no buffers, no rentals, no copies of the directory
    // span. The IEnumerationMatcher hot path stays allocation-free.
    private readonly GlobSpecification _specification;
    private readonly string? _rootDirectory;
    private bool _cacheValid;
    private PrefixAlignment _alignment;
    private int _rootPrefixLength;
    private bool _rootPrefixComputed;

    internal GlobMatch(GlobSpecification specification, string? rootDirectory)
    {
        _specification = specification;
        _rootDirectory = rootDirectory;
    }

    /// <summary>
    ///  The compiled specification driving this matcher.
    /// </summary>
    public GlobSpecification Specification => _specification;

    /// <summary>
    ///  The enumeration root the matcher is bound to, or <see langword="null"/> when
    ///  matching is path-unaware.
    /// </summary>
    public string? RootDirectory => _rootDirectory;

    /// <inheritdoc/>
    public void DirectoryFinished() => _cacheValid = false;

    /// <inheritdoc/>
    /// <remarks>
    ///  <para>
    ///   For path-unaware specifications, or when <see cref="RootDirectory"/> is
    ///   <see langword="null"/>, the matcher has no path context to prune with
    ///   and returns <c>!matchForExclusion</c>: inclusion calls always recurse,
    ///   exclusion calls never claim the subtree.
    ///  </para>
    ///  <para>
    ///   Exclusion calls (<paramref name="matchForExclusion"/> = <see langword="true"/>)
    ///   return <see langword="false"/> for most patterns; the matcher cannot
    ///   definitively claim a whole subtree for exclusion at the directory level
    ///   - that decision is deferred to per-file checks.
    ///  </para>
    ///  <para>
    ///   When <see cref="GlobSpecification.DirectoryOnly"/> is set (gitignore
    ///   trailing <c>/</c>) the exclusion path runs the pattern against the
    ///   candidate directory's relative path (parent + name, without trailing
    ///   separator) and, on a match, returns <see langword="true"/> so the
    ///   enumerator skips the entire subtree. The candidate path is stitched
    ///   onto a small stack buffer; if the relative directory exceeds the stack
    ///   budget the buffer is satisfied from <see cref="ArrayPool{T}"/> so the
    ///   hot path never allocates on the managed heap and never silently
    ///   rejects an oversized input.
    ///  </para>
    ///  <para>
    ///   Inclusion calls classify the candidate (parent + name) against
    ///   <see cref="GlobSpecification.LiteralPathPrefix"/> via a fresh
    ///   <see cref="PrefixAlignment"/> computation that walks the relative
    ///   directory span on the fly without copying or renting buffer space.
    ///   The per-directory cache populated by <see cref="MatchesFile"/> is
    ///   intentionally <em>not</em> consulted here because the two methods
    ///   classify different paths: <see cref="MatchesFile"/> classifies the
    ///   parent currently being walked, while <see cref="MatchesDirectory"/>
    ///   classifies a prospective parent + child join. The call returns
    ///   <see langword="true"/> when the candidate is on or beyond the literal
    ///   prefix and <see langword="false"/> when it has diverged; specifications
    ///   with an empty literal prefix always return <see langword="true"/>.
    ///  </para>
    ///  <para>
    ///   Before the literal-prefix check, inclusion calls for a pattern that
    ///   carries an extglob negation (<see cref="GlobSpecification.HasNegation"/>)
    ///   ask <see cref="GlobSpecification.MatchDirectory"/> whether the candidate
    ///   subtree can be pruned. An anchored negation
    ///   (<c>!(bin|obj)/...</c>, <c>src/!(bin)/**/*.cs</c>) that no backtracking
    ///   path can satisfy yields <see cref="MatchOutcome.Negative"/>, so the whole
    ///   subtree is skipped instead of being descended and rejected file by file.
    ///   The check is conservative: a directory with any matching descendant is
    ///   never pruned. The gitignore-style <see cref="GlobSpecification.Negated"/>
    ///   wrapper is excluded because it would invert the conclusion.
    ///  </para>
    /// </remarks>
    [SkipLocalsInit]
    public bool MatchesDirectory(
        ReadOnlySpan<char> currentDirectory,
        ReadOnlySpan<char> directoryName,
        bool matchForExclusion)
    {
        if (!_specification.IsPathAware || _rootDirectory is null)
        {
            return !matchForExclusion;
        }

        EnsureRootPrefixComputed();
        ReadOnlySpan<char> relativeDirectory = GetRelativeDirectory(currentDirectory);

        if (matchForExclusion)
        {
            // The matcher only claims a whole subtree for DirectoryOnly patterns
            // (gitignore trailing `/`). For other patterns we defer to per-file
            // decisions, so return false here and let MatchSet / OrderedMatchSet
            // keep recursing.
            if (!_specification.DirectoryOnly)
            {
                return false;
            }

            // DirectoryOnly: run the pattern against the candidate directory's
            // relative path (parent + name, without trailing separator). The
            // translated prefix is stitched onto the stack (with an ArrayPool
            // fallback for unusually long relative paths) so the matcher sees a
            // clean (prefix, name) pair.
            bool matched;
            if (relativeDirectory.IsEmpty)
            {
                matched = _specification.MatchCore(default, directoryName);
            }
            else
            {
                int prefixLength = relativeDirectory.Length + 1;
                using BufferScope<char> buffer = new(stackalloc char[StackBufferSize], prefixLength);
                Span<char> prefix = buffer[..prefixLength];
                BuildTranslatedPrefix(relativeDirectory, prefix, _specification.Separator);
                matched = _specification.MatchCore(prefix, directoryName);
            }

            return _specification.Negated ? !matched : matched;
        }

        // Directory-mode negation pruning. A pattern carrying an extglob negation
        // (`!(bin|obj)/...`, `src/!(bin)/**/*.cs`, etc.) can provably exclude a whole
        // subtree: if no backtracking path can consume the candidate directory path,
        // an anchored negation has rejected one of its segments and no descendant can
        // match. The engine answers this in directory mode via MatchDirectory; a
        // Negative outcome prunes the subtree. Run before the literal-prefix check so
        // negations nested under a literal prefix are reached. Gated by HasNegation
        // (a single field load, false for non-negation patterns) and excluded for the
        // gitignore-style `Negated` wrapper, which would invert the conclusion.
        if (_specification.HasNegation && !_specification.Negated)
        {
            MatchOutcome outcome;
            if (relativeDirectory.IsEmpty)
            {
                outcome = _specification.MatchDirectory(default, directoryName);
            }
            else
            {
                int prefixLength = relativeDirectory.Length + 1;
                using BufferScope<char> buffer = new(stackalloc char[StackBufferSize], prefixLength);
                Span<char> prefix = buffer[..prefixLength];
                BuildTranslatedPrefix(relativeDirectory, prefix, _specification.Separator);
                outcome = _specification.MatchDirectory(prefix, directoryName);
            }

            if (outcome == MatchOutcome.Negative)
            {
                return false;
            }
        }

        string literalPrefix = _specification.Strategy.LiteralPathPrefix;
        if (literalPrefix.Length == 0)
        {
            return true;
        }

        // Classify the candidate directory (parent + name) against the literal
        // prefix without materializing the join. The helper walks the two spans in
        // sequence and applies on-the-fly native-to-matcher separator translation.
        return ClassifyAlignment(
            relativeDirectory,
            directoryName,
            literalPrefix,
            _specification.Separator,
            _specification.IgnoreCaseKind) is not PrefixAlignment.Diverged;
    }

    /// <inheritdoc/>
    /// <remarks>
    ///  <para>
    ///   For path-unaware specifications (or when <see cref="RootDirectory"/> is
    ///   <see langword="null"/>) the file name is matched as-is via
    ///   <see cref="GlobSpecification.IsMatch(ReadOnlySpan{char})"/>. For path-aware
    ///   specifications with a root, the matcher stitches the translated relative-
    ///   directory prefix (with trailing <see cref="GlobSpecification.Separator"/>)
    ///   into a small stack buffer; if the relative directory exceeds the stack
    ///   budget the buffer is satisfied from <see cref="ArrayPool{T}"/> so the
    ///   per-file hot path never allocates on the managed heap and never silently
    ///   rejects an oversized input. Per-directory state (the alignment
    ///   classification) is cached in a single byte field invalidated by
    ///   <see cref="DirectoryFinished"/>, so the classification cost amortizes
    ///   across every file in the directory.
    ///  </para>
    /// </remarks>
    [SkipLocalsInit]
    public bool MatchesFile(ReadOnlySpan<char> currentDirectory, ReadOnlySpan<char> fileName)
    {
        // DirectoryOnly patterns (gitignore trailing '/') never match files.
        if (_specification.DirectoryOnly)
        {
            return _specification.Negated;
        }

        if (!_specification.IsPathAware || _rootDirectory is null)
        {
            // Path-unaware semantics: only the file name is considered. IsMatch
            // handles DisallowEmptyInput and the Negated wrapper.
            return _specification.IsMatch(fileName);
        }

        EnsureRootPrefixComputed();
        ReadOnlySpan<char> relativeDirectory = GetRelativeDirectory(currentDirectory);

        if (!_cacheValid)
        {
            _alignment = ClassifyAlignment(
                relativeDirectory,
                default,
                _specification.Strategy.LiteralPathPrefix,
                _specification.Separator,
                _specification.IgnoreCaseKind);
            _cacheValid = true;
        }

        if (_alignment != PrefixAlignment.Beyond)
        {
            return false;
        }

        bool matched;
        if (relativeDirectory.IsEmpty)
        {
            matched = _specification.MatchCore(default, fileName);
        }
        else
        {
            // Stitch the translated relative-directory prefix (with trailing
            // separator) into a small stack buffer; BufferScope rents from
            // ArrayPool for the rare relative-directory path that overflows
            // the stack budget, so the IEnumerationMatcher hot path never
            // allocates on the managed heap and never silently fails for
            // long paths.
            int prefixLength = relativeDirectory.Length + 1;
            using BufferScope<char> buffer = new(stackalloc char[StackBufferSize], prefixLength);
            Span<char> prefix = buffer[..prefixLength];
            BuildTranslatedPrefix(relativeDirectory, prefix, _specification.Separator);
            matched = _specification.MatchCore(prefix, fileName);
        }

        return _specification.Negated ? !matched : matched;
    }

    private void EnsureRootPrefixComputed()
    {
        if (_rootPrefixComputed)
        {
            return;
        }

        string root = _rootDirectory!;
        _rootPrefixLength = root.Length + (Path.EndsInDirectorySeparator(root) ? 0 : 1);
        _rootPrefixComputed = true;
    }

    private ReadOnlySpan<char> GetRelativeDirectory(ReadOnlySpan<char> currentDirectory) =>
        currentDirectory.Length > _rootPrefixLength
            ? currentDirectory[_rootPrefixLength..]
            : default;

    /// <summary>
    ///  Copies <paramref name="source"/> into <paramref name="destination"/> while
    ///  normalizing any native path separator to <paramref name="separator"/>, then
    ///  appends one final <paramref name="separator"/> so the resulting span ends on
    ///  the directory/file-name boundary.
    /// </summary>
    private static void BuildTranslatedPrefix(
        ReadOnlySpan<char> source,
        Span<char> destination,
        char separator)
    {
        char primary = Path.DirectorySeparatorChar;
        char alt = Path.AltDirectorySeparatorChar;
        ref char sourceRef = ref MemoryMarshal.GetReference(source);
        ref char destinationRef = ref MemoryMarshal.GetReference(destination);

        for (int i = 0; i < source.Length; i++)
        {
            char c = Unsafe.Add(ref sourceRef, i);
            Unsafe.Add(ref destinationRef, i) = (c == primary || c == alt) ? separator : c;
        }

        Unsafe.Add(ref destinationRef, source.Length) = separator;
    }

    /// <summary>
    ///  Classifies the candidate directory (<paramref name="parent"/> + separator +
    ///  <paramref name="child"/>) against <paramref name="literalPrefix"/> without
    ///  materializing the join. The literal prefix ends with the matcher's separator
    ///  by convention. Native path separators in the candidate are translated to
    ///  <paramref name="separator"/> on the fly.
    /// </summary>
    private static PrefixAlignment ClassifyAlignment(
        ReadOnlySpan<char> parent,
        ReadOnlySpan<char> child,
        string literalPrefix,
        char separator,
        IgnoreCaseKind caseKind)
    {
        if (literalPrefix.Length == 0)
        {
            return PrefixAlignment.Beyond;
        }

        int parentLength = parent.Length;
        int childLength = child.Length;
        int joinSeparator = (parentLength > 0 && childLength > 0) ? 1 : 0;
        int innerLength = parentLength + joinSeparator + childLength;
        int virtualLength = innerLength == 0 ? 0 : innerLength + 1;

        if (virtualLength == 0)
        {
            return PrefixAlignment.OnPrefix;
        }

        int common = Math.Min(virtualLength, literalPrefix.Length);
        for (int i = 0; i < common; i++)
        {
            char candidateChar;
            if (i < parentLength)
            {
                candidateChar = parent[i];
                if (candidateChar == Path.DirectorySeparatorChar || candidateChar == Path.AltDirectorySeparatorChar)
                {
                    candidateChar = separator;
                }
            }
            else if (joinSeparator == 1 && i == parentLength)
            {
                candidateChar = separator;
            }
            else if (i - parentLength - joinSeparator < childLength)
            {
                candidateChar = child[i - parentLength - joinSeparator];
                if (candidateChar == Path.DirectorySeparatorChar || candidateChar == Path.AltDirectorySeparatorChar)
                {
                    candidateChar = separator;
                }
            }
            else
            {
                candidateChar = separator;
            }

            char literalChar = literalPrefix[i];
            bool match = caseKind switch
            {
                IgnoreCaseKind.Ascii => GlobMatcherHelpers.AsciiFoldEquals(candidateChar, literalChar),
                IgnoreCaseKind.Unicode => GlobMatcherHelpers.UnicodeFoldEquals(candidateChar, literalChar),
                _ => candidateChar == literalChar,
            };
            if (!match)
            {
                return PrefixAlignment.Diverged;
            }
        }

        return virtualLength >= literalPrefix.Length
            ? PrefixAlignment.Beyond
            : PrefixAlignment.OnPrefix;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        // The specification owns the strategy lifetime; nothing to release here.
    }
}
