// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Text;
using Touki.Text;

namespace touki.perf;

/// <summary>
///  Head-to-head comparison of formatting a string with <see cref="Touki.Text.ValueStringBuilder"/>
///  versus a plain <see cref="StringBuilder"/>.
/// </summary>
/// <remarks>
///  <para>
///   The scenario is a representative composite-format build: one literal-heavy format
///   string with three holes of mixed types (a <see cref="string"/>, an <see cref="int"/>,
///   and a <see cref="double"/>) producing a short result that fits inside the 256-char
///   stack buffer. Both variants finish with <c>ToString()</c>, so each pays for the final
///   result string; the difference is everything else.
///  </para>
///  <para>
///   The plain <see cref="StringBuilder"/> path allocates the builder object, its internal
///   char buffer, and one box per value-type argument (<see cref="StringBuilder.AppendFormat(string, object?, object?, object?)"/>
///   takes <see cref="object"/> holes). The <see cref="Touki.Text.ValueStringBuilder"/> path keeps its
///   working buffer on the stack and passes arguments through <see cref="Value"/>, which
///   stores primitives inline, so it allocates only the final string.
///  </para>
/// </remarks>
[MemoryDiagnoser]
public class ValueStringBuilderFormattingPerf
{
    private const string Format = "User {0} has {1} points ({2:F1}% complete).";

    private readonly string _name = "Jeremy";
    private readonly int _score = 1234;
    private readonly double _percent = 87.5;

    [Benchmark(Baseline = true)]
    public string StringBuilder_AppendFormat()
    {
        StringBuilder builder = new();
        builder.AppendFormat(Format, _name, _score, _percent);
        return builder.ToString();
    }

    [Benchmark]
    public string ValueStringBuilder_AppendFormat()
    {
        ValueStringBuilder builder = new(stackalloc char[256]);
        builder.AppendFormat(Format, _name, _score, _percent);
        return builder.ToStringAndDispose();
    }
}
