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
    public static TheoryData<string, string> Rows => new()
    {
        // Bare runs
        { "***", "" },
        { "***", "a" },
        { "***", "abc" },
        { "***", "a/b" },
        { "****", "a" },
        { "****", "a/b" },

        // Runs between literals
        { "a***b", "ab" },
        { "a***b", "axb" },
        { "a***b", "axyzb" },
        { "a***b", "a/b" },
        { "a****b", "axyzb" },

        // Runs adjacent to a literal suffix
        { "***.cs", "foo.cs" },
        { "***.cs", "a/foo.cs" },
        { "a***", "a" },
        { "a***", "abc" },
        { "***b", "b" },
        { "***b", "ab" },

        // Runs adjacent to a separator
        { "a/***", "a/" },
        { "a/***", "a/b" },
        { "a/***", "a/b/c" },
        { "***/foo", "foo" },
        { "***/foo", "a/foo" },
        { "***/foo", "a/b/foo" },
        { "a/***/b", "a/b" },
        { "a/***/b", "a/x/b" },
        { "a/***/b", "a/x/y/b" },
    };
}
