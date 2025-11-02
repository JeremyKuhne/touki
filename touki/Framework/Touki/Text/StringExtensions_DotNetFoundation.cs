// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Original License Information:
//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Touki.Text;

public static partial class StringExtensions
{
    internal const string NewLineConst = "\r\n";
    internal const int StackallocCharBufferSizeLimit = 256;
    internal const string NewLineCharsExceptLineFeed = "\r\f\u0085\u2028\u2029";
    internal const string NewLineChars = "\r\f\u0085\u2028\u2029\n";

    extension(string stringValue)
    {
        /// <summary>
        /// Replaces all newline sequences in the current string with <see cref="Environment.NewLine"/>.
        /// </summary>
        /// <returns>
        /// A string whose contents match the current string, but with all newline sequences replaced
        /// with <see cref="Environment.NewLine"/>.
        /// </returns>
        /// <remarks>
        /// This method searches for all newline sequences within the string and canonicalizes them to match
        /// the newline sequence for the current environment. For example, when running on Windows, all
        /// occurrences of non-Windows newline sequences will be replaced with the sequence CRLF. When
        /// running on Unix, all occurrences of non-Unix newline sequences will be replaced with
        /// a single LF character.
        ///
        /// It is not recommended that protocol parsers utilize this API. Protocol specifications often
        /// mandate specific newline sequences. For example, HTTP/1.1 (RFC 8615) mandates that the request
        /// line, status line, and headers lines end with CRLF. Since this API operates over a wide range
        /// of newline sequences, a protocol parser utilizing this API could exhibit behaviors unintended
        /// by the protocol's authors.
        ///
        /// This overload is equivalent to calling <see cref="ReplaceLineEndings(string)"/>, passing
        /// <see cref="Environment.NewLine"/> as the <em>replacementText</em> parameter.
        ///
        /// This method is guaranteed O(n) complexity, where <em>n</em> is the length of the input string.
        /// </remarks>
        public string ReplaceLineEndings() => stringValue.ReplaceLineEndings(NewLineConst);

        /// <summary>
        /// Replaces all newline sequences in the current string with <paramref name="replacementText"/>.
        /// </summary>
        /// <returns>
        /// A string whose contents match the current string, but with all newline sequences replaced
        /// with <paramref name="replacementText"/>.
        /// </returns>
        /// <remarks>
        /// This method searches for all newline sequences within the string and canonicalizes them to the
        /// newline sequence provided by <paramref name="replacementText"/>. If <paramref name="replacementText"/>
        /// is <see cref="Empty"/>, all newline sequences within the string will be removed.
        ///
        /// It is not recommended that protocol parsers utilize this API. Protocol specifications often
        /// mandate specific newline sequences. For example, HTTP/1.1 (RFC 8615) mandates that the request
        /// line, status line, and headers lines end with CRLF. Since this API operates over a wide range
        /// of newline sequences, a protocol parser utilizing this API could exhibit behaviors unintended
        /// by the protocol's authors.
        ///
        /// The list of recognized newline sequences is CR (U+000D), LF (U+000A), CRLF (U+000D U+000A),
        /// NEL (U+0085), LS (U+2028), FF (U+000C), and PS (U+2029). This list is given by the Unicode
        /// Standard, Sec. 5.8, Recommendation R4 and Table 5-2.
        ///
        /// This method is guaranteed O(n * r) complexity, where <em>n</em> is the length of the input string,
        /// and where <em>r</em> is the length of <paramref name="replacementText"/>.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ReplaceLineEndings(string replacementText)
        {
            return replacementText == "\n"
                ? stringValue.ReplaceLineEndingsWithLineFeed()
                : stringValue.ReplaceLineEndingsCore(replacementText);
        }

