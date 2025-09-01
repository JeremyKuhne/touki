// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Io;

namespace touki.perf;

[ShortRunJob]
public class AreExpressionsExclusivePerf
{
    [Params(
        "?????,????",                // question-mark only, different lengths
        "foobar.txt,bar*.txt",       // prefix mismatch
        "foo.txt,foo*.bin",          // suffix mismatch
        "*foo,*bar",                 // differing fixed suffixes
        "foo*,bar*",                 // differing fixed prefixes
        "pre*mid*suf,pre*X*suf"      // not provably exclusive
    )]
    public string Case = string.Empty;

    private string _p1 = string.Empty;
    private string _p2 = string.Empty;

    [GlobalSetup]
    public void Setup()
    {
        int comma = Case.IndexOf(',');
        _p1 = Case[..comma];
        _p2 = Case[(comma + 1)..];
    }

    [Benchmark(Baseline = true)]
    public bool SimpleCaseSensitive() => Paths.AreExpressionsExclusive(_p1, _p2, MatchType.Simple, MatchCasing.CaseSensitive);

    [Benchmark]
    public bool SimpleCaseInsensitive() => Paths.AreExpressionsExclusive(_p1, _p2, MatchType.Simple, MatchCasing.CaseInsensitive);

    [Benchmark]
    public bool Win32CaseSensitive() => Paths.AreExpressionsExclusive(_p1, _p2, MatchType.Win32, MatchCasing.CaseSensitive);
}
