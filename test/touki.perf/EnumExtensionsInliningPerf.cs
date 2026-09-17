// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Runtime.CompilerServices;

#pragma warning disable CS8500 // takes the address of, gets the size of, or declares a pointer to a managed type

namespace touki.perf;

/// <summary>
///  Measures the effect of <see cref="MethodImplOptions.AggressiveInlining"/>
///  on the <see cref="Touki.EnumExtensions"/> shapes by running the *same*
///  bodies as static helpers both with and without the attribute.
/// </summary>
/// <remarks>
///  <para>
///   The production extensions are extension members on a generic
///   <c>T : unmanaged, Enum</c>. To isolate the inlining decision we copy each
///   body into a pair of static generic helpers below - <c>_NoInline</c>
///   variants get <c>NoInlining</c> so we always pay a real call+ret, and
///   <c>_Aggressive</c> variants get <c>AggressiveInlining</c>. The
///   <c>_Default</c> column lets the JIT pick its own heuristic (which on
///   net481 generally refuses to inline a body that contains a <c>fixed</c>
///   statement or a <c>sizeof(T)</c> ladder).
///  </para>
/// </remarks>
[MemoryDiagnoser]
[DisassemblyDiagnoser(maxDepth: 3, printSource: true, exportHtml: true)]
[SimpleJob(RuntimeMoniker.Net481, warmupCount: 1, iterationCount: 3, launchCount: 1)]
[SimpleJob(RuntimeMoniker.HostProcess, warmupCount: 1, iterationCount: 3, launchCount: 1)]
public unsafe class EnumExtensionsInliningPerf
{
    [Flags]
    public enum IntFlags : int { None = 0, A = 1, B = 2, C = 4, D = 8 }

    [Flags]
    public enum LongFlags : long { None = 0, A = 1, B = 2, C = 4, D = 8 }

    private IntFlags _intValue;
    private IntFlags _intFlags;
    private LongFlags _longValue;
    private LongFlags _longFlags;

    [GlobalSetup]
    public void Setup()
    {
        _intValue = IntFlags.A | IntFlags.C;
        _intFlags = IntFlags.A;
        _longValue = LongFlags.A | LongFlags.C;
        _longFlags = LongFlags.A;
    }

    // --------------- AreFlagsSet ---------------
    //
    // Body: `(value & flags) == flags` via size-of(T) ladder.
    // Expectation: AggressiveInlining wins on net481 because the ladder is
    // large enough that the default heuristic refuses to inline. On modern
    // .NET the helper already inlines by default.

    [Benchmark]
    public bool Int_AreFlagsSet_Default() => AreFlagsSet_Default(_intValue, _intFlags);

    [Benchmark]
    public bool Int_AreFlagsSet_Aggressive() => AreFlagsSet_Aggressive(_intValue, _intFlags);

    [Benchmark]
    public bool Int_AreFlagsSet_NoInline() => AreFlagsSet_NoInline(_intValue, _intFlags);

    [Benchmark]
    public bool Long_AreFlagsSet_Default() => AreFlagsSet_Default(_longValue, _longFlags);

    [Benchmark]
    public bool Long_AreFlagsSet_Aggressive() => AreFlagsSet_Aggressive(_longValue, _longFlags);

    // --------------- AreAnyFlagsSet ---------------

    [Benchmark]
    public bool Int_AreAnyFlagsSet_Default() => AreAnyFlagsSet_Default(_intValue, _intFlags);

    [Benchmark]
    public bool Int_AreAnyFlagsSet_Aggressive() => AreAnyFlagsSet_Aggressive(_intValue, _intFlags);

    [Benchmark]
    public bool Int_AreAnyFlagsSet_NoInline() => AreAnyFlagsSet_NoInline(_intValue, _intFlags);

    [Benchmark]
    public bool Long_AreAnyFlagsSet_Default() => AreAnyFlagsSet_Default(_longValue, _longFlags);

    [Benchmark]
    public bool Long_AreAnyFlagsSet_Aggressive() => AreAnyFlagsSet_Aggressive(_longValue, _longFlags);

    // --------------- IsOnlyOneFlagSet ---------------
    //
    // The production code already has AggressiveInlining (and documents that
    // it does not inline without it). These confirm that.

