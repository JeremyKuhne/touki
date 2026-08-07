// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.Extensions.FileSystemGlobbing;
using Touki.Io.Globbing;

namespace touki.perf;

/// <summary>
///  Compares Touki matching with FSG's end-to-end public
///  <c>Matcher.Match</c> wrapper for parity patterns.
/// </summary>
[MemoryDiagnoser]
public partial class FileSystemGlobbingParityMatchPerf
{
    [ParamsAllValues]
    public PatternKind Kind { get; set; }

    private string _input = null!;
    private GlobSpecification _touki = null!;
    private Matcher _oracle = null!;
    private string[] _oracleFiles = null!;

    [GlobalSetup]
    public void Setup()
    {
        (string Pattern, string Input) scenario = Kind switch
        {
            PatternKind.QuestionLiteral => ("src/file?.cs", "src/fileX.cs"),
            PatternKind.TrailingSeparator => ("src/", "src/generated/obj/File.cs"),
            PatternKind.RecursiveSuffix => ("**.cs", "src/generated/obj/File.cs"),
            PatternKind.SequentialSeparators => ("src///generated/*.cs", "src/a/b/generated/File.cs"),
            _ => throw new InvalidOperationException(),
        };

        string pattern = scenario.Pattern;
        _input = scenario.Input;
        _oracleFiles = [_input];

        _touki = GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing);
        _oracle = new Matcher(StringComparison.Ordinal);
        _oracle.AddInclude(pattern);

        bool toukiResult = _touki.IsMatch(_input);
        bool oracleResult = _oracle.Match("/", _oracleFiles).HasMatches;
        if (toukiResult != oracleResult)
        {
            throw new InvalidOperationException($"Matcher disagreement for '{pattern}' against '{_input}'.");
        }
    }

    [GlobalCleanup]
    public void Cleanup() => _touki.Dispose();

    [Benchmark(Baseline = true)]
    public bool Touki() => _touki.IsMatch(_input);

    [Benchmark]
    public bool Oracle_PublicMatch() => _oracle.Match("/", _oracleFiles).HasMatches;
}