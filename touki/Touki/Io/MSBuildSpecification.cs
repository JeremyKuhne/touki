// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Collections;
using Touki.Text;

namespace Touki.Io;

/// <summary>
///  An MSBuild specification used in an item include or an exclude.
/// </summary>
public class MSBuildSpecification : IEquatable<string>, IEquatable<StringSegment>, IEquatable<MSBuildSpecification>
{
    private static readonly string s_redundantWildCard = $"**{Path.DirectorySeparatorChar}**";

    /// <summary>
    ///  The specification as it was originally provided.
    /// </summary>
    public StringSegment Original { get; }

    /// <summary>
    ///  The normalized specification.
    /// </summary>
    public StringSegment Normalized { get; }

    /// <summary>
    ///  The fixed directory part of the specification, if any, which is the part before any wildcards.
    /// </summary>
    public StringSegment FixedPath { get; }

    /// <summary>
    ///  The wildcard directory part of the specification, if any, which is the part where wildcards are used outside
    ///  of the filename itself.
    /// </summary>
    public StringSegment WildPath { get; }

    /// <summary>
    ///  The file name part of the specification. This is the part after the last directory separator.
    /// </summary>
    public StringSegment FileName { get; }

    public bool FileNameIsOnlyWildCard => FileName == "*" || FileName == "**";

    /// <summary>
    ///  <see langword="true"/> if the specification contains any wildcards.
    ///  May be in <see cref="WildPath"/> or <see cref="FileName"/>, or both.
    /// </summary>
    public bool HasAnyWildCards { get; }

    /// <summary>
    ///  <see langword="true"/> if the <see cref="WildPath"/> is "**", meaning that it will match all subdirectories
    ///  once the primary <see cref="FixedPath"/> (if any) is matched.
    /// </summary>
    public bool IsSimpleRecursiveMatch { get; }

    /// <summary>
    /// If this spec ended with <c>\**</c> and nothing else, e.g. <c>foo/bar/**</c> or just <c>**</c>.
    /// </summary>
    public bool EndsInAnyDirectory => IsSimpleRecursiveMatch && FileNameIsOnlyWildCard;

    /// <summary>
    ///  <see langword="true"/> if the <see cref="Normalized"/> path is a nested relative path. This means that the path
    ///  is not <see cref="IsFullyQualified"/> but is guaranteed to be below the current directory when combined.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   The path can still be relative, but start with '..' or be drive relative (e.g. 'C:file.txt') and not
    ///   satisfy this requirement. You can directly compare two specs that are <see cref="IsNestedRelative"/>.
    ///   See remarks on <see cref="IsFullyQualified"/> for more information on other comparisons.
    ///  </para>
    /// </remarks>
    public bool IsNestedRelative { get; }

    /// <summary>
    ///  <see langword="true"/> if the <see cref="Normalized"/> path is rooted and will not change with the current directory.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   In effect this means that the path is completely normalized and can be compared directly against any normalized
    ///   full path (something that has been passed to <see cref="Path.GetFullPath(string)"/> or other <see cref="IsFullyQualified"/>
    ///   specs. When this isn't <see langword="true"/> and <see cref="Normalized"/> is not <see cref="IsNestedRelative"/>
    ///   it is unsafe to compare without first resolving against the current directory with <see cref="Path.GetFullPath(string)"/>,
    ///   or <see cref="Path.GetFullPath(string, string)"/>.
    ///  </para>
    /// </remarks>
    public bool IsFullyQualified { get; }

    /// <summary>
    ///  <see langword="true"/> if the specification doesn't match any directory portions and only cares about the fileName.
    /// </summary>
    public bool OnlyCaresAboutFileName => FixedPath.IsEmpty && WildPath.IsEmpty && !FileName.IsEmpty;

    /// <inheritdoc cref="MSBuildSpecification(StringSegment)"/>
    public MSBuildSpecification(string original) : this(new StringSegment(original)) { }