    [Benchmark]
    public bool Int_IsOnlyOneFlagSet_Default() => IsOnlyOneFlagSet_Default(_intValue, _intFlags);

    [Benchmark]
    public bool Int_IsOnlyOneFlagSet_Aggressive() => IsOnlyOneFlagSet_Aggressive(_intValue, _intFlags);

    [Benchmark]
    public bool Long_IsOnlyOneFlagSet_Default() => IsOnlyOneFlagSet_Default(_longValue, _longFlags);

    [Benchmark]
    public bool Long_IsOnlyOneFlagSet_Aggressive() => IsOnlyOneFlagSet_Aggressive(_longValue, _longFlags);

    // --------------- SetFlags (ref) ---------------
    //
    // Body contains a `fixed` statement plus the size-of(T) ladder. On
    // net481 the default heuristic refuses to inline through `fixed`, so
    // AggressiveInlining is the only way to flatten it.

    [Benchmark]
    public IntFlags Int_SetFlags_Default()
    {
        IntFlags v = _intValue;
        SetFlags_Default(ref v, _intFlags);
        return v;
    }

    [Benchmark]
    public IntFlags Int_SetFlags_Aggressive()
    {
        IntFlags v = _intValue;
        SetFlags_Aggressive(ref v, _intFlags);
        return v;
    }

    [Benchmark]
    public LongFlags Long_SetFlags_Default()
    {
        LongFlags v = _longValue;
        SetFlags_Default(ref v, _longFlags);
        return v;
    }

    [Benchmark]
    public LongFlags Long_SetFlags_Aggressive()
    {
        LongFlags v = _longValue;
        SetFlags_Aggressive(ref v, _longFlags);
        return v;
    }

    // ============== Helper implementations ==============
    // Bodies copied verbatim from EnumExtensions.cs (the net472 polyfill
    // path for AreFlagsSet - equivalent to the modern HasFlag intrinsic).

