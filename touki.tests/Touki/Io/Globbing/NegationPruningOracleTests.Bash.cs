// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if NET

namespace Touki.Io.Globbing;

/// <summary>
///  Oracle tests for extglob negation directory pruning, validated against the canonical
///  <c>bash</c> implementation via <see cref="BashInterop"/>. The directory-pruning
///  optimization in <c>GlobMatch.MatchesDirectory</c> is an enumeration shortcut; it must
///  never change which files an anchored negation matches.
/// </summary>
/// <remarks>
///  <para>
///   Each row enumerates a real fixture tree with <see cref="Touki.Io.GlobEnumerator"/>
///   (directory pruning active) and compares the produced file set against the canonical
///   bash pathname expansion of the same pattern over the same tree
///   (<see cref="BashInterop.ExpandPathnames"/>, run under
///   <c>shopt -s extglob globstar nullglob</c>). Pathname expansion - not
///   <c>[[ "$path" == $pattern ]]</c> string matching - is the faithful reference here:
///   bash's conditional operator does not implement globstar, so <c>**</c> would degrade
///   to <c>*</c> and the surrounding <c>/</c> would become a mandatory literal. Glob
///   expansion exercises bash's own path-aware traversal, including the globstar
///   zero-segment collapse and per-segment negation pruning we are validating. If touki's
///   pruning ever drops a file the pattern matches - the <c>**/!(bin)/*.cs</c>
///   floating-negation case is the classic foot-gun - the two sets diverge and the test
///   fails.
///  </para>
///  <para>
///   The enumeration uses the <see cref="GlobDialect.Bash"/> dialect with globstar and
///   extglob enabled so the touki matcher and the bash oracle share the same pattern
///   surface. Skipped automatically wherever a bash 4+ binary is unavailable (notably
///   macOS, whose system bash is 3.2) - see <see cref="BashInterop.ResolveBashPath"/>.
///  </para>
/// </remarks>
[TestClass]
public class NegationPruningBashOracleTests
{
    public static IEnumerable<object[]> NegationPatterns() =>
    [
        // First-segment anchored negation.
        ["!(bin|obj)/**/*.cs"],
        ["!(bin)/*.cs"],
        // Floating negation behind a globstar (the must-not-prune-root-bin case).
        ["**/!(bin)/*.cs"],
        // Negation anchored under a literal prefix.
        ["src/!(bin)/**/*.cs"],
        ["src/**/!(obj)/*.cs"],
        // Two anchored negations.
        ["!(bin|obj)/!(test)/*.cs"],
        // No negation - pruning inactive, must still agree with the oracle.
        ["**/*.cs"],
    ];

    [TestMethod]
    [DynamicData(nameof(NegationPatterns))]
    public void Enumerate_DirectoryPruning_AgreesWithBash(string pattern)
    {
        string? bashPath = BashInterop.ResolveBashPath();
        if (bashPath is null)
        {
            Assert.Inconclusive("bash oracle requires bash 4+ on PATH (or Git for Windows installed).");
            return;
        }

        using TempFolder folder = CreateFixture();
        string root = folder.TempPath;

        const GlobOptions Options = GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob;

        // Reference: the canonical bash pathname expansion of the pattern over the tree.
        HashSet<string> reference = BashInterop.ExpandPathnames(bashPath, root, pattern);

        // Actual: the pruned enumeration.
        HashSet<string> pruned = [];
        using GlobEnumerator enumerator = GlobEnumerator.Create(
            pattern,
            excludePattern: null,
            root,
            GlobDialect.Bash,
            Options);
        while (enumerator.MoveNext())
        {
            pruned.Add(ToForwardSlash(enumerator.Current));
        }

        pruned.Should().BeEquivalentTo(
            reference,
            because: $"directory pruning for '{pattern}' must yield the same files as the canonical bash expansion");
    }

    private static TempFolder CreateFixture()
    {
        TempFolder folder = new();
        string root = folder.TempPath;
        Directory.CreateDirectory(Path.Combine(root, "src", "nested"));
        Directory.CreateDirectory(Path.Combine(root, "src", "bin"));
        Directory.CreateDirectory(Path.Combine(root, "src", "lib"));
        Directory.CreateDirectory(Path.Combine(root, "src", "obj"));
        Directory.CreateDirectory(Path.Combine(root, "obj", "Debug"));
        Directory.CreateDirectory(Path.Combine(root, "bin", "Release"));
        Directory.CreateDirectory(Path.Combine(root, "binx"));
        Directory.CreateDirectory(Path.Combine(root, "lib", "bin"));
        Directory.CreateDirectory(Path.Combine(root, "a", "bin"));
        Directory.CreateDirectory(Path.Combine(root, "a", "lib"));

        File.WriteAllText(Path.Combine(root, "top.cs"), "");
        File.WriteAllText(Path.Combine(root, "src", "a.cs"), "");
        File.WriteAllText(Path.Combine(root, "src", "nested", "c.cs"), "");
        File.WriteAllText(Path.Combine(root, "src", "bin", "d.cs"), "");
        File.WriteAllText(Path.Combine(root, "src", "lib", "f.cs"), "");
        File.WriteAllText(Path.Combine(root, "src", "obj", "g.cs"), "");
        File.WriteAllText(Path.Combine(root, "obj", "Debug", "obj.cs"), "");
        File.WriteAllText(Path.Combine(root, "bin", "Release", "bin.cs"), "");
        File.WriteAllText(Path.Combine(root, "bin", "a.cs"), "");
        File.WriteAllText(Path.Combine(root, "binx", "e.cs"), "");
        File.WriteAllText(Path.Combine(root, "lib", "bin", "h.cs"), "");
        File.WriteAllText(Path.Combine(root, "lib", "k.cs"), "");
        File.WriteAllText(Path.Combine(root, "a", "bin", "x.cs"), "");
        File.WriteAllText(Path.Combine(root, "a", "lib", "y.cs"), "");
        return folder;
    }

    private static string ToForwardSlash(string path) =>
        path.Replace('\\', '/').Trim('/');
}

#endif
