// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.Extensions.FileSystemGlobbing;

namespace Touki.Io.Globbing;

/// <summary>
///  Oracle tests for FileSystemGlobbing behaviors that differ from ordinary glob syntax.
/// </summary>
[TestClass]
public class FileSystemGlobbingParityOracleTests
{
    [TestMethod]
    [DataRow("a?b", "a?b")]
    [DataRow("a?b", "axb")]
    [DataRow("a?b", "ab")]
    [DataRow("a/?/b", "a/?/b")]
    [DataRow("a/?/b", "a/x/b")]
    public void IsMatch_QuestionMarkPattern_AgreesWithMatcher(string pattern, string input)
    {
#if NETFRAMEWORK
        if (input.IndexOf('?') >= 0)
        {
            Assert.Inconclusive(
                "The FileSystemGlobbing net481 oracle rejects '?' in candidate paths before matching.");
        }
#endif

        ToukiMatches(pattern, input).Should().Be(
            OracleMatches(pattern, input),
            because: $"FileSystemGlobbing question-mark semantics must agree for '{pattern}' against '{input}'");
    }

    [TestMethod]
    [DataRow("a/", "a/file.txt", true)]
    [DataRow("a/", "a/b/file.txt", true)]
    [DataRow("a/", "a", false)]
    [DataRow("a\\", "a/file.txt", true)]
    [DataRow("/", "file.txt", false)]
    [DataRow("/", "a/file.txt", false)]
    [DataRow("///", "file.txt", false)]
    [DataRow("///", "a/file.txt", false)]
    [DataRow("a///", "a/file.txt", true)]
    [DataRow("a///", "a", false)]
    public void IsMatch_TrailingSeparatorPattern_AgreesWithMatcher(
        string pattern,
        string input,
        bool expected)
    {
        bool oracle = OracleMatches(pattern, input);
        oracle.Should().Be(
            expected,
            because: $"the oracle fixture must pin trailing-separator behavior for '{pattern}' against '{input}'");
        ToukiMatches(pattern, input).Should().Be(
            oracle,
            because: $"FileSystemGlobbing trailing-separator semantics must agree for '{pattern}' against '{input}'");
    }

    [TestMethod]
    [DataRow("**.cs", "file.cs")]
    [DataRow("**.cs", "dir/file.cs")]
    [DataRow("**.cs", "a/b/file.cs")]
    [DataRow("**.cs", "a/b/file.txt")]
    [DataRow("ab/**.suffix", "ab/file.suffix")]
    [DataRow("ab/**.suffix", "ab/x/file.suffix")]
    [DataRow("ab/**.suffix", "x/file.suffix")]
    [DataRow("src/**.*", "src/file.txt")]
    [DataRow("src/**.*", "src/a/file.txt")]
    public void IsMatch_RecursiveSuffixPattern_AgreesWithMatcher(string pattern, string input) =>
        ToukiMatches(pattern, input).Should().Be(
            OracleMatches(pattern, input),
            because: $"FileSystemGlobbing recursive-suffix semantics must agree for '{pattern}' against '{input}'");

    [TestMethod]
    [DataRow("../b", true)]
    [DataRow("../../b", true)]
    [DataRow("a/../b", false)]
    [DataRow("a/..", false)]
    [DataRow("**/../b", false)]
    [DataRow("*.cs/../", false)]
    [DataRow("..//../b", false)]
    public void Compile_ParentSegments_AgreesWithMatcher(string pattern, bool accepted)
    {
        Exception? oracleException = CaptureException(() =>
        {
            Matcher matcher = new(StringComparison.Ordinal);
            matcher.AddInclude(pattern);
        });

        if (accepted)
        {
            oracleException.Should().BeNull();
        }
        else
        {
            oracleException.Should().BeOfType<ArgumentException>()
                .Which.Message.Should().Be("\"..\" can be only added at the beginning of the pattern.");
        }

        Exception? toukiException = CaptureException(() =>
        {
            using GlobSpecification specification =
                GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing);
        });

        if (accepted)
        {
            toukiException.Should().BeNull();
        }
        else
        {
            GlobFormatException exception = toukiException.Should().BeOfType<GlobFormatException>().Which;
            exception.Error.Code.Should().Be(GlobCompileErrorCode.ParentSegmentNotAtBeginning);
            exception.Error.Message.Should().Be(oracleException!.Message);
        }
    }

    private static bool OracleMatches(string pattern, string input)
    {
        Matcher matcher = new(StringComparison.Ordinal);
        matcher.AddInclude(pattern);
        return matcher.Match(input).HasMatches;
    }

    private static bool ToukiMatches(string pattern, string input)
    {
        using GlobSpecification specification =
            GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing);

        return specification.IsMatch(input);
    }

    private static Exception? CaptureException(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }
}