    private static bool AreFlagsSet_Default<T>(T value, T flags) where T : unmanaged, Enum
    {
        if (sizeof(T) == sizeof(byte)) { byte f = *(byte*)&flags; return (*(byte*)&value & f) == f; }
        else if (sizeof(T) == sizeof(short)) { short f = *(short*)&flags; return (*(short*)&value & f) == f; }
        else if (sizeof(T) == sizeof(int)) { int f = *(int*)&flags; return (*(int*)&value & f) == f; }
        else if (sizeof(T) == sizeof(long)) { long f = *(long*)&flags; return (*(long*)&value & f) == f; }
        else { throw new InvalidOperationException(); }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool AreFlagsSet_Aggressive<T>(T value, T flags) where T : unmanaged, Enum
    {
        if (sizeof(T) == sizeof(byte)) { byte f = *(byte*)&flags; return (*(byte*)&value & f) == f; }
        else if (sizeof(T) == sizeof(short)) { short f = *(short*)&flags; return (*(short*)&value & f) == f; }
        else if (sizeof(T) == sizeof(int)) { int f = *(int*)&flags; return (*(int*)&value & f) == f; }
        else if (sizeof(T) == sizeof(long)) { long f = *(long*)&flags; return (*(long*)&value & f) == f; }
        else { throw new InvalidOperationException(); }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool AreFlagsSet_NoInline<T>(T value, T flags) where T : unmanaged, Enum
    {
        if (sizeof(T) == sizeof(byte)) { byte f = *(byte*)&flags; return (*(byte*)&value & f) == f; }
        else if (sizeof(T) == sizeof(short)) { short f = *(short*)&flags; return (*(short*)&value & f) == f; }
        else if (sizeof(T) == sizeof(int)) { int f = *(int*)&flags; return (*(int*)&value & f) == f; }
        else if (sizeof(T) == sizeof(long)) { long f = *(long*)&flags; return (*(long*)&value & f) == f; }
        else { throw new InvalidOperationException(); }
    }

    private static bool AreAnyFlagsSet_Default<T>(T value, T flags) where T : unmanaged, Enum
    {
        if (sizeof(T) == sizeof(byte)) { return (*(byte*)&value & *(byte*)&flags) != 0; }
        else if (sizeof(T) == sizeof(short)) { return (*(short*)&value & *(short*)&flags) != 0; }
        else if (sizeof(T) == sizeof(int)) { return (*(int*)&value & *(int*)&flags) != 0; }
        else if (sizeof(T) == sizeof(long)) { return (*(long*)&value & *(long*)&flags) != 0; }
        else { throw new InvalidOperationException(); }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool AreAnyFlagsSet_Aggressive<T>(T value, T flags) where T : unmanaged, Enum
    {
        if (sizeof(T) == sizeof(byte)) { return (*(byte*)&value & *(byte*)&flags) != 0; }
        else if (sizeof(T) == sizeof(short)) { return (*(short*)&value & *(short*)&flags) != 0; }
        else if (sizeof(T) == sizeof(int)) { return (*(int*)&value & *(int*)&flags) != 0; }
        else if (sizeof(T) == sizeof(long)) { return (*(long*)&value & *(long*)&flags) != 0; }
        else { throw new InvalidOperationException(); }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool AreAnyFlagsSet_NoInline<T>(T value, T flags) where T : unmanaged, Enum
    {
        if (sizeof(T) == sizeof(byte)) { return (*(byte*)&value & *(byte*)&flags) != 0; }
        else if (sizeof(T) == sizeof(short)) { return (*(short*)&value & *(short*)&flags) != 0; }
        else if (sizeof(T) == sizeof(int)) { return (*(int*)&value & *(int*)&flags) != 0; }
        else if (sizeof(T) == sizeof(long)) { return (*(long*)&value & *(long*)&flags) != 0; }
        else { throw new InvalidOperationException(); }
    }

    private static bool IsOnlyOneFlagSet_Default<T>(T value, T flags) where T : unmanaged, Enum
    {
        if (sizeof(T) == sizeof(byte)) { int v = *(byte*)&value & *(byte*)&flags; return v != 0 && (v & (v - 1)) == 0; }
        else if (sizeof(T) == sizeof(short)) { int v = *(short*)&value & *(short*)&flags; return v != 0 && (v & (v - 1)) == 0; }
        else if (sizeof(T) == sizeof(int)) { int v = *(int*)&value & *(int*)&flags; return v != 0 && (v & (v - 1)) == 0; }
        else if (sizeof(T) == sizeof(long)) { long v = *(long*)&value & *(long*)&flags; return v != 0 && (v & (v - 1)) == 0; }
        else { throw new InvalidOperationException(); }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsOnlyOneFlagSet_Aggressive<T>(T value, T flags) where T : unmanaged, Enum
    {
        if (sizeof(T) == sizeof(byte)) { int v = *(byte*)&value & *(byte*)&flags; return v != 0 && (v & (v - 1)) == 0; }
        else if (sizeof(T) == sizeof(short)) { int v = *(short*)&value & *(short*)&flags; return v != 0 && (v & (v - 1)) == 0; }
        else if (sizeof(T) == sizeof(int)) { int v = *(int*)&value & *(int*)&flags; return v != 0 && (v & (v - 1)) == 0; }
        else if (sizeof(T) == sizeof(long)) { long v = *(long*)&value & *(long*)&flags; return v != 0 && (v & (v - 1)) == 0; }
        else { throw new InvalidOperationException(); }
    }

    private static void SetFlags_Default<T>(ref T value, T flags) where T : unmanaged, Enum
    {
        fixed (T* v = &value)
        {
            if (sizeof(T) == sizeof(byte)) { *(byte*)v |= *(byte*)&flags; }
            else if (sizeof(T) == sizeof(short)) { *(short*)v |= *(short*)&flags; }
            else if (sizeof(T) == sizeof(int)) { *(int*)v |= *(int*)&flags; }
            else if (sizeof(T) == sizeof(long)) { *(long*)v |= *(long*)&flags; }
            else { throw new InvalidOperationException(); }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SetFlags_Aggressive<T>(ref T value, T flags) where T : unmanaged, Enum
    {
        fixed (T* v = &value)
        {
            if (sizeof(T) == sizeof(byte)) { *(byte*)v |= *(byte*)&flags; }
            else if (sizeof(T) == sizeof(short)) { *(short*)v |= *(short*)&flags; }
            else if (sizeof(T) == sizeof(int)) { *(int*)v |= *(int*)&flags; }
            else if (sizeof(T) == sizeof(long)) { *(long*)v |= *(long*)&flags; }
            else { throw new InvalidOperationException(); }
        }
    }
}