    /// <summary>
    ///  Constructs a new <see cref="MSBuildSpecification"/> from the given original specification.
    /// </summary>
    public MSBuildSpecification(StringSegment original) : this(original, Normalize(original))
    {
    }

    internal MSBuildSpecification(StringSegment original, StringSegment normalized)
    {
        if (normalized.IsEmpty)
        {
            throw new ArgumentException("Normalized specification cannot be empty.", nameof(normalized));
        }

        Original = original;
        Normalized = normalized;

        Debug.Assert(Normalized.Equals(Normalize(Original)));

        IsFullyQualified = Path.IsPathFullyQualified(Normalized.AsSpan());
        IsNestedRelative = !IsFullyQualified
            && !Path.IsPathRooted(Normalized.AsSpan())
            && !(Normalized.StartsWith("..")
                && (Normalized.Length == 2 || (Normalized.Length > 2 && Normalized[2] == Path.DirectorySeparatorChar)));

        int lastSeparator = Normalized.LastIndexOf(Path.DirectorySeparatorChar);
        int firstWildCard = Normalized.IndexOfAny('*', '?');

        HasAnyWildCards = firstWildCard >= 0;

        // Split into segments without separators between them.

        if (lastSeparator < 1)
        {
            // No separator, only special case is "**"
            FixedPath = default;

            if (Normalized == "**")
            {
                goto SimpleRecursiveMatch;
            }
            else
            {
                WildPath = default;
                FileName = Normalized;
            }

            return;
        }

        if (!HasAnyWildCards)
        {
            // No wildcards, split the fixed path and filename and exit.
            WildPath = default;
            FixedPath = Normalized[..lastSeparator];
            FileName = Normalized[(lastSeparator + 1)..];
            return;
        }

        FileName = Normalized[(lastSeparator + 1)..];
        bool trailingWildDirectory = FileName == "**";

        if (firstWildCard > lastSeparator)
        {
            // No wildcards before the last separator, the filename contains the wildcard (e.g. "foo/bar/*.txt").
            // Everything else is fixed, unless we have a trailing wild directory (e.g. "foo/bar/**").
            FixedPath = Normalized[..lastSeparator];

            if (trailingWildDirectory)
            {
                goto SimpleRecursiveMatch;
            }
            else
            {
                WildPath = default;
            }

            return;
        }

        // Handle cases where wildcards appear in the path

        StringSegment preWildCard = Normalized[..firstWildCard];
        int lastSeparatorBeforeWildCard = preWildCard.LastIndexOf(Path.DirectorySeparatorChar);

        if (lastSeparatorBeforeWildCard < 0)
        {
            // No separator before the first wildcard, so nothing is fixed (e.g. "*/foo/*.txt" or "**/foo/*.txt").
            FixedPath = default;

            if (trailingWildDirectory)
            {
                // Trailing wild directory (e.g. "*/foo/**" or "**/foo/**").
                WildPath = Normalized;
                FileName = "*";
                IsSimpleRecursiveMatch = WildPath == "**";
                return;
            }
            else
            {
                // Normal all wild scenario (e.g. "*/foo/*.txt" or "**/foo/*.txt")
                WildPath = Normalized[..^(FileName.Length + 1)];
            }
        }
        else
        {
            // We have a fixed part and a wildcard part
            FixedPath = Normalized[..lastSeparatorBeforeWildCard];

            if (trailingWildDirectory)
            {
                // Trailing wild directory (e.g. "foo/bar*/**").
                WildPath = Normalized[(lastSeparatorBeforeWildCard + 1)..];
                FileName = "*";
                IsSimpleRecursiveMatch = WildPath == "**";
                return;
            }
            else
            {
                // Normal case with wildcards in the path (e.g. "foo/bar*/baz/*.txt").
                WildPath = Normalized.Slice(FixedPath.Length + 1, Normalized.Length - FileName.Length - FixedPath.Length - 2);
            }
        }

        IsSimpleRecursiveMatch = WildPath == "**";

        if (!FileName.IsEmpty)
        {
            return;
        }

        // Handle empty filename edge cases

        if (IsSimpleRecursiveMatch
            || (WildPath.Length > 2 && WildPath[^3] == Path.DirectorySeparatorChar && WildPath[^2] == '*' && WildPath[^1] == '*'))
        {
            // Wildcard is "**" or ends with "**", so the filename is "*".
            // (This would potentially happen with "foo/bar/**/").
            FileName = "*";
        }

        return;

    SimpleRecursiveMatch:
        // If we reach here, it means that the specification is a simple recursive match (e.g. "**").
        // This is a special case where the entire path is just "**", so we set the wild path to "**" and the filename to "*".
        WildPath = "**";
        FileName = "*";
        IsSimpleRecursiveMatch = true;
    }

