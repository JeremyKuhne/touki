// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using BenchmarkDotNet.Configs;

namespace touki.perf;

#if NET11_0_OR_GREATER
/// <summary>
///  Boxing baseline: the C# <see langword="union"/> declaration shorthand lowers to a struct with a single
///  <see cref="object"/> field, so every value-type case is boxed on creation.
/// </summary>
/// <remarks>
///  <para>
///   The <see langword="union"/> declaration shorthand synthesizes a type implementing the BCL
///   <c>System.Runtime.CompilerServices.IUnion</c> interface, which ships only in .NET 11, so this boxing
///   baseline is compiled only on that target.
///  </para>
/// </remarks>
public union IntOrString(int, string);
#endif

/// <summary>
///  Measures <see cref="Value"/>'s language-union pattern matching (<see cref="Value"/> is a
///  <c>[Union]</c> type via its <c>IUnionMembers</c> provider) against direct
///  <see cref="Value.TryGetValue{T}(out T)"/> access, plus the boxing <see langword="union"/> shorthand
///  (<c>IntOrString</c>, compiled only on .NET 11) as a reference for the cost of the easy path.
/// </summary>
/// <remarks>
///  <para>
///   Creation is not compared: <see cref="Value"/>'s existing implicit operators shadow the union
///   conversion, so creating a <see cref="Value"/> is unchanged. The interesting path is matching -
///   <c>value is int</c> dispatches to the explicit <c>IUnionMembers.TryGetValue(out int)</c> via a
///   <c>constrained.</c> call (no boxing) versus a direct <see cref="Value.TryGetValue{T}(out T)"/>. Runs on
///   .NET 10 and .NET 11 (modern .NET RyuJIT) and .NET Framework 4.8.1 (RyuJIT) - name the JIT explicitly in
///   any claim drawn from a leg, and treat the sub-2 ns absolutes as cross-run noisy.
///  </para>
/// </remarks>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class ValueUnionPerf
{
    private readonly int _int = 42;
    private readonly string _string = "hello";

    private Value _valueInt;
    private Value _valueString;

    [GlobalSetup]
    public void Setup()
    {
        _valueInt = Value.Create(_int);
        _valueString = Value.Create(_string);
    }

    [BenchmarkCategory("MatchInt"), Benchmark(Baseline = true)]
    public int TryGetValue_Int() => _valueInt.TryGetValue(out int value) ? value : -1;

    [BenchmarkCategory("MatchInt"), Benchmark]
    public int IsPattern_Int() => _valueInt is int value ? value : -1;

    [BenchmarkCategory("MatchString"), Benchmark(Baseline = true)]
    public string TryGetValue_String() => _valueString.TryGetValue(out string value) ? value : "";

    [BenchmarkCategory("MatchString"), Benchmark]
    public string IsPattern_String() => _valueString is string value ? value : "";

#if NET11_0_OR_GREATER
    [BenchmarkCategory("Boxing"), Benchmark]
    public IntOrString Shorthand_CreateInt() => _int;
#endif
}
