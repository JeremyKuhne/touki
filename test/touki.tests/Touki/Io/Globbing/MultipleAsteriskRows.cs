// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Shared row set for the multiple-asterisk oracle theories. Used by the per-dialect
///  oracle test classes so the same patterns are exercised against every reference.
/// </summary>
internal static class MultipleAsteriskRows
{
    /// <summary>
    ///  (pattern, input) pairs covering runs of <c>***</c> / <c>****</c> in different
    ///  positions: alone, between literals, adjacent to separators, and adjacent to
    ///  globstar-shaped segments.
    /// </summary>
    public static IEnumerable<(string, string)> Rows()
    {
        // Bare runs
        yield return ("***", "");
        yield return ("***", "a");
        yield return ("***", "abc");
        yield return ("***", "a/b");
        yield return ("****", "a");
        yield return ("****", "a/b");

        // Runs between literals
        yield return ("a***b", "ab");
        yield return ("a***b", "axb");
        yield return ("a***b", "axyzb");
        yield return ("a***b", "a/b");
        yield return ("a****b", "axyzb");

        // Runs adjacent to a literal suffix
        yield return ("***.cs", "foo.cs");
        yield return ("***.cs", "a/foo.cs");
        yield return ("a***", "a");
        yield return ("a***", "abc");
        yield return ("***b", "b");
        yield return ("***b", "ab");

        // Runs adjacent to a separator
        yield return ("a/***", "a/");
        yield return ("a/***", "a/b");
        yield return ("a/***", "a/b/c");
        yield return ("***/foo", "foo");
        yield return ("***/foo", "a/foo");
        yield return ("***/foo", "a/b/foo");
        yield return ("a/***/b", "a/b");
        yield return ("a/***/b", "a/x/b");
        yield return ("a/***/b", "a/x/y/b");
    }
}
