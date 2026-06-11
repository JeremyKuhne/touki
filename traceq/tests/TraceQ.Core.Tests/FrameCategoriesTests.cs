// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Tracing;

[TestClass]
public sealed class FrameCategoriesTests
{
    [TestMethod]
    [DataRow("memset", FrameCategories.Zeroing)]
    [DataRow("ntdll!RtlZeroMemory", FrameCategories.Zeroing)]
    [DataRow("JIT_MemSet", FrameCategories.Zeroing)]
    [DataRow("memcpy", FrameCategories.Copying)]
    [DataRow("ntdll!memmove", FrameCategories.Copying)]
    [DataRow("JIT_MemCpy", FrameCategories.Copying)]
    [DataRow("JIT_WriteBarrier", FrameCategories.WriteBarrier)]
    [DataRow("BulkMoveWithWriteBarrier", FrameCategories.WriteBarrier)]
    [DataRow("WKS::gc_heap::plan_phase", FrameCategories.Gc)]
    [DataRow("SVR::gc_heap::mark_phase", FrameCategories.Gc)]
    [DataRow("JIT_New", FrameCategories.Gc)]
    [DataRow("clrjit!Compiler::compCompile", FrameCategories.Jit)]
    [DataRow("coreclr!MethodDesc::CompileMethod", FrameCategories.Jit)]
    [DataRow("PollGC", FrameCategories.Jit)]
    [DataRow("Touki.Io.Globbing.CompiledGlobStrategy.RunEngine", FrameCategories.Other)]
    [DataRow("System.SpanHelpers.IndexOf", FrameCategories.Other)]
    [DataRow("?", FrameCategories.Other)]
    [DataRow("", FrameCategories.Other)]
    public void Classify_AssignsTheExpectedCategory(string frame, string expected)
    {
        FrameCategories.Classify(frame).Should().Be(expected);
    }

    [TestMethod]
    public void Classify_PrefersTheSpecificOperationOverTheGenericJitBucket()
    {
        // JIT_MemCpy / JIT_MemSet / JIT_WriteBarrier / JIT_New all start with "JIT_",
        // but each must land in the operation it performs, not the generic "jit" bucket,
        // because the work-type rules are checked before the generic jit rule.
        FrameCategories.Classify("JIT_MemCpy").Should().Be(FrameCategories.Copying);
        FrameCategories.Classify("JIT_MemSet").Should().Be(FrameCategories.Zeroing);
        FrameCategories.Classify("JIT_WriteBarrier").Should().Be(FrameCategories.WriteBarrier);
        FrameCategories.Classify("JIT_New").Should().Be(FrameCategories.Gc);

        // A JIT_ helper that is none of those falls through to the generic jit bucket.
        FrameCategories.Classify("JIT_Stelem_Ref").Should().Be(FrameCategories.Jit);
    }

    [TestMethod]
    public void Classify_IsCaseInsensitive()
    {
        FrameCategories.Classify("MEMSET").Should().Be(FrameCategories.Zeroing);
        FrameCategories.Classify("MemCpy").Should().Be(FrameCategories.Copying);
    }
}
