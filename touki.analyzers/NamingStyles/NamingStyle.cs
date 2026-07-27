// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers.NamingStyles;

/// <summary>
///  A naming style: a required prefix and suffix, a word separator, and a capitalization scheme.
/// </summary>
/// <remarks>
///  <para>
///   Ported from <c>Microsoft.CodeAnalysis.NamingStyles.NamingStyle</c> in dotnet/roslyn. The option
///   serialization members (<c>CreateXElement</c>, <c>WriteTo</c>, <c>ReadFrom</c>) are dropped; styles here
///   are only ever built from an <c>.editorconfig</c>.
///  </para>
/// </remarks>
internal readonly partial struct NamingStyle
{
    /// <summary>
    ///  The name of the style as written in the <c>.editorconfig</c>.
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///  The prefix every name must start with. Empty when no prefix is required.
    /// </summary>
    public string Prefix { get; }

    /// <summary>
    ///  The suffix every name must end with. Empty when no suffix is required.
    /// </summary>
    public string Suffix { get; }

    /// <summary>
    ///  The string separating words within a name. Empty when words are not separated.
    /// </summary>
    public string WordSeparator { get; }

    /// <summary>
    ///  How the words of the name are capitalized.
    /// </summary>
    public Capitalization CapitalizationScheme { get; }

    /// <summary>
    ///  Initializes a new instance of the <see cref="NamingStyle"/> struct.
    /// </summary>
    public NamingStyle(
        string name,
        string? prefix = null,
        string? suffix = null,
        string? wordSeparator = null,
        Capitalization capitalizationScheme = Capitalization.PascalCase)
    {
        Name = name;
        Prefix = prefix ?? "";
        Suffix = suffix ?? "";
        WordSeparator = wordSeparator ?? "";
        CapitalizationScheme = capitalizationScheme;
    }

    /// <summary>
    ///  Builds a name from <paramref name="words"/> using this style.
    /// </summary>
    public string CreateName(ImmutableArray<string> words)
    {
        IEnumerable<string> wordsWithCasing = ApplyCapitalization(words);
        string combinedWordsWithCasing = string.Join(WordSeparator, wordsWithCasing);
        return Prefix + combinedWordsWithCasing + Suffix;
    }

    private IEnumerable<string> ApplyCapitalization(IEnumerable<string> words) => CapitalizationScheme switch
    {
        Capitalization.PascalCase => words.Select(CapitalizeFirstLetter),
        Capitalization.CamelCase => words.Take(1).Select(DecapitalizeFirstLetter)
            .Concat(words.Skip(1).Select(CapitalizeFirstLetter)),
        Capitalization.FirstUpper => words.Take(1).Select(CapitalizeFirstLetter)
            .Concat(words.Skip(1).Select(DecapitalizeFirstLetter)),
        Capitalization.AllUpper => words.Select(word => word.ToUpperInvariant()),
        Capitalization.AllLower => words.Select(word => word.ToLowerInvariant()),
        _ => throw new InvalidOperationException()
    };

    private static string CapitalizeFirstLetter(string word)
    {
        if (word.Length == 0 || char.IsUpper(word[0]))
        {
            return word;
        }

        char[] chars = word.ToCharArray();
        chars[0] = char.ToUpperInvariant(chars[0]);
        return new string(chars);
    }

    private static string DecapitalizeFirstLetter(string word)
    {
        if (word.Length == 0 || char.IsLower(word[0]))
        {
            return word;
        }

        char[] chars = word.ToCharArray();
        chars[0] = char.ToLowerInvariant(chars[0]);
        return new string(chars);
    }

    /// <summary>
    ///  Returns <see langword="true"/> when <paramref name="name"/> satisfies this style, otherwise
    ///  <see langword="false"/> with <paramref name="failureReason"/> describing the first violation.
    /// </summary>
    public bool IsNameCompliant(string name, out string? failureReason)
    {
        if (!name.StartsWith(Prefix, StringComparison.Ordinal))
        {
            failureReason = $"Missing prefix: '{Prefix}'";
            return false;
        }

        if (!name.EndsWith(Suffix, StringComparison.Ordinal))
        {
            failureReason = $"Missing suffix: '{Suffix}'";
            return false;
        }

        if (name.Length <= Prefix.Length + Suffix.Length)
        {
            // The name consists only of the prefix and suffix with no base name. The prefix and suffix are
            // allowed to overlap, for example prefix "s_", suffix "_t", name "s_t".
            failureReason = null;
            return true;
        }

        string nameWithoutPrefix = name.Substring(Prefix.Length);

        // DEVIATION from dotnet/roslyn: upstream always strips the well known "common prefixes" (m_, s_, t_, _)
        // from the remainder and fails when it finds one, even for a style that requires no prefix at all. That
        // makes an all_upper or camel_case rule with no required prefix reject S_VALUE and s_value outright,
        // because the strip is case insensitive and unconditional. See dotnet/roslyn#57706 and
        // dotnet/roslyn#55845. A style that asks for no prefix has nothing to say about a leading s_/m_/t_/_,
        // so only run the check when a prefix was actually required.
        if (Prefix.Length > 0)
        {
            StripCommonPrefixes(nameWithoutPrefix, out string extraPrefix);

            if (extraPrefix.Length > 0)
            {
                // The name started with the required prefix but carries at least one additional common prefix,
                // for example required prefix "test_" and actual prefix "test_m_".
                failureReason = $"Prefix '{extraPrefix}' does not match expected prefix '{Prefix}'";
                return false;
            }
        }

        TextSpan spanToCheck = TextSpan.FromBounds(Prefix.Length, name.Length - Suffix.Length);

        // DEVIATION from dotnet/roslyn: when no word separator is configured upstream treats the whole name as a
        // single word, so pascal_case only ever validates the very first character and My_variable, MYVARIABLE
        // and Myvariable all pass. See dotnet/roslyn#70709. An underscore is a word separator, so a style that
        // did not ask for one should not silently accept it.
        if (WordSeparator.Length == 0
            && CapitalizationScheme != Capitalization.AllUpper
            && CapitalizationScheme != Capitalization.AllLower
            && name.IndexOf('_', spanToCheck.Start, spanToCheck.Length) >= 0)
        {
            failureReason = "The name must not contain word separators: '_'";
            return false;
        }

        return CapitalizationScheme switch
        {
            Capitalization.PascalCase => CheckPascalCase(name, spanToCheck, out failureReason),
            Capitalization.CamelCase => CheckCamelCase(name, spanToCheck, out failureReason),
            Capitalization.FirstUpper => CheckFirstUpper(name, spanToCheck, out failureReason),
            Capitalization.AllUpper => CheckAllUpper(name, spanToCheck, out failureReason),
            Capitalization.AllLower => CheckAllLower(name, spanToCheck, out failureReason),
            _ => throw new InvalidOperationException()
        };
    }

    private WordSpanEnumerable GetWordSpans(string name, TextSpan nameSpan) => new(name, nameSpan, WordSeparator);

    private static string Substring(string name, TextSpan wordSpan) => name.Substring(wordSpan.Start, wordSpan.Length);

    private static bool FirstCharIsLowerCase(string value, TextSpan span) =>
        !DoesCharacterHaveCasing(value[span.Start]) || char.IsLower(value[span.Start]);

    private static bool FirstCharIsUpperCase(string value, TextSpan span) =>
        !DoesCharacterHaveCasing(value[span.Start]) || char.IsUpper(value[span.Start]);

    private static bool WordIsAllUpperCase(string value, TextSpan span)
    {
        for (int i = span.Start, n = span.End; i < n; i++)
        {
            if (DoesCharacterHaveCasing(value[i]) && !char.IsUpper(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool WordIsAllLowerCase(string value, TextSpan span)
    {
        for (int i = span.Start, n = span.End; i < n; i++)
        {
            if (DoesCharacterHaveCasing(value[i]) && !char.IsLower(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    private bool CheckAllWords(
        string name,
        TextSpan nameSpan,
        Func<string, TextSpan, bool> wordCheck,
        string messageFormat,
        out string? reason)
    {
        reason = null;
        List<string> violations = [];

        foreach (TextSpan wordSpan in GetWordSpans(name, nameSpan))
        {
            if (!wordCheck(name, wordSpan))
            {
                violations.Add(Substring(name, wordSpan));
            }
        }

        if (violations.Count > 0)
        {
            reason = messageFormat + string.Join(", ", violations);
        }

        return reason is null;
    }

    private bool CheckPascalCase(string name, TextSpan nameSpan, out string? reason) => CheckAllWords(
        name,
        nameSpan,
        FirstCharIsUpperCase,
        "These words must begin with upper case characters: ",
        out reason);

    private bool CheckAllUpper(string name, TextSpan nameSpan, out string? reason) => CheckAllWords(
        name,
        nameSpan,
        WordIsAllUpperCase,
        "These words cannot contain lower case characters: ",
        out reason);

    private bool CheckAllLower(string name, TextSpan nameSpan, out string? reason) => CheckAllWords(
        name,
        nameSpan,
        WordIsAllLowerCase,
        "These words cannot contain upper case characters: ",
        out reason);

    private bool CheckFirstAndRestWords(
        string name,
        TextSpan nameSpan,
        Func<string, TextSpan, bool> firstWordCheck,
        Func<string, TextSpan, bool> restWordCheck,
        string firstMessageFormat,
        string restMessageFormat,
        out string? reason)
    {
        reason = null;
        List<string> violations = [];
        bool first = true;

        foreach (TextSpan wordSpan in GetWordSpans(name, nameSpan))
        {
            if (first)
            {
                if (!firstWordCheck(name, wordSpan))
                {
                    reason = firstMessageFormat + Substring(name, wordSpan);
                }
            }
            else if (!restWordCheck(name, wordSpan))
            {
                violations.Add(Substring(name, wordSpan));
            }

            first = false;
        }

        if (violations.Count > 0)
        {
            string restString = restMessageFormat + string.Join(", ", violations);

            // Upstream joins the two reasons with Environment.NewLine. RS1035 bans Environment in an analyzer
            // and a diagnostic message should be a single line anyway, so join with a space.
            reason = reason is null ? restString : reason + " " + restString;
        }

        return reason is null;
    }

    private bool CheckCamelCase(string name, TextSpan nameSpan, out string? reason) => CheckFirstAndRestWords(
        name,
        nameSpan,
        FirstCharIsLowerCase,
        FirstCharIsUpperCase,
        "The first word must begin with a lower case character: ",
        "These non-leading words must begin with an upper case letter: ",
        out reason);

    private bool CheckFirstUpper(string name, TextSpan nameSpan, out string? reason) => CheckFirstAndRestWords(
        name,
        nameSpan,
        FirstCharIsUpperCase,
        FirstCharIsLowerCase,
        "The first word must begin with an upper case character: ",
        "These non-leading words must begin with a lower case letter: ",
        out reason);

    private static bool DoesCharacterHaveCasing(char c) => char.ToLowerInvariant(c) != char.ToUpperInvariant(c);

    /// <summary>
    ///  Returns the names that would satisfy this style, best candidate first.
    /// </summary>
    public IEnumerable<string> MakeCompliant(string name)
    {
        string name1 = CreateCompliantNameReusingPartialPrefixesAndSuffixes(name);
        yield return name1;

        string name2 = CreateCompliantNameDirectly(name);
        if (name2 != name1)
        {
            yield return name2;
        }
    }

    private string CreateCompliantNameDirectly(string name)
    {
        // For a required prefix of "Test_" and a name of "Test_m_BaseName" this removes "Test_m_". The "Test_"
        // is added back below. As in IsNameCompliant, a style that requires no prefix has no business removing
        // a leading s_/m_/t_/_ the author wrote. See dotnet/roslyn#57706.
        if (Prefix.Length > 0)
        {
            name = StripCommonPrefixes(
                name.StartsWith(Prefix, StringComparison.Ordinal) ? name.Substring(Prefix.Length) : name,
                out _);
        }

        if (!name.StartsWith(Prefix, StringComparison.Ordinal))
        {
            name = Prefix + name;
        }

        if (!name.EndsWith(Suffix, StringComparison.Ordinal))
        {
            name += Suffix;
        }

        return FinishFixingName(name);
    }

    private string CreateCompliantNameReusingPartialPrefixesAndSuffixes(string name)
    {
        if (Prefix.Length > 0)
        {
            name = StripCommonPrefixes(name, out _);
        }

        name = EnsurePrefix(name);
        name = EnsureSuffix(name);
        return FinishFixingName(name);
    }

    /// <summary>
    ///  Removes the well known <c>m_</c>, <c>s_</c>, <c>t_</c> and <c>_</c> prefixes from
    ///  <paramref name="name"/>, reporting what was removed in <paramref name="prefix"/>.
    /// </summary>
    public static string StripCommonPrefixes(string name, out string prefix)
    {
        int index = 0;
        while (index + 1 < name.Length)
        {
            switch (char.ToLowerInvariant(name[index]))
            {
                case 'm':
                case 's':
                case 't':
                    if (index + 2 < name.Length && name[index + 1] == '_')
                    {
                        index++;
                        continue;
                    }

                    break;

                case '_':
                    if (index + 1 < name.Length && !char.IsDigit(name[index + 1]))
                    {
                        index++;
                        continue;
                    }

                    break;

                default:
                    break;
            }

            // The current iteration did not strip any additional characters.
            break;
        }

        prefix = name.Substring(0, index);
        return name.Substring(index);
    }

    private string FinishFixingName(string name)
    {
        // Edge case: prefix "as", suffix "sa", name "asa".
        if (Suffix.Length + Prefix.Length >= name.Length)
        {
            return name;
        }

        name = name.Substring(Prefix.Length, name.Length - Suffix.Length - Prefix.Length);
        ImmutableArray<string> words = [name];

        if (WordSeparator.Length > 0)
        {
            words = [.. name.Split([WordSeparator], StringSplitOptions.RemoveEmptyEntries)];

            // Edge case: the only characters in the name are the word separator.
            if (words.Length == 0)
            {
                return name;
            }
        }

        if (words.Length == 1)
        {
            // Only split when the name has not been split already.
            words = WordSplitter.SplitIdentifier(name);
        }

        return Prefix + string.Join(WordSeparator, ApplyCapitalization(words)) + Suffix;
    }

    private string EnsureSuffix(string name)
    {
        // If the name already ends with any prefix of the suffix, only append the part of the suffix that is
        // not already there. For a required suffix of "_catdog" and a name of "test_cat", only append "dog".
        for (int i = Suffix.Length; i > 0; i--)
        {
            if (name.EndsWith(Suffix.Substring(0, i), StringComparison.Ordinal))
            {
                return name + Suffix.Substring(i);
            }
        }

        return name + Suffix;
    }

    private string EnsurePrefix(string name)
    {
        // Exceptional case: if the name is an interface-like name (for example InputStream) and the rule is a
        // single upper case character prefix, don't treat the existing 'I' as a match. Produce IInputStream.
        if (Prefix.Length == 1
            && char.IsUpper(Prefix[0])
            && name.Length >= 2
            && Prefix[0] == name[0]
            && char.IsLower(name[1]))
        {
            return Prefix + name;
        }

        // If the name already starts with any suffix of the prefix, only prepend the part of the prefix that is
        // not already there. For a required prefix of "catdog_" and a name of "dog_test", only prepend "cat".
        for (int i = 0; i < Prefix.Length; i++)
        {
            if (name.StartsWith(Prefix.Substring(i), StringComparison.Ordinal))
            {
                return Prefix.Substring(0, i) + name;
            }
        }

        return Prefix + name;
    }
}
