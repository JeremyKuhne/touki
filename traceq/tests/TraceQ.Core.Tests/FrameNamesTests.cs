// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Tracing;

[TestClass]
public sealed class FrameNamesTests
{
    [TestMethod]
    public void Short_ModuleAndSignature_KeepsMethodIdentifier()
    {
        string result = FrameNames.Short("touki!Touki.Io.Globbing.CompiledGlobStrategy.RunEngine(int32, bool)");
        result.Should().Be("Touki.Io.Globbing.CompiledGlobStrategy.RunEngine");
    }

    [TestMethod]
    public void Short_ValueClassNoise_IsStripped()
    {
        string result = FrameNames.Short("mod!value class Some.Type.Method(int32)");
        result.Should().Be("Some.Type.Method");
    }

    [TestMethod]
    public void Short_NoModulePrefix_ReturnsNameUnchanged()
    {
        FrameNames.Short("CPU_TIME").Should().Be("CPU_TIME");
    }

    [TestMethod]
    public void IsFolded_DefaultPatterns_FoldSyntheticAndHelperLeaves()
    {
        Regex[] fold = FrameNames.CompileFoldPatterns(FrameNames.DefaultFoldPatterns);
        FrameNames.IsFolded("CPU_TIME", fold).Should().BeTrue();
        FrameNames.IsFolded("UNMANAGED_CODE_TIME", fold).Should().BeTrue();
        FrameNames.IsFolded("JIT_WriteBarrier", fold).Should().BeTrue();
        FrameNames.IsFolded("System.Buffer.Memmove", fold).Should().BeTrue();
    }

    [TestMethod]
    public void IsFolded_RealMethod_IsNotFolded()
    {
        Regex[] fold = FrameNames.CompileFoldPatterns(FrameNames.DefaultFoldPatterns);
        FrameNames.IsFolded("Touki.Io.Globbing.CompiledGlobStrategy.RunEngine", fold).Should().BeFalse();
    }
}
