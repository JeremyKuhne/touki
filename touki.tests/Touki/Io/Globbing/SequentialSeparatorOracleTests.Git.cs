// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using LibGit2Sharp;

namespace Touki.Io.Globbing;

/// <summary>
///  Oracle tests that pin down how the <see cref="GlobDialect.Git"/> dialect handles
///  multiple sequential <c>/</c> characters inside the pattern, by comparing each verdict
///  against a real Git ignore evaluation via <see cref="LibGit2Sharp"/>.
/// </summary>
/// <remarks>
///  <para>
///   Git's <c>wildmatch</c> treats embedded sequential separators specifically: a leading
///   <c>/</c> anchors the pattern to the gitignore's directory, but additional consecutive
///   <c>/</c>s are not coalesced by Git itself. See
///   <see href="https://git-scm.com/docs/gitignore#_pattern_format">gitignore(5)</see>.
///  </para>
///  <para>
///   The fixture creates a one-shot scratch repository in the temp directory; per-row
///   evaluation swaps the temporary ignore rule. Per-call cost is ~1-5 ms with
///   LibGit2Sharp.
///  </para>
/// </remarks>
[ClassDataSource<SequentialSeparatorGitOracleTests.RepoFixture>(Shared = SharedType.PerClass)]
[NotInParallel(nameof(SequentialSeparatorGitOracleTests))]
public sealed class SequentialSeparatorGitOracleTests
{
    public sealed class RepoFixture : IDisposable
    {
        public string Path { get; }

        public Repository Repository { get; }

        public RepoFixture()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "touki-glob-oracle-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
            Repository.Init(Path);
            Repository = new Repository(Path);
        }

        public bool IsIgnored(string pattern, string path)
        {
            // LibGit2Sharp throws on an empty relative path; gitignore semantics
            // for "no file" are vacuously "not ignored", so report False directly.
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            Repository.Ignore.ResetAllTemporaryRules();
            Repository.Ignore.AddTemporaryRules([pattern]);
            return Repository.Ignore.IsPathIgnored(path);
        }

        public void Dispose()
        {
            Repository.Dispose();
            try
            {
                // Git working trees set read-only attributes on pack files. Clear them
                // before delete so the temp directory can actually go away.
                foreach (string file in Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
                Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // Best-effort cleanup; an orphaned temp directory is not worth failing the test run.
            }
        }
    }

    private readonly RepoFixture _fixture;

    public SequentialSeparatorGitOracleTests(RepoFixture fixture) => _fixture = fixture;

    private static bool ToukiMatches(string pattern, string input) =>
        GlobSpecification.Compile(pattern, GlobDialect.Git).IsMatch(input);

    [Test]
    // --- Doubled separator between literal segments ---
    [Arguments("a//b", "a/b")]
    [Arguments("a//b", "a//b")]
    [Arguments("a//b", "a///b")]
    [Arguments("a//b", "ab")]
    [Arguments("a//b", "a/x/b")]
    // --- Tripled / quadrupled separator runs ---
    [Arguments("a///b", "a/b")]
    [Arguments("a///b", "a//b")]
    [Arguments("a////b", "a/b")]
    // --- Leading separator runs (anchored vs unanchored) ---
    [Arguments("//a", "a")]
    [Arguments("//a", "x/a")]
    // --- Trailing separator runs (directory marker semantics) ---
    [Arguments("a//", "a/b")]
    [Arguments("a//", "a")]
    // --- Doubled separator surrounding a wildcard ---
    [Arguments("a//*", "a/b")]
    [Arguments("a//*", "a//b")]
    [Arguments("*//b", "a/b")]
    // --- Doubled separator adjacent to globstar ---
    [Arguments("**//*.cs", "Foo.cs")]
    [Arguments("**//*.cs", "src/Foo.cs")]
    [Arguments("**//*.cs", "src/sub/Foo.cs")]
    [Arguments("a//**//b", "a/b")]
    [Arguments("a//**//b", "a/x/b")]
    [Arguments("a//**//b", "a/x/y/b")]
    public void IsMatch_GitDialect_SequentialSeparators_AgreesWithLibGit2(string pattern, string input)
    {
        bool oracle = _fixture.IsIgnored(pattern, input);
        bool actual = ToukiMatches(pattern, input);
        actual.Should().Be(
            oracle,
            because: $"GlobSpecification(Git) and LibGit2Sharp gitignore must agree on pattern '{pattern}' vs input '{input}'");
    }
}
