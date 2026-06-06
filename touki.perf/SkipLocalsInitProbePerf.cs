// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Runtime.CompilerServices;

namespace touki.perf;

/// <summary>
///  A/B codegen probe for the <c>[SkipLocalsInit]</c> question: does ".NET
///  Framework 4.8.1 RyuJIT" honor the absence of the <c>localsinit</c> flag, and
///  for which kinds of local? Each shape is measured with and without the
///  attribute against "modern .NET RyuJIT", with the disassembly diagnoser so
///  the prologue zeroing is visible in the exported asm.
/// </summary>
/// <remarks>
///  <para>
///   Net481 honors <c>[SkipLocalsInit]</c> for the locals it is allowed to leave
///   dirty, but the desktop CLR still force-zeros <b>GC-tracked</b> frame slots
///   regardless of the flag so the GC can report them. Three shapes isolate the
///   cases (all numbers are net481 Mean):
///  </para>
///  <para>
///   1. <b><c>Stack4096_*</c></b> - a 4 KB <c>stackalloc</c> (localloc), no GC
///   refs. The attribute removes the zeroing loop: ~53 ns drops to ~1.8 ns.
///  </para>
///  <para>
///   2. <b><c>Frame48_*</c></b> - a 48-byte address-taken non-GC struct. The
///   attribute removes the prologue <c>rep stosd</c>: ~5.8 ns drops to ~1.3 ns.
///  </para>
///  <para>
///   3. <b><c>GcFrame_*</c></b> - a 48-byte <c>object</c>-containing struct. The
///   <c>rep stosd</c> stays even with <c>[SkipLocalsInit]</c> and
///   <c>Unsafe.SkipInit</c> on both TFMs (the GC carve-out): net481 ~8.6 ns
///   either way. A <c>Span&lt;T&gt;</c> or a pinned <c>fixed</c> pointer makes a
///   frame GC-tracked the same way, which is why span helpers still see prologue
///   zeroing. See <c>docs/framework-span-performance.md</c> section 1.3 and
///   <c>docs/arraypool-performance.md</c>.
///  </para>
///  <para>
///   Companion to <c>StackZeroInitPerf</c>, which sweeps the zeroing cost by
///   size; this class sweeps it by local <i>kind</i>.
///  </para>
/// </remarks>
[MemoryDiagnoser]
[DisassemblyDiagnoser(maxDepth: 3, printSource: true, exportGithubMarkdown: true)]
[SimpleJob(RuntimeMoniker.Net481, warmupCount: 1, iterationCount: 3, launchCount: 1)]
[SimpleJob(RuntimeMoniker.HostProcess, warmupCount: 1, iterationCount: 3, launchCount: 1)]
public unsafe class SkipLocalsInitProbePerf
{
    private struct Frame48
    {
        public int F0, F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11;
    }

    private struct GcFrame
    {
        public object O0, O1, O2, O3, O4, O5;
    }

    // ---- Shape 1: 4 KB stackalloc (localloc) ----

    [Benchmark]
    public int Stack4096_Default() => Stack4096DefaultHelper();

    [Benchmark]
    public int Stack4096_SkipInit() => Stack4096SkipInitHelper();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Stack4096DefaultHelper()
    {
        Span<byte> s = stackalloc byte[4096];
        s[0] = 1;
        s[4095] = 2;
        return s[0] + s[4095];
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [SkipLocalsInit]
    private static int Stack4096SkipInitHelper()
    {
        Span<byte> s = stackalloc byte[4096];
        s[0] = 1;
        s[4095] = 2;
        return s[0] + s[4095];
    }

    // ---- Shape 2: 48-byte address-taken fixed-frame struct (no localloc) ----

    [Benchmark]
    public int Frame48_Default() => Frame48DefaultHelper();

    [Benchmark]
    public int Frame48_SkipInit() => Frame48SkipInitHelper();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Frame48DefaultHelper()
    {
        Frame48 f;
        EscapeFrame(&f);
        return f.F0 + f.F11;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [SkipLocalsInit]
    private static int Frame48SkipInitHelper()
    {
        Frame48 f;
        EscapeFrame(&f);
        return f.F0 + f.F11;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void EscapeFrame(Frame48* p)
    {
        p->F0 = 1;
        p->F11 = 2;
    }

    // ---- Shape 3: GC-pointer-containing frame struct (the carve-out) ----
    // ECMA-335 / the CLR require GC-tracked frame slots to be zeroed before the
    // method becomes GC-interruptible, REGARDLESS of the localsinit flag, so the
    // GC can safely report them. This pair shows that [SkipLocalsInit] does NOT
    // suppress zeroing here on either TFM - explaining a residual prologue zero
    // in a method whose locals contain managed references (e.g. a Span).

    [Benchmark]
    public int GcFrame_Default() => GcFrameDefaultHelper();

    [Benchmark]
    public int GcFrame_SkipInit() => GcFrameSkipInitHelper();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int GcFrameDefaultHelper()
    {
        GcFrame f = default;
        EscapeGcFrame(ref f);
        return f.O0 is null ? 0 : 1;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [SkipLocalsInit]
    private static int GcFrameSkipInitHelper()
    {
        Unsafe.SkipInit(out GcFrame f);
        EscapeGcFrame(ref f);
        return f.O0 is null ? 0 : 1;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void EscapeGcFrame(ref GcFrame f)
    {
        f.O0 = "a";
        f.O1 = "b";
        f.O2 = "c";
        f.O3 = "d";
        f.O4 = "e";
        f.O5 = "f";
    }
}
