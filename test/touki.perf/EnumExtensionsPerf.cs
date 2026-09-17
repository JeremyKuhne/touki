// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Runtime.CompilerServices;

namespace touki.perf;

/// <summary>
///  Measures <see cref="Touki.EnumExtensions"/> versus hand-written bitwise checks
///  and <see cref="Enum.HasFlag(Enum)"/> on both .NET Framework 4.8.1 RyuJIT
///  and modern .NET RyuJIT.
/// </summary>
/// <remarks>
///  <para>
///   Each benchmark intentionally compares three shapes for the same logical
///   query: the <c>EnumExtensions</c> helper, the equivalent open-coded
///   bitwise expression on the enum's underlying type, and (where defined)
///   <c>Enum.HasFlag</c>. Inputs come from mutable instance fields so the JIT
///   cannot constant-fold the operation away.
///  </para>
///  <para>
///   Run with <c>--filter *EnumExtensionsPerf* --runtimes net481 net10.0
///   --disasm</c> to capture asm for both targets.
///  </para>
/// </remarks>
[MemoryDiagnoser]
[DisassemblyDiagnoser(maxDepth: 3, printSource: true, exportHtml: true)]
[SimpleJob(RuntimeMoniker.Net481, warmupCount: 1, iterationCount: 3, launchCount: 1)]
[SimpleJob(RuntimeMoniker.HostProcess, warmupCount: 1, iterationCount: 3, launchCount: 1)]
public class EnumExtensionsPerf
{
    [Flags]
    public enum ByteFlags : byte { None = 0, A = 1, B = 2, C = 4, D = 8 }

    [Flags]
    public enum ShortFlags : short { None = 0, A = 1, B = 2, C = 4, D = 8 }

    [Flags]
    public enum IntFlags : int { None = 0, A = 1, B = 2, C = 4, D = 8 }

    [Flags]
    public enum LongFlags : long { None = 0, A = 1, B = 2, C = 4, D = 8 }

    // Mutable fields prevent constant folding.
    private ByteFlags _byteValue;
    private ByteFlags _byteFlags;
    private ShortFlags _shortValue;
    private ShortFlags _shortFlags;
    private IntFlags _intValue;
    private IntFlags _intFlags;
    private LongFlags _longValue;
    private LongFlags _longFlags;

    [GlobalSetup]
    public void Setup()
    {
        _byteValue = ByteFlags.A | ByteFlags.C;
        _byteFlags = ByteFlags.A;
        _shortValue = ShortFlags.A | ShortFlags.C;
        _shortFlags = ShortFlags.A;
        _intValue = IntFlags.A | IntFlags.C;
        _intFlags = IntFlags.A;
        _longValue = LongFlags.A | LongFlags.C;
        _longFlags = LongFlags.A;
    }

    // -------------------- AreFlagsSet (== HasFlag) --------------------
    //
    // AreFlagsSet is a thin wrapper over Enum.HasFlag. On modern .NET (.NET 5+)
    // HasFlag is intrinsified by the JIT into a tiny `(value & flag) == flag`
    // sequence. On net481 it stays a virtual call into Enum.HasFlag, boxes
    // both operands, and performs a type-check + memcmp-style underlying
    // comparison. So:
    //   - Modern: AreFlagsSet ~= Manual_AreFlagsSet (both ~sub-ns, 0 alloc).
    //   - net481: AreFlagsSet & HasFlag are an order of magnitude slower and
    //     allocate two boxed Enums (~48 B / call for int).

    [Benchmark]
    public bool Int_AreFlagsSet() => _intValue.AreFlagsSet(_intFlags);

    [Benchmark]
    public bool Int_HasFlag() => _intValue.HasFlag(_intFlags);

    [Benchmark]
    public bool Int_AreFlagsSet_Manual() => Manual_AreFlagsSet(_intValue, _intFlags);

    [Benchmark]
    public bool Long_AreFlagsSet() => _longValue.AreFlagsSet(_longFlags);

    [Benchmark]
    public bool Long_HasFlag() => _longValue.HasFlag(_longFlags);

    [Benchmark]
    public bool Long_AreFlagsSet_Manual() => Manual_AreFlagsSet(_longValue, _longFlags);

    // -------------------- AreAnyFlagsSet --------------------
    //
    // Open-coded `(value & flags) != 0` on the underlying type. This is the
    // call this library was built for: HasFlag answers "are ALL of these
    // set", and there is no BCL equivalent for "are ANY of these set" that
    // doesn't allocate.

    [Benchmark]
    public bool Byte_AreAnyFlagsSet() => _byteValue.AreAnyFlagsSet(_byteFlags);

    [Benchmark]
    public bool Byte_AreAnyFlagsSet_Manual() => Manual_AreAnyFlagsSet(_byteValue, _byteFlags);

    [Benchmark]
    public bool Short_AreAnyFlagsSet() => _shortValue.AreAnyFlagsSet(_shortFlags);

