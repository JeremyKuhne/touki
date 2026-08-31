// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

public sealed partial class GlobSpecification
{
    /// <summary>
    ///  Compile-time pattern normalization helpers shared by
    ///  <see cref="Factory"/>. Each helper takes a <see cref="StringSegment"/>
    ///  reference and rewrites it only when the dialect's rule set actually
    ///  requires a change; the no-op path is allocation-free and the
    ///  <see cref="StringSegment"/> still points into the caller's original
    ///  source string.
    /// </summary>
    private static partial class Normalization
    {
        /// <summary>
        ///  Applies the FileSystemGlobbing-specific compile-time rewrites that
        ///  <c>Microsoft.Extensions.FileSystemGlobbing.Matcher</c> applies
        ///  internally. See the factory call site for the catalogue of rules.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   Two-pass design: a single forward scan via
        ///   <see cref="AnalyzeFileSystemGlobbing"/> decides whether any rewrite
        ///   would fire. When none does, <paramref name="pattern"/> is returned
        ///   untouched and no string is allocated. When a rewrite is needed
        ///   the helper walks the pattern once into a
        ///   <see cref="ValueStringBuilder"/> seeded on the stack and produces
        ///   exactly one string via <see cref="ValueStringBuilder.ToString"/>.
        ///   End-to-end costs for no-op and rewrite patterns are tracked by
        ///   <c>touki.perf/FileSystemGlobbingCompilePerf.cs</c> on modern .NET
        ///   RyuJIT and .NET Framework 4.8.1 RyuJIT.
        ///  </para>
        /// </remarks>
        [SkipLocalsInit]
        public static bool TryFileSystemGlobbing(
            ref StringSegment pattern,
            char separator,
            out bool rewritten,
            out int starCount,
            out int firstStarPosition,
            out bool hasAsteriskRun,
            out GlobCompileError error)
        {
            ReadOnlySpan<char> source = pattern.AsSpan();
            if (!AnalyzeFileSystemGlobbing(
                    source,
                    separator,
                    out bool needsRewrite,
                    out int invalidParentPosition,
                    out starCount,
                    out firstStarPosition,
                    out hasAsteriskRun))
            {
                rewritten = false;
                error = new GlobCompileError(
                    GlobCompileErrorCode.ParentSegmentNotAtBeginning,
                    invalidParentPosition,
                    message: "\"..\" can be only added at the beginning of the pattern.");

                return false;
            }

            error = default;
            if (!needsRewrite)
            {
                rewritten = false;
                return true;
            }

            rewritten = true;
            starCount = 0;
            firstStarPosition = -1;
            hasAsteriskRun = false;

            int n = source.Length;
            bool hasTrailingSeparator = source[^1] == separator;
            if (hasTrailingSeparator)
            {
                while (n > 0 && source[n - 1] == separator)
                {
                    n--;
                }

                if (n == 0)
                {
                    pattern = string.Empty;
                    return true;
                }
            }

            ValueStringBuilder builder = new(stackalloc char[256]);

            int i = 0;

            // FSG trims every leading separator and current-directory segment.
            while (i < n)
            {
                if (source[i] == separator)
                {
                    i++;
                    continue;
                }

                if (i + 1 < n && source[i] == '.' && source[i + 1] == separator)
                {
                    i += 2;
                    continue;
                }

                break;
            }

            // Walk remaining segments. Each internal empty segment is one
            // non-empty-component wildcard in FSG.
            bool firstEmitted = true;
            bool prevWasDoubleStar = false;
            int segStart = i;

            while (true)
            {
                while (i < n && source[i] != separator)
                {
                    i++;
                }

                ReadOnlySpan<char> seg = source[segStart..i];
                bool atEnd = i == n;
                FileSystemGlobbingSegmentKind kind = ClassifyFileSystemGlobbingSegment(seg);

                switch (kind)
                {
                    case FileSystemGlobbingSegmentKind.Empty:
                        if (!firstEmitted)
                        {
                            builder.Append(separator);
                        }

                        builder.Append('*');
                        firstEmitted = false;
                        prevWasDoubleStar = false;
                        break;

                    case FileSystemGlobbingSegmentKind.Current:
                        // Drop "." segments (collapses "/./" and trailing "/.").
                        break;

                    case FileSystemGlobbingSegmentKind.RecursiveSuffix:
                        // FSG parses "**.suffix" as a recursive segment followed by
                        // a wildcard file-name segment: "**/*.suffix".
                        if (!prevWasDoubleStar)
                        {
                            if (!firstEmitted)
                            {
                                builder.Append(separator);
                            }

                            builder.Append("**");
                            firstEmitted = false;
                        }

                        builder.Append(separator);
                        ReadOnlySpan<char> fileSegment = seg[1..];
                        bool isStarDotStar =
                            fileSegment.Length == 3
                                && fileSegment[0] == '*'
                                && fileSegment[1] == '.'
                                && fileSegment[2] == '*';

                        builder.Append(isStarDotStar ? "*" : fileSegment);
                        prevWasDoubleStar = false;
                        break;

                    case FileSystemGlobbingSegmentKind.DoubleStar:
                        if (!prevWasDoubleStar)
                        {
                            if (!firstEmitted)
                            {
                                builder.Append(separator);
                            }

                            builder.Append(seg);
                            firstEmitted = false;
                        }

                        prevWasDoubleStar = true;
                        break;

                    case FileSystemGlobbingSegmentKind.Literal:
                    case FileSystemGlobbingSegmentKind.Parent:
                    case FileSystemGlobbingSegmentKind.StarDotStar:
                        if (!firstEmitted)
                        {
                            builder.Append(separator);
                        }

                        builder.Append(kind == FileSystemGlobbingSegmentKind.StarDotStar ? "*" : seg);
                        firstEmitted = false;
                        prevWasDoubleStar = false;
                        break;
                }

                if (atEnd)
                {
                    break;
                }

                i++;
                segStart = i;
            }

            if (hasTrailingSeparator)
            {
                if (builder.Length > 0)
                {
                    builder.Append(separator);
                }

                builder.Append("**");
            }

            // Trailing "/**" requires at least one path component beyond the prior
            // literal. Rewrite "X/**" -> "X/*/**" so the leading "/*" forces the
            // required segment while the trailing "/**" continues to allow zero or
            // more deeper segments. Bare "**" (length 2, no leading segment) is left
            // alone - it means "everything", including the implicit root.
            if (builder.Length > 3
                && builder[^1] == '*'
                && builder[^2] == '*'
                && builder[^3] == separator)
            {
                builder.Length -= 2;
                builder.Append('*');
                builder.Append(separator);
                builder.Append("**");
            }

            pattern = builder.ToString();
            builder.Dispose();
            return true;
        }