        private string ReplaceLineEndingsCore(string replacementText)
        {
            ArgumentNullException.ThrowIfNull(replacementText);

            // Early-exit: do we need to do anything at all?
            // If not, return this string as-is.
            int idxOfFirstNewlineChar = IndexOfNewlineChar(stringValue, replacementText, out int stride);
            if (idxOfFirstNewlineChar < 0)
            {
                return stringValue;
            }

            // While writing to the builder, we don't bother memcpying the first
            // or the last segment into the builder. We'll use the builder only
            // for the intermediate segments, then we'll sandwich everything together
            // with one final string.Concat call.

            ReadOnlySpan<char> firstSegment = stringValue.AsSpan(0, idxOfFirstNewlineChar);
            ReadOnlySpan<char> remaining = stringValue.AsSpan(idxOfFirstNewlineChar + stride);

            var builder = new ValueStringBuilder(stackalloc char[StackallocCharBufferSizeLimit]);
            while (true)
            {
                int idx = IndexOfNewlineChar(remaining, replacementText, out stride);
                if (idx < 0)
                { break; } // no more newline chars
                builder.Append(replacementText);
                builder.Append(remaining[..idx]);
                remaining = remaining[(idx + stride)..];
            }

            string retVal = string.Concat(firstSegment, builder.AsSpan(), replacementText, remaining);
            builder.Dispose();
            return retVal;
        }

        // Scans the input text, returning the index of the first newline char other than the replacement text.
        // Newline chars are given by the Unicode Standard, Sec. 5.8.
        private static int IndexOfNewlineChar(ReadOnlySpan<char> text, string replacementText, out int stride)
        {
            // !! IMPORTANT !!
            //
            // We expect this method may be called with untrusted input, which means we need to
            // bound the worst-case runtime of this method. We rely on MemoryExtensions.IndexOfAny
            // having worst-case runtime O(i), where i is the index of the first needle match within
            // the haystack; or O(n) if no needle is found. This ensures that in the common case
            // of this method being called within a loop, the worst-case runtime is O(n) rather than
            // O(n^2), where n is the length of the input text.

            stride = default;
            int offset = 0;

            while (true)
            {
                int idx = text.IndexOfAny(NewLineChars);

                if ((uint)idx >= (uint)text.Length)
                {
                    return -1;
                }

                offset += idx;
                stride = 1; // needle found

                // Did we match CR? If so, and if it's followed by LF, then we need
                // to consume both chars as a single newline function match.

                if (text[idx] == '\r')
                {
                    int nextCharIdx = idx + 1;
                    if ((uint)nextCharIdx < (uint)text.Length && text[nextCharIdx] == '\n')
                    {
                        stride = 2;

                        if (replacementText != "\r\n")
                        {
                            return offset;
                        }
                    }
                    else if (replacementText != "\r")
                    {
                        return offset;
                    }
                }
                else if (replacementText.Length != 1 || replacementText[0] != text[idx])
                {
                    return offset;
                }

                offset += stride;
                text = text[(idx + stride)..];
            }
        }

        private string ReplaceLineEndingsWithLineFeed()
        {
            // If we are going to replace the new line with a line feed ('\n'),
            // we can skip looking for it to avoid breaking out of the vectorized path unnecessarily.
            int idxOfFirstNewlineChar = stringValue.AsSpan().IndexOfAny(NewLineCharsExceptLineFeed);
            if ((uint)idxOfFirstNewlineChar >= (uint)stringValue.Length)
            {
                return stringValue;
            }

            int stride = stringValue[idxOfFirstNewlineChar] == '\r'
                && (uint)(idxOfFirstNewlineChar + 1) < (uint)stringValue.Length
                && stringValue[idxOfFirstNewlineChar + 1] == '\n' ? 2 : 1;

            ReadOnlySpan<char> remaining = stringValue.AsSpan(idxOfFirstNewlineChar + stride);

            ValueStringBuilder builder = new(stackalloc char[StackallocCharBufferSizeLimit]);
            while (true)
            {
                int idx = remaining.IndexOfAny(NewLineCharsExceptLineFeed);
                if ((uint)idx >= (uint)remaining.Length)
                    break; // no more newline chars
                stride = remaining[idx] == '\r' && (uint)(idx + 1) < (uint)remaining.Length && remaining[idx + 1] == '\n' ? 2 : 1;
                builder.Append('\n');
                builder.Append(remaining[..idx]);
                remaining = remaining[(idx + stride)..];
            }

            builder.Append('\n');
            string retVal = string.Concat(stringValue.AsSpan(0, idxOfFirstNewlineChar), builder.AsSpan(), remaining);
            builder.Dispose();
            return retVal;
        }
    }
}
