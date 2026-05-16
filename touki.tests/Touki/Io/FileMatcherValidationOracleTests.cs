// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Pins down MSBuild's <c>FileMatcher.GetFiles</c> behavior for specs that touki's
///  <see cref="MSBuildSpecification.Validate"/> classifies as errors (embedded null character,
///  <c>...</c> substring, misplaced <c>**</c>). If MSBuild changes how it handles these inputs
///  the corresponding oracle test will break and force us to revisit
///  <see cref="MSBuildEnumerator.CreateResult"/>'s error-surfacing behavior.
/// </summary>
public class FileMatcherValidationOracleTests
{
    private static void CreateFixture(string root)
    {
        File.WriteAllText(Path.Combine(root, "a.txt"), string.Empty);
        Directory.CreateDirectory(Path.Combine(root, "Foo"));
        File.WriteAllText(Path.Combine(root, "Foo", "b.txt"), string.Empty);
    }

    // 1. No-wildcard illegal specs hit the GetFiles no-wildcard shortcut FIRST and are returned
    //    verbatim with SearchAction.None, BEFORE GetFileSpecInfo / RawFileSpecIsValid have a chance
    //    to run. Touki's MSBuildEnumerator.CreateResult, by contrast, validates the spec up front
    //    and returns ReturnFileSpec for the embedded null character / "..." cases.
    //
    //    This is the same layered divergence already documented for trailing-separator no-wildcard
    //    specs: MSBuild's GetFiles short-circuits anything without '*' or '?' regardless of
    //    contents, so the wrapper assertions below only confirm what MSBuild does, not what we'd
    //    ideally want it to do.

    [Theory]
    [InlineData("foo\0bar")]                 // embedded null character
    [InlineData("foo.../bar.cs")]            // illegal '...' sequence
    [InlineData("foo/....cs")]
    public void GetFiles_NoWildcardIllegalSpec_ShortCircuitsToVerbatimReturn(string spec)
    {
        using TempFolder tempFolder = new();
        CreateFixture(tempFolder.TempPath);

        FileMatcherWrapper.GetFilesResult result = FileMatcherWrapper.GetFiles(tempFolder.TempPath, spec);

        result.Action.Should().Be(FileMatcherWrapper.SearchAction.None);
        result.FileList.Should().ContainSingle().Which.Should().Be(spec);
    }

    // 2. Wildcard specs containing illegal characters fall through the HasWildcards check and reach
    //    GetFileSpecInfo. RawFileSpecIsValid rejects them; downstream the search returns the spec
    //    verbatim as a literal item. This is the case where touki and MSBuild actually agree on
    //    the surfacing: both produce a ReturnFileSpec disposition.

    [Theory]
    [InlineData("foo\0bar*")]                // embedded null character + wildcard
    [InlineData("foo.../*.cs")]              // '...' sequence + wildcard
    public void GetFiles_WildcardIllegalSpec_ReturnsSpecVerbatimAsReturnFileSpec(string spec)
    {
        using TempFolder tempFolder = new();
        CreateFixture(tempFolder.TempPath);

        FileMatcherWrapper.GetFilesResult result = FileMatcherWrapper.GetFiles(tempFolder.TempPath, spec);

        result.Action.Should().Be(FileMatcherWrapper.SearchAction.ReturnFileSpec);
        result.FileList.Should().ContainSingle().Which.Should().Be(spec);
    }

    // 3. Misplaced "**" (not surrounded by separators) is caught by IsLegalFileSpec AFTER
    //    SplitFileSpec runs. Result is the same SearchAction.ReturnFileSpec disposition.

    [Theory]
    [InlineData("a**b")]                     // ** between non-separator chars
    [InlineData("foo/a**b/baz")]
    [InlineData("foo/**bar")]
    [InlineData("foo/bar**")]
    [InlineData("*.cs**")]
    public void GetFiles_MisplacedDoubleStar_ReturnsSpecVerbatimAsReturnFileSpec(string spec)
    {
        using TempFolder tempFolder = new();
        CreateFixture(tempFolder.TempPath);

        FileMatcherWrapper.GetFilesResult result = FileMatcherWrapper.GetFiles(tempFolder.TempPath, spec);

        result.Action.Should().Be(FileMatcherWrapper.SearchAction.ReturnFileSpec);
        result.FileList.Should().ContainSingle().Which.Should().Be(spec);
    }

    // 4. Legal "**" specs (standalone, between separators, end-anchored to a separator) reach the
    //    full search path. RunSearch confirms MSBuild treats these as legal globs.

    [Theory]
    [InlineData("**", new[] { "a.txt", "Foo/b.txt" })]
    [InlineData("**/*.txt", new[] { "a.txt", "Foo/b.txt" })]
    [InlineData("Foo/**", new[] { "Foo/b.txt" })]
    public void GetFiles_LegalDoubleStar_RunsSearch(string spec, string[] expected)
    {
        using TempFolder tempFolder = new();
        CreateFixture(tempFolder.TempPath);

        FileMatcherWrapper.GetFilesResult result = FileMatcherWrapper.GetFiles(tempFolder.TempPath, spec);

        result.Action.Should().Be(FileMatcherWrapper.SearchAction.RunSearch);

        string prefix = tempFolder.TempPath.EndsWith(Path.DirectorySeparatorChar)
            ? tempFolder.TempPath
            : tempFolder.TempPath + Path.DirectorySeparatorChar;
        string[] normalized = [.. result.FileList
            .Select(f => f.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? f[prefix.Length..] : f)
            .Select(f => f.Replace('\\', '/'))];

        normalized.Should().BeEquivalentTo(expected);
    }

    // 5. End-to-end parity: for wildcard illegal specs, touki and MSBuild agree on the
    //    ReturnFileSpec disposition. This is the key behavior we want pinned: changes to either
    //    side will break the parity assertion below.

    [Theory]
    [InlineData("foo\0bar*")]
    [InlineData("foo.../*.cs")]
    [InlineData("a**b")]
    [InlineData("*.cs**")]
    public void CreateResult_WildcardIllegalSpec_ToukiAndMSBuildBothReturnFileSpec(string spec)
    {
        using TempFolder tempFolder = new();
        CreateFixture(tempFolder.TempPath);

        FileMatcherWrapper.GetFilesResult oracle = FileMatcherWrapper.GetFiles(tempFolder.TempPath, spec);
        MSBuildEnumerationResult touki = MSBuildEnumerator.CreateResult(
            fileSpec: spec,
            projectDirectory: tempFolder.TempPath);

        oracle.Action.Should().Be(FileMatcherWrapper.SearchAction.ReturnFileSpec);
        touki.Action.Should().Be(MSBuildSearchAction.ReturnFileSpec);
    }
}