        /// <summary>
        ///  Allocation-free validation and detection scan. Returns <see langword="false"/>
        ///  when a parent segment appears after a non-parent segment. Otherwise,
        ///  <paramref name="needsRewrite"/> reports whether
        ///  <see cref="TryFileSystemGlobbing"/> would change <paramref name="pattern"/>.
        ///  Mirrors the rule set in the rewrite body so the two stay in sync.
        /// </summary>
        private static bool AnalyzeFileSystemGlobbing(
            ReadOnlySpan<char> pattern,
            char separator,
            out bool needsRewrite,
            out int invalidParentPosition,
            out int starCount,
            out int firstStarPosition,
            out bool hasAsteriskRun)
        {
            int n = pattern.Length;
            starCount = 0;
            firstStarPosition = -1;
            hasAsteriskRun = false;
            if (n == 0)
            {
                needsRewrite = false;
                invalidParentPosition = -1;
                return true;
            }

            needsRewrite = pattern[0] == separator || pattern[^1] == separator;

            int patternStart = 0;
            while (patternStart < n && pattern[patternStart] == separator)
            {
                patternStart++;
            }

            int patternEnd = n;
            while (patternEnd > patternStart && pattern[patternEnd - 1] == separator)
            {
                patternEnd--;
            }

            int segStart = patternStart;
            int segIndex = 0;
            bool prevWasDoubleStar = false;
            bool parentSegmentAllowed = true;
            int asteriskRunLength = 0;

            for (int i = patternStart; i <= patternEnd; i++)
            {
                if (i < patternEnd && pattern[i] != separator)
                {
                    if (!needsRewrite && pattern[i] == '*')
                    {
                        if (firstStarPosition < 0)
                        {
                            firstStarPosition = i;
                        }

                        starCount++;
                    }

                    if (pattern[i] == '*')
                    {
                        if (++asteriskRunLength >= 3)
                        {
                            hasAsteriskRun = true;
                        }
                    }
                    else
                    {
                        asteriskRunLength = 0;
                    }

                    continue;
                }

                asteriskRunLength = 0;

                ReadOnlySpan<char> seg = pattern[segStart..i];
                FileSystemGlobbingSegmentKind kind = ClassifyFileSystemGlobbingSegment(seg);
                if (kind == FileSystemGlobbingSegmentKind.Parent && !parentSegmentAllowed)
                {
                    needsRewrite = false;
                    invalidParentPosition = segStart;
                    return false;
                }

                if (kind is FileSystemGlobbingSegmentKind.Empty
                    or FileSystemGlobbingSegmentKind.Current
                    or FileSystemGlobbingSegmentKind.StarDotStar
                    or FileSystemGlobbingSegmentKind.RecursiveSuffix)
                {
                    needsRewrite = true;
                }

                if (kind != FileSystemGlobbingSegmentKind.Parent)
                {
                    parentSegmentAllowed = false;
                }

                bool isDoubleStar = kind == FileSystemGlobbingSegmentKind.DoubleStar;
                if (isDoubleStar && prevWasDoubleStar)
                {
                    needsRewrite = true;
                }

                // Trailing "**" segment with at least one preceding non-empty segment.
                if (isDoubleStar && i == patternEnd && segIndex > 0)
                {
                    needsRewrite = true;
                }

                prevWasDoubleStar = isDoubleStar;
                segIndex++;
                segStart = i + 1;
            }

            invalidParentPosition = -1;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static FileSystemGlobbingSegmentKind ClassifyFileSystemGlobbingSegment(
            ReadOnlySpan<char> segment)
        {
            if (segment.IsEmpty)
            {
                return FileSystemGlobbingSegmentKind.Empty;
            }

            if (segment.Length == 1 && segment[0] == '.')
            {
                return FileSystemGlobbingSegmentKind.Current;
            }

            if (segment.Length == 2)
            {
                if (segment[0] == '.' && segment[1] == '.')
                {
                    return FileSystemGlobbingSegmentKind.Parent;
                }

                if (segment[0] == '*' && segment[1] == '*')
                {
                    return FileSystemGlobbingSegmentKind.DoubleStar;
                }
            }

            if (segment.Length == 3
                && segment[0] == '*'
                && segment[1] == '.'
                && segment[2] == '*')
            {
                return FileSystemGlobbingSegmentKind.StarDotStar;
            }

            return segment.Length > 2
                && segment[0] == '*'
                && segment[1] == '*'
                && segment[2] == '.'
                    ? FileSystemGlobbingSegmentKind.RecursiveSuffix
                    : FileSystemGlobbingSegmentKind.Literal;
        }
    }
}