    [Benchmark]
    public bool Short_AreAnyFlagsSet_Manual() => Manual_AreAnyFlagsSet(_shortValue, _shortFlags);

    [Benchmark]
    public bool Int_AreAnyFlagsSet() => _intValue.AreAnyFlagsSet(_intFlags);

    [Benchmark]
    public bool Int_AreAnyFlagsSet_Manual() => Manual_AreAnyFlagsSet(_intValue, _intFlags);

    [Benchmark]
    public bool Long_AreAnyFlagsSet() => _longValue.AreAnyFlagsSet(_longFlags);

    [Benchmark]
    public bool Long_AreAnyFlagsSet_Manual() => Manual_AreAnyFlagsSet(_longValue, _longFlags);

    // -------------------- IsOnlyOneFlagSet --------------------
    //
    // `v != 0 && (v & (v - 1)) == 0`. Modern JIT recognises the blsr-style
    // power-of-two test and emits a 6-instruction sequence using BLSR (BMI1)
    // when the platform supports it. net481 RyuJIT emits the longhand form
    // with two compares and a branch.

    [Benchmark]
    public bool Int_IsOnlyOneFlagSet() => _intValue.IsOnlyOneFlagSet(_intFlags);

    [Benchmark]
    public bool Int_IsOnlyOneFlagSet_Manual() => Manual_IsOnlyOneFlagSet(_intValue, _intFlags);

    [Benchmark]
    public bool Long_IsOnlyOneFlagSet() => _longValue.IsOnlyOneFlagSet(_longFlags);

    [Benchmark]
    public bool Long_IsOnlyOneFlagSet_Manual() => Manual_IsOnlyOneFlagSet(_longValue, _longFlags);

    // -------------------- SetFlags / ClearFlags --------------------
    //
    // Ref-receiver extensions that fold to a single `or`/`and` instruction
    // when the enum size matches the host register. The Manual_* variants
    // use raw bitwise ops on the underlying type; on both runtimes they
    // should be indistinguishable from the helper.

    [Benchmark]
    public IntFlags Int_SetFlags()
    {
        IntFlags v = _intValue;
        v.SetFlags(_intFlags);
        return v;
    }

    [Benchmark]
    public IntFlags Int_SetFlags_Manual()
    {
        IntFlags v = _intValue;
        Manual_SetFlags(ref v, _intFlags);
        return v;
    }

    [Benchmark]
    public IntFlags Int_ClearFlags()
    {
        IntFlags v = _intValue;
        v.ClearFlags(_intFlags);
        return v;
    }

    [Benchmark]
    public IntFlags Int_ClearFlags_Manual()
    {
        IntFlags v = _intValue;
        Manual_ClearFlags(ref v, _intFlags);
        return v;
    }

    [Benchmark]
    public LongFlags Long_SetFlags()
    {
        LongFlags v = _longValue;
        v.SetFlags(_longFlags);
        return v;
    }

    [Benchmark]
    public LongFlags Long_SetFlags_Manual()
    {
        LongFlags v = _longValue;
        Manual_SetFlags(ref v, _longFlags);
        return v;
    }

    [Benchmark]
    public LongFlags Long_ClearFlags()
    {
        LongFlags v = _longValue;
        v.ClearFlags(_longFlags);
        return v;
    }

    [Benchmark]
    public LongFlags Long_ClearFlags_Manual()
    {
        LongFlags v = _longValue;
        Manual_ClearFlags(ref v, _longFlags);
        return v;
    }

    // ----- Manual baselines (no inlining so the codegen is comparable) -----

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool Manual_AreFlagsSet(IntFlags v, IntFlags f) => ((int)v & (int)f) == (int)f;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool Manual_AreFlagsSet(LongFlags v, LongFlags f) => ((long)v & (long)f) == (long)f;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool Manual_AreAnyFlagsSet(ByteFlags v, ByteFlags f) => ((byte)v & (byte)f) != 0;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool Manual_AreAnyFlagsSet(ShortFlags v, ShortFlags f) => ((short)v & (short)f) != 0;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool Manual_AreAnyFlagsSet(IntFlags v, IntFlags f) => ((int)v & (int)f) != 0;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool Manual_AreAnyFlagsSet(LongFlags v, LongFlags f) => ((long)v & (long)f) != 0;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool Manual_IsOnlyOneFlagSet(IntFlags value, IntFlags flags)
    {
        int v = (int)value & (int)flags;
        return v != 0 && (v & (v - 1)) == 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool Manual_IsOnlyOneFlagSet(LongFlags value, LongFlags flags)
    {
        long v = (long)value & (long)flags;
        return v != 0 && (v & (v - 1)) == 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Manual_SetFlags(ref IntFlags v, IntFlags f) => v |= f;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Manual_SetFlags(ref LongFlags v, LongFlags f) => v |= f;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Manual_ClearFlags(ref IntFlags v, IntFlags f) => v &= ~f;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Manual_ClearFlags(ref LongFlags v, LongFlags f) => v &= ~f;
}
