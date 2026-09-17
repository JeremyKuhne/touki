// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace touki.perf;

public partial class FileSystemGlobbingCompilePerf
{
    public enum PatternKind
    {
        CommonGlobStar,
        CommonLiteral,
        LeadingParents,
        QuestionLiteral,
        TrailingSeparator,
        RecursiveSuffix,
        SequentialSeparators,
        HeavyRewrite,
        ExtGlobNoRewrite,
        ExtGlobStarRun,
    }
}