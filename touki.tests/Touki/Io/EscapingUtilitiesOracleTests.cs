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
    [Test]
    // Strings with no escape characters round-trip unchanged.
    [Arguments("")]
    [Arguments("file.cs")]
    [Arguments("foo/bar/baz.txt")]
    [Arguments("**/*.cs")]
    // Valid %XX escapes are decoded.
    [Arguments("foo%20bar")]                 // %20 = space
    [Arguments("foo%2520bar")]               // %25 = '%', then "20bar"
    [Arguments("%2A.cs")]                    // %2A = '*'
    [Arguments("dir%2Fname")]                // %2F = '/'
    [Arguments("100%25done")]                // %25 = '%'
    [Arguments("%00")]                       // %00 = NUL - decoded but illegal at the spec layer
    [Arguments("file%2Bname")]               // %2B = '+'
    [Arguments("hex%41%42%43")]              // %41-%43 = 'A','B','C'
    // Lowercase hex digits.
    [Arguments("foo%2abar")]
    [Arguments("foo%2fbar")]
    // Mixed-case hex digits.
    [Arguments("foo%2Abar")]
    [Arguments("foo%2Fbar")]
    // Malformed escapes (non-hex digits, truncated). MSBuild leaves these literal.
    [Arguments("%XX")]
    [Arguments("%GG")]
    [Arguments("%2")]                        // truncated, single hex digit
    [Arguments("%")]                         // bare %
    [Arguments("foo%")]                      // bare % at end
    [Arguments("foo%Z")]
    [Arguments("foo%2Zbar")]                 // valid first hex, invalid second
    [Arguments("100% done")]                 // % followed by non-hex
    [Arguments("%%20")]                      // bare % followed by valid escape
    [Arguments("a%20%XX%20b")]               // mix of valid and malformed
    // Multiple escapes in sequence.
    [Arguments("%20%20%20")]
    [Arguments("%2A%2A%2F%2A%2E%63%73")]     // "**/*.cs" as escapes
    public void Unescape_MatchesMSBuildUnescapeAll(string input)
    {
        string toukiResult = MSBuildSpecification.Unescape(input).ToString();
        string oracleResult = EscapingUtilitiesWrapper.UnescapeAll(input);

        toukiResult.Should().Be(oracleResult, $"input was '{input}'");
    }
}
