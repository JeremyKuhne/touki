// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Collections;

namespace Touki.Io;

/// <summary>
///  An ordered list of include/exclude matchers where the last rule that matches a
///  given path wins. Models <c>.gitignore</c>-style semantics, where a later
///  <c>!</c> re-include can rescue a file that an earlier rule excluded.
/// </summary>
/// <remarks>
///  <para>
///   Differs from <see cref="MatchSet"/> in evaluation order. <see cref="MatchSet"/>
///   evaluates excludes-then-includes; any matching exclude wins outright.
///   <see cref="OrderedMatchSet"/> walks rules in insertion order and tracks the
///   last matching rule's verdict, so a later include can override an earlier
///   exclude (and vice versa). This is the evaluation model <c>.gitignore</c>
///   specifies.
///  </para>
///  <para>
///   Each rule's <see cref="IEnumerationMatcher.MatchesFile"/> /
///   <see cref="IEnumerationMatcher.MatchesDirectory"/> result is interpreted as
///   &quot;does this rule apply to this entry?&quot; (i.e., the pattern matches the
///   entry's path). The include/exclude verdict is held externally via which
///   method (<see cref="AddInclude"/> or <see cref="AddExclude"/>) the rule was
///   added through. Consumers compiling <c>!</c>-prefixed gitignore lines should
///   construct the matcher with <c>Negated</c> stripped (or otherwise ensure the
///   matcher's <c>MatchesFile</c> reports raw pattern membership), and add it with
///   <see cref="AddInclude"/> instead of <see cref="AddExclude"/>.
///  </para>
/// </remarks>
public sealed class OrderedMatchSet : DisposableBase, IEnumerationMatcher
{
    private readonly SingleOptimizedList<Rule, ArrayPoolList<Rule>> _rules = [];
    private readonly bool _includeByDefault;

    /// <summary>
    ///  Constructs an empty <see cref="OrderedMatchSet"/>. Add rules via
    ///  <see cref="AddInclude"/> / <see cref="AddExclude"/> in source order.
    /// </summary>
    /// <param name="includeByDefault">
    ///  When <see langword="true"/>, entries that match no rule are reported as
    ///  included; rules then act as <i>filters</i> that subtract from (or add back to)
    ///  the default-include set. This is the model <c>.gitignore</c> uses: by default
    ///  every file in the working tree is included; ignore rules remove paths;
    ///  <c>!</c> re-includes restore them. When <see langword="false"/> (the default),
    ///  entries that match no rule are <i>not</i> included, so the set acts as an
    ///  <i>allow list</i> driven by <see cref="AddInclude"/> rules.
    /// </param>
    public OrderedMatchSet(bool includeByDefault = false)
    {
        _includeByDefault = includeByDefault;
    }

    /// <summary>
    ///  Gets a value indicating whether entries that match no rule are reported as
    ///  included (gitignore semantics) or not (allow-list semantics).
    /// </summary>
    public bool IncludeByDefault => _includeByDefault;

    /// <summary>
    ///  Appends an include rule to the set. A file or directory matched by this rule
    ///  is included unless a later rule excludes it.
    /// </summary>
    public void AddInclude(IEnumerationMatcher matcher)
    {
        ArgumentNullException.ThrowIfNull(matcher);
        _rules.Add(new Rule(matcher, isExclude: false));
    }

    /// <summary>
    ///  Appends an exclude rule to the set. A file or directory matched by this rule
    ///  is excluded unless a later rule re-includes it.
    /// </summary>
    public void AddExclude(IEnumerationMatcher matcher)
    {
        ArgumentNullException.ThrowIfNull(matcher);
        _rules.Add(new Rule(matcher, isExclude: true));
    }

    /// <summary>
    ///  Gets the number of rules currently in the set.
    /// </summary>
    public int Count => _rules.Count;

    void IEnumerationMatcher.DirectoryFinished()
    {
        for (int i = 0; i < _rules.Count; i++)
        {
            _rules[i].Matcher.DirectoryFinished();
        }
    }

    bool IEnumerationMatcher.MatchesFile(ReadOnlySpan<char> currentDirectory, ReadOnlySpan<char> fileName)
    {
        // Walk all rules in source order; the last rule whose pattern matches the
        // file (via the underlying matcher's MatchesFile) decides the verdict.
        // If no rule matches, fall back to the configured default (allow-list
        // mode: not included; gitignore mode: included).
        //
        // Note: gitignore-style DirectoryOnly excludes such as `bin/` rely on
        // the walker honoring the MatchesDirectory(matchForExclusion=true)
        // verdict and not recursing into the excluded subtree at all. This
        // matches strict git semantics (gitignore(5): "It is not possible to
        // re-include a file if a parent directory of that file is excluded.").
        // Callers driving the set without a walker should consult
        // MatchesDirectory on each ancestor segment themselves.
        bool included = _includeByDefault;
        for (int i = 0; i < _rules.Count; i++)
        {
            Rule rule = _rules[i];
            if (rule.Matcher.MatchesFile(currentDirectory, fileName))
            {
                included = !rule.IsExclude;
            }
        }

        return included;
    }

    bool IEnumerationMatcher.MatchesDirectory(
        ReadOnlySpan<char> currentDirectory,
        ReadOnlySpan<char> directoryName,
        bool matchForExclusion)
    {
        // Strict gitignore semantics: the last DirectoryOnly exclude that
        // matches the candidate directory wins. The walker uses the verdict to
        // skip excluded subtrees entirely (per gitignore(5): "It is not
        // possible to re-include a file if a parent directory of that file is
        // excluded."), so file-level includes inside an excluded subtree do not
        // need to be considered here.
        //
        // Include rules' DirectoryOnly matches are not visible via the
        // underlying GlobMatch.MatchesDirectory(matchForExclusion=true) call
        // (the matcher's Negated flag flips the result), so this layer can only
        // detect exclude-side claims. A later DirectoryOnly include (`!bin/`)
        // therefore cannot rescue a previously-excluded subtree at this layer
        // &mdash; consistent with the strict-gitignore intent.
        bool excluded = false;
        for (int i = 0; i < _rules.Count; i++)
        {
            Rule rule = _rules[i];
            if (rule.IsExclude
                && rule.Matcher.MatchesDirectory(currentDirectory, directoryName, matchForExclusion: true))
            {
                excluded = true;
            }
        }

        // matchForExclusion=true asks "claim this dir for exclusion?";
        // matchForExclusion=false asks "should we recurse?". Both map to the
        // same underlying verdict.
        return matchForExclusion ? excluded : !excluded;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            for (int i = 0; i < _rules.Count; i++)
            {
                _rules[i].Matcher.Dispose();
            }

            _rules.Dispose();
        }
    }

    private readonly struct Rule
    {
        public Rule(IEnumerationMatcher matcher, bool isExclude)
        {
            Matcher = matcher;
            IsExclude = isExclude;
        }

        public IEnumerationMatcher Matcher { get; }

        public bool IsExclude { get; }
    }
}