    /// <summary>
    ///  Unescapes the given <paramref name="specification"/> if needed. `%` is used to escape special characters
    ///  in MSBuild strings, such as `*`, `?`, and `%`.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   This method will allocate a new string for the segment if needed.
    ///  </para>
    /// </remarks>
    public static StringSegment Unescape(StringSegment specification)
    {
        // Don't bother if the segment doesn't contain an escape character.
        if (!specification.Contains('%'))
        {
            return specification;
        }

        // Path segments should never be over 256 characters, so we shouldn't need to stack allocate more than this.
        using ValueStringBuilder builder = new(stackalloc char[256]);

        // Iterate through the segment and unescape characters as needed. Escape sequences are of the form `%XX` where
        // `XX` is the hexadecimal representation of the character. Invalid escape sequences are left as is.
        for (int i = 0; i < specification.Length; i++)
        {
            char c = specification[i];
            if (c != '%'
                || i + 2 >= specification.Length
                || !specification[i + 1].TryDecodeHexDigit(out int firstDigit)
                || !specification[i + 2].TryDecodeHexDigit(out int secondDigit))
            {
                builder.Append(c);
            }
            else
            {
                // Valid escape sequence, decode it.
                builder.Append((char)((firstDigit << 4) + secondDigit));
                i += 2;
            }
        }

        // It is possible that all of the escapes are "invalid", so check if we've actually built anything different.
        return builder.Length < specification.Length ? builder.ToString() : specification;
    }


