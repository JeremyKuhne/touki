// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers.NamingStyles;

internal readonly partial struct NamingStyle
{
    /// <summary>
    ///  Allocation-free enumeration of the word spans within a name.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Upstream declares this and <see cref="WordSpanEnumerator"/> in separate files
    ///   (<c>NamingStyle.WordSpanEnumerable.cs</c> and <c>NamingStyle.WordSpanEnumerator.cs</c>); they are kept
    ///   together here.
    ///  </para>
    /// </remarks>
    private readonly struct WordSpanEnumerable(string name, TextSpan nameSpan, string wordSeparator)
    {
        public WordSpanEnumerator GetEnumerator() => new(name, nameSpan, wordSeparator);
    }

    private struct WordSpanEnumerator(string name, TextSpan nameSpan, string wordSeparator)
    {
        private readonly string _name = name;
        private readonly TextSpan _nameSpan = nameSpan;
        private readonly string _wordSeparator = wordSeparator;

        public TextSpan Current { get; private set; } = new TextSpan(nameSpan.Start, 0);

        public bool MoveNext()
        {
            if (_wordSeparator.Length == 0)
            {
                // No separator, so only ever return a single word.
                if (Current.Length == 0)
                {
                    Current = _nameSpan;
                    return true;
                }

                return false;
            }

            while (true)
            {
                int nextWordSeparator = _name.IndexOf(_wordSeparator, Current.End, StringComparison.Ordinal);
                if (nextWordSeparator == Current.End)
                {
                    // Right at the word separator. Skip it and continue searching.
                    Current = new TextSpan(Current.End + _wordSeparator.Length, 0);
                    continue;
                }

                // If no word separator was found, act as if the next one is at the end of the name span.
                if (nextWordSeparator < 0)
                {
                    nextWordSeparator = _nameSpan.End;
                }

                // Walked past the name span, so there are no more words to return.
                if (Current.End > _nameSpan.End)
                {
                    return false;
                }

                // Found a separator ahead. It may be inside the suffix, so use the lesser of the separator
                // position and the end of the span being checked.
                Current = TextSpan.FromBounds(Current.End, Math.Min(_nameSpan.End, nextWordSeparator));
                break;
            }

            return Current.Length > 0 && Current.End <= _nameSpan.End;
        }
    }
}
