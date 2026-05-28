// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Parity tests confirming touki's <see cref="MSBuildSpecification.Unescape"/> behaves the same as
///  MSBuild's <c>EscapingUtilities.UnescapeAll</c> for valid and malformed <c>%XX</c> escape
///  sequences. MSBuild deliberately tolerates malformed escapes (emits the raw characters) rather
///  than reporting an error class, so the contract here is "match MSBuild's tolerance," not
///  "validate."
/// </summary>
public class EscapingUtilitiesOracleTests
{
    [Theory]
    // Strings with no escape characters round-trip unchanged.
    [InlineData("")]
    [InlineData("file.cs")]
    [InlineData("foo/bar/baz.txt")]
    [InlineData("**/*.cs")]
    // Valid %XX escapes are decoded.
    [InlineData("foo%20bar")]                 // %20 = space
    [InlineData("foo%2520bar")]               // %25 = '%', then "20bar"
    [InlineData("%2A.cs")]                    // %2A = '*'
    [InlineData("dir%2Fname")]                // %2F = '/'
    [InlineData("100%25done")]                // %25 = '%'
    [InlineData("%00")]                       // %00 = NUL - decoded but illegal at the spec layer
    [InlineData("file%2Bname")]               // %2B = '+'
    [InlineData("hex%41%42%43")]              // %41-%43 = 'A','B','C'
    // Lowercase hex digits.
    [InlineData("foo%2abar")]
    [InlineData("foo%2fbar")]
    // Mixed-case hex digits.
    [InlineData("foo%2Abar")]
    [InlineData("foo%2Fbar")]
    // Malformed escapes (non-hex digits, truncated). MSBuild leaves these literal.
    [InlineData("%XX")]
    [InlineData("%GG")]
    [InlineData("%2")]                        // truncated, single hex digit
    [InlineData("%")]                         // bare %
    [InlineData("foo%")]                      // bare % at end
    [InlineData("foo%Z")]
    [InlineData("foo%2Zbar")]                 // valid first hex, invalid second
    [InlineData("100% done")]                 // % followed by non-hex
    [InlineData("%%20")]                      // bare % followed by valid escape
    [InlineData("a%20%XX%20b")]               // mix of valid and malformed
    // Multiple escapes in sequence.
    [InlineData("%20%20%20")]
    [InlineData("%2A%2A%2F%2A%2E%63%73")]     // "**/*.cs" as escapes
    public void Unescape_MatchesMSBuildUnescapeAll(string input)
    {
        string toukiResult = MSBuildSpecification.Unescape(input).ToString();
        string oracleResult = EscapingUtilitiesWrapper.UnescapeAll(input);

        toukiResult.Should().Be(oracleResult, $"input was '{input}'");
    }
}