    /// <summary>
    ///  Simplify a path by converting backslashes to forward slashes on Unix systems, collapsing
    ///  consecutive directory separators into a single separator, trimming whitespace, and removing any
    ///  redundant path segments (".", "..", and duplicate "**").
    /// </summary>
    /// <param name="specification">The path specification to normalize.</param>
    /// <remarks>
    ///  <para>
    ///   Multiple specifications (separated by ';') must be split first for this to work correctly.
    ///  </para>
    /// </remarks>
    public static StringSegment Normalize(StringSegment specification)
    {
        // TODO: What is the behavior of a trailing separator in MSBuild? Is "Foo\" the same as "Foo" or is it
        // expected to be the same as "Foo\*"?

        specification = specification.Trim();

        char separatorToReplace = Path.DirectorySeparatorChar == '\\' ? '/' : '\\';

        ValueStringBuilder replaceBuilder = new(stackalloc char[Paths.MaxShortPath]);

        if (specification.Contains(separatorToReplace))
        {
            replaceBuilder.Append(specification);
            replaceBuilder.Replace(separatorToReplace, Path.DirectorySeparatorChar);
            ReadOnlySpan<char> currentState = replaceBuilder;
            replaceBuilder.Length = 0;
            Paths.RemoveRelativeSegments(currentState, ref replaceBuilder);
            RemoveDuplicateMatchAnyDirectory(ref replaceBuilder);
            return replaceBuilder.ToStringAndDispose();
        }
        else
        {
            if (specification.IndexOf(Path.DirectorySeparatorChar) < 0)
            {
                // No path segments to normalize, return the original segment.
                return specification;
            }

            bool modified = Paths.RemoveRelativeSegments(specification, ref replaceBuilder);
            modified |= RemoveDuplicateMatchAnyDirectory(ref replaceBuilder);

            if (modified)
            {
                return replaceBuilder.ToStringAndDispose();
            }
            else
            {
                // No changes made, return the original segment.
                replaceBuilder.Dispose();
                return specification;
            }
        }

        static bool RemoveDuplicateMatchAnyDirectory(ref ValueStringBuilder builder)
        {
            ReadOnlySpan<char> redundantWildCard = s_redundantWildCard.AsSpan();
            ReadOnlySpan<char> original = builder.AsSpan();
            int index = original.IndexOf(redundantWildCard, StringComparison.Ordinal);

            if (index == -1)
            {
                return false;
            }

            ValueStringBuilder replaceBuilder = new(stackalloc char[Paths.MaxShortPath]);
            if (original[0] == Path.DirectorySeparatorChar)
            {
                // If the first character is a separator, we need to keep it.
                replaceBuilder.Append(Path.DirectorySeparatorChar);
            }

            bool foundMatchAny = false;

            PathSegmentEnumerator enumerator = new(original);
            while (enumerator.MoveNext())
            {
                ReadOnlySpan<char> current = enumerator.Current;
                if (current.SequenceEqual("**".AsSpan()))
                {
                    if (foundMatchAny)
                    {
                        // Skip this match any directory, we already have one.
                        continue;
                    }

                    foundMatchAny = true;
                }
                else
                {
                    foundMatchAny = false;
                }

                replaceBuilder.Append(current);
                replaceBuilder.Append(Path.DirectorySeparatorChar);
            }

            if (!Path.EndsInDirectorySeparator(original))
            {
                replaceBuilder.Length--;
            }

            builder.Clear();
            builder.Append(replaceBuilder.AsSpan());
            return true;
        }
    }

