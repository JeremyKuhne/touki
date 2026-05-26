// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if NET

namespace Touki.Io.Globbing;

/// <summary>
///  Oracle tests that pin down how the <see cref="GlobDialect.Simple"/> dialect handles
///  runs of three or more consecutive <c>*</c> characters in the pattern, by comparing
///  each verdict against
///  <see cref="FileSystemName.MatchesSimpleExpression(System.ReadOnlySpan{char}, System.ReadOnlySpan{char}, bool)"/>.
/// </summary>
/// <remarks>
///  <para>
///   Only available on .NET 10+; <see cref="System.IO.Enumeration.FileSystemName"/> does
///   not exist on .NET Framework. Skipped on net481.
///  </para>
/// </remarks>
public class MultipleAsteriskSimpleOracleTests
{
    public static TheoryData<string, string> Rows => MultipleAsteriskRows.Rows;

    [Theory]
    [MemberData(nameof(Rows))]
    public void IsMatch_SimpleDialect_MultipleAsterisks_AgreesWithBcl(string pattern, string input)
    {
        bool oracle = FileSystemName.MatchesSimpleExpression(pattern.AsSpan(), input.AsSpan(), ignoreCase: false);
        bool actual = GlobSpecification.Compile(pattern, GlobDialect.Simple).IsMatch(input);
        actual.Should().Be(
            oracle,
            because: $"GlobSpecification(Simple) and FileSystemName.MatchesSimpleExpression must agree on pattern '{pattern}' vs input '{input}'");
    }
}

#endif
