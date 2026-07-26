// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Analyzers;

/// <summary>
///  Builds the <c>helpLinkUri</c> for a diagnostic descriptor.
/// </summary>
/// <remarks>
///  <para>
///   Every rule is documented in <c>docs/analyzers.md</c> under a heading that is its diagnostic id, so the
///   anchor is derived from the id rather than written out. That keeps the link and the rule from drifting
///   apart when a rule is renumbered.
///  </para>
/// </remarks>
internal static class HelpLinks
{
    private const string DocumentationUri = "https://github.com/JeremyKuhne/touki/blob/main/docs/analyzers.md";

    /// <summary>
    ///  Returns the documentation link for <paramref name="diagnosticId"/>.
    /// </summary>
    internal static string ForRule(string diagnosticId) =>
        $"{DocumentationUri}#{diagnosticId.ToLowerInvariant()}";
}
