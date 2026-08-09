// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Io.Globbing;

namespace touki.perf;

/// <summary>
///  Compares the FileSystemGlobbing literal-question multi-suffix specialization
///  with an equivalent nested extglob that forces the generic bytecode path.
/// </summary>
[MemoryDiagnoser]
public class FileSystemGlobbingLiteralQuestionSuffixPerf
{
    private const string Input = "src/generated/xbar?";

    private GlobSpecification _specialized = null!;
    private GlobSpecification _generic = null!;

    [GlobalSetup]
    public void Setup()
    {
        _specialized = GlobSpecification.Compile(
            "**/@(*foo?|*bar?)",
            GlobDialect.FileSystemGlobbing,
            GlobOptions.AllowExtGlob);

        _generic = GlobSpecification.Compile(
            "**/@(@(*foo?|*bar?))",
            GlobDialect.FileSystemGlobbing,
            GlobOptions.AllowExtGlob);

        if (_specialized.IsMatch(Input) != _generic.IsMatch(Input))
        {
            throw new InvalidOperationException("Specialized and generic matchers disagree.");
        }
    }

    [Benchmark(Baseline = true)]
    public bool Specialized() => _specialized.IsMatch(Input);

    [Benchmark]
    public bool GenericBytecode() => _generic.IsMatch(Input);
}