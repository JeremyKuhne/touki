// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

public sealed partial class GlobSpecification
{
    /// <summary>
    ///  Compile-time pattern normalization helpers shared by
    ///  <see cref="Factory"/>. Each helper takes a <see cref="ReadOnlySpan{T}"/>
    ///  reference and rewrites it only when the dialect's rule set actually
    ///  requires a change; the no-op path is allocation-free.
    /// </summary>
    private static class Normalization
    {
        /// <summary>
        ///  Applies the FileSystemGlobbing-specific compile-time rewrites that
        ///  <c>Microsoft.Extensions.FileSystemGlobbing.Matcher</c> applies
        ///  internally. See the factory call site for the catalogue of rules.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   Two-pass design: a single forward scan via
        ///   <see cref="NeedsFileSystemGlobbing"/> decides whether any rewrite
        ///   would fire. When none does, <paramref name="pattern"/> is returned
        ///   untouched and no string is allocated. When a rewrite is needed
        ///   the helper walks the pattern once into a
        ///   <see cref="ValueStringBuilder"/> seeded on the stack and produces
        ///   exactly one string via <see cref="ValueStringBuilder.ToStringAndDispose"/>.
        ///   Verified via
        ///   <c>touki.perf/FileSystemGlobbingNormalizePerf.cs</c> to outperform
        ///   a single-pass build-then-compare on the common case (real-world
        ///   patterns rarely need any rewrite), especially on net481 RyuJIT
        ///   where the build pass costs over 50 ns even without an allocation.
        ///  </para>
        /// </remarks>
        [SkipLocalsInit]
        public static void FileSystemGlobbing(ref ReadOnlySpan<char> pattern, char separator)
        {
            if (!NeedsFileSystemGlobbing(pattern, separator))
            {
                return;
            }

            ValueStringBuilder builder = new(stackalloc char[256]);

            int n = pattern.Length;
            int i = 0;

            // Strip leading "./" segments and any single leading separator
            // ("//"+ is left for Factory.TryNormalizeRuns to turn into "/*/").
            while (i < n)
            {
                if (i + 1 < n && pattern[i] == '.' && pattern[i + 1] == separator)
                {
                    i += 2;
                    continue;
                }

                if (pattern[i] == separator && (i + 1 >= n || pattern[i + 1] != separator))
                {
                    i++;
                    continue;
                }

                break;
            }

            // Walk remaining segments. Empty segments (from internal "//") are
            // preserved verbatim so Factory.TryNormalizeRuns can still see them.
            bool firstEmitted = true;
            bool prevWasDoubleStar = false;
            int segStart = i;

            while (true)
            {
                while (i < n && pattern[i] != separator)
                {
                    i++;
                }

                ReadOnlySpan<char> seg = pattern[segStart..i];
                bool atEnd = i == n;

                // Drop "." segments (collapses "/./" and trailing "/.").
                bool dropSeg = seg.Length == 1 && seg[0] == '.';

                if (!dropSeg)
                {
                    // Replace "*.*" segment with "*".
                    if (seg.Length == 3 && seg[0] == '*' && seg[1] == '.' && seg[2] == '*')
                    {
                        seg = "*".AsSpan();
                    }

                    bool isDoubleStar = seg.Length == 2 && seg[0] == '*' && seg[1] == '*';

                    if (isDoubleStar && prevWasDoubleStar)
                    {
                        // Collapse adjacent "**" segments.
                    }
                    else
                    {
                        if (!firstEmitted)
                        {
                            builder.Append(separator);
                        }

                        builder.Append(seg);
                        firstEmitted = false;
                        prevWasDoubleStar = isDoubleStar;
                    }
                }

                if (atEnd)
                {
                    break;
                }

                i++;
                segStart = i;
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

            pattern = builder.ToStringAndDispose().AsSpan();
        }

        /// <summary>
        ///  Allocation-free detection scan. Returns <see langword="true"/> when
        ///  <see cref="FileSystemGlobbing"/> would change <paramref name="pattern"/>,
        ///  otherwise <see langword="false"/>. Mirrors the rule set in the
        ///  rewrite body so the two stay in sync.
        /// </summary>
        private static bool NeedsFileSystemGlobbing(ReadOnlySpan<char> pattern, char separator)
        {
            int n = pattern.Length;
            if (n == 0)
            {
                return false;
            }

            // Single leading separator (the "//"+ case is left for Factory.TryNormalizeRuns).
            if (pattern[0] == separator && (n == 1 || pattern[1] != separator))
            {
                return true;
            }

            // Leading "./" segment.
            if (n >= 2 && pattern[0] == '.' && pattern[1] == separator)
            {
                return true;
            }

            int segStart = 0;
            int segIndex = 0;
            bool prevWasDoubleStar = false;

            for (int i = 0; i <= n; i++)
            {
                if (i < n && pattern[i] != separator)
                {
                    continue;
                }

                ReadOnlySpan<char> seg = pattern[segStart..i];

                // "." segment: came from "/./" or trailing "/.".
                if (seg.Length == 1 && seg[0] == '.')
                {
                    return true;
                }

                // "*.*" segment -> "*".
                if (seg.Length == 3 && seg[0] == '*' && seg[1] == '.' && seg[2] == '*')
                {
                    return true;
                }

                bool isDoubleStar = seg.Length == 2 && seg[0] == '*' && seg[1] == '*';

                if (isDoubleStar && prevWasDoubleStar)
                {
                    return true;
                }

                // Trailing "**" segment with at least one preceding non-empty segment.
                if (isDoubleStar && i == n && segIndex > 0)
                {
                    return true;
                }

                prevWasDoubleStar = isDoubleStar;
                segIndex++;
                segStart = i + 1;
            }

            return false;
        }
    }
}
