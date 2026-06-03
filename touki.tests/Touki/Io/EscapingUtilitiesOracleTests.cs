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
[TestClass]
public class EscapingUtilitiesOracleTests
{
    [TestMethod]
    // Strings with no escape characters round-trip unchanged.
    [DataRow("")]
    [DataRow("file.cs")]
    [DataRow("foo/bar/baz.txt")]
    [DataRow("**/*.cs")]
    // Valid %XX escapes are decoded.
    [DataRow("foo%20bar")]                 // %20 = space
    [DataRow("foo%2520bar")]               // %25 = '%', then "20bar"
    [DataRow("%2A.cs")]                    // %2A = '*'
    [DataRow("dir%2Fname")]                // %2F = '/'
    [DataRow("100%25done")]                // %25 = '%'
    [DataRow("%00")]                       // %00 = NUL - decoded but illegal at the spec layer
    [DataRow("file%2Bname")]               // %2B = '+'
    [DataRow("hex%41%42%43")]              // %41-%43 = 'A','B','C'
    // Lowercase hex digits.
    [DataRow("foo%2abar")]
    [DataRow("foo%2fbar")]
    // Mixed-case hex digits.
    [DataRow("foo%2Abar")]
    [DataRow("foo%2Fbar")]
    // Malformed escapes (non-hex digits, truncated). MSBuild leaves these literal.
    [DataRow("%XX")]
    [DataRow("%GG")]
    [DataRow("%2")]                        // truncated, single hex digit
    [DataRow("%")]                         // bare %
    [DataRow("foo%")]                      // bare % at end
    [DataRow("foo%Z")]
    [DataRow("foo%2Zbar")]                 // valid first hex, invalid second
    [DataRow("100% done")]                 // % followed by non-hex
    [DataRow("%%20")]                      // bare % followed by valid escape
    [DataRow("a%20%XX%20b")]               // mix of valid and malformed
    // Multiple escapes in sequence.
    [DataRow("%20%20%20")]
    [DataRow("%2A%2A%2F%2A%2E%63%73")]     // "**/*.cs" as escapes
    public void Unescape_MatchesMSBuildUnescapeAll(string input)
    {
        string toukiResult = MSBuildSpecification.Unescape(input).ToString();
        string oracleResult = EscapingUtilitiesWrapper.UnescapeAll(input);

        toukiResult.Should().Be(oracleResult, $"input was '{input}'");
    }
}
