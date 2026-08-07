// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Io.Globbing;

namespace touki.perf;

/// <summary>
///  Measures FileSystemGlobbing pattern compilation across common and normalized shapes.
/// </summary>
[MemoryDiagnoser]
public partial class FileSystemGlobbingCompilePerf
{
    [ParamsAllValues]
    public PatternKind Kind { get; set; }

    private string _pattern = null!;

    [GlobalSetup]
    public void Setup()
    {
        _pattern = Kind switch
        {
            PatternKind.CommonGlobStar => "src/**/*.cs",
            PatternKind.CommonLiteral => "src/Program.cs",
            PatternKind.LeadingParents => "../../src/*.cs",
            PatternKind.QuestionLiteral => "src/file?.cs",
            PatternKind.TrailingSeparator => "src/",
            PatternKind.RecursiveSuffix => "**.cs",
            PatternKind.SequentialSeparators => "src///generated/*.cs",
            PatternKind.HeavyRewrite => "././**/./**/*.*",
            PatternKind.ExtGlobNoRewrite => "@(foo|bar)",
            PatternKind.ExtGlobStarRun => "***/@(x)",
            _ => throw new InvalidOperationException(),
        };
    }

    [Benchmark]
    public int Compile()
    {
        using GlobSpecification specification = GlobSpecification.Compile(
            _pattern,
            GlobDialect.FileSystemGlobbing,
            Kind is PatternKind.ExtGlobNoRewrite or PatternKind.ExtGlobStarRun
                ? GlobOptions.AllowExtGlob
                : GlobOptions.None);

        return specification.LiteralPathPrefix.Length + specification.Separator;
    }
}