// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Globalization;
using Touki.Text;

namespace touki.perf;

[MemoryDiagnoser]
public class FormatValuesProviderPerf
{
    private readonly CultureInfo _provider = CultureInfo.CurrentCulture;
    private readonly Value _number = Value.Create(1234.5);
    private readonly Value _percentage = Value.Create(0.25);

    [Benchmark(Baseline = true)]
    public string ExistingPath() =>
        string.FormatValues("{0:N1} {1:P0}", _number, _percentage);

    [Benchmark]
    public string ExplicitProvider() =>
        string.FormatValues(_provider, "{0:N1} {1:P0}", _number, _percentage);
}
