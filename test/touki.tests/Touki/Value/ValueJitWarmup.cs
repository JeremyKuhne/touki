// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Text;

namespace Touki;

/// <summary>
///  Forces JIT compilation of the <see cref="Value"/> generic
///  specializations used by the <c>StoringXxx</c> tests so that
///  subsequent <see cref="MemoryWatch.Create"/> checks measure only
///  the code under test and not the JIT itself.
/// </summary>
/// <remarks>
///  <para>
///   The first call to a generic instantiation allocates on the managed
///   heap. <c>Touki.Value</c> has a wide surface of value-type
///   specializations (<see cref="Value.Create{T}(T)"/>,
///   <see cref="Value.TryGetValue{T}(out T)"/>, <see cref="Value.As{T}"/>)
///   that every <c>StoringXxx</c> allocation test needs warmed up before
///   the watch is taken.
///  </para>
///  <para>
///   This helper is intentionally Value-specific. The general
///   <see cref="MemoryWatch"/> in <c>touki.testsupport</c> is a neutral
///   thread-allocated-bytes recorder; it knows nothing about
///   <see cref="Value"/>.
///  </para>
/// </remarks>
internal static class ValueJitWarmup
{
    private static bool s_warmed;

    /// <summary>
    ///  Module initializer that runs <see cref="Ensure"/> once on test
    ///  assembly load so individual <c>StoringXxx</c> tests can simply
    ///  open a <see cref="MemoryWatch"/> without their own warm-up call.
    /// </summary>
    [ModuleInitializer]
    internal static void Initialize() => Ensure();

    /// <summary>
    ///  Ensures all <see cref="Value"/> generic instantiations used by
    ///  the test suite have been JIT-compiled. Safe to call repeatedly;
    ///  the warm-up runs only on the first call.
    /// </summary>
    public static void Ensure()
    {
        if (s_warmed)
        {
            return;
        }

        Value.Create((bool)default).As<bool>();
        Value.Create((byte)default).As<byte>();
        Value.Create((sbyte)default).As<sbyte>();
        Value.Create((char)default).As<char>();
        Value.Create((double)default).As<double>();
        Value.Create((short)default).As<short>();
        Value.Create((int)default).As<int>();
        Value.Create((long)default).As<long>();
        Value.Create((ushort)default).As<ushort>();
        Value.Create((uint)default).As<uint>();
        Value.Create((ulong)default).As<ulong>();
        Value.Create((float)default).As<float>();
        Value.Create((double)default).As<double>();
        Value.Create((DateTime)default).As<DateTime>();
        Value.Create((DateTimeOffset)default).As<DateTimeOffset>();
        Value.Create(default(StringSegment)).As<StringSegment>();

        Value.Create((bool?)default).As<bool?>();
        Value.Create((byte?)default).As<byte?>();
        Value.Create((sbyte?)default).As<sbyte?>();
        Value.Create((char?)default).As<char?>();
        Value.Create((double?)default).As<double?>();
        Value.Create((short?)default).As<short?>();
        Value.Create((int?)default).As<int?>();
        Value.Create((long?)default).As<long?>();
        Value.Create((ushort?)default).As<ushort?>();
        Value.Create((uint?)default).As<uint?>();
        Value.Create((ulong?)default).As<ulong?>();
        Value.Create((float?)default).As<float?>();
        Value.Create((double?)default).As<double?>();
        Value.Create((DateTime?)default).As<DateTime?>();
        Value.Create((DateTimeOffset?)default).As<DateTimeOffset?>();

        Value value = default;
        value.TryGetValue(out bool _);
        value.TryGetValue(out byte _);
        value.TryGetValue(out sbyte _);
        value.TryGetValue(out char _);
        value.TryGetValue(out double _);
        value.TryGetValue(out short _);
        value.TryGetValue(out int _);
        value.TryGetValue(out long _);
        value.TryGetValue(out ushort _);
        value.TryGetValue(out uint _);
        value.TryGetValue(out ulong _);
        value.TryGetValue(out float _);
        value.TryGetValue(out double _);
        value.TryGetValue(out DateTime _);
        value.TryGetValue(out DateTimeOffset _);
        value.TryGetValue(out StringSegment _);

        s_warmed = true;
    }
}
