// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Io.Globbing;

namespace Touki.Io;

/// <summary>
///  Loads <c>.gitignore</c> content and produces an
///  <see cref="IEnumerationMatcher"/> (an <see cref="OrderedMatchSet"/> of
///  <see cref="GlobDialect.Git"/> matchers) honoring gitignore evaluation order:
///  the last rule that matches a given path wins, so a later <c>!</c> re-include
///  can rescue a file or directory that an earlier rule excluded.
/// </summary>
/// <remarks>
///  <para>
///   The parser follows the
///   <see href="https://git-scm.com/docs/gitignore">gitignore(5) format</see>:
///  </para>
///  <list type="bullet">
///   <item><description>Blank lines are skipped (used as visual separators).</description></item>
///   <item><description>Lines starting with <c>#</c> are comments. Escape a leading
///    <c>#</c> with <c>\#</c> if the literal <c>#</c> is needed at the start.</description></item>
///   <item><description>Trailing whitespace is stripped unless escaped with
///    <c>\&#160;</c> - this implementation strips trailing whitespace
///    unconditionally (the rare escaped-trailing-space case can be added later).</description></item>
///   <item><description>A leading <c>\</c> escapes <c>!</c> or <c>#</c> at the start
///    of a line (e.g., <c>\!important.txt</c> matches a file literally named
///    <c>!important.txt</c>).</description></item>
///   <item><description>A leading <c>!</c> negates the pattern (re-include).</description></item>
///   <item><description>Other gitignore markers (<c>/</c> prefix for root-anchor,
///    <c>/</c> suffix for directory-only) are consumed by
///    <see cref="GlobSpecification.Compile(StringSegment, GlobDialect, GlobOptions, GlobPathSeparator, int)"/> via the <see cref="GlobDialect.Git"/>
///    dialect.</description></item>
///  </list>
///  <para>
///   Each non-comment, non-blank line becomes one rule in the returned
///   <see cref="OrderedMatchSet"/>. Non-<c>!</c> lines are added as excludes
///   (the default in gitignore); <c>!</c> lines are added as includes (re-includes).
///   The <c>!</c> marker is stripped from the pattern before compiling so the
///   resulting <see cref="GlobMatch.MatchesFile"/> reports raw pattern membership
///   and the <see cref="OrderedMatchSet"/> drives the verdict.
///  </para>
/// </remarks>
public static class GitIgnore
{
    /// <summary>
    ///  Parses gitignore <paramref name="content"/> and returns an
    ///  <see cref="OrderedMatchSet"/> ready to be plugged into a
    ///  <see cref="MatchEnumerator{TResult}"/>. The caller owns the returned set and
    ///  is responsible for <see cref="IDisposable.Dispose"/> (which fans out to all
    ///  contained matchers).
    /// </summary>
    /// <param name="content">The full text of a <c>.gitignore</c> file (one rule per line).</param>
    /// <param name="rootDirectory">
    ///  The directory the rules are evaluated against. Each compiled
    ///  <see cref="GlobMatch"/> has its <see cref="GlobMatch.RootDirectory"/> set
    ///  to this so the matchers can drive an <see cref="IEnumerationMatcher"/> walk.
    /// </param>
    /// <param name="options">Optional glob options to apply to every compiled rule.</param>
    public static OrderedMatchSet Parse(
        string content,
        string rootDirectory,
        GlobOptions options = GlobOptions.None)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(rootDirectory);

        // Gitignore semantics: by default every file in the working tree is
        // included; ignore rules subtract paths; '!' re-includes restore them.
        // Without `includeByDefault: true` an OrderedMatchSet built from a
        // pattern such as `*.log` would report `trace.txt` (which matches no
        // rule) as not-included, inverting the intended meaning.
        OrderedMatchSet set = new(includeByDefault: true);
        AddRules(set, content.AsSpan(), rootDirectory, options);
        return set;
    }

    /// <summary>
    ///  Appends the rules parsed from <paramref name="content"/> to an existing
    ///  <see cref="OrderedMatchSet"/>. Useful when stacking multiple
    ///  <c>.gitignore</c> files (parent <c>.gitignore</c> first, then the directory's
    ///  own, then nested overrides).
    /// </summary>
    public static void AddRules(
        OrderedMatchSet set,
        string content,
        string rootDirectory,
        GlobOptions options = GlobOptions.None)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(rootDirectory);

        AddRules(set, content.AsSpan(), rootDirectory, options);
    }

    private static void AddRules(
        OrderedMatchSet set,
        ReadOnlySpan<char> content,
        string rootDirectory,
        GlobOptions options)
    {
        while (!content.IsEmpty)
        {
            ReadOnlySpan<char> line;
            int newlineIndex = content.IndexOfAny('\r', '\n');
            if (newlineIndex < 0)
            {
                line = content;
                content = default;
            }
            else
            {
                line = content[..newlineIndex];
                // Consume the newline: handle CRLF, LF, and stray CR.
                content = content[newlineIndex..];
                if (content.Length >= 2 && content[0] == '\r' && content[1] == '\n')
                {
                    content = content[2..];
                }
                else
                {
                    content = content[1..];
                }
            }

            // Strip trailing whitespace (gitignore strips unless escaped; the rare
            // escaped-trailing-space case is not yet supported here).
            while (line.Length > 0 && (line[^1] == ' ' || line[^1] == '\t'))
            {
                line = line[..^1];
            }

            if (line.IsEmpty)
            {
                continue;
            }

            if (line[0] == '#')
            {
                continue;
            }

            // Determine whether this is an include (re-include) line based on the
            // leading `!`. Strip the marker before compiling so the resulting matcher
            // reports raw pattern membership; OrderedMatchSet uses the include flag
            // to decide the verdict.
            bool isInclude = false;
            if (line[0] == '!')
            {
                isInclude = true;
                line = line[1..];
                if (line.IsEmpty)
                {
                    continue;
                }
            }

            // Leading `\` escapes a literal `#` or `!` at start-of-line. Strip the
            // backslash so the matcher sees the literal character.
            if (line.Length >= 2
                && line[0] == '\\'
                && (line[1] == '#' || line[1] == '!'))
            {
                line = line[1..];
            }

            // Compile with the Git dialect. The factory consumes the trailing `/`
            // (DirectoryOnly) and leading `/` (RootAnchored) markers and applies the
            // "match anywhere when no internal `/`" transform.
            GlobMatch matcher = GlobSpecification.Compile(line.ToString(), GlobDialect.Git, options).CreateMatcher(rootDirectory);

            if (isInclude)
            {
                set.AddInclude(matcher);
            }
            else
            {
                set.AddExclude(matcher);
            }
        }
    }
}
