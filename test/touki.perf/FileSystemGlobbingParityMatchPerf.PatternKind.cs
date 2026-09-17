// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace touki.perf;

public partial class FileSystemGlobbingParityMatchPerf
{
    public enum PatternKind
    {
        QuestionLiteral,
        TrailingSeparator,
        RecursiveSuffix,
        SequentialSeparators,
    }
}