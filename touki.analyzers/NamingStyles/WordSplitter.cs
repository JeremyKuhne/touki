// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Immutable;

namespace Touki.Analyzers.NamingStyles;

/// <summary>
///  Splits an identifier into its constituent words.
/// </summary>
/// <remarks>
///  <para>
///   Stands in for <c>Microsoft.CodeAnalysis.Shared.Utilities.StringBreaker</c>, which pulls in the Roslyn
///   pooled-object infrastructure. Only the word-splitting behavior that <see cref="NamingStyle.MakeCompliant"/>
///   needs is reproduced: words break on underscores, on a transition into upper case, on the last upper case
///   character of a run that is followed by lower case, and on the boundaries of a digit run.
///  </para>
/// </remarks>
internal static class WordSplitter
{
    /// <summary>
    ///  Splits <paramref name="identifier"/> into words. Returns the identifier itself when it contains no
    ///  character that can start a word.
    /// </summary>
    internal static ImmutableArray<string> SplitIdentifier(string identifier)
    {
        ImmutableArray<string>.Builder words = ImmutableArray.CreateBuilder<string>();
        int wordStart = -1;

        for (int i = 0; i < identifier.Length; i++)
        {
            char current = identifier[i];

            if (current == '_')
            {
                AddWord(words, identifier, wordStart, i);
                wordStart = -1;
                continue;
            }

            if (wordStart < 0)
            {
                wordStart = i;
                continue;
            }

            if (StartsNewWord(identifier, i))
            {
                AddWord(words, identifier, wordStart, i);
                wordStart = i;
            }
        }

        AddWord(words, identifier, wordStart, identifier.Length);

        return words.Count == 0 ? [identifier] : words.ToImmutable();
    }

    private static bool StartsNewWord(string identifier, int index)
    {
        char current = identifier[index];
        char previous = identifier[index - 1];

        // A digit run is its own word, as is the character that follows one.
        if (char.IsDigit(current) != char.IsDigit(previous))
        {
            return true;
        }

        if (!char.IsUpper(current))
        {
            return false;
        }

        // A transition into upper case always starts a word: fooBar -> foo, Bar.
        if (!char.IsUpper(previous))
        {
            return true;
        }

        // Within a run of upper case characters, the last one starts a word when a lower case character follows:
        // IOStream -> IO, Stream.
        return index + 1 < identifier.Length && char.IsLower(identifier[index + 1]);
    }

    private static void AddWord(ImmutableArray<string>.Builder words, string identifier, int start, int end)
    {
        if (start >= 0 && end > start)
        {
            words.Add(identifier.Substring(start, end - start));
        }
    }
}
