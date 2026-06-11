// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Tracing;

/// <summary>
///  Classifies a leaf frame into a runtime work category - zeroing, copying, GC,
///  write-barrier, JIT, or "other" - so a CPU profile can be summarized as "where did
///  the time go: zeroing memory? copying strings? in the GC?".
/// </summary>
/// <remarks>
///  <para>
///   The categories name the unmanaged runtime work that a per-method ranking either
///   hides (folded into its managed caller) or scatters across many small frames.
///   Classification is by frame-name substring, checked in a fixed priority order so a
///   frame that could match two buckets lands in the more specific one: a
///   <c>JIT_MemCpy</c> helper is <see cref="Copying"/>, not <see cref="Jit"/>, and
///   <c>JIT_WriteBarrier</c> is <see cref="WriteBarrier"/>. Everything that matches no
///   work pattern - managed methods, unresolved native frames - is <see cref="Other"/>.
///  </para>
///  <para>
///   This is a heuristic over well-known runtime symbol names, not an exhaustive
///   taxonomy; it is only meaningful once native symbols are resolved (otherwise the
///   native leaves are the unresolved <c>?</c> frame and fall in <see cref="Other"/>).
///  </para>
/// </remarks>
public static class FrameCategories
{
    /// <summary>Memory zeroing: <c>memset</c>, <c>RtlZeroMemory</c>, <c>JIT_MemSet</c>.</summary>
    public const string Zeroing = "zeroing";

    /// <summary>Memory copying: <c>memcpy</c>, <c>memmove</c>, <c>JIT_MemCpy</c>.</summary>
    public const string Copying = "copying";

    /// <summary>GC write barriers: <c>JIT_WriteBarrier</c>, <c>BulkMoveWithWriteBarrier</c>.</summary>
    public const string WriteBarrier = "write-barrier";

    /// <summary>Garbage collection: <c>gc_heap</c>, <c>WKS::</c> / <c>SVR::</c>, <c>JIT_New</c>.</summary>
    public const string Gc = "gc";

    /// <summary>Just-in-time compilation: <c>clrjit</c>, <c>CompileMethod</c>, other <c>JIT_</c> helpers.</summary>
    public const string Jit = "jit";

    /// <summary>Anything not matching a runtime work pattern: managed methods, unresolved frames.</summary>
    public const string Other = "other";

    // Each category is a set of case-insensitive substrings. Order is significant:
    // the more specific memory/barrier operations are tested before the generic GC and
    // JIT buckets so an overlapping helper (JIT_MemCpy, JIT_WriteBarrier, JIT_New) lands
    // in the operation it actually performs rather than the generic "jit" bucket.
    private static readonly (string Category, string[] Tokens)[] s_rules =
    [
        (Zeroing, ["memset", "rtlzeromemory", "zeromemory", "jit_memset", "memclr"]),
        (Copying, ["memcpy", "memmove", "jit_memcpy", "wmemcpy"]),
        (WriteBarrier, ["writebarrier"]),
        (Gc, ["gc_heap", "wks::", "svr::", "gcheap", "jit_new", "jit_box", "sohallocate", "allocateobject"]),
        (Jit, ["clrjit", "compilemethod", "jit_", "pollgc"])
    ];

    /// <summary>
    ///  Classifies a (shortened) leaf frame name into a runtime work category.
    /// </summary>
    /// <param name="shortLeafName">The shortened leaf frame name.</param>
    /// <returns>
    ///  One of <see cref="Zeroing"/>, <see cref="Copying"/>, <see cref="WriteBarrier"/>,
    ///  <see cref="Gc"/>, <see cref="Jit"/>, or <see cref="Other"/>.
    /// </returns>
    public static string Classify(string shortLeafName)
    {
        if (string.IsNullOrEmpty(shortLeafName))
        {
            return Other;
        }

        foreach ((string category, string[] tokens) in s_rules)
        {
            foreach (string token in tokens)
            {
                if (shortLeafName.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    return category;
                }
            }
        }

        return Other;
    }
}
