// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

/// <summary>
///  Enum extension methods.
/// </summary>
/// <remarks>
///  <para>
///   Every method here follows the same pattern: a single generic body that
///   uses <c>sizeof(T)</c> to dispatch to the right underlying-integer code,
///   plus <see cref="MethodImplOptions.AggressiveInlining"/>. The JIT
///   specializes the generic for each concrete <c>T</c>,
///   treats <c>sizeof(T)</c> as a compile-time constant, dead-code-
///   eliminates the three non-matching branches, and the body collapses to
///   a single instruction sequence over the underlying integer.
///  </para>
///  <para>
///   The result on both runtimes is essentially the same instructions you
///   would write by hand. See <c>touki.perf/EnumExtensionsPerf.cs</c> and
///   <c>EnumExtensionsInliningPerf.cs</c> for the disassembly and numbers
///   that back that up.
///  </para>
/// </remarks>
public static unsafe partial class EnumExtensions
{
    // Why AggressiveInlining on every method?
    //
    //  On modern .NET (.NET 5+) the size-of(T) ladder is folded and the
    //  helper is inlined regardless - AggressiveInlining is a no-op there.
    //
    //  On .NET Framework 4.7.2 the JIT looks at the *unfolded* IL size of
    //  the four-arm ladder and decides the method is too big to inline.
    //  The fold still happens (the helper body is only one `and; cmp; sete`
    //  for AreFlagsSet, for example), but the caller pays a real call/ret
    //  plus a stack spill - roughly +0.35 ns per call. AggressiveInlining
    //  overrides that heuristic so the call site gets the same flat code
    //  as on modern .NET. Confirmed in touki.perf/EnumExtensionsInliningPerf.cs
    //  asm output: with the attribute the call disappears and the inlined
    //  body is ~3 extra instructions (load + and + cmp) over a hand-written
    //  bitwise expression; without it the inlined body is replaced by a
    //  call frame.

    extension<T>(T value) where T : unmanaged, Enum
    {
        /// <summary>
        ///  Returns true if the given flag or flags are set.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   Equivalent to <see cref="Enum.HasFlag(Enum)"/> but without the boxing
        ///   penalty. Compared to <see cref="Enum.HasFlag(Enum)"/> on net472 (which
        ///   boxes both operands and virtcalls into <c>Enum.HasFlag</c>) this is ~20x
        ///   faster and allocates zero bytes.
        ///  </para>
        ///  <para>
        ///   The underlying-integer pointer ladder is used on <em>both</em> runtimes.
        ///   The obvious <c>#if NET</c> shortcut of delegating to
        ///   <c>value.HasFlag(flags)</c> on modern .NET does NOT work as intended:
        ///   RyuJIT's no-box <c>Enum.HasFlag</c> intrinsic only fires when the call is
        ///   made directly on a concrete enum, not when it is reached through this
        ///   generic <c>extension&lt;T&gt;</c> body. Inside the generic the
        ///   <paramref name="flags"/> argument is widened to the <see cref="Enum"/>
        ///   parameter of <see cref="Enum.HasFlag(Enum)"/> and boxes on every call,
        ///   reintroducing the exact allocation this method exists to avoid (caught by
        ///   an allocation-asserting test on net10). The pointer ladder folds to the
        ///   same <c>and; cmp; sete</c> on net10 with no box, so it is the correct
        ///   choice for both targets.
        ///  </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AreFlagsSet(T flags)
        {
            // HasFlag boxes (see remarks); read the underlying integer via a raw
            // pointer on both runtimes to avoid it.
            if (sizeof(T) == sizeof(byte))
            {
                byte f = *(byte*)&flags;
                return (*(byte*)&value & f) == f;
            }
            else if (sizeof(T) == sizeof(short))
            {
                short f = *(short*)&flags;
                return (*(short*)&value & f) == f;
            }
            else if (sizeof(T) == sizeof(int))
            {
                int f = *(int*)&flags;
                return (*(int*)&value & f) == f;
            }
            else if (sizeof(T) == sizeof(long))
            {
                long f = *(long*)&flags;
                return (*(long*)&value & f) == f;
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        /// <summary>
        ///  Returns true if only one of the specified <paramref name="flags"/>
        ///  is set.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsOnlyOneFlagSet(T flags)
        {
            //  Uses the classic `v != 0 && (v & (v - 1)) == 0` power-of-two test.
            //  On modern .NET RyuJIT this is recognized and emitted with the
            //  BMI1 `blsr` instruction (~24 bytes total).

            if (sizeof(T) == sizeof(byte))
            {
                int v = *(byte*)&value & *(byte*)&flags;
                return v != 0 && (v & (v - 1)) == 0;
            }
            else if (sizeof(T) == sizeof(short))
            {
                int v = *(ushort*)&value & *(ushort*)&flags;
                return v != 0 && (v & (v - 1)) == 0;
            }
            else if (sizeof(T) == sizeof(int))
            {
                int v = *(int*)&value & *(int*)&flags;
                return v != 0 && (v & (v - 1)) == 0;
            }
            else if (sizeof(T) == sizeof(long))
            {
                long v = *(long*)&value & *(long*)&flags;
                return v != 0 && (v & (v - 1)) == 0;
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        /// <summary>
        ///  Returns true if any of the given flags are set.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   There is no BCL equivalent. <see cref="Enum.HasFlag"/>
        ///   answers "are all of these set", not "are any of them set".
        ///  </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AreAnyFlagsSet(T flags)
        {
            if (sizeof(T) == sizeof(byte))
            {
                return (*(byte*)&value & *(byte*)&flags) != 0;
            }
            else if (sizeof(T) == sizeof(short))
            {
                return (*(short*)&value & *(short*)&flags) != 0;
            }
            else if (sizeof(T) == sizeof(int))
            {
                return (*(int*)&value & *(int*)&flags) != 0;
            }
            else if (sizeof(T) == sizeof(long))
            {
                return (*(long*)&value & *(long*)&flags) != 0;
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
    }

    extension<T>(ref T value) where T : unmanaged, Enum
    {
        /// <summary>
        ///  Sets the given flag or flags on <paramref name="value"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetFlags(T flags)
        {
            fixed (T* v = &value)
            {
                if (sizeof(T) == sizeof(byte))
                {
                    *(byte*)v |= *(byte*)&flags;
                }
                else if (sizeof(T) == sizeof(short))
                {
                    *(short*)v |= *(short*)&flags;
                }
                else if (sizeof(T) == sizeof(int))
                {
                    *(int*)v |= *(int*)&flags;
                }
                else if (sizeof(T) == sizeof(long))
                {
                    *(long*)v |= *(long*)&flags;
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
        }

        /// <summary>
        ///  Clears the given flag or flags on <paramref name="value"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearFlags(T flags)
        {
            fixed (T* v = &value)
            {
                if (sizeof(T) == sizeof(byte))
                {
                    *(byte*)v &= (byte)~*(byte*)&flags;
                }
                else if (sizeof(T) == sizeof(short))
                {
                    *(short*)v &= (short)~*(short*)&flags;
                }
                else if (sizeof(T) == sizeof(int))
                {
                    *(int*)v &= ~*(int*)&flags;
                }
                else if (sizeof(T) == sizeof(long))
                {
                    *(long*)v &= ~*(long*)&flags;
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
        }
    }
}