    /// <summary>
    ///  Splits semicolon-separated MSBuild specifications and buckets them into wildcard and literal versions.
    /// </summary>
    /// <param name="specs">
    ///  The possibly semicolon-separated MSBuild specification to split. In MSBuild this is processed unescaped, but
    ///  the logic would still work here if you wanted escaped `*` and `?` characters to be treated as literals in
    ///  matches ("/foo/bar?.text" is a valid file name on Unix).
    /// </param>
    /// <returns>
    ///  The list of split specifications. Each specification is normalized, meaning that backslashes are converted to
    ///  forward slashes on Unix and vice versa on Windows. Consecutive separators will be collapsed to a single
    ///  separator. Duplicate specifications will be removed, including some that are effectively duplicate, such as
    ///  "bin/**" and "bin/Debug/**" (the latter is a subdirectory of the former, so it will be replaced).
    /// </returns>
    public static ListBase<MSBuildSpecification> Split(
        StringSegment specs,
        bool ignoreCase)
    {
        SingleOptimizedList<MSBuildSpecification> splitSpecs = [];

        // MSBuild normally validates specifications after it splits each one into fixed, wildcard, and file parts.
        // None of the validation is strictly necessary, but would be done here if we wanted to roughly match.
        // Things that are considered "invalid: should be put in the literalSpecs even if they contain wildcards.
        //
        // What MSBuild considers invalid:
        //
        //  - InvalidPathCharacters - not needed anymore, the only illegal character is null.
        //  - A colon after the second character - not a real risk unless you create a `Uri` and breaks Unix paths.
        //  - `...` - this isn't needed either, there is no risk with this.
        //  - `**` not between separators - a lot of pain for not much gain, so we don't do this.

        StringSegment right = specs;

        while (right.TrySplit(';', out StringSegment left, out right))
        {
            if (left.IsEmpty)
            {
                // Skip empty segments.
                continue;
            }

            // Normalize the spec. This will collapse any consecutive separators into a single separator and reduce
            // unnecessary segments like "." and "..". MSBuild doesn't fully do this at this stage. For performance we
            // want to dedupe the segments, which can done more efficiently if we normalize first. This will also
            // improve performance further downstream as we can be more efficient with additional comparisons.

            StringSegment normalized = Normalize(left);
            bool found = false;

            for (int i = 0; i < splitSpecs.Count; i++)
            {
                if (splitSpecs[i].Normalized.Equals(normalized, ignoreCase))
                {
                    // Already present.
                    found = true;
                    break;
                }
            }

            if (found)
            {
                // Skip duplicates.
                continue;
            }

            MSBuildSpecification newSpec = new(left, normalized);
            if (!newSpec.IsSimpleRecursiveMatch)
            {
                splitSpecs.Add(newSpec);
                continue;
            }

            // Looking for duplicates akin to "bin\Debug\/**" and "bin\/**"
            for (int i = 0; i < splitSpecs.Count; i++)
            {
                MSBuildSpecification current = splitSpecs[i];
                if (!current.IsSimpleRecursiveMatch
                    || !current.FileName.Equals(newSpec.FileName, ignoreCase))
                {
                    continue;
                }

                if (current.FixedPath.Length > newSpec.FixedPath.Length)
                {
                    if (Paths.IsSameOrSubdirectory(newSpec.FixedPath, current.FixedPath, ignoreCase))
                    {
                        // Current is a subdirectory of the new, replace it.
                        splitSpecs[i] = newSpec;
                        found = true;
                        break;
                    }
                }
                else if (Paths.IsSameOrSubdirectory(current.FixedPath, newSpec.FixedPath, ignoreCase))
                {
                    // New is a subdirectory of the current, we can skip this.
                    // We already have a simple recursive match for this fixed path.
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                splitSpecs.Add(newSpec);
            }
        }

        return splitSpecs;
    }

    /// <summary>
    ///  Implicitly converts a <see langword="string"/> to a <see cref="MSBuildSpecification"/>.
    /// </summary>
    /// <param name="specification">The string specification to convert.</param>
    public static implicit operator MSBuildSpecification(string specification) => new MSBuildSpecification(specification);

    /// <summary>
    ///  Explicitly converts a <see cref="MSBuildSpecification"/> to a <see langword="string"/>.
    /// </summary>
    /// <param name="specification">The string specification to convert.</param>
    public static explicit operator string(MSBuildSpecification specification) => (string)specification.Normalized;

    /// <summary>
    ///  Implicitly converts a <see cref="StringSegment"/> to a <see cref="MSBuildSpecification"/>.
    /// </summary>
    /// <param name="specification">The string specification to convert.</param>
    public static implicit operator MSBuildSpecification(StringSegment specification) => new MSBuildSpecification(specification);

    /// <summary>
    ///  Implicitly converts a <see cref="MSBuildSpecification"/> to a <see cref="StringSegment"/>.
    /// </summary>
    /// <param name="specification">The string specification to convert.</param>
    public static implicit operator StringSegment(MSBuildSpecification specification) => specification.Normalized;

    /// <inheritdoc/>
    public override string ToString() => Normalized.ToString();

    /// <inheritdoc cref="Equals(object?)"/>
    public bool Equals(string? other) => other is not null && Original.Equals(other, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc cref="Equals(object?)"/>
    public bool Equals(MSBuildSpecification? other) =>
        other is not null && Original.Equals(other.Original, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc cref="Equals(object?)"/>
    public bool Equals(StringSegment other) =>
        Original.Equals(other, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is MSBuildSpecification other && Equals(other)
        || obj is string str && Equals(str);

    /// <inheritdoc/>
    public override int GetHashCode() => Original.GetHashCode();
}
